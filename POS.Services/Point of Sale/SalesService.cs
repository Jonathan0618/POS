using POS.Core.Attributes;
using POS.Domains.BusinessObjects;
using POS.Services.Repository;

namespace POS.Services.Point_of_Sale
{
    public class SalesService
    {
        private readonly BaseRepository<Product, int> _productRepo;
        private readonly BaseRepository<Category, int> _categoryRepo;

        public SalesService()
        {
            _productRepo = new BaseRepository<Product, int>();
            _categoryRepo = new BaseRepository<Category, int>();
        }

        public void AddItem(Product product) 
        { 

        }

    }
}
