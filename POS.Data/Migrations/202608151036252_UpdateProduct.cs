namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UpdateProduct : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Products", "CostPrice", c => c.Decimal(nullable: false, precision: 18, scale: 2));
            AddColumn("dbo.Products", "Quantity", c => c.Int(nullable: false));
            AddColumn("dbo.Products", "Barcode", c => c.String());
            AddColumn("dbo.Products", "ExpiryDate", c => c.String());
            AddColumn("dbo.Products", "IsActive", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Products", "IsActive");
            DropColumn("dbo.Products", "ExpiryDate");
            DropColumn("dbo.Products", "Barcode");
            DropColumn("dbo.Products", "Quantity");
            DropColumn("dbo.Products", "CostPrice");
        }
    }
}
