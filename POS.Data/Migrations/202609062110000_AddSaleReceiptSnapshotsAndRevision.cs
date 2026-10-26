namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddSaleReceiptSnapshotsAndRevision : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Sales", "StoreName", c => c.String(maxLength: 150));
            AddColumn("dbo.Sales", "StoreAddress", c => c.String(maxLength: 300));
            AddColumn("dbo.Sales", "StoreTaxIdentifier", c => c.String(maxLength: 50));
            AddColumn("dbo.Sales", "ReceiptFooter", c => c.String(maxLength: 500));
            AddColumn("dbo.Sales", "RegisterCode", c => c.String(maxLength: 30));
            AddColumn("dbo.Sales", "RegisterName", c => c.String(maxLength: 100));
            AddColumn("dbo.Sales", "CashierName", c => c.String(maxLength: 256));
            AddColumn("dbo.Sales", "CustomerCode", c => c.String(maxLength: 30));
            AddColumn("dbo.Sales", "CustomerName", c => c.String(maxLength: 150));
            AddColumn("dbo.Sales", "RowVersion", c => c.Binary(nullable: false, fixedLength: true, timestamp: true, storeType: "rowversion"));
            Sql(@"
UPDATE sale
SET StoreName = store.StoreName,
    StoreAddress = store.Address,
    StoreTaxIdentifier = store.TaxIdentifier,
    ReceiptFooter = store.ReceiptFooter,
    RegisterCode = register.Code,
    RegisterName = register.Name,
    CashierName = cashier.UserName,
    CustomerCode = customer.Code,
    CustomerName = customer.Name
FROM dbo.Sales sale
OUTER APPLY (SELECT TOP (1) StoreName, Address, TaxIdentifier, ReceiptFooter FROM dbo.StoreSettings ORDER BY Id) store
LEFT JOIN dbo.RegisterStations register ON register.Id = sale.RegisterStationId
LEFT JOIN dbo.Users cashier ON cashier.Id = sale.CashierUserId
LEFT JOIN dbo.Customers customer ON customer.Id = sale.CustomerId
WHERE sale.StoreName IS NULL;");
            Sql(@"
IF EXISTS (
    SELECT TenderType, ExternalReference
    FROM dbo.Payments
    WHERE Status = 1 AND ExternalReference IS NOT NULL
    GROUP BY TenderType, ExternalReference
    HAVING COUNT_BIG(*) > 1)
    THROW 51010, 'Duplicate completed payment references exist. Review payment history before applying this migration.', 1;

CREATE UNIQUE INDEX UX_Payments_CompletedExternalReference
ON dbo.Payments(TenderType, ExternalReference)
WHERE Status = 1 AND ExternalReference IS NOT NULL;");
        }
        
        public override void Down()
        {
            DropIndex("dbo.Payments", "UX_Payments_CompletedExternalReference");
            DropColumn("dbo.Sales", "RowVersion");
            DropColumn("dbo.Sales", "CustomerName");
            DropColumn("dbo.Sales", "CustomerCode");
            DropColumn("dbo.Sales", "CashierName");
            DropColumn("dbo.Sales", "RegisterName");
            DropColumn("dbo.Sales", "RegisterCode");
            DropColumn("dbo.Sales", "ReceiptFooter");
            DropColumn("dbo.Sales", "StoreTaxIdentifier");
            DropColumn("dbo.Sales", "StoreAddress");
            DropColumn("dbo.Sales", "StoreName");
        }
    }
}
