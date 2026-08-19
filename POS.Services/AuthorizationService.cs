using POS.Common.Enumerations;
using POS.Core;
using System;

namespace POS.Services
{
    public static class AuthorizationService
    {
        public static bool HasPermission(
            string resource,
            ClaimActionType action)
        {
            if (!CurrentUser.IsAuthenticated)
                return false;

            return CurrentUser.Principal.HasClaim(
                claim =>
                    claim.Type.Equals(
                        resource,
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    claim.Value.Equals(
                        action.ToString(),
                        StringComparison.OrdinalIgnoreCase));
        }
    }
}
