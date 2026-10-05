namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddUniqueSaleReceiptNumber : DbMigration
    {
        public override void Up()
        {
            CreateIndex("dbo.Sales", "ReceiptNumber", unique: true);
        }
        
        public override void Down()
        {
            DropIndex("dbo.Sales", new[] { "ReceiptNumber" });
        }
    }
}
