using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using POS.Common.Enumerations;
using POS.Core;
using POS.Data.Context;
using POS.Domains.Security;
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
    public class RoleService
    {
        private readonly RoleManager<Role> _roleManager;
        private readonly BaseRepository<RoleClaim, string> _claimsRepo;
        private readonly BaseRepository<Module, string> _moduleRepo;
        private readonly UserManager<User> _userManager;
        public RoleService()
        {
            _roleManager = new RoleManager<Role>(new RoleStore<Role>(new POSContext()));
            _claimsRepo = new BaseRepository<RoleClaim, string>();
            _moduleRepo = new BaseRepository<Module, string>();
            _userManager = new UserManager<User>(new UserStore<User>(new POSContext()));
        }

        public void SetupClaims(string roleId, string username)
        {
            var user = _userManager.FindByName(username);
            roleId = user.Roles.FirstOrDefault().RoleId;
            var claims = new List<Claim>()
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                //new Claim(ClaimTypes.Role, user.Roles.f),
            };

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
            return _roleManager.Roles.Select(x => new RoleDTO
            {
                Name = x.Name,
                RoleId = x.Id
            }).ToList();
        }

        public void AddRole(RoleDTO dto)
        {
            var role = new Role
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name
            };
            _roleManager.Create(role);
        }

        public void UpdateRole(RoleDTO dto)
        {
            var role = _roleManager.FindById(dto.RoleId);
            role.Name = dto.Name;
            _roleManager.Update(role);
        }

        public IEnumerable<RoleClaimDTO> GetClaims(string roleId)
        {
            var modules = _moduleRepo.GetAll().ToList();

            var existingClaims = _claimsRepo.GetAll()
                .Where(x => x.RoleId == roleId);

            var missingModules = modules
                .Where(m => !existingClaims.Any(c => c.ModuleId == m.Id))
                .ToList();

            foreach (var module in missingModules)
            {
                var claim = new RoleClaim
                {
                    Id = Guid.NewGuid().ToString(),
                    RoleId = roleId,
                    ModuleId = module.Id,
                    CanView = false,
                    CanAdd = false,
                    CanEdit = false,
                    CanDelete = false
                };

                _claimsRepo.Add(claim);
            }

            return _claimsRepo.GetAllAsQueryable()
                .Include(x => x.Module)
                .Select(x => new RoleClaimDTO
                {
                    RoleClaimId = x.Id,
                    Name = x.Module.Name,
                    ModuleId = x.ModuleId,
                    CanView = x.CanView,
                    CanAdd = x.CanAdd,
                    CanDelete = x.CanDelete,
                    CanEdit = x.CanEdit,
                    RoleId = roleId
                })
                .ToList();
        }

        public void AddClaim(RoleClaimDTO roleClaim)
        {
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
        }

        public void UpdateClaim(RoleClaimDTO dto)
        {
            var roleClaim = _claimsRepo.GetById(dto.RoleClaimId);
            roleClaim.ModuleId = dto.ModuleId;
            roleClaim.CanView = dto.CanView;
            roleClaim.CanAdd = dto.CanAdd;
            roleClaim.CanDelete = dto.CanDelete;
            roleClaim.CanEdit = dto.CanEdit;
            roleClaim.RoleId = dto.RoleId;
            _claimsRepo.Update(roleClaim);
        }
    }
}
