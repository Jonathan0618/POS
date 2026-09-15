namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddExchangeSaleForeignKey : DbMigration
    {
        public override void Up()
        {
            CreateIndex("dbo.SaleReturns", "ExchangeSaleId");
            AddForeignKey("dbo.SaleReturns", "ExchangeSaleId", "dbo.Sales", "Id");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.SaleReturns", "ExchangeSaleId", "dbo.Sales");
            DropIndex("dbo.SaleReturns", new[] { "ExchangeSaleId" });
        }
    }
}
