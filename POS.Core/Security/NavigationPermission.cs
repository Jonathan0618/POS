using POS.Common.Enumerations;
using POS.Core.Abstractions;
using System;

namespace POS.Core.Security
{
    public sealed class NavigationPermission
    {
        private readonly IAuthorizationService _authorization;

        public NavigationPermission(IAuthorizationService authorization)
        {
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        }

        public bool CanOpen(string resource)
        {
            return _authorization.HasPermission(resource, ClaimActionType.View);
        }
    }
}
