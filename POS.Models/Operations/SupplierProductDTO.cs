using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Models.Operations
{
    public sealed class SupplierProductDTO
    {
        public int Id { get; set; }
        [Range(1, int.MaxValue)] public int SupplierId { get; set; }
        [Range(1, int.MaxValue)] public int ProductId { get; set; }
        [StringLength(50)] public string SupplierSku { get; set; }
        [Range(typeof(decimal), "0", "9999999999999999.99")] public decimal DefaultCost { get; set; }
        [Range(0, int.MaxValue)] public int LeadTimeDays { get; set; }
        public string Revision { get; set; }
        public string ProductName { get; set; }
        public string ProductSku { get; set; }
        public bool ProductIsActive { get; set; }
    }

    public sealed class SupplierProductPageDTO
    {
        public List<SupplierProductDTO> Items { get; set; } = new List<SupplierProductDTO>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
