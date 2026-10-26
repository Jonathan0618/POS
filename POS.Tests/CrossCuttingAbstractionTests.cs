using POS.Common.Enumerations;
using POS.Core.Abstractions;
using POS.Core.Results;
using POS.Core;
using POS.Core.Security;
using POS.Services;
using POS.Services.Security;
using POS.Data.Context;
using POS.Models.Security;
using POS.Models.Operations;
using POS.Services.Settings;
using System;
using System.Security.Claims;

namespace POS.Tests
{
    internal static class CrossCuttingAbstractionTests
    {
        public static void RunAll()
        {
            OperationResultsRepresentSuccessAndFailure();
            ClaimsAuthorizationUsesInjectedCurrentUser();
            CurrentUserReadsAndClearsClaims();
            ServiceAuthorizationCannotBeBypassed();
            NavigationRequiresViewPermission();
        }

        private static void ServiceAuthorizationCannotBeBypassed()
        {
            using (var context = new POSContext(
                "Data Source=(LocalDB)\\MSSQLLocalDB;Initial Catalog=POS_UnauthorizedGuard;Integrated Security=True"))
            using (var service = new UserService(context, new SystemClock(), new DenyAuthorization()))
            {
                var readDenied = ThrowsUnauthorized(
                    () => service.GetUsers().GetAwaiter().GetResult());
                var audit = new AuditService(() => throw new InvalidOperationException("Unauthorized query opened a database."),
                    new DenyAuthorization());
                if (!ThrowsUnauthorized(() => audit.SearchAsync(new AuditSearchDTO()).GetAwaiter().GetResult()))
                    throw new InvalidOperationException("Audit query authorization was bypassed.");
                using (var roles = new RoleService(context, new DenyAuthorization()))
                using (var modules = new ModuleService(context, new DenyAuthorization()))
                using (var settings = new StoreSettingsService(context, new SystemClock(), new DenyAuthorization()))
                {
                    if (readDenied &&
                        ThrowsUnauthorized(() => service.GetUserActivityAsync("user-1").GetAwaiter().GetResult()) &&
                        ThrowsUnauthorized(() => service.AddUserAsync(new UserDTO()).GetAwaiter().GetResult()) &&
                        ThrowsUnauthorized(() => roles.AddRole(new RoleDTO())) &&
                        ThrowsUnauthorized(() => modules.AddModule(new ModuleDTO())) &&
                        ThrowsUnauthorized(() => settings.GetSettings()) &&
                        ThrowsUnauthorized(() => settings.BuildReceiptPreview(new StoreSettingsDTO())) &&
                        ThrowsUnauthorized(() => settings.SaveSettings(new StoreSettingsDTO())))
                        return;
                }
            }

            throw new InvalidOperationException("User service authorization was bypassed.");
        }

        private static bool ThrowsUnauthorized(Action action)
        {
            try { action(); }
            catch (UnauthorizedAccessException) { return true; }
            return false;
        }

        private static void CurrentUserReadsAndClearsClaims()
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(ClaimTypes.Name, "cashier"),
                new Claim(ClaimTypes.Role, "Cashier"),
                new Claim(SessionClaimTypes.RoleId, "role-1"),
                new Claim(SessionClaimTypes.DisplayName, "Casey Cashier")
            }, "Test");

            CurrentUser.Principal = new ClaimsPrincipal(identity);
            if (CurrentUser.UserId != "user-1" || CurrentUser.Username != "cashier" ||
                CurrentUser.RoleId != "role-1" || CurrentUser.RoleName != "Cashier" ||
                CurrentUser.Fullname != "Casey Cashier")
                throw new InvalidOperationException("Current user claims were not projected correctly.");

            CurrentUser.Logout();
            if (CurrentUser.IsAuthenticated || CurrentUser.UserId != null)
                throw new InvalidOperationException("Logout did not clear the current session.");
        }

        private static void OperationResultsRepresentSuccessAndFailure()
        {
            var success = OperationResult<int>.Success(42);
            if (!success.Succeeded || success.Value != 42 || success.Errors.Count != 0)
                throw new InvalidOperationException("Generic operation success result failed.");

            var failure = OperationResult.Failure("Conflict");
            if (failure.Succeeded || failure.Errors.Count != 1 || failure.Errors[0] != "Conflict")
                throw new InvalidOperationException("Operation failure result failed.");
        }

        private static void ClaimsAuthorizationUsesInjectedCurrentUser()
        {
            var identity = new ClaimsIdentity(
                new[] { new Claim("Products", ClaimActionType.Edit.ToString()) },
                "Test");
            var currentUser = new TestCurrentUser(new ClaimsPrincipal(identity));
            var authorization = new ClaimsAuthorizationService(currentUser);

            if (!authorization.HasPermission("products", ClaimActionType.Edit))
                throw new InvalidOperationException("Expected permission was not granted.");
            if (authorization.HasPermission("Products", ClaimActionType.Delete))
                throw new InvalidOperationException("Unexpected permission was granted.");
        }

        private static void NavigationRequiresViewPermission()
        {
            var allowed = new NavigationPermission(new SinglePermissionAuthorization(
                ResourceCodes.Users,
                ClaimActionType.View));
            if (!allowed.CanOpen(ResourceCodes.Users) || allowed.CanOpen(ResourceCodes.Roles))
                throw new InvalidOperationException("Navigation visibility did not follow resource permission.");

            var editOnly = new NavigationPermission(new SinglePermissionAuthorization(
                ResourceCodes.Users,
                ClaimActionType.Edit));
            if (editOnly.CanOpen(ResourceCodes.Users))
                throw new InvalidOperationException("Edit permission incorrectly exposed navigation without View.");
        }

        private sealed class TestCurrentUser : ICurrentUser
        {
            public TestCurrentUser(ClaimsPrincipal principal) { Principal = principal; }
            public ClaimsPrincipal Principal { get; }
            public bool IsAuthenticated => Principal.Identity.IsAuthenticated;
            public string UserId => "test-user";
            public string Username => "tester";
            public string RoleId => "test-role";
            public string RoleName => "Test Role";
        }

        private sealed class DenyAuthorization : IAuthorizationService
        {
            public bool HasPermission(string resource, ClaimActionType action) => false;
        }

        private sealed class SinglePermissionAuthorization : IAuthorizationService
        {
            private readonly string _resource;
            private readonly ClaimActionType _action;

            public SinglePermissionAuthorization(string resource, ClaimActionType action)
            {
                _resource = resource;
                _action = action;
            }

            public bool HasPermission(string resource, ClaimActionType action)
            {
                return string.Equals(resource, _resource, StringComparison.OrdinalIgnoreCase) &&
                       action == _action;
            }
        }
    }
}
