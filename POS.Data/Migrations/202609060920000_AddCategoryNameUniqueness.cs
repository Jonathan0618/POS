namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddCategoryNameUniqueness : DbMigration
    {
        public override void Up()
        {
            // Preserve existing names. Conflicts require deliberate source-data cleanup.
            Sql(@"
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
DECLARE @categoryCount bigint;
SELECT @categoryCount = COUNT_BIG(*) FROM dbo.Categories WITH (TABLOCKX, HOLDLOCK);

IF EXISTS (
    SELECT LTRIM(RTRIM(Name)) COLLATE Latin1_General_100_CI_AS
    FROM dbo.Categories WHERE Name IS NOT NULL AND Name <> N''
    GROUP BY LTRIM(RTRIM(Name)) COLLATE Latin1_General_100_CI_AS
    HAVING COUNT_BIG(*) > 1)
    THROW 51003, 'Duplicate category names exist. Run the category name preflight report and resolve conflicts before retrying.', 1;

ALTER TABLE dbo.Categories ADD NormalizedName AS
    (LTRIM(RTRIM(Name)) COLLATE Latin1_General_100_CI_AS) PERSISTED;
CREATE UNIQUE INDEX UX_Categories_NormalizedName ON dbo.Categories(NormalizedName)
    WHERE Name IS NOT NULL AND Name <> N'';
");
        }

        public override void Down()
        {
            Sql(@"
DROP INDEX UX_Categories_NormalizedName ON dbo.Categories;
ALTER TABLE dbo.Categories DROP COLUMN NormalizedName;
");
        }
    }
}
