namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddSaleReturnPostingGuards : DbMigration
    {
        public override void Up()
        {
            Sql(@"
IF EXISTS (SELECT 1 FROM dbo.RefundPayments)
    THROW 51020, 'Legacy refund payments require reviewed original-payment linkage before this migration can be applied.', 1;
IF EXISTS (
    SELECT 1 FROM dbo.SaleReturns r
    JOIN dbo.Sales s ON s.Id = r.SaleId
    WHERE s.RegisterStationId IS NULL OR s.CashierShiftId IS NULL)
    THROW 51021, 'Legacy sale returns without a resolvable register or shift require review before migration.', 1;");
            AddColumn("dbo.RefundPayments", "OriginalPaymentId", c => c.Long());
            AddColumn("dbo.SaleReturns", "RequestId", c => c.Guid());
            AddColumn("dbo.SaleReturns", "RequestHash", c => c.String(maxLength: 64));
            AddColumn("dbo.SaleReturns", "RegisterStationId", c => c.Int());
            AddColumn("dbo.SaleReturns", "CashierShiftId", c => c.Int());
            AddColumn("dbo.SaleReturns", "ExchangeSaleId", c => c.Int());
            Sql(@"
UPDATE r
SET RequestId = NEWID(),
    RequestHash = CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT('legacy-sale-return:', r.Id)), 2),
    RegisterStationId = s.RegisterStationId,
    CashierShiftId = s.CashierShiftId
FROM dbo.SaleReturns r
JOIN dbo.Sales s ON s.Id = r.SaleId;");
            AlterColumn("dbo.RefundPayments", "OriginalPaymentId", c => c.Long(nullable: false));
            AlterColumn("dbo.SaleReturns", "RequestId", c => c.Guid(nullable: false));
            AlterColumn("dbo.SaleReturns", "RequestHash", c => c.String(nullable: false, maxLength: 64));
            AlterColumn("dbo.SaleReturns", "RegisterStationId", c => c.Int(nullable: false));
            AlterColumn("dbo.SaleReturns", "CashierShiftId", c => c.Int(nullable: false));
            CreateIndex("dbo.RefundPayments", "OriginalPaymentId");
            CreateIndex("dbo.SaleReturns", "RequestId", unique: true);
            CreateIndex("dbo.SaleReturns", "RegisterStationId");
            CreateIndex("dbo.SaleReturns", "CashierShiftId");
            CreateIndex("dbo.SaleReturnLines", new[] { "SaleReturnId", "SaleItemId" }, unique: true, name: "UX_SaleReturnLines_DocumentItem");
            Sql("CREATE UNIQUE INDEX UX_SaleReturns_ExchangeSaleId ON dbo.SaleReturns(ExchangeSaleId) WHERE ExchangeSaleId IS NOT NULL;");
            Sql(@"
IF EXISTS (SELECT ExternalReference FROM dbo.RefundPayments WHERE ExternalReference IS NOT NULL GROUP BY ExternalReference HAVING COUNT_BIG(*) > 1)
    THROW 51022, 'Duplicate refund provider references require review before migration.', 1;
CREATE UNIQUE INDEX UX_RefundPayments_ExternalReference ON dbo.RefundPayments(ExternalReference) WHERE ExternalReference IS NOT NULL;");
            AddForeignKey("dbo.RefundPayments", "OriginalPaymentId", "dbo.Payments", "Id");
            AddForeignKey("dbo.SaleReturns", "CashierShiftId", "dbo.CashierShifts", "Id");
            AddForeignKey("dbo.SaleReturns", "RegisterStationId", "dbo.RegisterStations", "Id");
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.SaleReturns", "RegisterStationId", "dbo.RegisterStations");
            DropForeignKey("dbo.SaleReturns", "CashierShiftId", "dbo.CashierShifts");
            DropForeignKey("dbo.RefundPayments", "OriginalPaymentId", "dbo.Payments");
            DropIndex("dbo.SaleReturns", new[] { "CashierShiftId" });
            DropIndex("dbo.SaleReturns", new[] { "RegisterStationId" });
            DropIndex("dbo.SaleReturns", "UX_SaleReturns_ExchangeSaleId");
            DropIndex("dbo.SaleReturns", new[] { "RequestId" });
            DropIndex("dbo.SaleReturnLines", "UX_SaleReturnLines_DocumentItem");
            DropIndex("dbo.RefundPayments", "UX_RefundPayments_ExternalReference");
            DropIndex("dbo.RefundPayments", new[] { "OriginalPaymentId" });
            DropColumn("dbo.SaleReturns", "ExchangeSaleId");
            DropColumn("dbo.SaleReturns", "CashierShiftId");
            DropColumn("dbo.SaleReturns", "RegisterStationId");
            DropColumn("dbo.SaleReturns", "RequestHash");
            DropColumn("dbo.SaleReturns", "RequestId");
            DropColumn("dbo.RefundPayments", "OriginalPaymentId");
        }
    }
}
