namespace POS.Data.Migrations
{
    using Microsoft.AspNet.Identity;
    using Microsoft.AspNet.Identity.EntityFramework;
    using POS.Data.Context;
    using POS.Domains.Security;
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;

    internal sealed class Configuration : DbMigrationsConfiguration<POS.Data.Context.POSContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(POSContext context)
        {
            var roleStore = new RoleStore<Role>(
                context
            );

            var roleManager = new RoleManager<Role>(roleStore);

            var userStore = new UserStore<User>(context);

            var userManager = new UserManager<User>(userStore);

            const string roleName = "System Administrator";
            const string username = "admin";

            var role = roleManager.FindByName(roleName);

            if (role == null)
            {
                role = new Role
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = roleName
                };

                roleManager.Create(role);
            }

            var user = userManager.FindByName(username);

            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = username,
                    Email = "admin@system.admin"
                };

                var result = userManager.Create(user, "Admin@123");

                if (!result.Succeeded)
                {
                    throw new Exception(
                        string.Join(", ", result.Errors)
                    );
                }

                userManager.AddToRole(user.Id, roleName);
            }
        }
    }
}
