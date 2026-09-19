using POS.Data.Context;
using POS.Domains.BusinessObjects;
using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Services.Repository;
using POS.Services.Settings;
using POS.Services;
using System;
using System.Data.Entity;
using System.Linq;
using POS.Domains.Operations;
using System.Collections.Generic;
using POS.Core.Security;
using POS.Services.Security;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using POS.Models.Operations;

namespace POS.Services.Point_of_Sale
{
    public class SalesService : IDisposable
    {
        private readonly POSContext _context;
        private readonly BaseRepository<Product, int> _productRepo;
        private readonly BaseRepository<Sale, int> _salesRepo;
        private readonly bool _ownsContext;
        private readonly SalesCalculator _calculator;
        private readonly IClock _clock;
        private readonly NumberSequenceService _sequenceService;
        private readonly ICurrentUser _currentUser;
        private readonly IAuthorizationService _authorization;

        public SalesService()
            : this(new POSContext(), new SystemClock(), new CurrentUserAccessor(), null, true)
        {
        }

        public SalesService(POSContext context)
            : this(context, new SystemClock(), new CurrentUserAccessor(), null, false)
        {
        }

        public SalesService(POSContext context, IClock clock)
            : this(context, clock, new CurrentUserAccessor(), null, false)
        {
        }

        public SalesService(POSContext context, IClock clock, ICurrentUser currentUser, IAuthorizationService authorization)
            : this(context, clock, currentUser, authorization ?? throw new ArgumentNullException(nameof(authorization)), false)
        {
        }

        private SalesService(POSContext context, IClock clock, ICurrentUser currentUser, IAuthorizationService authorization, bool ownsContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _authorization = authorization ?? new ClaimsAuthorizationService(_currentUser);
            _ownsContext = ownsContext;
            _calculator = new SalesCalculator();
            _productRepo = new BaseRepository<Product, int>(_context, false);
            _salesRepo = new BaseRepository<Sale, int>(_context, false);
            _sequenceService = new NumberSequenceService(
                _context,
                _authorization);
        }

        public Sale CreateSale(Sale sale)
        {
            return CreateSale(sale, null, null);
        }

        private Sale CreateSale(Sale sale, int? heldSaleId, string heldRevision)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.Add);
            var cashierId = _currentUser.UserId;
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(cashierId))
                throw new UnauthorizedAccessException("An authenticated cashier is required to complete a sale.");
            ValidateSale(sale);
            if (sale.DiscountAmount > 0)
                PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.Edit);
            NormalizeAndValidatePayments(sale);
            var requestHash = ComputeRequestHash(sale);
            if (sale.CustomerId.HasValue)
                PermissionGuard.Demand(_authorization, ResourceCodes.Customers, ClaimActionType.View);

            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin(sale.RegisterStationId))
            {
                if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                    throw new InvalidOperationException("Checkout requires a context without pending changes or an active transaction.");
                RestoreTrackedState();

                using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        var registerId = sale.RegisterStationId.Value;
                        var shiftId = sale.CashierShiftId.Value;
                        var existingSale = _context.Sales
                            .Include(existing => existing.SaleItems)
                            .Include(existing => existing.Payments)
                            .SingleOrDefault(existing => existing.RequestId == sale.RequestId);
                        if (existingSale != null)
                        {
                            if (!string.Equals(existingSale.RequestHash, requestHash, StringComparison.Ordinal) ||
                                !string.Equals(existingSale.CashierUserId, cashierId, StringComparison.Ordinal))
                                throw new InvalidOperationException("The checkout request ID was already used for different sale details or by another cashier.");

                            transaction.Commit();
                            return existingSale;
                        }

                        if (heldSaleId.HasValue)
                        {
                            var held = _context.Sales
                                .Include(candidate => candidate.SaleItems)
                                .SingleOrDefault(candidate => candidate.Id == heldSaleId.Value);
                            if (held == null || held.Status != DocumentStatus.Held)
                                throw new InvalidOperationException("The held sale was not found or is no longer available.");
                            EnsureSaleAccess(held);
                            var expectedRevision = ParseRevision(heldRevision, "held sale");
                            if (!held.RowVersion.SequenceEqual(expectedRevision))
                                throw new InvalidOperationException("The held sale changed. Reload it before checkout.");
                            if (held.RegisterStationId != sale.RegisterStationId || held.CustomerId != sale.CustomerId ||
                                held.DiscountAmount != sale.DiscountAmount ||
                                !HaveSameItems(held.SaleItems, sale.SaleItems))
                                throw new InvalidOperationException("Checkout details do not match the reviewed held sale.");
                            held.Status = DocumentStatus.Cancelled;
                        }

                        var register = _context.RegisterStations.SingleOrDefault(candidate =>
                            candidate.Id == registerId && candidate.IsActive);
                        if (register == null)
                            throw new InvalidOperationException("The selected register does not exist or is inactive.");

                        var shift = _context.CashierShifts.SingleOrDefault(candidate => candidate.Id == shiftId);
                        if (shift == null)
                            throw new InvalidOperationException("The selected cashier shift was not found.");
                        if (shift.Status != ShiftStatus.Open)
                            throw new InvalidOperationException("Checkout requires an open cashier shift.");
                        if (shift.RegisterStationId != registerId)
                            throw new InvalidOperationException("The cashier shift does not belong to the selected register.");
                        if (!string.Equals(shift.CashierUserId, cashierId, StringComparison.Ordinal))
                            throw new UnauthorizedAccessException("The cashier shift belongs to another user.");

                        Customer customer = null;
                        if (sale.CustomerId.HasValue)
                        {
                            customer = _context.Customers.SingleOrDefault(candidate =>
                                candidate.Id == sale.CustomerId.Value && candidate.IsActive);
                            if (customer == null)
                                throw new InvalidOperationException("The selected customer does not exist or is inactive.");
                        }

                        decimal subtotal = 0;
                        decimal taxTotal = 0;
                        var configuration = GetCheckoutConfiguration();
                        var tax = configuration.Tax;
                        var decimalPlaces = configuration.Store.MoneyDecimalPlaces;
                        var movements = new List<StockMovement>();
                        var reconciledProducts = new HashSet<int>();

                        foreach (var item in sale.SaleItems.OrderBy(item => item.ProductId))
                        {
                            var product = _productRepo.GetById(item.ProductId);

                            if (product == null)
                            {
                                throw new InvalidOperationException(
                                    $"Product with Id {item.ProductId} was not found.");
                            }

                            if (!product.IsActive)
                                throw new InvalidOperationException($"Product '{product.Name}' is inactive and cannot be sold.");

                            var balance = _context.InventoryBalances.SingleOrDefault(b => b.ProductId == item.ProductId);
                            if (balance == null)
                                throw new InvalidOperationException($"Inventory balance is missing for '{product.Name}'. Reconcile inventory before checkout.");
                            if (balance.QuantityOnHand != product.Quantity)
                                throw new InvalidOperationException($"Inventory quantities disagree for '{product.Name}'. Reconcile inventory before checkout.");

                            // Validate persisted ledger facts before this product's first
                            // deduction. Later lines use the already-decremented tracked
                            // balance; their pending sale movements are not in SQL yet.
                            if (reconciledProducts.Add(product.Id))
                            {
                                var ledgerQuantity = _context.StockMovements
                                    .Where(m => m.ProductId == product.Id)
                                    .Sum(m => (long?)m.QuantityDelta) ?? 0L;
                                if (ledgerQuantity != balance.QuantityOnHand)
                                    throw new InvalidOperationException($"The stock ledger and balance disagree for '{product.Name}'. Reconcile inventory before checkout.");
                            }

                            if (balance.QuantityOnHand < item.Quantity)
                            {
                                throw new InvalidOperationException(
                                    $"Not enough stock for '{product.Name}'. " +
                                    $"Available: {balance.QuantityOnHand}, " +
                                    $"requested: {item.Quantity}.");
                            }

                            item.UnitPrice = product.Price;
                            item.CostPrice = product.CostPrice;
                            item.ProductName = product.Name;
                            item.Sku = product.Sku;
                            item.Barcode = product.Barcode;
                            var lineAmount = _calculator.RoundMoney(product.Price * item.Quantity, decimalPlaces);
                            item.TaxRate = tax.Rate;
                            item.TaxAmount = _calculator.CalculateTax(
                                lineAmount,
                                tax.Rate,
                                tax.IsInclusive,
                                decimalPlaces);
                            item.Subtotal = tax.IsInclusive
                                ? lineAmount - item.TaxAmount
                                : lineAmount;
                            subtotal += item.Subtotal;
                            taxTotal += item.TaxAmount;

                            balance.QuantityOnHand = checked(balance.QuantityOnHand - item.Quantity);
                            product.Quantity = balance.QuantityOnHand;
                            movements.Add(new StockMovement
                            {
                                ProductId = product.Id,
                                MovementType = StockMovementType.Sale,
                                QuantityDelta = -item.Quantity,
                                Reason = "Completed sale",
                                ReferenceType = "SaleReceipt"
                            });
                        }

                        sale.SaleDate = _clock.UtcNow;
                        sale.RequestHash = requestHash;
                        sale.ReceiptNumber = _sequenceService.AllocateReceiptNumber();
                        sale.Status = DocumentStatus.Completed;
                        sale.Subtotal = subtotal;
                        sale.TaxAmount = taxTotal;
                        if (sale.DiscountAmount > subtotal + taxTotal)
                            throw new InvalidOperationException("The sale discount cannot exceed the amount due.");
                        sale.TotalAmount = _calculator.RoundMoney(subtotal + taxTotal - sale.DiscountAmount, decimalPlaces);
                        sale.CurrencyCode = configuration.Store.CurrencyCode;
                        sale.TaxName = tax.Name;
                        sale.TaxInclusive = tax.IsInclusive;
                        sale.MoneyDecimalPlaces = decimalPlaces;
                        sale.RoundingMethod = MidpointRounding.AwayFromZero.ToString();
                        sale.StoreName = configuration.Store.StoreName;
                        sale.StoreAddress = configuration.Store.Address;
                        sale.StoreTaxIdentifier = configuration.Store.TaxIdentifier;
                        sale.ReceiptFooter = configuration.Store.ReceiptFooter;
                        sale.RegisterCode = register.Code;
                        sale.RegisterName = register.Name;
                        sale.CashierName = _context.Users.Where(user => user.Id == cashierId)
                            .Select(user => user.UserName).SingleOrDefault();
                        sale.CustomerCode = customer?.Code;
                        sale.CustomerName = customer?.Name;

                        ApplyTenderTotals(sale);
                        sale.CashierUserId = cashierId;
                        foreach (var payment in sale.Payments)
                        {
                            payment.Status = PaymentStatus.Completed;
                            payment.CreatedUtc = sale.SaleDate;
                        }
                        foreach (var movement in movements)
                        {
                            movement.ReferenceId = sale.ReceiptNumber;
                            movement.UserId = sale.CashierUserId;
                            movement.CreatedUtc = sale.SaleDate;
                            _context.StockMovements.Add(movement);
                        }
                        _salesRepo.Add(sale);

                        _context.SaveChanges();
                        transaction.Commit();
                        return sale;
                    }
                    catch (Exception exception)
                    {
                        try
                        {
                            // SQL Server/EF may already roll back and detach the provider
                            // transaction after a connection-level concurrency failure.
                            // Calling Rollback again in that state masks the real error.
                            if (transaction.UnderlyingTransaction.Connection != null)
                                transaction.Rollback();
                        }
                        finally { RestoreTrackedState(); }
                        if (POS.Services.Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "IX_RequestId"))
                            throw new System.ComponentModel.DataAnnotations.ValidationException(
                                "This checkout request was committed concurrently. Retry with the same request ID and unchanged details to retrieve the completed sale.", exception);
                        if (POS.Services.Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "UX_Payments_CompletedExternalReference"))
                            throw new System.ComponentModel.DataAnnotations.ValidationException(
                                "A completed payment already uses that provider reference. Review payment history before retrying checkout.", exception);
                        if (POS.Services.Inventory.InventoryConflict.IsRecognized(exception))
                            throw new System.ComponentModel.DataAnnotations.ValidationException(
                                "Checkout conflicted with another operation. Reload stock and review the cart before starting a new checkout attempt.",
                                exception);
                        throw;
                    }
                }
            }
        }

        public Sale Checkout(SaleCheckoutDTO request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            var sale = new Sale
            {
                RequestId = request.RequestId,
                RegisterStationId = request.RegisterStationId,
                CashierShiftId = request.CashierShiftId,
                CustomerId = request.CustomerId,
                DiscountAmount = request.DiscountAmount
            };
            foreach (var item in request.Items ?? new List<SaleCheckoutLineDTO>())
            {
                if (item == null)
                    throw new InvalidOperationException("Checkout items cannot contain an empty entry.");
                sale.SaleItems.Add(new SaleItem { ProductId = item.ProductId, Quantity = item.Quantity });
            }
            foreach (var tender in request.Tenders ?? new List<SaleTenderDTO>())
            {
                if (tender == null)
                    throw new InvalidOperationException("Checkout tenders cannot contain an empty entry.");
                sale.Payments.Add(new Payment
                {
                    TenderType = tender.TenderType,
                    Amount = tender.Amount,
                    ExternalReference = tender.ExternalReference
                });
            }
            return CreateSale(sale, request.HeldSaleId, request.HeldRevision);
        }

        public HeldSaleDTO SaveHeldSale(HeldSaleSaveDTO request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.Add);
            var cashierId = RequireAuthenticatedCashier();
            ValidateHeldRequest(request);
            if (request.DiscountAmount > 0)
                PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.Edit);
            if (request.CustomerId.HasValue)
                PermissionGuard.Demand(_authorization, ResourceCodes.Customers, ClaimActionType.View);
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Holding a sale requires a context without pending changes or an active transaction.");
            RestoreTrackedState();

            using (var audit = AuditOperation.Begin(request.RegisterStationId))
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    if (!_context.RegisterStations.Any(x => x.Id == request.RegisterStationId && x.IsActive))
                        throw new InvalidOperationException("The selected register does not exist or is inactive.");
                    if (request.CustomerId.HasValue && !_context.Customers.Any(x => x.Id == request.CustomerId.Value && x.IsActive))
                        throw new InvalidOperationException("The selected customer does not exist or is inactive.");

                    var requestHash = ComputeHeldHash(request);
                    Sale held;
                    if (request.Id.HasValue)
                    {
                        held = _context.Sales.Include(x => x.SaleItems).SingleOrDefault(x => x.Id == request.Id.Value);
                        if (held == null || held.Status != DocumentStatus.Held)
                            throw new InvalidOperationException("The held sale was not found or is no longer editable.");
                        EnsureSaleAccess(held);
                        if (held.RequestId != request.RequestId)
                            throw new InvalidOperationException("The held-sale request ID cannot be changed.");
                        var revision = ParseRevision(request.Revision, "held sale");
                        if (!held.RowVersion.SequenceEqual(revision))
                            throw new InvalidOperationException("The held sale changed. Reload it before saving.");
                        _context.SaleItems.RemoveRange(held.SaleItems.ToList());
                        held.SaleItems.Clear();
                    }
                    else
                    {
                        held = _context.Sales.Include(x => x.SaleItems)
                            .SingleOrDefault(x => x.RequestId == request.RequestId);
                        if (held != null)
                        {
                            EnsureSaleAccess(held);
                            if (held.Status != DocumentStatus.Held || !string.Equals(held.RequestHash, requestHash, StringComparison.Ordinal))
                                throw new InvalidOperationException("The held-sale request ID was already used for different details.");
                            transaction.Commit();
                            return ToHeldDTO(held);
                        }
                        held = new Sale { RequestId = request.RequestId, Status = DocumentStatus.Held };
                        _context.Sales.Add(held);
                    }

                    PopulateHeldSale(held, request, cashierId, requestHash);
                    _context.SaveChanges();
                    transaction.Commit();
                    return ToHeldDTO(held);
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); }
                    finally { RestoreTrackedState(); }
                    if (POS.Services.Inventory.InventoryConflict.IsUniqueIndexViolation(exception, "IX_RequestId"))
                        throw new System.ComponentModel.DataAnnotations.ValidationException(
                            "This held-sale request was committed concurrently. Reload held sales and retry with the same request ID and unchanged details.", exception);
                    throw;
                }
            }
        }

        public HeldSaleDTO GetHeldSale(int id)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.View);
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            var held = _context.Sales.AsNoTracking().Include(x => x.SaleItems)
                .SingleOrDefault(x => x.Id == id && x.Status == DocumentStatus.Held);
            if (held == null) throw new InvalidOperationException("The held sale was not found.");
            EnsureSaleAccess(held);
            return ToHeldDTO(held);
        }

        public IReadOnlyList<HeldSaleDTO> GetHeldSales(int maximum = 100)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.View);
            var cashierId = RequireAuthenticatedCashier();
            var limit = Math.Max(1, Math.Min(200, maximum));
            var canManageAll = _authorization.HasPermission(ResourceCodes.Sales, ClaimActionType.Edit);
            var query = _context.Sales.AsNoTracking().Include(x => x.SaleItems)
                .Where(x => x.Status == DocumentStatus.Held);
            if (!canManageAll) query = query.Where(x => x.CashierUserId == cashierId);
            return query.OrderByDescending(x => x.SaleDate).ThenByDescending(x => x.Id)
                .Take(limit).ToList().Select(ToHeldDTO).ToList();
        }

        public void CancelHeldSale(int id, string revision)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Sales, ClaimActionType.Add);
            RequireAuthenticatedCashier();
            var expected = ParseRevision(revision, "held sale");
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Cancelling a held sale requires a clean context.");
            RestoreTrackedState();
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var held = _context.Sales.SingleOrDefault(x => x.Id == id);
                    if (held == null || held.Status != DocumentStatus.Held)
                        throw new InvalidOperationException("The held sale was not found or is no longer cancellable.");
                    EnsureSaleAccess(held);
                    if (!held.RowVersion.SequenceEqual(expected))
                        throw new InvalidOperationException("The held sale changed. Reload it before cancellation.");
                    held.Status = DocumentStatus.Cancelled;
                    using (var audit = AuditOperation.Begin(held.RegisterStationId))
                        _context.SaveChanges();
                    transaction.Commit();
                }
                catch
                {
                    try { transaction.Rollback(); }
                    finally { RestoreTrackedState(); }
                    throw;
                }
            }
        }

        private void RestoreTrackedState()
        {
            foreach (var entry in _context.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }

        public void RemoveItemFromCart(Sale sale, int productId)
        {
            var item = sale.SaleItems
                .FirstOrDefault(x => x.ProductId == productId);

            if (item != null)
            {
                sale.SaleItems.Remove(item);
            }
        }

        public void UpdateItemQuantity(
            Sale sale, int productId, int quantity)
        {
            var item = sale.SaleItems
                .FirstOrDefault(x => x.ProductId == productId);

            if (item == null)
                return;

            item.Quantity = quantity;
            item.Subtotal = item.UnitPrice * quantity;
        }

        public decimal CalculateSubtotal(Sale sale)
        {
            decimal subtotal = 0;

            foreach (var item in sale.SaleItems)
            {
                subtotal += item.Subtotal;
            }

            return subtotal;
        }

        public decimal CalculateVAT(decimal subtotal)
        {
            var configuration = GetCheckoutConfiguration();
            return _calculator.CalculateTax(
                subtotal,
                configuration.Tax.Rate,
                configuration.Tax.IsInclusive,
                configuration.Store.MoneyDecimalPlaces);
        }

        private CheckoutConfiguration GetCheckoutConfiguration()
        {
            var stores = _context.StoreSettings.AsNoTracking().ToList();
            if (stores.Count != 1)
                throw new InvalidOperationException(
                    "Exactly one store configuration is required before checkout.");

            var defaultTaxRateId = stores[0].DefaultTaxRateId;
            var tax = _context.TaxRates.AsNoTracking().SingleOrDefault(x =>
                x.Id == defaultTaxRateId && x.IsActive && !x.EffectiveToUtc.HasValue);
            if (tax == null)
                throw new InvalidOperationException(
                    "The store's default tax configuration is missing or inactive.");
            return new CheckoutConfiguration { Store = stores[0], Tax = tax };
        }

        private sealed class CheckoutConfiguration
        {
            public POS.Domains.Operations.StoreSetting Store { get; set; }
            public POS.Domains.Operations.TaxRate Tax { get; set; }
        }

        public decimal ApplyDiscount(decimal subtotal, decimal discount)
        {
            return _calculator.ApplyDiscount(subtotal, discount);
        }

        public decimal CalculateTotal(decimal subtotal, decimal vat, decimal discount)
        {
            return _calculator.CalculateTotal(subtotal, vat, discount);
        }

        private static void ValidateSale(Sale sale)
        {
            if (sale == null)
                throw new ArgumentNullException(nameof(sale));
            if (sale.Id != 0)
                throw new InvalidOperationException("A completed or persisted sale cannot be submitted as a new sale.");
            if (sale.RequestId == Guid.Empty)
                throw new InvalidOperationException("A checkout request ID is required and must be retained unchanged for retries.");
            if (!sale.RegisterStationId.HasValue || sale.RegisterStationId.Value <= 0)
                throw new InvalidOperationException("A valid register is required to complete a sale.");
            if (!sale.CashierShiftId.HasValue || sale.CashierShiftId.Value <= 0)
                throw new InvalidOperationException("An open cashier shift is required to complete a sale.");
            if (sale.CustomerId.HasValue && sale.CustomerId.Value <= 0)
                throw new InvalidOperationException("The selected customer is invalid.");
            if (sale.DiscountAmount < 0 || sale.DiscountAmount > 9999999999999999.99m || decimal.Round(sale.DiscountAmount, 2) != sale.DiscountAmount)
                throw new InvalidOperationException("The sale discount must be nonnegative and use no more than two decimal places.");

            if (sale.SaleItems == null || !sale.SaleItems.Any())
                throw new InvalidOperationException("A sale must contain at least one item.");
            if (sale.SaleItems.Count > 500)
                throw new InvalidOperationException("A sale cannot contain more than 500 item lines.");

            if (sale.SaleItems.Any(item => item == null || item.ProductId <= 0 || item.Quantity <= 0))
                throw new InvalidOperationException("Sale item quantities must be greater than zero.");
            if (sale.SaleItems.GroupBy(item => item.ProductId).Any(group => group.Count() > 1))
                throw new InvalidOperationException("Combine duplicate products into one sale line.");
        }

        private static string ComputeRequestHash(Sale sale)
        {
            var canonical = new StringBuilder()
                .Append("checkout|v1|")
                .Append(sale.RegisterStationId.Value).Append('|')
                .Append(sale.CashierShiftId.Value).Append('|')
                .Append(sale.CustomerId.HasValue ? sale.CustomerId.Value.ToString(CultureInfo.InvariantCulture) : "walk-in").Append('|')
                .Append(sale.DiscountAmount.ToString("0.00", CultureInfo.InvariantCulture)).Append('|')
                .Append(sale.CashReceived.ToString("0.############################", CultureInfo.InvariantCulture));

            foreach (var item in sale.SaleItems
                .OrderBy(candidate => candidate.ProductId)
                .ThenBy(candidate => candidate.Quantity))
            {
                canonical.Append('|')
                    .Append(item.ProductId.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(item.Quantity.ToString(CultureInfo.InvariantCulture));
            }

            foreach (var payment in sale.Payments
                .OrderBy(candidate => candidate.TenderType)
                .ThenBy(candidate => candidate.Amount)
                .ThenBy(candidate => candidate.ExternalReference))
            {
                canonical.Append('|')
                    .Append((int)payment.TenderType).Append(':')
                    .Append(payment.Amount.ToString("0.00", CultureInfo.InvariantCulture)).Append(':')
                    .Append(payment.ExternalReference ?? string.Empty);
            }

            using (var sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())))
                    .Replace("-", string.Empty);
            }
        }

        private static string ComputeHeldHash(HeldSaleSaveDTO request)
        {
            var canonical = new StringBuilder()
                .Append("held-sale|v1|")
                .Append(request.RegisterStationId).Append('|')
                .Append(request.CustomerId.HasValue ? request.CustomerId.Value.ToString(CultureInfo.InvariantCulture) : "walk-in");
            canonical.Append('|').Append(request.DiscountAmount.ToString("0.00", CultureInfo.InvariantCulture));
            foreach (var item in request.Items.OrderBy(x => x.ProductId))
                canonical.Append('|').Append(item.ProductId).Append(':').Append(item.Quantity);
            using (var sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))).Replace("-", string.Empty);
        }

        private void PopulateHeldSale(Sale held, HeldSaleSaveDTO request, string cashierId, string requestHash)
        {
            var configuration = GetCheckoutConfiguration();
            decimal subtotal = 0;
            decimal taxTotal = 0;
            foreach (var requested in request.Items.OrderBy(x => x.ProductId))
            {
                var product = _productRepo.GetById(requested.ProductId);
                if (product == null)
                    throw new InvalidOperationException($"Product with Id {requested.ProductId} was not found.");
                if (!product.IsActive)
                    throw new InvalidOperationException($"Product '{product.Name}' is inactive.");
                var lineAmount = _calculator.RoundMoney(product.Price * requested.Quantity, configuration.Store.MoneyDecimalPlaces);
                var taxAmount = _calculator.CalculateTax(lineAmount, configuration.Tax.Rate,
                    configuration.Tax.IsInclusive, configuration.Store.MoneyDecimalPlaces);
                var lineSubtotal = configuration.Tax.IsInclusive ? lineAmount - taxAmount : lineAmount;
                held.SaleItems.Add(new SaleItem
                {
                    ProductId = product.Id,
                    Quantity = requested.Quantity,
                    ProductName = product.Name,
                    Sku = product.Sku,
                    Barcode = product.Barcode,
                    UnitPrice = product.Price,
                    CostPrice = product.CostPrice,
                    TaxRate = configuration.Tax.Rate,
                    TaxAmount = taxAmount,
                    Subtotal = lineSubtotal
                });
                subtotal += lineSubtotal;
                taxTotal += taxAmount;
            }
            held.RegisterStationId = request.RegisterStationId;
            held.CashierShiftId = null;
            held.CustomerId = request.CustomerId;
            held.CashierUserId = cashierId;
            held.SaleDate = _clock.UtcNow;
            held.Status = DocumentStatus.Held;
            held.RequestHash = requestHash;
            held.Subtotal = subtotal;
            held.DiscountAmount = request.DiscountAmount;
            held.TaxAmount = taxTotal;
            held.RoundingAmount = 0;
            if (request.DiscountAmount > subtotal + taxTotal)
                throw new InvalidOperationException("The held-sale discount cannot exceed its total.");
            held.TotalAmount = _calculator.RoundMoney(subtotal + taxTotal - request.DiscountAmount, configuration.Store.MoneyDecimalPlaces);
            held.CashReceived = 0;
            held.Change = 0;
            held.CurrencyCode = configuration.Store.CurrencyCode;
            held.TaxName = configuration.Tax.Name;
            held.TaxInclusive = configuration.Tax.IsInclusive;
            held.MoneyDecimalPlaces = configuration.Store.MoneyDecimalPlaces;
            held.RoundingMethod = MidpointRounding.AwayFromZero.ToString();
        }

        private HeldSaleDTO ToHeldDTO(Sale sale)
        {
            var registerCode = _context.RegisterStations.AsNoTracking()
                .Where(x => x.Id == sale.RegisterStationId).Select(x => x.Code).SingleOrDefault();
            var customerName = sale.CustomerId.HasValue
                ? _context.Customers.AsNoTracking().Where(x => x.Id == sale.CustomerId.Value).Select(x => x.Name).SingleOrDefault()
                : null;
            return new HeldSaleDTO
            {
                Id = sale.Id,
                RequestId = sale.RequestId,
                Revision = Convert.ToBase64String(sale.RowVersion),
                RegisterStationId = sale.RegisterStationId.Value,
                RegisterCode = registerCode,
                CustomerId = sale.CustomerId,
                CustomerName = customerName,
                CashierUserId = sale.CashierUserId,
                HeldUtc = sale.SaleDate,
                Subtotal = sale.Subtotal,
                TaxAmount = sale.TaxAmount,
                DiscountAmount = sale.DiscountAmount,
                TotalAmount = sale.TotalAmount,
                Items = sale.SaleItems.OrderBy(x => x.Id).Select(x => new HeldSaleLineDTO
                {
                    ProductId = x.ProductId,
                    ProductName = x.ProductName,
                    Sku = x.Sku,
                    Barcode = x.Barcode,
                    Quantity = x.Quantity,
                    UnitPrice = x.UnitPrice,
                    TaxAmount = x.TaxAmount,
                    LineTotal = x.Subtotal + x.TaxAmount
                }).ToList()
            };
        }

        private string RequireAuthenticatedCashier()
        {
            var userId = _currentUser.UserId;
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(userId))
                throw new UnauthorizedAccessException("An authenticated cashier is required.");
            return userId;
        }

        private void EnsureSaleAccess(Sale sale)
        {
            var userId = RequireAuthenticatedCashier();
            if (!string.Equals(sale.CashierUserId, userId, StringComparison.Ordinal) &&
                !_authorization.HasPermission(ResourceCodes.Sales, ClaimActionType.Edit))
                throw new UnauthorizedAccessException("The held sale belongs to another cashier.");
        }

        private static void ValidateHeldRequest(HeldSaleSaveDTO request)
        {
            if (request.RequestId == Guid.Empty)
                throw new InvalidOperationException("A held-sale request ID is required.");
            if (request.RegisterStationId <= 0)
                throw new InvalidOperationException("A valid register is required.");
            if (request.CustomerId.HasValue && request.CustomerId.Value <= 0)
                throw new InvalidOperationException("The selected customer is invalid.");
            if (request.DiscountAmount < 0 || request.DiscountAmount > 9999999999999999.99m || decimal.Round(request.DiscountAmount, 2) != request.DiscountAmount)
                throw new InvalidOperationException("The held-sale discount must be nonnegative and use no more than two decimal places.");
            if (request.Items == null || request.Items.Count == 0 || request.Items.Count > 500)
                throw new InvalidOperationException("A held sale must contain between 1 and 500 items.");
            if (request.Items.Any(x => x == null || x.ProductId <= 0 || x.Quantity <= 0))
                throw new InvalidOperationException("Held-sale items require a valid product and positive quantity.");
            if (request.Items.GroupBy(x => x.ProductId).Any(group => group.Count() > 1))
                throw new InvalidOperationException("Combine duplicate products into one held-sale line.");
            if (request.Id.HasValue && request.Id.Value <= 0)
                throw new InvalidOperationException("The held-sale ID is invalid.");
        }

        private static byte[] ParseRevision(string revision, string label)
        {
            try
            {
                var value = Convert.FromBase64String(revision ?? string.Empty);
                if (value.Length != 8) throw new FormatException();
                return value;
            }
            catch (FormatException)
            {
                throw new InvalidOperationException($"A valid {label} revision is required.");
            }
        }

        private static bool HaveSameItems(IEnumerable<SaleItem> left, IEnumerable<SaleItem> right)
        {
            var first = left.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
            var second = right.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
            return first.Count == second.Count && first.All(pair => second.TryGetValue(pair.Key, out var quantity) && quantity == pair.Value);
        }

        private static void NormalizeAndValidatePayments(Sale sale)
        {
            if (sale.Payments == null || sale.Payments.Count == 0)
                throw new InvalidOperationException("At least one payment tender is required.");
            if (sale.Payments.Count > 10)
                throw new InvalidOperationException("A checkout cannot contain more than 10 payment tenders.");

            foreach (var payment in sale.Payments)
            {
                if (payment == null)
                    throw new InvalidOperationException("Payment tenders cannot contain an empty entry.");
                if (!Enum.IsDefined(typeof(TenderType), payment.TenderType))
                    throw new InvalidOperationException("Select a supported payment tender.");
                if (payment.Amount <= 0 || payment.Amount > 9999999999999999.99m || decimal.Round(payment.Amount, 2) != payment.Amount)
                    throw new InvalidOperationException("Payment amounts must be positive and use no more than two decimal places.");

                payment.ExternalReference = string.IsNullOrWhiteSpace(payment.ExternalReference)
                    ? null
                    : payment.ExternalReference.Trim();
                if ((payment.ExternalReference?.Length ?? 0) > 100 ||
                    (payment.ExternalReference?.Any(char.IsControl) ?? false))
                    throw new InvalidOperationException("Payment references cannot exceed 100 characters or contain control characters.");
                if (payment.TenderType == TenderType.Cash && payment.ExternalReference != null)
                    throw new InvalidOperationException("Cash payments cannot store an external payment reference.");
                if (payment.TenderType != TenderType.Cash && payment.ExternalReference == null)
                    throw new InvalidOperationException("Non-cash payments require a safe provider or transaction reference.");
                if (payment.TenderType == TenderType.StoreCredit && !sale.CustomerId.HasValue)
                    throw new InvalidOperationException("Store credit requires a selected customer.");
            }
        }

        private static void ApplyTenderTotals(Sale sale)
        {
            var tendered = sale.Payments.Sum(payment => payment.Amount);
            if (tendered < sale.TotalAmount)
                throw new InvalidOperationException("Payment tender total is less than the sale total.");

            var cash = sale.Payments
                .Where(payment => payment.TenderType == TenderType.Cash)
                .Sum(payment => payment.Amount);
            var change = tendered - sale.TotalAmount;
            if (change > cash)
                throw new InvalidOperationException("Non-cash tenders cannot exceed the amount due.");

            sale.CashReceived = cash;
            sale.Change = change;
        }

        public void Dispose()
        {
            _productRepo.Dispose();
            _salesRepo.Dispose();
            _sequenceService.Dispose();

            if (_ownsContext)
                _context.Dispose();
        }
    }
}
