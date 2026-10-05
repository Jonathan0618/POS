using System.Security.Claims;

namespace POS.Core.Abstractions
{
    public interface ICurrentUser
    {
        ClaimsPrincipal Principal { get; }
        bool IsAuthenticated { get; }
        string UserId { get; }
        string Username { get; }
        string RoleId { get; }
        string RoleName { get; }
    }

    public sealed class CurrentUserAccessor : ICurrentUser
    {
        public ClaimsPrincipal Principal => CurrentUser.Principal;
        public bool IsAuthenticated => CurrentUser.IsAuthenticated;
        public string UserId => CurrentUser.UserId;
        public string Username => CurrentUser.Username;
        public string RoleId => CurrentUser.RoleId;
        public string RoleName => CurrentUser.RoleName;
    }
}
