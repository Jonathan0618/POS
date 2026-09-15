namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class stock : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Stocks", "Unit", c => c.String());
        }

        public override void Down()
        {
            DropColumn("dbo.Stocks", "Unit");
        }
    }
}
