using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Data.Context;
using POS.Domains.BusinessObjects;
using POS.Models.Operations;
using POS.Services.Repository;
using POS.Services.Security;
using System;
using System.Data.Entity;
using System.Linq;

namespace POS.Services.Point_of_Sale
{
    public sealed class SalesCartService : IDisposable
    {
        private readonly POSContext _context;
        private readonly BaseRepository<Product, int> _products;
        private readonly IAuthorizationService _authorization;
        private readonly SalesCalculator _calculator = new SalesCalculator();
        private readonly bool _ownsContext;

        public SalesCartService(POSContext context, IAuthorizationService authorization)
            : this(context, authorization, false) { }

        private SalesCartService(POSContext context, IAuthorizationService authorization, bool ownsContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _products = new BaseRepository<Product, int>(_context, false);
            _ownsContext = ownsContext;
        }

        public SaleCartDTO Create(int registerStationId, int? customerId = null)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.View);
            if (registerStationId <= 0) throw new ArgumentOutOfRangeException(nameof(registerStationId));
            return Recalculate(new SaleCartDTO { RegisterStationId = registerStationId, CustomerId = customerId });
        }

        public SaleCartProductPageDTO SearchProducts(string search, int pageNumber = 1, int pageSize = 50)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.View);
            var term = search?.Trim();
            if ((term?.Length ?? 0) > 100) throw new InvalidOperationException("Product search text is too long.");
            var size = Math.Max(1, Math.Min(200, pageSize));
            var query = from product in _products.GetAllAsQueryable().AsNoTracking()
                        join balance in _context.InventoryBalances.AsNoTracking() on product.Id equals balance.ProductId
                        where product.IsActive
                        select new { product, balance };
            if (!string.IsNullOrEmpty(term)) query = query.Where(x =>
                x.product.Name.Contains(term) || x.product.Sku.Contains(term) || x.product.Barcode.Contains(term));
            var count = query.Count();
            var pages = Math.Max(1, (int)Math.Ceiling(count / (double)size));
            var page = Math.Max(1, Math.Min(pages, pageNumber));
            return new SaleCartProductPageDTO
            {
                TotalCount = count,
                TotalPages = pages,
                PageNumber = page,
                Items = query.OrderBy(x => x.product.Name).ThenBy(x => x.product.Id)
                    .Skip((page - 1) * size).Take(size).Select(x => new SaleCartLineDTO
                    {
                        ProductId = x.product.Id,
                        ProductName = x.product.Name,
                        Sku = x.product.Sku,
                        Barcode = x.product.Barcode,
                        QuantityAvailable = x.balance.QuantityOnHand,
                        UnitPrice = x.product.Price
                    }).ToList()
            };
        }

        public SaleCartDTO AddByBarcode(SaleCartDTO cart, string barcode, int quantity = 1)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.View);
            var normalized = barcode?.Trim();
            if (string.IsNullOrEmpty(normalized) || normalized.Length > 50)
                throw new InvalidOperationException("Enter a valid barcode.");
            var matches = _products.GetAllAsQueryable().AsNoTracking()
                .Where(x => x.Barcode == normalized).Select(x => x.Id).Take(2).ToList();
            if (matches.Count != 1)
                throw new InvalidOperationException(matches.Count == 0 ? "No active product matches that barcode." : "The barcode is ambiguous.");
            return AddProduct(cart, matches[0], quantity);
        }

        public SaleCartDTO AddProduct(SaleCartDTO cart, int productId, int quantity = 1)
        {
            ValidateCart(cart);
            if (productId <= 0 || quantity <= 0) throw new InvalidOperationException("A valid product and positive quantity are required.");
            var existing = cart.Items.SingleOrDefault(x => x.ProductId == productId);
            if (existing == null)
                cart.Items.Add(new SaleCartLineDTO { ProductId = productId, Quantity = quantity });
            else
                existing.Quantity = checked(existing.Quantity + quantity);
            return Recalculate(cart);
        }

        public SaleCartDTO UpdateQuantity(SaleCartDTO cart, int productId, int quantity)
        {
            ValidateCart(cart);
            var line = cart.Items.SingleOrDefault(x => x.ProductId == productId);
            if (line == null) throw new InvalidOperationException("The product is not in the cart.");
            if (quantity <= 0) throw new InvalidOperationException("Cart quantity must be positive.");
            line.Quantity = quantity;
            return Recalculate(cart);
        }

        public SaleCartDTO RemoveProduct(SaleCartDTO cart, int productId)
        {
            ValidateCart(cart);
            var line = cart.Items.SingleOrDefault(x => x.ProductId == productId);
            if (line != null) cart.Items.Remove(line);
            return Recalculate(cart);
        }

        public SaleCartDTO Recalculate(SaleCartDTO cart)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.View);
            ValidateCart(cart);
            var store = _context.StoreSettings.AsNoTracking().SingleOrDefault()
                ?? throw new InvalidOperationException("Store settings are required.");
            var tax = _context.TaxRates.AsNoTracking().SingleOrDefault(x =>
                x.Id == store.DefaultTaxRateId && x.IsActive && !x.EffectiveToUtc.HasValue)
                ?? throw new InvalidOperationException("The active default tax rate is required.");
            decimal subtotal = 0, taxTotal = 0;
            foreach (var line in cart.Items)
            {
                var product = _products.GetById(line.ProductId);
                if (product == null || !product.IsActive)
                    throw new InvalidOperationException($"Product with Id {line.ProductId} is missing or inactive.");
                var balance = _context.InventoryBalances.AsNoTracking().SingleOrDefault(x => x.ProductId == line.ProductId)
                    ?? throw new InvalidOperationException($"Inventory balance is missing for '{product.Name}'.");
                var amount = _calculator.RoundMoney(product.Price * line.Quantity, store.MoneyDecimalPlaces);
                var lineTax = _calculator.CalculateTax(amount, tax.Rate, tax.IsInclusive, store.MoneyDecimalPlaces);
                var lineSubtotal = tax.IsInclusive ? amount - lineTax : amount;
                line.ProductName = product.Name;
                line.Sku = product.Sku;
                line.Barcode = product.Barcode;
                line.QuantityAvailable = balance.QuantityOnHand;
                line.UnitPrice = product.Price;
                line.TaxRate = tax.Rate;
                line.TaxAmount = lineTax;
                line.LineTotal = lineSubtotal + lineTax;
                subtotal += lineSubtotal;
                taxTotal += lineTax;
            }
            cart.CurrencyCode = store.CurrencyCode;
            cart.Subtotal = subtotal;
            cart.TaxAmount = taxTotal;
            if (cart.DiscountAmount < 0 || cart.DiscountAmount > subtotal + taxTotal || decimal.Round(cart.DiscountAmount, 2) != cart.DiscountAmount)
                throw new InvalidOperationException("The cart discount is invalid.");
            if (cart.DiscountAmount > 0)
                PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.Edit);
            cart.TotalAmount = _calculator.RoundMoney(subtotal + taxTotal - cart.DiscountAmount, store.MoneyDecimalPlaces);
            return cart;
        }

        private static void ValidateCart(SaleCartDTO cart)
        {
            if (cart == null) throw new ArgumentNullException(nameof(cart));
            if (cart.RegisterStationId <= 0) throw new InvalidOperationException("A valid register is required.");
            if (cart.Items == null) throw new InvalidOperationException("Cart items are required.");
            if (cart.Items.Count > 500) throw new InvalidOperationException("A cart cannot contain more than 500 products.");
            if (cart.Items.Any(x => x == null || x.ProductId <= 0 || x.Quantity <= 0) ||
                cart.Items.GroupBy(x => x.ProductId).Any(x => x.Count() > 1))
                throw new InvalidOperationException("Cart products must be unique with positive quantities.");
        }

        public void Dispose()
        {
            _products.Dispose();
            if (_ownsContext) _context.Dispose();
        }
    }
}
