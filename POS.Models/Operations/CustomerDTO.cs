using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace POS.Models.Operations
{
    public sealed class CustomerDTO
    {
        public int Id { get; set; }
        [Required, StringLength(30)] public string Code { get; set; }
        [Required, StringLength(150)] public string Name { get; set; }
        [StringLength(50)] public string Phone { get; set; }
        [EmailAddress, StringLength(100)] public string Email { get; set; }
        [StringLength(300)] public string Address { get; set; }
        [StringLength(50)] public string TaxIdentifier { get; set; }
        public bool IsActive { get; set; }
        public string Revision { get; set; }
    }

    public sealed class CustomerPageDTO
    {
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<CustomerDTO> Items { get; set; } = new List<CustomerDTO>();
    }

    public enum CustomerHistoryKind { Sale, Return }

    public sealed class CustomerHistorySearchDTO
    {
        public int CustomerId { get; set; }
        public CustomerHistoryKind? Kind { get; set; }
        public int? ProductId { get; set; }
        public string ReceiptNumber { get; set; }
        public System.DateTime? FromUtc { get; set; }
        public System.DateTime? ToUtcExclusive { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public sealed class CustomerHistoryItemDTO
    {
        public CustomerHistoryKind Kind { get; set; }
        public int DocumentId { get; set; }
        public int SaleId { get; set; }
        public string ReceiptNumber { get; set; }
        public string Status { get; set; }
        public System.DateTime EventUtc { get; set; }
        public decimal Amount { get; set; }
        public int LineCount { get; set; }
    }

    public sealed class CustomerHistoryPageDTO
    {
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public List<CustomerHistoryItemDTO> Items { get; set; } = new List<CustomerHistoryItemDTO>();
    }
}
