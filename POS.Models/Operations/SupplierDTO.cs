using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Models.Operations
{
    public sealed class SupplierDTO
    {
        public int Id { get; set; }
        [Required, StringLength(30)] public string Code { get; set; }
        [Required, StringLength(150)] public string Name { get; set; }
        [StringLength(100)] public string ContactName { get; set; }
        [StringLength(50)] public string Phone { get; set; }
        [EmailAddress, StringLength(100)] public string Email { get; set; }
        [StringLength(300)] public string Address { get; set; }
        [StringLength(50)] public string TaxIdentifier { get; set; }
        public bool IsActive { get; set; }
        public string Revision { get; set; }
    }

    public sealed class SupplierPageDTO
    {
        public List<SupplierDTO> Items { get; set; } = new List<SupplierDTO>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
