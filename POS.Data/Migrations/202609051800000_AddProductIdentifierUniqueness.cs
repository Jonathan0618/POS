namespace POS.Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddProductIdentifierUniqueness : DbMigration
    {
        public override void Up()
        {
            // SQL-only computed keys preserve the EF model and stored identifiers.
            // The migration transaction holds this lock through index creation.
            Sql(@"
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
DECLARE @productCount bigint;
SELECT @productCount = COUNT_BIG(*) FROM dbo.Products WITH (TABLOCKX, HOLDLOCK);

IF EXISTS (SELECT 1 FROM dbo.Products WHERE LEN(LTRIM(RTRIM(Sku))) > 50)
    THROW 51000, 'Product SKU exceeds 50 characters. Run the product identifier preflight report and correct the source records before retrying.', 1;

IF EXISTS (
    SELECT CONVERT(nvarchar(50), UPPER(LTRIM(RTRIM(Sku)))) COLLATE Latin1_General_100_CI_AS
    FROM dbo.Products WHERE Sku IS NOT NULL AND Sku <> N''
    GROUP BY CONVERT(nvarchar(50), UPPER(LTRIM(RTRIM(Sku)))) COLLATE Latin1_General_100_CI_AS
    HAVING COUNT_BIG(*) > 1)
    THROW 51001, 'Duplicate product SKUs exist. Run the product identifier preflight report and resolve duplicates before retrying.', 1;

IF EXISTS (
    SELECT CONVERT(nvarchar(50), UPPER(LTRIM(RTRIM(Barcode)))) COLLATE Latin1_General_100_CI_AS
    FROM dbo.Products WHERE Barcode IS NOT NULL AND Barcode <> N''
    GROUP BY CONVERT(nvarchar(50), UPPER(LTRIM(RTRIM(Barcode)))) COLLATE Latin1_General_100_CI_AS
    HAVING COUNT_BIG(*) > 1)
    THROW 51002, 'Duplicate product barcodes exist. Run the product identifier preflight report and resolve duplicates before retrying.', 1;

ALTER TABLE dbo.Products ADD CONSTRAINT CK_Products_SkuLength
    CHECK (Sku IS NULL OR LEN(LTRIM(RTRIM(Sku))) <= 50);
ALTER TABLE dbo.Products ADD NormalizedSku AS
    (CONVERT(nvarchar(50), UPPER(LTRIM(RTRIM(Sku)))) COLLATE Latin1_General_100_CI_AS) PERSISTED;
ALTER TABLE dbo.Products ADD NormalizedBarcode AS
    (CONVERT(nvarchar(50), UPPER(LTRIM(RTRIM(Barcode)))) COLLATE Latin1_General_100_CI_AS) PERSISTED;

CREATE UNIQUE INDEX UX_Products_NormalizedSku ON dbo.Products(NormalizedSku)
    WHERE Sku IS NOT NULL AND Sku <> N'';
CREATE UNIQUE INDEX UX_Products_NormalizedBarcode ON dbo.Products(NormalizedBarcode)
    WHERE Barcode IS NOT NULL AND Barcode <> N'';
");
        }

        public override void Down()
        {
            Sql(@"
DROP INDEX UX_Products_NormalizedBarcode ON dbo.Products;
DROP INDEX UX_Products_NormalizedSku ON dbo.Products;
ALTER TABLE dbo.Products DROP COLUMN NormalizedBarcode, NormalizedSku;
ALTER TABLE dbo.Products DROP CONSTRAINT CK_Products_SkuLength;
");
        }
    }
}
