namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class roleclaimtableupdate : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.RoleClaims", "CanView", c => c.Boolean(nullable: false));
            AddColumn("dbo.RoleClaims", "CanAdd", c => c.Boolean(nullable: false));
            AddColumn("dbo.RoleClaims", "CanEdit", c => c.Boolean(nullable: false));
            AddColumn("dbo.RoleClaims", "CanDelete", c => c.Boolean(nullable: false));
            DropColumn("dbo.RoleClaims", "Action");
        }
        
        public override void Down()
        {
            AddColumn("dbo.RoleClaims", "Action", c => c.String());
            DropColumn("dbo.RoleClaims", "CanDelete");
            DropColumn("dbo.RoleClaims", "CanEdit");
            DropColumn("dbo.RoleClaims", "CanAdd");
            DropColumn("dbo.RoleClaims", "CanView");
        }
    }
}
