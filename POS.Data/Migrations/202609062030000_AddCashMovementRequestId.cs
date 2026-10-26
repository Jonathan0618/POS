namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCashMovementRequestId : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.CashMovements", "RequestId", c => c.Guid());
            Sql("UPDATE dbo.CashMovements SET RequestId = NEWID() WHERE RequestId IS NULL");
            AlterColumn("dbo.CashMovements", "RequestId", c => c.Guid(nullable: false));
            CreateIndex("dbo.CashMovements", "RequestId", unique: true);
        }
        
        public override void Down()
        {
            DropIndex("dbo.CashMovements", new[] { "RequestId" });
            DropColumn("dbo.CashMovements", "RequestId");
        }
    }
}
