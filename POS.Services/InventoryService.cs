using POS.Domains.BusinessObjects;
using POS.Models.Security;
using POS.Data.Context;
using POS.Services.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Common.Enumerations;
using POS.Services.Security;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using POS.Models.Operations;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace POS.Services
{
    public class InventoryService : IDisposable
    {
        private readonly POSContext _context;
        private readonly BaseRepository<Product, int> _productRepo;
        private readonly BaseRepository<Category, int> _categoryRepo;
        private readonly bool _ownsContext;
        private readonly IAuthorizationService _authorization;

        public InventoryService() : this(new POSContext(), new ClaimsAuthorizationService(new CurrentUserAccessor()), true) { }
        public InventoryService(POSContext context) : this(context, new ClaimsAuthorizationService(new CurrentUserAccessor()), false) { }
        public InventoryService(POSContext context, IAuthorizationService authorization) : this(context, authorization, false) { }

        private InventoryService(POSContext context, IAuthorizationService authorization, bool ownsContext)
        {
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ownsContext = ownsContext;
            _productRepo = new BaseRepository<Product, int>(_context);
            _categoryRepo = new BaseRepository<Category, int>(_context);
        }

        public void AddProduct(InventoryDTO product)
        {
            Demand(ClaimActionType.Add);
            if (product != null && product.Id != 0)
                throw new ValidationException("A new product must not have an existing product ID.");
            ExecuteCatalogWrite(() =>
            {
                ValidateProduct(product);
                var sku = string.IsNullOrWhiteSpace(product.Sku) ? null : product.Sku.Trim().ToUpperInvariant();
                var unit = string.IsNullOrWhiteSpace(product.Unit) ? "piece" : product.Unit.Trim();
                ValidateIdentifiers(sku, unit, false);
                DemandUniqueSku(sku, 0);
                var newProduct = new Product
                {
                    Name = product.ProductName.Trim(),
                    Description = string.IsNullOrWhiteSpace(product.Description) ? null : product.Description.Trim(),
                    Price = product.SellingPrice,
                    CostPrice = product.CostPrice,
                    Barcode = product.Barcode.Trim(),
                    Sku = sku,
                    Unit = unit,
                    CategoryId = product.CategoryId,
                    BuyingThreshold = product.BuyingThreshold,
                    IsActive = true,
                };
                _productRepo.Add(newProduct);
                _context.InventoryBalances.Add(new POS.Domains.Operations.InventoryBalance
                {
                    ProductId = newProduct.Id,
                    QuantityOnHand = 0
                });
                _context.SaveChanges();
            });
        }

        public void UpdateProductIdentifiers(ProductIdentifiersDTO identifiers)
        {
            Demand(ClaimActionType.Edit);
            if (identifiers == null || identifiers.ProductId <= 0)
                throw new ValidationException("Select an existing product to update its SKU and unit.");
            var sku = identifiers.Sku?.Trim().ToUpperInvariant();
            var unit = identifiers.Unit?.Trim();
            ValidateIdentifiers(sku, unit, true);
            ExecuteCatalogWrite(() =>
            {
                var product = _productRepo.GetById(identifiers.ProductId);
                if (product == null)
                    throw new ValidationException("The product no longer exists. Reload the product list.");
                DemandUniqueSku(sku, product.Id);
                product.Sku = sku;
                // Unit is a display label; this operation never converts quantities.
                product.Unit = unit;
                _context.SaveChanges();
            });
        }

        private static void ValidateIdentifiers(string sku, string unit, bool requireSku)
        {
            if (requireSku && string.IsNullOrWhiteSpace(sku))
                throw new ValidationException("SKU is required.");
            if (sku != null && (sku.Length > 50 || sku.Any(char.IsControl)))
                throw new ValidationException("SKU cannot exceed 50 characters or contain control characters.");
            if (string.IsNullOrWhiteSpace(unit) || unit.Length > 30 || unit.Any(char.IsControl))
                throw new ValidationException("Unit is required, cannot exceed 30 characters, and cannot contain control characters.");
        }

        private void DemandUniqueSku(string sku, int productId)
        {
            if (sku != null && _productRepo.GetAllAsQueryable()
                .Any(p => p.Id != productId && p.Sku != null && p.Sku.Trim().ToUpper() == sku))
                throw new ValidationException("This SKU is already assigned to another product, including inactive products.");
        }

        public void UpdateProduct(Product product)
        {
            Demand(ClaimActionType.Edit);
            if (product == null) throw new ValidationException("Product is required.");
            UpdateProductDetails(new InventoryDTO
            {
                Id = product.Id, ProductName = product.Name, Description = product.Description,
                SellingPrice = product.Price, CostPrice = product.CostPrice,
                Barcode = product.Barcode, CategoryId = product.CategoryId,
                BuyingThreshold = product.BuyingThreshold
            });
        }

        public void UpdateProductDetails(InventoryDTO product)
        {
            Demand(ClaimActionType.Edit);
            if (product == null || product.Id <= 0)
                throw new ValidationException("Select an existing product to update.");
            ExecuteCatalogWrite(() =>
            {
                ValidateProduct(product);
                var existing = _productRepo.GetById(product.Id);
                if (existing == null)
                    throw new ValidationException("The product no longer exists. Reload the product list.");
                existing.Name = product.ProductName.Trim();
                existing.Description = string.IsNullOrWhiteSpace(product.Description) ? null : product.Description.Trim();
                existing.Price = product.SellingPrice;
                existing.CostPrice = product.CostPrice;
                existing.Barcode = product.Barcode.Trim();
                existing.CategoryId = product.CategoryId;
                existing.BuyingThreshold = product.BuyingThreshold;
                // Save only changed catalog properties. Quantity, active state, SKU,
                // unit and expiry cannot be overwritten by a detached Product object.
                _context.SaveChanges();
            });
        }

        private void ValidateProduct(InventoryDTO product)
        {
            if (product == null) throw new ValidationException("Product details are required.");
            var errors = new List<ValidationResult>();
            Validator.TryValidateObject(product, new ValidationContext(product), errors, true);
            if (decimal.Round(product.SellingPrice, 2) != product.SellingPrice ||
                decimal.Round(product.CostPrice, 2) != product.CostPrice)
                errors.Add(new ValidationResult("Product prices support at most two decimal places."));
            if (errors.Count > 0)
                throw new ValidationException(string.Join(Environment.NewLine, errors.Select(e => e.ErrorMessage)));
            if (!_categoryRepo.GetAllAsQueryable().Any(c => c.Id == product.CategoryId))
                throw new ValidationException("Select an existing category. Reload categories if it was removed.");
            // Existing assignments may be retained when editing catalog details.
            // Deactivation prevents new assignments without disabling its products.
            if (!_categoryRepo.GetAllAsQueryable().Any(c => c.Id == product.CategoryId && c.IsActive) &&
                !_productRepo.GetAllAsQueryable().Any(p => p.Id == product.Id && p.CategoryId == product.CategoryId))
                throw new ValidationException("Select an active category for a new product or category assignment.");
            var barcode = product.Barcode.Trim();
            if (_productRepo.GetAllAsQueryable().Any(p => p.Id != product.Id && p.Barcode.Trim() == barcode))
                throw new ValidationException("This barcode is already assigned to another product.");
        }

        public void DeleteProduct(Product product)
        {
            Demand(ClaimActionType.Delete);
            if (product == null) throw new ValidationException("Select a product to deactivate.");
            DeactivateProduct(product.Id);
        }

        public void DeactivateProduct(int productId)
        {
            Demand(ClaimActionType.Delete);
            SetProductActive(productId, false);
        }

        public void ReactivateProduct(int productId)
        {
            Demand(ClaimActionType.Edit);
            SetProductActive(productId, true);
        }

        private void SetProductActive(int productId, bool active)
        {
            if (productId <= 0) throw new ValidationException("Select an existing product.");
            ExecuteCatalogWrite(() =>
            {
                var product = _productRepo.GetById(productId);
                if (product == null)
                    throw new ValidationException("The product no longer exists. Reload the product list.");
                if (product.IsActive == active) return;
                product.IsActive = active;
                _context.SaveChanges();
            });
        }

        public IEnumerable<ProductViewModel> GetAllProducts()
        {
            Demand(ClaimActionType.View);
            return ProjectProducts(_productRepo.GetAllAsQueryable()).ToList();
        }

        public ProductPageDTO SearchProducts(ProductSearchDTO search)
        {
            Demand(ClaimActionType.View);
            if (search == null) throw new ValidationException("Product search options are required.");
            var term = search.Search?.Trim();
            if (term != null && term.Length > 100)
                throw new ValidationException("Product search cannot exceed 100 characters.");
            if (search.CategoryId.HasValue && search.CategoryId.Value <= 0)
                throw new ValidationException("Select a valid category or search all categories.");

            var pageSize = Math.Max(1, Math.Min(200, search.PageSize));
            var products = _productRepo.GetAllAsQueryable();
            if (!string.IsNullOrEmpty(term))
                products = products.Where(p => p.Name.Contains(term) ||
                    (p.Sku != null && p.Sku.Contains(term)) ||
                    (p.Barcode != null && p.Barcode.Contains(term)));
            if (search.CategoryId.HasValue)
                products = products.Where(p => p.CategoryId == search.CategoryId.Value);
            if (search.IsActive.HasValue)
                products = products.Where(p => p.IsActive == search.IsActive.Value);

            var totalCount = products.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            var pageNumber = Math.Max(1, Math.Min(totalPages, search.PageNumber));
            var items = products.OrderBy(p => p.Name).ThenBy(p => p.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize)
                .Select(p => new ProductSummaryDTO
                {
                    Id = p.Id, Name = p.Name, Sku = p.Sku, Barcode = p.Barcode,
                    Description = p.Description, CategoryId = p.CategoryId,
                    CategoryName = p.Category == null ? null : p.Category.Name,
                    Unit = p.Unit, SellingPrice = p.Price, CostPrice = p.CostPrice,
                    Quantity = _context.InventoryBalances.Where(b => b.ProductId == p.Id)
                        .Select(b => (int?)b.QuantityOnHand).FirstOrDefault(),
                    BuyingThreshold = p.BuyingThreshold,
                    IsActive = p.IsActive, ExpiryDate = p.ExpiryDate
                }).ToList();
            return new ProductPageDTO
            {
                Items = items, TotalCount = totalCount, TotalPages = totalPages,
                PageNumber = pageNumber, PageSize = pageSize
            };
        }

        public ProductPriceHistoryDTO GetProductPriceHistory(int productId, int pageNumber = 1, int pageSize = 50)
        {
            Demand(ClaimActionType.View);
            PermissionGuard.Demand(_authorization, ResourceCodes.Audit, ClaimActionType.View);
            if (productId <= 0) throw new ValidationException("Select an existing product.");
            var recordId = productId.ToString(CultureInfo.InvariantCulture);
            // Added records establish the initial prices. Modified records contain
            // only changed fields; stock-only and identifier-only changes are excluded.
            var query = _context.AuditLogs.AsNoTracking().Where(a => a.TableName == "Product" &&
                a.RecordId == recordId && (a.Action == "Added" ||
                    (a.Action == "Modified" && (a.NewValue.Contains("\"Price\"") || a.NewValue.Contains("\"CostPrice\"")))));
            pageSize = Math.Max(1, Math.Min(200, pageSize));
            var totalCount = query.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            pageNumber = Math.Max(1, Math.Min(totalPages, pageNumber));
            var records = query.OrderByDescending(a => a.DateLogged).ThenByDescending(a => a.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize)
                .Select(a => new { a.Id, a.DateLogged, a.UserId, a.CorrelationId, a.RegisterCode, a.Action, a.OldValue, a.NewValue })
                .ToList();
            var result = new ProductPriceHistoryDTO
            {
                ProductId = productId, PageNumber = pageNumber, PageSize = pageSize,
                TotalCount = totalCount, TotalPages = totalPages
            };
            foreach (var record in records)
            {
                var oldValues = ParsePriceValues(record.OldValue);
                var newValues = ParsePriceValues(record.NewValue);
                var previousPrice = ReadPrice(oldValues, "Price");
                var price = ReadPrice(newValues, "Price");
                var previousCost = ReadPrice(oldValues, "CostPrice");
                var cost = ReadPrice(newValues, "CostPrice");
                result.Items.Add(new ProductPriceChangeDTO
                {
                    AuditId = record.Id, ChangedUtc = DateTime.SpecifyKind(record.DateLogged, DateTimeKind.Utc),
                    UserId = record.UserId, CorrelationId = record.CorrelationId,
                    RegisterCode = record.RegisterCode, Action = record.Action,
                    PreviousSellingPrice = previousPrice, SellingPrice = price,
                    PreviousCostPrice = previousCost, CostPrice = cost,
                    DetailsAvailable = newValues != null &&
                        (record.Action == "Added" ? price.HasValue && cost.HasValue :
                            oldValues != null && (price.HasValue || cost.HasValue) &&
                            price.HasValue == previousPrice.HasValue && cost.HasValue == previousCost.HasValue)
                });
            }
            return result;
        }

        private static Dictionary<string, string> ParsePriceValues(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 65536) return null;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>),
                    new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true, MaxItemsInObjectGraph = 256 });
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    return serializer.ReadObject(stream) as Dictionary<string, string>;
            }
            catch (SerializationException) { return null; }
            catch (System.Xml.XmlException) { return null; }
            catch (ArgumentException) { return null; }
        }

        private static decimal? ReadPrice(Dictionary<string, string> values, string field)
        {
            if (values != null && values.TryGetValue(field, out var text) &&
                decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var price)) return price;
            return null;
        }

        public IEnumerable<ProductViewModel> GetProductsByCategoryName(string categoryName)
        {
            Demand(ClaimActionType.View);
            return ProjectProducts(_productRepo.GetAllAsQueryable()
                .Where(p => p.Category != null && p.Category.Name == categoryName)).ToList();
        }
        public IEnumerable<CategoryViewModel> GetAllCategories()
        {
            return GetCategories(null);
        }

        public CategoryPageDTO SearchCategories(CategorySearchDTO search)
        {
            Demand(ClaimActionType.View);
            if (search == null) throw new ValidationException("Category search options are required.");
            var term = search.Search?.Trim();
            if (term != null && term.Length > 100)
                throw new ValidationException("Category search cannot exceed 100 characters.");

            var query = _categoryRepo.GetAllAsQueryable();
            if (!string.IsNullOrEmpty(term))
                query = query.Where(c => c.Name != null && c.Name.Contains(term));
            if (search.IsActive.HasValue)
                query = query.Where(c => c.IsActive == search.IsActive.Value);

            var pageSize = Math.Max(1, Math.Min(200, search.PageSize));
            var totalCount = query.Count();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            var pageNumber = Math.Max(1, Math.Min(totalPages, search.PageNumber));
            var items = query.OrderBy(c => c.Name).ThenBy(c => c.Id)
                .Skip((pageNumber - 1) * pageSize).Take(pageSize)
                .Select(c => new CategorySummaryDTO
                {
                    Id = c.Id, Name = c.Name, Description = c.Description,
                    IsActive = c.IsActive, ProductCount = c.Products.Count()
                }).ToList();
            foreach (var item in items)
                item.Revision = CategoryRevision(item.Id, item.Name, item.Description, item.IsActive);
            return new CategoryPageDTO
            {
                Items = items, TotalCount = totalCount, TotalPages = totalPages,
                PageNumber = pageNumber, PageSize = pageSize
            };
        }

        public IEnumerable<CategoryViewModel> GetCategories(bool? isActive)
        {
            Demand(ClaimActionType.View);
            var query = _categoryRepo.GetAllAsQueryable();
            if (isActive.HasValue) query = query.Where(c => c.IsActive == isActive.Value);
            var categories = query.OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new CategoryViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                IsActive = x.IsActive
            }).ToList();
            foreach (var category in categories)
                category.Revision = CategoryRevision(category.Id, category.Name, category.Description, category.IsActive);
            return categories;
        }


        public void AddCategory(CategoryViewModel category)
        {
            Demand(ClaimActionType.Add);
            ValidateCategory(category, false);
            ExecuteCatalogWrite(() =>
            {
                DemandUniqueCategoryName(category);
                var newCategory = new Category
                {
                    Name = category.Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(category.Description) ? null : category.Description.Trim()
                };
                _categoryRepo.Add(newCategory);
            });
        }

        public void DeleteCategory(CategoryViewModel category)
        {
            Demand(ClaimActionType.Delete);
            if (category == null || category.Id <= 0)
                throw new ValidationException("Select an existing category to delete.");
            ExecuteCatalogWrite(() =>
            {
            var categoryToDelete = _categoryRepo.GetById(category.Id);
            if (categoryToDelete == null)
                throw new ValidationException("The category no longer exists. Reload the category list.");
            DemandCategoryRevision(categoryToDelete, category.Revision);
            if (_productRepo.GetAllAsQueryable().Any(p => p.CategoryId == category.Id))
                throw new ValidationException("This category contains products and cannot be deleted. Move its products to another category first.");
            _categoryRepo.Delete(categoryToDelete);
            });
        }

        public void UpdateCategory(CategoryViewModel category)
        {
            Demand(ClaimActionType.Edit);
            ValidateCategory(category, true);
            ExecuteCatalogWrite(() =>
            {
                DemandUniqueCategoryName(category);
                var categoryToUpdate = _categoryRepo.GetById(category.Id);
                if (categoryToUpdate == null)
                    throw new ValidationException("The category no longer exists. Reload the category list.");
                DemandCategoryRevision(categoryToUpdate, category.Revision);
                categoryToUpdate.Name = category.Name.Trim();
                categoryToUpdate.Description = string.IsNullOrWhiteSpace(category.Description) ? null : category.Description.Trim();
                _context.SaveChanges();
            });
        }

        private static void ValidateCategory(CategoryViewModel category, bool updating)
        {
            if (category == null) throw new ValidationException("Category details are required.");
            if (updating ? category.Id <= 0 : category.Id != 0)
                throw new ValidationException("Select a valid category for this operation.");
            if (string.IsNullOrWhiteSpace(category.Name) || category.Name.Trim().Length > 100)
                throw new ValidationException("Category name is required and cannot exceed 100 characters.");
        }

        public void DeactivateCategory(int categoryId, string revision)
        {
            Demand(ClaimActionType.Delete);
            SetCategoryActive(categoryId, false, revision);
        }

        public void ReactivateCategory(int categoryId, string revision)
        {
            Demand(ClaimActionType.Edit);
            SetCategoryActive(categoryId, true, revision);
        }

        private void SetCategoryActive(int categoryId, bool active, string revision)
        {
            if (categoryId <= 0) throw new ValidationException("Select an existing category.");
            ExecuteCatalogWrite(() =>
            {
                var category = _categoryRepo.GetById(categoryId);
                if (category == null)
                    throw new ValidationException("The category no longer exists. Reload the category list.");
                DemandCategoryRevision(category, revision);
                if (category.IsActive == active) return;
                category.IsActive = active;
                _context.SaveChanges();
            });
        }

        private static void DemandCategoryRevision(Category category, string revision)
        {
            if (string.IsNullOrEmpty(revision) || !string.Equals(revision,
                CategoryRevision(category.Id, category.Name, category.Description, category.IsActive),
                StringComparison.Ordinal))
                throw new ValidationException("The category changed or its revision is missing. Reload and review it before saving or deleting.");
        }

        private static string CategoryRevision(int id, string name, string description, bool active)
        {
            // Length-prefixed fields distinguish null, empty, and delimiter-containing
            // values. This represents current content, not a database row version.
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write(id);
                    writer.Write(active);
                    writer.Write(name != null);
                    if (name != null) writer.Write(name);
                    writer.Write(description != null);
                    if (description != null) writer.Write(description);
                }
                using (var hash = System.Security.Cryptography.SHA256.Create())
                    return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
            }
        }

        private void DemandUniqueCategoryName(CategoryViewModel category)
        {
            var name = category.Name.Trim();
            // Hold the duplicate-name read through commit so another catalog
            // writer cannot insert or rename a category into the same name.
            // Match the database index collation even on differently collated databases.
            if (_context.Database.SqlQuery<int>(@"
                SELECT TOP (1) Id FROM dbo.Categories
                WHERE Id <> @id AND Name IS NOT NULL AND Name <> N''
                AND LTRIM(RTRIM(Name)) COLLATE Latin1_General_100_CI_AS = @name COLLATE Latin1_General_100_CI_AS",
                new System.Data.SqlClient.SqlParameter("@id", category.Id),
                new System.Data.SqlClient.SqlParameter("@name", System.Data.SqlDbType.NVarChar, 100) { Value = name }).Any())
                throw new ValidationException("A category with this name already exists, including inactive categories.");
        }

        private void ExecuteCatalogWrite(Action write)
        {
            if (_context.Database.CurrentTransaction != null || _context.ChangeTracker.HasChanges())
                throw new InvalidOperationException("Save catalog changes using a context without pending changes or an active transaction.");
            foreach (var entry in _context.ChangeTracker.Entries().ToList())
                entry.State = EntityState.Detached;
            using (var operation = AuditOperation.Begin())
            using (var transaction = _context.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    // Keep reference and uniqueness checks valid through commit.
                    // Database reference protection also requires the cascade-removal migration.
                    write();
                    transaction.Commit();
                }
                catch (Exception exception)
                {
                    try { transaction.Rollback(); }
                    finally
                    {
                        foreach (var entry in _context.ChangeTracker.Entries().ToList())
                            entry.State = EntityState.Detached;
                    }
                    for (var cause = exception; cause != null; cause = cause.InnerException)
                    {
                        var sql = cause as System.Data.SqlClient.SqlException;
                        if (sql == null || (sql.Number != 2601 && sql.Number != 2627)) continue;
                        if (sql.Message.Contains("UX_Categories_NormalizedName"))
                            throw new ValidationException("A category with this name already exists, including inactive categories. Reload and choose a different name.", exception);
                        if (sql.Message.Contains("UX_Products_NormalizedSku"))
                            throw new ValidationException("This SKU is already assigned to another product. Reload and choose a different SKU.", exception);
                        if (sql.Message.Contains("UX_Products_NormalizedBarcode"))
                            throw new ValidationException("This barcode is already assigned to another product. Reload and choose a different barcode.", exception);
                    }
                    throw;
                }
            }
        }

        public IEnumerable<ProductViewModel> LowStockProducts()
        {
            Demand(ClaimActionType.View);
            return ProjectProducts(_productRepo.GetAllAsQueryable()
                .Where(p => p.IsActive && _context.InventoryBalances.Any(b =>
                    b.ProductId == p.Id && b.QuantityOnHand <= p.BuyingThreshold))).ToList();
        }

        private IQueryable<ProductViewModel> ProjectProducts(IQueryable<Product> products)
        {
            return products.OrderBy(p => p.Name).ThenBy(p => p.Id)
                .Select(p => new ProductViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Sku = p.Sku,
                    Description = p.Description,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category == null ? null : p.Category.Name,
                    CostPrice = p.CostPrice,
                    Price = p.Price,
                    Quantity = _context.InventoryBalances.Where(b => b.ProductId == p.Id)
                        .Select(b => (int?)b.QuantityOnHand).FirstOrDefault(),
                    Barcode = p.Barcode,
                    Unit = p.Unit,
                    BuyingThreshold = p.BuyingThreshold,
                    IsActive = p.IsActive
                });
        }

        public IEnumerable<StockViewModel> GetAllStocks()
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Inventory, ClaimActionType.View);
            return _context.InventoryBalances.AsNoTracking().OrderBy(b => b.ProductId)
                .Select(balance => new StockViewModel
                {
                    Id = balance.Id,
                    ProductId = balance.ProductId,
                    ProductName = balance.Product.Name,
                    Quantity = balance.QuantityOnHand,
                    Unit = balance.Product.Unit,
                    ReorderLevel = balance.Product.BuyingThreshold
                }).ToList();

        }

        public class CategoryViewModel
        {
            [System.ComponentModel.Browsable(false)]
            public string Revision { get; set; }
            public bool IsActive { get; set; }
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
        }

        public class ProductViewModel
        {
            public int Id { get; set; }
            public string Sku { get; set; }
            public string Unit { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }

            public int CategoryId { get; set; }
            public string CategoryName { get; set; }

            public decimal CostPrice { get; set; }
            public decimal Price { get; set; }

            public int? Quantity { get; set; }
            public bool MissingInventoryBalance => !Quantity.HasValue;

            public string Barcode { get; set; }
            public string ExpiryDate { get; set; }

            public int BuyingThreshold { get; set; }

            public bool IsActive { get; set; }
        }

        public class StockViewModel 
        {
            public int Id { get; set; }

            public int ProductId { get; set; }

            public string ProductName { get; set; }

            public string Unit { get; set; }

            public int Quantity { get; set; }

            public int ReorderLevel { get; set; }

        }

        private void Demand(ClaimActionType action)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Products, action);
        }

        public void Dispose()
        {
            _productRepo.Dispose();
            _categoryRepo.Dispose();
            if (_ownsContext) _context.Dispose();
        }

    }
}
