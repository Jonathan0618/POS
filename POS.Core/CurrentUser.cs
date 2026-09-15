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

        public static string Fullname => GetClaim(SessionClaimTypes.DisplayName);
        public static string Username => Principal?.Identity?.Name;
        public static string UserId => GetClaim(ClaimTypes.NameIdentifier);
        public static string RoleId => GetClaim(SessionClaimTypes.RoleId);
        public static string RoleName => GetClaim(ClaimTypes.Role);

        public static void Logout() => Principal = null;

        private static string GetClaim(string claimType)
        {
            return Principal?.FindFirst(claimType)?.Value;
        }
    }
}
