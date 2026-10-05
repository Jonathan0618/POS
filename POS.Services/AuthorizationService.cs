using POS.Common.Enumerations;
using POS.Core;
using POS.Core.Abstractions;
using System;

namespace POS.Services
{
    public sealed class ClaimsAuthorizationService : IAuthorizationService
    {
        private readonly ICurrentUser _currentUser;

        public ClaimsAuthorizationService(ICurrentUser currentUser)
        {
            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public bool HasPermission(string resource, ClaimActionType action)
        {
            if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(resource))
                return false;

            return _currentUser.Principal.HasClaim(
                claim => claim.Type.Equals(resource, StringComparison.OrdinalIgnoreCase) &&
                         claim.Value.Equals(action.ToString(), StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class AuthorizationService
    {
        private static readonly IAuthorizationService Default =
            new ClaimsAuthorizationService(new CurrentUserAccessor());

        public static bool HasPermission(
            string resource,
            ClaimActionType action)
        {
            return Default.HasPermission(resource, action);
        }
    }
}
