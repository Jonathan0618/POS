namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class update : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Products", "Unit", c => c.String());
        }
        
        public override void Down()
        {
            AlterColumn("dbo.Products", "Unit", c => c.String(nullable: false));
        }
    }
}
