namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddManagementQueryIndexes : DbMigration
    {
        public override void Up()
        {
            CreateIndex("dbo.Sales", new[] { "Status", "SaleDate", "RegisterStationId" }, name: "IX_Sales_ReportScope");
            CreateIndex("dbo.SaleReturns", new[] { "Status", "CreatedUtc", "RegisterStationId" }, name: "IX_SaleReturns_ReportScope");
            CreateIndex("dbo.GoodsReceipts", new[] { "Status", "ReceivedUtc", "SupplierId" }, name: "IX_GoodsReceipts_ReportScope");
            CreateIndex("dbo.PurchaseReturns", new[] { "Status", "ReturnedUtc", "SupplierId" }, name: "IX_PurchaseReturns_ReportScope");
            CreateIndex("dbo.CashierShifts", new[] { "OpenedUtc", "RegisterStationId", "Status" }, name: "IX_CashierShifts_ReportScope");
            CreateIndex("dbo.StockMovements", "CreatedUtc", name: "IX_StockMovements_CreatedUtc");
            CreateIndex("dbo.AuditLogs", "DateLogged", name: "IX_AuditLogs_DateLogged");
        }
        
        public override void Down()
        {
            DropIndex("dbo.AuditLogs", "IX_AuditLogs_DateLogged");
            DropIndex("dbo.StockMovements", "IX_StockMovements_CreatedUtc");
            DropIndex("dbo.CashierShifts", "IX_CashierShifts_ReportScope");
            DropIndex("dbo.PurchaseReturns", "IX_PurchaseReturns_ReportScope");
            DropIndex("dbo.GoodsReceipts", "IX_GoodsReceipts_ReportScope");
            DropIndex("dbo.SaleReturns", "IX_SaleReturns_ReportScope");
            DropIndex("dbo.Sales", "IX_Sales_ReportScope");
        }
    }
}
