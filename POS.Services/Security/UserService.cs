using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using POS.Data.Context;
using POS.Domains.Security;
using POS.Models.Store;
using POS.Services.Repository;
using System.Data;
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

        public async Task CreateUser(User user, string password)
        {
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new DataException(string.Join(", ", result.Errors));
            }
        }

        public async Task<User> FindByIdAsync(string userId)
        {
            return await _userManager.FindByIdAsync(userId);
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
            if(isSuccess)
                UserStore.UserId = user.Id;
            
            return isSuccess;
        }

    }
}
