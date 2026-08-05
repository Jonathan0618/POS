using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using POS.Domains.Security;
using System.Threading.Tasks;

namespace POS.Services.Security
{
    public class UserService
    {
        private readonly UserManager<User> _userManager;
        public UserService()
        {
            UserManager<User> userManager = new UserManager<User>(new UserStore<User>());
        }

        public async Task CreateUser(string username, string password)
        {
            var user = new User { UserName = username };
            await _userManager.CreateAsync(user, password);
        }

        public async Task<User> FindByIdAsync(string userId)
        {
            return await _userManager.FindByIdAsync(userId);
        }

        public async Task<bool> Login(string username, string password)
        {
            var user = await _userManager.FindAsync(username, password);
            return user == null ? false : true;
        }
    }
}
