using System.ComponentModel.DataAnnotations;

namespace POS.Domains.Security
{
    public class RoleClaim
    {
        [StringLength(36)]
        public string Id { get; set; }
        public string RoleId { get; set; }
        public Role Role { get; set; }
        public string Name { get; set; }
        public bool CanView { get; set; }
        public bool CanEdit { get; set; }
        public bool CanAdd { get; set; }
        public bool CanDelete { get; set; }
    }
}
