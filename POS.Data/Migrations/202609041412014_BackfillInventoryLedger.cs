namespace POS.Data.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class BackfillInventoryLedger : DbMigration
    {
        public override void Up()
        {
            Sql(@"INSERT INTO dbo.InventoryBalances (ProductId, QuantityOnHand)
                  SELECT p.Id, p.Quantity
                  FROM dbo.Products p
                  WHERE NOT EXISTS (SELECT 1 FROM dbo.InventoryBalances b WHERE b.ProductId = p.Id);");

            Sql(@"INSERT INTO dbo.StockMovements
                    (ProductId, MovementType, QuantityDelta, ReferenceType, ReferenceId, Reason, UserId, CreatedUtc)
                  SELECT p.Id, 0, p.Quantity, 'Migration', CAST(p.Id AS nvarchar(50)),
                         'Opening balance migrated from Products.Quantity', NULL, GETUTCDATE()
                  FROM dbo.Products p
                  WHERE p.Quantity <> 0
                    AND NOT EXISTS (
                        SELECT 1 FROM dbo.StockMovements m
                        WHERE m.ProductId = p.Id AND m.ReferenceType = 'Migration');");
        }
        
        public override void Down()
        {
            Sql("DELETE FROM dbo.StockMovements WHERE ReferenceType = 'Migration' AND Reason = 'Opening balance migrated from Products.Quantity';");
        }
    }
}
