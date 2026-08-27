namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class ProductUnit : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Products", "Unit", c => c.String(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Products", "Unit");
        }
    }
}
