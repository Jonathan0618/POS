using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using POS.Core;
using POS.Data.Context;
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
    public class UserService
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<Role> _roleManager;
        private readonly POSContext _context;
        public UserService()
        {
            _context = new POSContext();
            _roleManager = new RoleManager<Role>(new RoleStore<Role>(_context));
            _userManager = new UserManager<User>(new UserStore<User>(_context));
        }

        public async Task<IEnumerable<UserDTO>> GetUsers()
        {
            var users = await _userManager.Users
                .Select(x => new UserDTO
                {
                    UserId = x.Id,
                    FullName = x.FirstName + " " + x.LastName,
                    Username = x.UserName,
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

        public async Task AddUserAsync(UserDTO user)
        {
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
                EmailConfirmed = true
            };
            await _userManager.CreateAsync(newUser, user.Password);
            if (!string.IsNullOrEmpty(user.Role))
            {
                await AssingRoleToUser(newUser.Id, user.Role);
            }
        }

        public async Task UpdateUserAsync(EditUserDTO user)
        {
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
                await AssingRoleToUser(user.UserId, user.Role);
            }

            if (!string.IsNullOrEmpty(user.ConfirmPassword))
            {
                var token = _userManager.GeneratePasswordResetToken(user.UserId);
                await _userManager.ResetPasswordAsync(user.UserId, token, user.ConfirmPassword);
            }
        }

        public async Task<UserDTO> GetUserByIdAsync(string userId)
        {
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

            user.Role = res.Roles?.FirstOrDefault()?.RoleId ?? "";
            return user;
        }

        public IEnumerable<RoleDTO> GetRoles()
        {
            var roles = _roleManager.Roles.Select(x => new RoleDTO
            {
                RoleId = x.Id,
                Name = x.Name
            });
            return roles.ToList();
        }

        public async Task AddRoleAsync(RoleDTO role)
        {
            var newRole = new Role
            {
                Id = Guid.NewGuid().ToString(),
                Name = role.Name
            };
            await _roleManager.CreateAsync(newRole);
        }

        public async Task UpdateRoleAsync(RoleDTO role)
        {
            var newRole = await _roleManager.FindByIdAsync(role.RoleId);
            newRole.Name = role.Name;
            await _roleManager.UpdateAsync(newRole);
        }

        public async Task AssingRoleToUser(string userId, string roleName)
        {
            var roles = await _userManager.GetRolesAsync(userId);
            foreach (var item in roles)
            {
                await _userManager.RemoveFromRoleAsync(userId, item);
            }
            await _userManager.AddToRoleAsync(userId, roleName);
        }
        public async Task <bool> Login(string username, string password)
        {
            var user = await _userManager.FindAsync(username, password);
            var isSuccess = user != null;
            if (isSuccess)
            {
                CurrentUser.UserId = user.Id;
                CurrentUser.RoleId = "";
            };
            return isSuccess;
        }

    }
}
