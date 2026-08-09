using POS.Data.Context;
using POS.Domains.Security;

namespace POS.Data.Repositories
{
    internal class UserRepository
    {
        private readonly POSContext _context;

        public UserRepository(POSContext context)
        {
            _context = context;
        }

        public void Add(User user)
        {
            _context.Users.Add(user);
            _context.SaveChanges();
        }

    }
}
