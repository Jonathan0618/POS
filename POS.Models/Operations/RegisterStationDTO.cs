using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Models.Operations
{
    public sealed class RegisterStationDTO
    {
        public int Id { get; set; }
        [Required, StringLength(30)] public string Code { get; set; }
        [Required, StringLength(100)] public string Name { get; set; }
        [StringLength(200)] public string PrinterName { get; set; }
        public bool IsActive { get; set; }
        public string Revision { get; set; }
    }

    public sealed class RegisterStationPageDTO
    {
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<RegisterStationDTO> Items { get; set; } = new List<RegisterStationDTO>();
    }
}
