using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace POS.Domains.AuditEntry
{
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        [StringLength(20)]
        public string TableName { get; set; }

        public string RecordId { get; set; }

        public string Action { get; set; } // Added, Modified, Deleted

        public string OldValue { get; set; }

        public string NewValue { get; set; }
        [StringLength(36)]
        public string UserId { get; set; }

        public DateTime DateLogged { get; set; }
    }
}
