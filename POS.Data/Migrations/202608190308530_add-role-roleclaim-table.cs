namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class addroleroleclaimtable : DbMigration
    {
        public override void Up()
        {
            DropIndex("dbo.Roles", "RoleNameIndex");
            CreateTable(
                "dbo.RoleClaims",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 36),
                        RoleId = c.String(nullable: false, maxLength: 128),
                        Name = c.String(),
                        Action = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Roles", t => t.RoleId)
                .Index(t => t.RoleId);
            
            AlterColumn("dbo.Roles", "Name", c => c.String(nullable: false, maxLength: 100));
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.RoleClaims", "RoleId", "dbo.Roles");
            DropIndex("dbo.RoleClaims", new[] { "RoleId" });
            AlterColumn("dbo.Roles", "Name", c => c.String(nullable: false, maxLength: 256));
            DropTable("dbo.RoleClaims");
            CreateIndex("dbo.Roles", "Name", unique: true, name: "RoleNameIndex");
        }
    }
}
