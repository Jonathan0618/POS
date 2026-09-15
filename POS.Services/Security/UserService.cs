using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using POS.Core;
using POS.Core.Results;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Common.Enumerations;
using POS.Data.Context;
using POS.Domains.AuditEntry;
using POS.Domains.Security;
using POS.Models.Security;
using POS.Models.Store;
using POS.Services.Repository;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace POS.Services.Security
{
    public class UserService : IDisposable
    {
        private readonly UserManager<User, string> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly POSContext _context;
        private readonly bool _ownsContext;
        private readonly IClock _clock;
        private readonly BaseRepository<AuditLog, long> _auditRepository;
        private readonly IAuthorizationService _authorization;

        public UserService() : this(new POSContext(), new SystemClock(), new ClaimsAuthorizationService(new CurrentUserAccessor()), true) { }
        public UserService(POSContext context) : this(context, new SystemClock(), new ClaimsAuthorizationService(new CurrentUserAccessor()), false) { }
        public UserService(POSContext context, IClock clock, IAuthorizationService authorization) : this(context, clock, authorization, false) { }

        private UserService(POSContext context, IClock clock, IAuthorizationService authorization, bool ownsContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _ownsContext = ownsContext;
            var roleStore = new RoleStore<Role>(_context) { DisposeContext = false };
            var userStore = new UserStore<User, Role, string, IdentityUserLogin,
                IdentityUserRole, IdentityUserClaim>(_context) { DisposeContext = false };
            _roleManager = new RoleManager<Role>(roleStore);
            _userManager = new UserManager<User, string>(userStore);
            _userManager.UserLockoutEnabledByDefault = true;
            _userManager.DefaultAccountLockoutTimeSpan = TimeSpan.FromMinutes(15);
            _userManager.MaxFailedAccessAttemptsBeforeLockout = 5;
            _auditRepository = new BaseRepository<AuditLog, long>(_context);
        }

        public async Task<IEnumerable<UserDTO>> GetUsers()
        {
            Demand(ResourceCodes.Users, ClaimActionType.View);
            var users = await _userManager.Users
                .Select(x => new UserDTO
                {
                    UserId = x.Id,
                    FullName = x.FirstName + " " + x.LastName,
                    Username = x.UserName,
                    IsActive = x.LockoutEndDateUtc != DateTime.MaxValue,
                    Role = x.Roles
                        .Select(r => r.RoleId)
                        .FirstOrDefault()
                })
                .ToListAsync();

            foreach (var user in users)
            {
                if (!string.IsNullOrEmpty(user.Role))
                {
                    var role = await _roleManager.FindByIdAsync(user.Role);
                    user.RoleName = role?.Name;
                }
            }

            return users;
        }

        public async Task<PagedResult<UserDTO>> GetUsersPageAsync(
            string searchText,
            int pageNumber,
            int pageSize)
        {
            Demand(ResourceCodes.Users, ClaimActionType.View);
            if (pageNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(pageNumber));
            if (pageSize < 1 || pageSize > 200)
                throw new ArgumentOutOfRangeException(nameof(pageSize));

            var query = _userManager.Users.AsQueryable();
            var search = (searchText ?? string.Empty).Trim();
            if (search.Length > 0)
            {
                var matchingRoleIds = await _roleManager.Roles
                    .Where(x => x.Name.Contains(search))
                    .Select(x => x.Id)
                    .ToListAsync();
                query = query.Where(x =>
                    x.UserName.Contains(search) ||
                    x.FirstName.Contains(search) ||
                    x.MiddleName.Contains(search) ||
                    x.LastName.Contains(search) ||
                    x.Roles.Any(role => matchingRoleIds.Contains(role.RoleId)));
            }

            var totalCount = await query.CountAsync();
            var users = await query
                .OrderBy(x => x.UserName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new UserDTO
                {
                    UserId = x.Id,
                    FullName = x.FirstName + " " + x.LastName,
                    Username = x.UserName,
                    IsActive = x.LockoutEndDateUtc != DateTime.MaxValue,
                    Role = x.Roles.Select(role => role.RoleId).FirstOrDefault()
                })
                .ToListAsync();

            var roleIds = users.Select(x => x.Role).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            var roleNames = await _roleManager.Roles
                .Where(x => roleIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name);
            foreach (var user in users)
            {
                string roleName;
                if (!string.IsNullOrEmpty(user.Role) && roleNames.TryGetValue(user.Role, out roleName))
                    user.RoleName = roleName;
            }

            return new PagedResult<UserDTO>(users, totalCount, pageNumber, pageSize);
        }

        public async Task AddUserAsync(UserDTO user)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ResourceCodes.Users, ClaimActionType.Add);
                var newUser = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = user.Username,
                    Salutation = user.Salutation,
                    FirstName = user.FirstName,
                    MiddleName = user.MiddleName,
                    LastName = user.LastName,
                    NameExtension = user.NameExt,
                    Email = $"{user.Username}@pos.user",
                    EmailConfirmed = true,
                    LockoutEnabled = true
                };
                var createResult = await _userManager.CreateAsync(newUser, user.Password);
                if (!createResult.Succeeded)
                    throw new InvalidOperationException(string.Join(", ", createResult.Errors));
                if (!string.IsNullOrEmpty(user.Role))
                {
                    var assignment = await AssignRoleToUserAsync(newUser.Id, user.Role);
                    if (!assignment.Succeeded)
                        throw new InvalidOperationException(string.Join(", ", assignment.Errors));
                }
            }
        }

        public async Task UpdateUserAsync(EditUserDTO user)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ResourceCodes.Users, ClaimActionType.Edit);
                var existingUser = await _userManager.FindByIdAsync(user.UserId);
                existingUser.UserName = user.Username;
                existingUser.Salutation = user.Salutation;
                existingUser.FirstName = user.FirstName;
                existingUser.MiddleName = user.MiddleName;
                existingUser.LastName = user.LastName;
                existingUser.NameExtension = user.NameExt;
                existingUser.Email = $"{user.Username}@pos.user";

                await _userManager.UpdateAsync(existingUser);
                if (!string.IsNullOrEmpty(user.Role))
                {
                    var assignment = await AssignRoleToUserAsync(user.UserId, user.Role);
                    if (!assignment.Succeeded)
                        throw new InvalidOperationException(string.Join(", ", assignment.Errors));
                }

                if (!string.IsNullOrEmpty(user.ConfirmPassword))
                {
                    var resetResult = await ResetPasswordByAdministratorAsync(user.UserId, user.ConfirmPassword);
                    if (!resetResult.Succeeded)
                        throw new InvalidOperationException(string.Join(", ", resetResult.Errors));
                }
            }
        }

        public async Task<UserDTO> GetUserByIdAsync(string userId)
        {
            Demand(ResourceCodes.Users, ClaimActionType.View);
            var res = await _userManager.FindByIdAsync(userId);
            var user = new UserDTO
            {
                UserId = res.Id,
                Username = res.UserName,
                Salutation = res.Salutation,
                FirstName = res.FirstName,
                MiddleName = res.MiddleName,
                LastName = res.LastName
            };

            user.IsActive = res.LockoutEndDateUtc != DateTime.MaxValue;

            user.Role = res.Roles?.FirstOrDefault()?.RoleId ?? "";
            return user;
        }

        public async Task<IReadOnlyList<UserActivityDTO>> GetUserActivityAsync(
            string userId,
            int maximumRows = 100)
        {
            Demand(ResourceCodes.Audit, ClaimActionType.View);
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("A user is required.", nameof(userId));
            if (maximumRows < 1 || maximumRows > 500)
                throw new ArgumentOutOfRangeException(nameof(maximumRows));

            return await _context.AuditLogs
                .AsNoTracking()
                .Where(x => x.UserId == userId || x.RecordId == userId)
                .OrderByDescending(x => x.DateLogged)
                .ThenByDescending(x => x.Id)
                .Take(maximumRows)
                .Select(x => new UserActivityDTO
                {
                    ActivityId = x.Id,
                    DateLogged = x.DateLogged,
                    Category = x.TableName,
                    Action = x.Action,
                    Details = x.NewValue ?? x.OldValue
                })
                .ToListAsync();
        }

        public IEnumerable<RoleDTO> GetRoles()
        {
            Demand(ResourceCodes.Users, ClaimActionType.View);
            var roles = _roleManager.Roles.Select(x => new RoleDTO
            {
                RoleId = x.Id,
                Name = x.Name
            });
            return roles.ToList();
        }

        public async Task AddRoleAsync(RoleDTO role)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ResourceCodes.Roles, ClaimActionType.Add);
                var newRole = new Role
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = role.Name
                };
                await _roleManager.CreateAsync(newRole);
            }
        }

        public async Task UpdateRoleAsync(RoleDTO role)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ResourceCodes.Roles, ClaimActionType.Edit);
                var newRole = await _roleManager.FindByIdAsync(role.RoleId);
                newRole.Name = role.Name;
                await _roleManager.UpdateAsync(newRole);
            }
        }

        public async Task<OperationResult> AssignRoleToUserAsync(string userId, string roleId)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ResourceCodes.Users, ClaimActionType.Edit);
                var user = await _userManager.FindByIdAsync(userId);
                var targetRole = await _roleManager.FindByIdAsync(roleId);
                if (user == null || targetRole == null)
                    return OperationResult.Failure("The selected user or role no longer exists.");

                var administratorRole = await _roleManager.FindByNameAsync("System Administrator");
                var isAdministrator = administratorRole != null && user.Roles.Any(x => x.RoleId == administratorRole.Id);
                var keepsAdministratorRole = administratorRole != null && targetRole.Id == administratorRole.Id;
                if (isAdministrator && !keepsAdministratorRole)
                {
                    if (CurrentUser.UserId == userId)
                        return OperationResult.Failure("You cannot remove your own administrator role.");
                    if (await CountActiveAdministratorsAsync(administratorRole.Id) <= 1)
                        return OperationResult.Failure("The final active administrator cannot be demoted.");
                }

                var roles = await _userManager.GetRolesAsync(userId);
                if (roles.Count == 1 && roles[0].Equals(targetRole.Name, StringComparison.OrdinalIgnoreCase))
                    return OperationResult.Success();

                using (var transaction = _context.Database.BeginTransaction())
                {
                    try
                    {
                        foreach (var item in roles)
                        {
                            var removal = await _userManager.RemoveFromRoleAsync(userId, item);
                            if (!removal.Succeeded)
                            {
                                transaction.Rollback();
                                return OperationResult.Failure(removal.Errors.ToArray());
                            }
                        }

                        var addition = await _userManager.AddToRoleAsync(userId, targetRole.Name);
                        if (!addition.Succeeded)
                        {
                            transaction.Rollback();
                            return OperationResult.Failure(addition.Errors.ToArray());
                        }

                        _auditRepository.Add(new AuditLog
                        {
                            TableName = "UserRole",
                            RecordId = userId,
                            Action = "UserRoleAssigned",
                            OldValue = POS.Data.Auditing.AuditEventDetails.PreviousRoles(roles),
                            NewValue = POS.Data.Auditing.AuditEventDetails.AssignedRole(targetRole.Id, targetRole.Name),
                            UserId = CurrentUser.UserId,
                            DateLogged = _clock.UtcNow
                        });
                        transaction.Commit();
                        return OperationResult.Success();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }
        public async Task<OperationResult<string>> AuthenticateAsync(string username, string password)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
                {
                    RecordAuthenticationEvent("LoginFailed", null, username, "Missing credentials");
                    return OperationResult<string>.Failure("Username and password are required.");
                }

                var user = await _userManager.FindByNameAsync(username.Trim());
                if (user == null)
                {
                    RecordAuthenticationEvent("LoginFailed", null, username, "Unknown user or invalid password");
                    return OperationResult<string>.Failure("Invalid username or password.");
                }

                if (!user.LockoutEnabled)
                    await _userManager.SetLockoutEnabledAsync(user.Id, true);

                if (user.LockoutEndDateUtc == DateTime.MaxValue)
                {
                    RecordAuthenticationEvent("LoginDisabled", user.Id, user.UserName, "Account is disabled");
                    return OperationResult<string>.Failure("This account has been disabled. Contact an administrator.");
                }

                if (await _userManager.IsLockedOutAsync(user.Id))
                {
                    RecordAuthenticationEvent("LoginLockedOut", user.Id, user.UserName, "Account is locked");
                    return OperationResult<string>.Failure("This account is temporarily locked. Please try again later.");
                }

                if (!await _userManager.CheckPasswordAsync(user, password))
                {
                    await _userManager.AccessFailedAsync(user.Id);
                    if (await _userManager.IsLockedOutAsync(user.Id))
                    {
                        RecordAuthenticationEvent("LoginLockedOut", user.Id, user.UserName, "Failure threshold reached");
                        return OperationResult<string>.Failure("This account is temporarily locked. Please try again later.");
                    }

                    RecordAuthenticationEvent("LoginFailed", user.Id, user.UserName, "Invalid password");
                    return OperationResult<string>.Failure("Invalid username or password.");
                }

                await _userManager.ResetAccessFailedCountAsync(user.Id);
                RecordAuthenticationEvent("LoginSucceeded", user.Id, user.UserName, "Authenticated");
                return OperationResult<string>.Success(user.Id);
            }
        }

        public async Task<bool> Login(string username, string password)
        {
            return (await AuthenticateAsync(username, password)).Succeeded;
        }

        public async Task<OperationResult> ChangePasswordAsync(
            string userId,
            string currentPassword,
            string newPassword)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                if (string.IsNullOrWhiteSpace(userId))
                    return OperationResult.Failure("No authenticated user is available.");
                if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
                    return OperationResult.Failure("Current and new passwords are required.");

                var result = await _userManager.ChangePasswordAsync(userId, currentPassword, newPassword);
                if (result.Succeeded)
                    RecordAuthenticationEvent("PasswordChanged", userId, null, "Self-service change");
                return result.Succeeded
                    ? OperationResult.Success()
                    : OperationResult.Failure(result.Errors.ToArray());
            }
        }

        public async Task<OperationResult> ResetPasswordByAdministratorAsync(
            string userId,
            string newPassword)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ResourceCodes.Users, ClaimActionType.Edit);
                if (string.IsNullOrWhiteSpace(userId))
                    return OperationResult.Failure("A user is required.");

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return OperationResult.Failure("The selected user no longer exists.");

                var validation = await _userManager.PasswordValidator.ValidateAsync(newPassword);
                if (!validation.Succeeded)
                    return OperationResult.Failure(validation.Errors.ToArray());

                user.PasswordHash = _userManager.PasswordHasher.HashPassword(newPassword);
                user.SecurityStamp = Guid.NewGuid().ToString();
                var update = await _userManager.UpdateAsync(user);
                if (update.Succeeded)
                    RecordAuthenticationEvent("PasswordReset", user.Id, user.UserName, "Administrator reset");
                return update.Succeeded
                    ? OperationResult.Success()
                    : OperationResult.Failure(update.Errors.ToArray());
            }
        }

        public void Logout()
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                var userId = CurrentUser.UserId;
                if (!string.IsNullOrEmpty(userId))
                    RecordAuthenticationEvent("Logout", userId, CurrentUser.Username, "Session ended");
                CurrentUser.Logout();
            }
        }

        public void LockSession()
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                var userId = CurrentUser.UserId;
                if (!string.IsNullOrEmpty(userId))
                    RecordAuthenticationEvent("SessionLocked", userId, CurrentUser.Username, "Register locked");
                CurrentUser.Logout();
            }
        }

        public async Task<OperationResult> SetUserEnabledAsync(string userId, bool enabled)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ResourceCodes.Users, ClaimActionType.Edit);
                if (string.IsNullOrWhiteSpace(userId))
                    return OperationResult.Failure("A user is required.");

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return OperationResult.Failure("The selected user no longer exists.");

                if (!enabled)
                {
                    if (CurrentUser.UserId == userId)
                        return OperationResult.Failure("You cannot disable your own active account.");

                    var administratorRole = await _roleManager.FindByNameAsync("System Administrator");
                    if (administratorRole != null &&
                        user.Roles.Any(x => x.RoleId == administratorRole.Id) &&
                        await CountActiveAdministratorsAsync(administratorRole.Id) <= 1)
                        return OperationResult.Failure("The final active administrator cannot be disabled.");
                }

                if (enabled)
                {
                    user.AccessFailedCount = 0;
                    user.LockoutEndDateUtc = null;
                }
                else
                {
                    user.LockoutEnabled = true;
                    user.LockoutEndDateUtc = DateTime.MaxValue;
                }

                var update = await _userManager.UpdateAsync(user);
                if (!update.Succeeded)
                    return OperationResult.Failure(update.Errors.ToArray());

                RecordAuthenticationEvent(
                    enabled ? "UserEnabled" : "UserDisabled",
                    user.Id,
                    user.UserName,
                    enabled ? "Account enabled" : "Account disabled");
                return OperationResult.Success();
            }
        }

        private Task<int> CountActiveAdministratorsAsync(string administratorRoleId)
        {
            return _userManager.Users.CountAsync(x =>
                x.Roles.Any(role => role.RoleId == administratorRoleId) &&
                (!x.LockoutEndDateUtc.HasValue || x.LockoutEndDateUtc.Value != DateTime.MaxValue));
        }

        public void RecordSessionUnlocked(string userId, string username)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                if (string.IsNullOrWhiteSpace(userId))
                    throw new ArgumentException("A user is required.", nameof(userId));
                RecordAuthenticationEvent("SessionUnlocked", userId, username, "Register unlocked");
            }
        }

        private void RecordAuthenticationEvent(
            string action,
            string recordId,
            string username,
            string outcome)
        {
            _auditRepository.Add(new AuditLog
            {
                TableName = "Authentication",
                RecordId = recordId,
                Action = action,
                NewValue = POS.Data.Auditing.AuditEventDetails.Authentication(username, outcome),
                UserId = CurrentUser.UserId ?? recordId,
                DateLogged = _clock.UtcNow
            });
        }

        private void Demand(string resource, ClaimActionType action)
        {
            PermissionGuard.Demand(_authorization, resource, action);
        }

        public void Dispose()
        {
            _roleManager.Dispose();
            _userManager.Dispose();
            _auditRepository.Dispose();
            if (_ownsContext) _context.Dispose();
        }

    }
}
