namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddAdjustmentRequestUniqueness : DbMigration
    {
        public override void Up()
        {
            // StockMovementType.Adjustment = 4. Existing facts are never rewritten.
            Sql(@"
DECLARE @movementCount bigint;
SELECT @movementCount = COUNT_BIG(*) FROM dbo.StockMovements WITH (TABLOCKX, HOLDLOCK);

IF EXISTS (
    SELECT ReferenceId FROM dbo.StockMovements
    WHERE MovementType = 4 AND ReferenceId IS NOT NULL
    GROUP BY ReferenceId HAVING COUNT_BIG(*) > 1)
    THROW 51004, 'Duplicate inventory adjustment request IDs exist. Run the adjustment request preflight report and review the original movements before retrying.', 1;

CREATE UNIQUE INDEX UX_StockMovements_AdjustmentRequestId
ON dbo.StockMovements(ReferenceId)
WHERE MovementType = 4 AND ReferenceId IS NOT NULL;
");
        }

        public override void Down()
        {
            DropIndex("dbo.StockMovements", "UX_StockMovements_AdjustmentRequestId");
        }
    }
}
