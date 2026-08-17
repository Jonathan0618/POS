namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class product : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Products", "BuyingThreshold", c => c.Int(nullable: false));
            AddColumn("dbo.Products", "ModifiedBy", c => c.String(maxLength: 100));
            AddColumn("dbo.Products", "ModifiedAt", c => c.DateTime());
            AddColumn("dbo.Products", "CreatedBy", c => c.String(maxLength: 100));
            AddColumn("dbo.Products", "CreatedAt", c => c.DateTime(nullable: false));
            AlterColumn("dbo.Products", "Name", c => c.String(nullable: false, maxLength: 100));
            AlterColumn("dbo.Products", "Description", c => c.String(maxLength: 500));
            AlterColumn("dbo.Products", "Barcode", c => c.String(maxLength: 50));
            AlterColumn("dbo.Products", "ExpiryDate", c => c.String(maxLength: 50));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.Products", "ExpiryDate", c => c.String());
            AlterColumn("dbo.Products", "Barcode", c => c.String());
            AlterColumn("dbo.Products", "Description", c => c.String());
            AlterColumn("dbo.Products", "Name", c => c.String());
            DropColumn("dbo.Products", "CreatedAt");
            DropColumn("dbo.Products", "CreatedBy");
            DropColumn("dbo.Products", "ModifiedAt");
            DropColumn("dbo.Products", "ModifiedBy");
            DropColumn("dbo.Products", "BuyingThreshold");
        }
    }
}
