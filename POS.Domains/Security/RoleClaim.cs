using System.ComponentModel.DataAnnotations;

namespace POS.Domains.Security
{
    public class RoleClaim
    {
        [StringLength(36)]
        public string Id { get; set; }
        public string RoleId { get; set; }
        public Role Role { get; set; }
        public int ModuleId { get; set; }
        public Module Module { get; set; }
        public bool CanView { get; set; } = false;
        public bool CanAdd { get; set; } = false;
        public bool CanEdit { get; set; } = false;
        public bool CanDelete { get; set; } = false;
    }
}
