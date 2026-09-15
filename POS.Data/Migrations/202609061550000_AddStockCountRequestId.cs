namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddStockCountRequestId : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.StockCounts", "RequestId", c => c.Guid());
            AddColumn("dbo.StockCounts", "RequestHash", c => c.String(maxLength: 64));
            Sql("CREATE UNIQUE INDEX UX_StockCounts_RequestId ON dbo.StockCounts(RequestId) WHERE RequestId IS NOT NULL;");
        }

        public override void Down()
        {
            DropIndex("dbo.StockCounts", "UX_StockCounts_RequestId");
            DropColumn("dbo.StockCounts", "RequestHash");
            DropColumn("dbo.StockCounts", "RequestId");
        }
    }
}
