using System.Linq;
using System.Security.Claims;

namespace POS.Core
{
    public class CurrentUser
    {
        public static ClaimsPrincipal Principal { get; set; }

        public static bool IsAuthenticated
        {
            get
            {
                return Principal != null &&
                       Principal.Identity != null &&
                       Principal.Identity.IsAuthenticated;
            }
        }

        public static void Logout()
        {
            Principal = null;
        }
        public static string Fullname { get; set; }
        public static string Username { get; set; }
        public static string UserId { get; set; }
        public static string RoleId { get; set; }
        public static string RoleName { get; set; }
    }
}
