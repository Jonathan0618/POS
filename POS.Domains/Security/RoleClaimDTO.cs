using POS.Common.Enumerations;
using System.ComponentModel.DataAnnotations;

namespace POS.Domains.Security
{
    public class RoleClaimDTO
    {
        public string RoleClaimId { get; set; }
        [Required]
        public int ModuleId { get; set; }
        public string Name { get; set; }
        public string RoleId { get; set; }
        public bool CanView { get; set; } = false;
        public bool CanAdd { get; set; } = false;
        public bool CanEdit { get; set; } = false;
        public bool CanDelete { get; set; } = false;
    }
}
