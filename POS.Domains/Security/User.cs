using Microsoft.AspNet.Identity.EntityFramework;
using POS.Core.Common.Enumerations;

namespace POS.Domains.Security
{
    public class User : IdentityUser
    {
        public string Salutation { get; set; }
        public string NameExtension { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public Gender Gender { get; set; }
    }
}
