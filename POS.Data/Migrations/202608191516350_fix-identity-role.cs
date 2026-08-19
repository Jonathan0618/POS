namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class fixidentityrole : DbMigration
    {
        public override void Up()
        {
            DropIndex("dbo.RoleClaims", new[] { "RoleId" });
            AlterColumn("dbo.RoleClaims", "RoleId", c => c.String(maxLength: 128));
            CreateIndex("dbo.RoleClaims", "RoleId");
        }
        
        public override void Down()
        {
            DropIndex("dbo.RoleClaims", new[] { "RoleId" });
            AlterColumn("dbo.RoleClaims", "RoleId", c => c.String(nullable: false, maxLength: 128));
            CreateIndex("dbo.RoleClaims", "RoleId");
        }
    }
}
