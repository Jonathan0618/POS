using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using POS.Common.Enumerations;
using POS.Core;
using POS.Core.Abstractions;
using POS.Core.Security;
using POS.Core.Results;
using POS.Data.Context;
using POS.Domains.Security;
using POS.Domains.AuditEntry;
using POS.Models.Security;
using POS.Services.Repository;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace POS.Services.Security
{
    public class RoleService : IDisposable
    {
        private readonly POSContext _context;
        private readonly RoleManager<Role> _roleManager;
        private readonly BaseRepository<RoleClaim, string> _claimsRepo;
        private readonly BaseRepository<Module, int> _moduleRepo;
        private readonly UserManager<User, string> _userManager;
        private readonly bool _ownsContext;
        private readonly IAuthorizationService _authorization;
        private readonly IClock _clock;
        private readonly BaseRepository<AuditLog, long> _auditRepo;

        public RoleService() : this(new POSContext(), new ClaimsAuthorizationService(new CurrentUserAccessor()), new SystemClock(), true) { }
        public RoleService(POSContext context) : this(context, new ClaimsAuthorizationService(new CurrentUserAccessor()), new SystemClock(), false) { }
        public RoleService(POSContext context, IAuthorizationService authorization) : this(context, authorization, new SystemClock(), false) { }
        public RoleService(POSContext context, IAuthorizationService authorization, IClock clock) : this(context, authorization, clock, false) { }

        private RoleService(POSContext context, IAuthorizationService authorization, IClock clock, bool ownsContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _ownsContext = ownsContext;
            var roleStore = new RoleStore<Role>(_context) { DisposeContext = false };
            var userStore = new UserStore<User, Role, string, IdentityUserLogin,
                IdentityUserRole, IdentityUserClaim>(_context) { DisposeContext = false };
            _roleManager = new RoleManager<Role>(roleStore);
            _claimsRepo = new BaseRepository<RoleClaim, string>(_context, false);
            _moduleRepo = new BaseRepository<Module, int>(_context);
            _auditRepo = new BaseRepository<AuditLog, long>(_context, false);
            _userManager = new UserManager<User, string>(userStore);
        }

        public void SetupClaims(string roleId, string username)
        {
            var user = _userManager.FindByName(username);
            if (user == null)
                throw new InvalidOperationException("The authenticated user no longer exists.");

            roleId = user.Roles.Select(x => x.RoleId).FirstOrDefault();
            var role = string.IsNullOrEmpty(roleId) ? null : _roleManager.FindById(roleId);
            var displayName = string.Join(" ", new[] { user.FirstName, user.LastName }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            var claims = new List<Claim>()
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
            };

            if (!string.IsNullOrEmpty(roleId))
                claims.Add(new Claim(SessionClaimTypes.RoleId, roleId));
            if (role != null)
                claims.Add(new Claim(ClaimTypes.Role, role.Name));
            if (!string.IsNullOrEmpty(displayName))
                claims.Add(new Claim(SessionClaimTypes.DisplayName, displayName));

            var roleClaims = _claimsRepo.GetAllAsQueryable()
                .Include(x => x.Module)
                .Where(x => x.RoleId == roleId)
                .ToList();

            foreach (var claim in roleClaims)
            {
                if(claim.CanView)
                    claims.Add(new Claim(claim.Module.Name, ClaimActionType.View.ToString()));


                if (claim.CanAdd)
                    claims.Add(new Claim(claim.Module.Name, ClaimActionType.Add.ToString()));


                if (claim.CanEdit)
                    claims.Add(new Claim(claim.Module.Name, ClaimActionType.Edit.ToString()));


                if (claim.CanDelete)
                    claims.Add(new Claim(claim.Module.Name, ClaimActionType.Delete.ToString()));
            }

            var identity = new ClaimsIdentity(claims, "Application");
            CurrentUser.Principal = new ClaimsPrincipal(identity);
        }

        public IEnumerable<RoleDTO> GetRoles()
        {
            Demand(ClaimActionType.View);
            return _roleManager.Roles.Select(x => new RoleDTO
            {
                Name = x.Name,
                RoleId = x.Id
            }).ToList();
        }

        public OperationResult AddRole(RoleDTO dto)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ClaimActionType.Add);
                var validation = ValidateRoleName(dto?.Name, null);
                if (!validation.Succeeded)
                    return validation;

                var role = new Role
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = dto.Name.Trim()
                };
                var result = _roleManager.Create(role);
                if (result.Succeeded)
                {
                    dto.RoleId = role.Id;
                    return OperationResult.Success();
                }
                return OperationResult.Failure(result.Errors.ToArray());
            }
        }

        public OperationResult UpdateRole(RoleDTO dto)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ClaimActionType.Edit);
                if (dto == null || string.IsNullOrWhiteSpace(dto.RoleId))
                    return OperationResult.Failure("A saved role is required.");
                var role = _roleManager.FindById(dto.RoleId);
                if (role == null)
                    return OperationResult.Failure("The selected role no longer exists.");
                if (role.Name.Equals("System Administrator", StringComparison.OrdinalIgnoreCase) &&
                    !role.Name.Equals(dto.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
                    return OperationResult.Failure("The System Administrator role cannot be renamed.");

                var validation = ValidateRoleName(dto.Name, role.Id);
                if (!validation.Succeeded)
                    return validation;
                role.Name = dto.Name.Trim();
                var result = _roleManager.Update(role);
                return result.Succeeded
                    ? OperationResult.Success()
                    : OperationResult.Failure(result.Errors.ToArray());
            }
        }

        public OperationResult DeleteRole(string roleId)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ClaimActionType.Delete);
                if (string.IsNullOrWhiteSpace(roleId))
                    return OperationResult.Failure("A saved role is required.");
                var role = _roleManager.FindById(roleId);
                if (role == null)
                    return OperationResult.Failure("The selected role no longer exists.");
                if (role.Name.Equals("System Administrator", StringComparison.OrdinalIgnoreCase))
                    return OperationResult.Failure("The System Administrator role cannot be deleted.");
                if (_userManager.Users.Any(x => x.Roles.Any(userRole => userRole.RoleId == roleId)))
                    return OperationResult.Failure("A role assigned to users cannot be deleted.");

                var result = _roleManager.Delete(role);
                return result.Succeeded
                    ? OperationResult.Success()
                    : OperationResult.Failure(result.Errors.ToArray());
            }
        }

        private OperationResult ValidateRoleName(string name, string excludedRoleId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return OperationResult.Failure("Role name is required.");
            var trimmed = name.Trim();
            if (_roleManager.Roles.Any(x =>
                x.Name == trimmed && (excludedRoleId == null || x.Id != excludedRoleId)))
                return OperationResult.Failure("A role with this name already exists.");
            return OperationResult.Success();
        }

        public IEnumerable<RoleClaimDTO> GetClaims(string roleId)
        {
            Demand(ClaimActionType.View);
            var modules = _moduleRepo.GetAll().ToList();
            var existingClaims = _claimsRepo.GetAllAsQueryable()
                .Where(x => x.RoleId == roleId)
                .ToList()
                .ToDictionary(x => x.ModuleId);

            return modules.Select(module =>
            {
                RoleClaim claim;
                existingClaims.TryGetValue(module.Id, out claim);
                return new RoleClaimDTO
                {
                    RoleClaimId = claim?.Id,
                    Name = module.Name,
                    ModuleId = module.Id,
                    CanView = claim?.CanView ?? false,
                    CanAdd = claim?.CanAdd ?? false,
                    CanDelete = claim?.CanDelete ?? false,
                    CanEdit = claim?.CanEdit ?? false,
                    RoleId = roleId
                };
            })
                .OrderBy(x => x.Name)
                .ToList();
        }

        public void AddClaim(RoleClaimDTO roleClaim)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ClaimActionType.Edit);
                var existing = _claimsRepo.GetAllAsQueryable().SingleOrDefault(x =>
                    x.RoleId == roleClaim.RoleId && x.ModuleId == roleClaim.ModuleId);
                if (existing != null)
                {
                    roleClaim.RoleClaimId = existing.Id;
                    UpdateClaim(roleClaim);
                    return;
                }

                var newRoleClaim = new RoleClaim
                {
                    Id = Guid.NewGuid().ToString(),
                    ModuleId = roleClaim.ModuleId,
                    CanView = roleClaim.CanView,
                    CanAdd = roleClaim.CanAdd,
                    CanDelete = roleClaim.CanDelete,
                    CanEdit = roleClaim.CanEdit,
                    RoleId = roleClaim.RoleId
                };
                _claimsRepo.Add(newRoleClaim);
                roleClaim.RoleClaimId = newRoleClaim.Id;
                RecordPermissionAudit("PermissionCreated", newRoleClaim.Id, null, FormatPermissions(newRoleClaim));
                _context.SaveChanges();
            }
        }

        public void UpdateClaim(RoleClaimDTO dto)
        {
            using (var auditOperation = POS.Core.Abstractions.AuditOperation.Begin())
            {
                Demand(ClaimActionType.Edit);
                if (string.IsNullOrEmpty(dto.RoleClaimId))
                {
                    AddClaim(dto);
                    return;
                }
                var roleClaim = _claimsRepo.GetById(dto.RoleClaimId);
                if (roleClaim == null)
                    throw new InvalidOperationException("The selected permission no longer exists.");
                var oldValue = FormatPermissions(roleClaim);
                roleClaim.ModuleId = dto.ModuleId;
                roleClaim.CanView = dto.CanView;
                roleClaim.CanAdd = dto.CanAdd;
                roleClaim.CanDelete = dto.CanDelete;
                roleClaim.CanEdit = dto.CanEdit;
                roleClaim.RoleId = dto.RoleId;
                _claimsRepo.Update(roleClaim);
                RecordPermissionAudit("PermissionUpdated", roleClaim.Id, oldValue, FormatPermissions(roleClaim));
                _context.SaveChanges();
            }
        }

        private void RecordPermissionAudit(string action, string recordId, string oldValue, string newValue)
        {
            _auditRepo.Add(new AuditLog
            {
                TableName = "RolePermission",
                RecordId = recordId,
                Action = action,
                OldValue = oldValue,
                NewValue = newValue,
                UserId = CurrentUser.UserId,
                DateLogged = _clock.UtcNow
            });
        }

        private static string FormatPermissions(RoleClaim claim)
        {
            return POS.Data.Auditing.AuditEventDetails.Permissions(claim.RoleId, claim.ModuleId,
                claim.CanView, claim.CanAdd, claim.CanEdit, claim.CanDelete);
        }

        private void Demand(ClaimActionType action)
        {
            PermissionGuard.Demand(_authorization, ResourceCodes.Roles, action);
        }

        public void Dispose()
        {
            _roleManager.Dispose();
            _userManager.Dispose();
            _claimsRepo.Dispose();
            _auditRepo.Dispose();
            _moduleRepo.Dispose();
            if (_ownsContext) _context.Dispose();
        }
    }
}
