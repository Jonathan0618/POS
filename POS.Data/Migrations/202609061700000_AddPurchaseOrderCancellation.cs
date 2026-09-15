namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddPurchaseOrderCancellation : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PurchaseOrders", "CancelledUtc", c => c.DateTime());
            AddColumn("dbo.PurchaseOrders", "CancelReason", c => c.String(maxLength: 250));
        }

        public override void Down()
        {
            DropColumn("dbo.PurchaseOrders", "CancelReason");
            DropColumn("dbo.PurchaseOrders", "CancelledUtc");
        }
    }
}
