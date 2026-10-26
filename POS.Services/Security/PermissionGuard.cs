using POS.Common.Enumerations;
using POS.Core.Abstractions;
using System;

namespace POS.Services.Security
{
    internal static class PermissionGuard
    {
        public static void Demand(
            IAuthorizationService authorization,
            string resource,
            ClaimActionType action)
        {
            if (!authorization.HasPermission(resource, action))
                throw new UnauthorizedAccessException(
                    $"Permission '{resource}:{action}' is required.");
        }
    }
}
