using POS.Domains.BusinessObjects;
using POS.Services.Repository;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace POS.Services
{
    public class InventoryService
    {
        private readonly BaseRepository<Product> _productRepo;
        private readonly BaseRepository<Category> _categoryRepo;

        public InventoryService()
        {
            _productRepo = new BaseRepository<Product>();
            _categoryRepo = new BaseRepository<Category>();
        }

        public void AddProduct(ProductViewModel product)
        {
            var newProduct = new Product
            {
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                CategoryId = _categoryRepo.GetAll().FirstOrDefault(c => c.Name == product.CategoryName).Id,
                };
            _productRepo.Add(newProduct);
        }

        public void UpdateProduct(Product product)
        {
            _productRepo.Update(product);
        }

        public void DeleteProduct(Product product)
        {
            _productRepo.Delete(product);
        }

        public IEnumerable<ProductViewModel> GetAllProducts()
        {
            var products = _productRepo.GetAll();
            var productViewModels = new List<ProductViewModel>();
            foreach (var product in products)
            {
                var category = _categoryRepo.GetById(product.CategoryId);
                productViewModels.Add(new ProductViewModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    Quantity = product.Quantity,
                    CategoryName = _categoryRepo.GetById(product.CategoryId)?.Name
                });
            }
            return productViewModels;
        }

        public IEnumerable<ProductViewModel> GetProductsByCategoryName(string categoryName)
        {
            var category = _categoryRepo.GetAll().FirstOrDefault(c => c.Name == categoryName);
            if (category == null)
            {
                return new List<ProductViewModel>();
            }
            var products = _productRepo.GetAll().Where(p => p.CategoryId == category.Id).Select(p => new ProductViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                CategoryName = category.Name
            });
            return products;
        }
        public IEnumerable<CategoryViewModel> GetAllCategories()
        {
            var categories = _categoryRepo.GetAll().Select(x => new CategoryViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description
            });
            return categories;
        }

        public void AddCategory(CategoryViewModel category)
        {
            var newCategory = new Category
            {
                Name = category.Name,
                Description = category.Description
            };
            _categoryRepo.Add(newCategory);
        }

        public void DeleteCategory(CategoryViewModel category)
        {
            var categoryToDelete = _categoryRepo.GetById(category.Id);
            if (categoryToDelete != null)
            {
                _categoryRepo.Delete(categoryToDelete);
            }
        }

        public void UpdateCategory(CategoryViewModel category)
        {
            var categoryToUpdate = _categoryRepo.GetById(category.Id);
            if (categoryToUpdate != null)
            {
                categoryToUpdate.Name = category.Name;
                categoryToUpdate.Description = category.Description;
                _categoryRepo.Update(categoryToUpdate);
            }
        }

        public IEnumerable<ProductViewModel> GetLowStockProducts(int threshold)
        {
            var products = _productRepo.GetAll().Where(p => p.Quantity < threshold).Select(p => new ProductViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                CategoryName = _categoryRepo.GetById(p.CategoryId)?.Name,
                Quantity = p.Quantity
            });
            return products;
        }



    }

    public class CategoryViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class ProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public int CategoryId { get; set; }
        public string CategoryName { get; set; }

        public decimal CostPrice { get; set; }
        public decimal Price { get; set; }

        public int Quantity { get; set; }

        public string Barcode { get; set; }
        public string ExpiryDate { get; set; }

        public bool IsActive { get; set; }
    }

}
