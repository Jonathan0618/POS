namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class addmoduletable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Modules",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ParentModuleId = c.Int(),
                        Name = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            AddColumn("dbo.RoleClaims", "ModuleId", c => c.Int(nullable: false));
            CreateIndex("dbo.RoleClaims", "ModuleId");
            AddForeignKey("dbo.RoleClaims", "ModuleId", "dbo.Modules", "Id", cascadeDelete: true);
            DropColumn("dbo.RoleClaims", "Name");
        }
        
        public override void Down()
        {
            AddColumn("dbo.RoleClaims", "Name", c => c.String());
            DropForeignKey("dbo.RoleClaims", "ModuleId", "dbo.Modules");
            DropIndex("dbo.RoleClaims", new[] { "ModuleId" });
            DropColumn("dbo.RoleClaims", "ModuleId");
            DropTable("dbo.Modules");
        }
    }
}
