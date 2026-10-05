namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddShiftOpeningGuards : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.CashierShifts", "RequestId", c => c.Guid());
            Sql("UPDATE dbo.CashierShifts SET RequestId = NEWID() WHERE RequestId IS NULL");
            AlterColumn("dbo.CashierShifts", "RequestId", c => c.Guid(nullable: false));
            CreateIndex("dbo.CashierShifts", "RequestId", unique: true);
            Sql("CREATE UNIQUE INDEX IX_OpenShiftRegister ON dbo.CashierShifts(RegisterStationId) WHERE Status = 0");
            Sql("CREATE UNIQUE INDEX IX_OpenShiftCashier ON dbo.CashierShifts(CashierUserId) WHERE Status = 0");
        }
        
        public override void Down()
        {
            Sql("DROP INDEX IX_OpenShiftCashier ON dbo.CashierShifts");
            Sql("DROP INDEX IX_OpenShiftRegister ON dbo.CashierShifts");
            DropIndex("dbo.CashierShifts", new[] { "RequestId" });
            DropColumn("dbo.CashierShifts", "RequestId");
        }
    }
}
