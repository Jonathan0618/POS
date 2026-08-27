namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class auditLog : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Products", "ExpiryDate", c => c.DateTime());
            DropColumn("dbo.Products", "Unit");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Products", "Unit", c => c.String(nullable: false));
            AlterColumn("dbo.Products", "ExpiryDate", c => c.String(maxLength: 50));
        }
    }
}
