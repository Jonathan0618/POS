using System.Collections.Generic;

namespace POS.Models.Operations
{
    public class CategorySearchDTO
    {
        public string Search { get; set; }
        public bool? IsActive { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class CategorySummaryDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public string Revision { get; set; }
        // All references count, including inactive products. This is live query
        // information; deletion must recheck references inside its transaction.
        public int ProductCount { get; set; }
    }

    public class CategoryPageDTO
    {
        public List<CategorySummaryDTO> Items { get; set; } = new List<CategorySummaryDTO>();
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }
}
