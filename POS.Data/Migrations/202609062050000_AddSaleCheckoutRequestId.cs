namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddSaleCheckoutRequestId : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Sales", "RequestId", c => c.Guid());
            AddColumn("dbo.Sales", "RequestHash", c => c.String(maxLength: 64));
            Sql(@"
UPDATE dbo.Sales
SET RequestId = NEWID(),
    RequestHash = CONVERT(varchar(64), HASHBYTES('SHA2_256', CONCAT('legacy-sale:', Id)), 2)
WHERE RequestId IS NULL OR RequestHash IS NULL;");
            AlterColumn("dbo.Sales", "RequestId", c => c.Guid(nullable: false));
            AlterColumn("dbo.Sales", "RequestHash", c => c.String(nullable: false, maxLength: 64));
            CreateIndex("dbo.Sales", "RequestId", unique: true);
        }
        
        public override void Down()
        {
            DropIndex("dbo.Sales", new[] { "RequestId" });
            DropColumn("dbo.Sales", "RequestHash");
            DropColumn("dbo.Sales", "RequestId");
        }
    }
}
