/*
    Read-only verification for scripts/populate-demo-inventory.sql.

    Run against the disposable development database or restored backup that was seeded.
    This script does not modify application data. It returns summary/detail result sets and
    throws if the 16 deterministic categories and 200 active DEMO products are not complete,
    uniquely identifiable, rerunnable, and consistent across product/balance/ledger storage.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Products', N'U') IS NULL
   OR OBJECT_ID(N'dbo.InventoryBalances', N'U') IS NULL
   OR OBJECT_ID(N'dbo.StockMovements', N'U') IS NULL
    THROW 51200, 'Required inventory tables are missing. Apply the repository migrations first.', 1;

CREATE TABLE #ExpectedCategories
(
    Ordinal int NOT NULL PRIMARY KEY,
    Name nvarchar(100) NOT NULL UNIQUE,
    ProductStem nvarchar(40) NOT NULL
);

INSERT #ExpectedCategories (Ordinal, Name, ProductStem)
VALUES
    (1, N'Beverages', N'Beverage'), (2, N'Packaged Snacks', N'Snack'),
    (3, N'Bakery', N'Bakery Item'), (4, N'Dairy', N'Dairy Item'),
    (5, N'Fresh Produce', N'Fresh Produce'), (6, N'Pantry', N'Pantry Staple'),
    (7, N'Frozen Foods', N'Frozen Food'), (8, N'Household', N'Household Item'),
    (9, N'Personal Care', N'Care Product'), (10, N'Stationery', N'Stationery'),
    (11, N'Canned Goods', N'Canned Good'), (12, N'Condiments', N'Condiment'),
    (13, N'Breakfast', N'Breakfast Item'), (14, N'Confectionery', N'Confectionery'),
    (15, N'Pet Supplies', N'Pet Supply'), (16, N'Health Products', N'Health Product');

CREATE TABLE #ExpectedProducts
(
    SequenceNumber int NOT NULL PRIMARY KEY,
    Sku nvarchar(50) NOT NULL UNIQUE,
    Barcode nvarchar(50) NOT NULL UNIQUE,
    Name nvarchar(100) NOT NULL,
    CategoryName nvarchar(100) NOT NULL
);

;WITH Numbers AS
(
    SELECT 1 AS n
    UNION ALL
    SELECT n + 1 FROM Numbers WHERE n < 200
), ProductBase AS
(
    SELECT n,
           N'DEMO-' + RIGHT(N'0000' + CONVERT(nvarchar(4), n), 4) AS Sku,
           N'290000000' + RIGHT(N'000' + CONVERT(nvarchar(3), n), 3) AS BarcodeBase,
           ((n - 1) % 16) + 1 AS CategoryOrdinal
    FROM Numbers
), ProductValues AS
(
    SELECT pb.*,
           (10 -
             ((CONVERT(int, SUBSTRING(pb.BarcodeBase, 1, 1)) +
               CONVERT(int, SUBSTRING(pb.BarcodeBase, 3, 1)) +
               CONVERT(int, SUBSTRING(pb.BarcodeBase, 5, 1)) +
               CONVERT(int, SUBSTRING(pb.BarcodeBase, 7, 1)) +
               CONVERT(int, SUBSTRING(pb.BarcodeBase, 9, 1)) +
               CONVERT(int, SUBSTRING(pb.BarcodeBase, 11, 1)) +
               3 * (CONVERT(int, SUBSTRING(pb.BarcodeBase, 2, 1)) +
                    CONVERT(int, SUBSTRING(pb.BarcodeBase, 4, 1)) +
                    CONVERT(int, SUBSTRING(pb.BarcodeBase, 6, 1)) +
                    CONVERT(int, SUBSTRING(pb.BarcodeBase, 8, 1)) +
                    CONVERT(int, SUBSTRING(pb.BarcodeBase, 10, 1)) +
                    CONVERT(int, SUBSTRING(pb.BarcodeBase, 12, 1)))) % 10)) % 10 AS CheckDigit
    FROM ProductBase pb
)
INSERT #ExpectedProducts (SequenceNumber, Sku, Barcode, Name, CategoryName)
SELECT pv.n, pv.Sku, pv.BarcodeBase + CONVERT(nvarchar(1), pv.CheckDigit),
       ec.ProductStem + N' ' + RIGHT(N'000' + CONVERT(nvarchar(3), pv.n), 3), ec.Name
FROM ProductValues pv
JOIN #ExpectedCategories ec ON ec.Ordinal = pv.CategoryOrdinal
OPTION (MAXRECURSION 200);

CREATE TABLE #Issues
(
    IssueType nvarchar(60) NOT NULL,
    Identifier nvarchar(100) NOT NULL,
    Details nvarchar(4000) NOT NULL
);

INSERT #Issues
SELECT N'Category identity/count', ec.Name,
       N'Expected exactly one active category; found ' + CONVERT(nvarchar(20), COUNT(c.Id)) +
       N' total and ' + CONVERT(nvarchar(20), COALESCE(SUM(CASE WHEN c.IsActive = 1 THEN 1 ELSE 0 END), 0)) + N' active.'
FROM #ExpectedCategories ec
LEFT JOIN dbo.Categories c
  ON LTRIM(RTRIM(c.Name)) COLLATE Latin1_General_100_CI_AS = ec.Name COLLATE Latin1_General_100_CI_AS
GROUP BY ec.Name
HAVING COUNT(c.Id) <> 1 OR COALESCE(SUM(CASE WHEN c.IsActive = 1 THEN 1 ELSE 0 END), 0) <> 1;

INSERT #Issues
SELECT N'Product identity/count', ep.Sku,
       N'Expected exactly one active product with the deterministic SKU; found ' + CONVERT(nvarchar(20), COUNT(p.Id)) +
       N' total and ' + CONVERT(nvarchar(20), COALESCE(SUM(CASE WHEN p.IsActive = 1 THEN 1 ELSE 0 END), 0)) + N' active.'
FROM #ExpectedProducts ep
LEFT JOIN dbo.Products p
  ON UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS = ep.Sku COLLATE Latin1_General_100_CI_AS
GROUP BY ep.Sku
HAVING COUNT(p.Id) <> 1 OR COALESCE(SUM(CASE WHEN p.IsActive = 1 THEN 1 ELSE 0 END), 0) <> 1;

INSERT #Issues
SELECT N'Product identifier mismatch', ep.Sku,
       N'Barcode, name, or category does not match the deterministic identity.'
FROM #ExpectedProducts ep
JOIN dbo.Products p
  ON UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS = ep.Sku COLLATE Latin1_General_100_CI_AS
LEFT JOIN dbo.Categories c ON c.Id = p.CategoryId
WHERE ISNULL(UPPER(LTRIM(RTRIM(p.Barcode))), N'') COLLATE Latin1_General_100_CI_AS <> ep.Barcode COLLATE Latin1_General_100_CI_AS
   OR p.Name COLLATE Latin1_General_100_CI_AS <> ep.Name COLLATE Latin1_General_100_CI_AS
   OR c.Name IS NULL
   OR LTRIM(RTRIM(c.Name)) COLLATE Latin1_General_100_CI_AS <> ep.CategoryName COLLATE Latin1_General_100_CI_AS;

INSERT #Issues
SELECT N'Barcode collision', ep.Barcode,
       N'The deterministic barcode is assigned to a product other than ' + ep.Sku + N'.'
FROM #ExpectedProducts ep
JOIN dbo.Products p
  ON UPPER(LTRIM(RTRIM(p.Barcode))) COLLATE Latin1_General_100_CI_AS = ep.Barcode COLLATE Latin1_General_100_CI_AS
WHERE p.Sku IS NULL
   OR UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS <> ep.Sku COLLATE Latin1_General_100_CI_AS;

INSERT #Issues
SELECT N'Invalid EAN-13', ep.Sku, N'The deterministic barcode does not pass the EAN-13 checksum.'
FROM #ExpectedProducts ep
WHERE LEN(ep.Barcode) <> 13
   OR
   (
       CONVERT(int, SUBSTRING(ep.Barcode, 13, 1)) <>
       (10 -
         ((CONVERT(int, SUBSTRING(ep.Barcode, 1, 1)) + CONVERT(int, SUBSTRING(ep.Barcode, 3, 1)) +
           CONVERT(int, SUBSTRING(ep.Barcode, 5, 1)) + CONVERT(int, SUBSTRING(ep.Barcode, 7, 1)) +
           CONVERT(int, SUBSTRING(ep.Barcode, 9, 1)) + CONVERT(int, SUBSTRING(ep.Barcode, 11, 1)) +
           3 * (CONVERT(int, SUBSTRING(ep.Barcode, 2, 1)) + CONVERT(int, SUBSTRING(ep.Barcode, 4, 1)) +
                CONVERT(int, SUBSTRING(ep.Barcode, 6, 1)) + CONVERT(int, SUBSTRING(ep.Barcode, 8, 1)) +
                CONVERT(int, SUBSTRING(ep.Barcode, 10, 1)) + CONVERT(int, SUBSTRING(ep.Barcode, 12, 1)))) % 10)) % 10
   );

INSERT #Issues
SELECT N'Inventory balance count', ep.Sku,
       N'Expected exactly one inventory balance; found ' + CONVERT(nvarchar(20), COUNT(b.Id)) + N'.'
FROM #ExpectedProducts ep
JOIN dbo.Products p
  ON UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS = ep.Sku COLLATE Latin1_General_100_CI_AS
LEFT JOIN dbo.InventoryBalances b ON b.ProductId = p.Id
GROUP BY ep.Sku
HAVING COUNT(b.Id) <> 1;

INSERT #Issues
SELECT N'Opening movement count', ep.Sku,
       N'Expected exactly one deterministic OpeningBalance movement; found ' + CONVERT(nvarchar(20), COUNT(m.Id)) + N'.'
FROM #ExpectedProducts ep
JOIN dbo.Products p
  ON UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS = ep.Sku COLLATE Latin1_General_100_CI_AS
LEFT JOIN dbo.StockMovements m
  ON m.ProductId = p.Id
 AND m.MovementType = 0
 AND m.ReferenceType = N'DemoInventorySeed'
 AND m.ReferenceId = ep.Sku
GROUP BY ep.Sku
HAVING COUNT(m.Id) <> 1;

INSERT #Issues
SELECT N'Quantity/ledger mismatch', ep.Sku,
       N'Product quantity, balance, and ledger sum must agree. Product=' + CONVERT(nvarchar(30), p.Quantity) +
       N', balance=' + COALESCE(CONVERT(nvarchar(30), b.QuantityOnHand), N'<missing>') +
       N', ledger=' + CONVERT(nvarchar(30), ledger.LedgerQuantity) + N'.'
FROM #ExpectedProducts ep
JOIN dbo.Products p
  ON UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS = ep.Sku COLLATE Latin1_General_100_CI_AS
LEFT JOIN dbo.InventoryBalances b ON b.ProductId = p.Id
OUTER APPLY
(
    SELECT COALESCE(SUM(CONVERT(bigint, m.QuantityDelta)), 0) AS LedgerQuantity
    FROM dbo.StockMovements m
    WHERE m.ProductId = p.Id
) ledger
WHERE b.Id IS NULL
   OR p.Quantity <> b.QuantityOnHand
   OR CONVERT(bigint, p.Quantity) <> ledger.LedgerQuantity;

DECLARE @ActiveProductCount bigint =
(
    SELECT COUNT_BIG(*)
    FROM #ExpectedProducts ep
    JOIN dbo.Products p
      ON UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS = ep.Sku COLLATE Latin1_General_100_CI_AS
    WHERE p.IsActive = 1
);

IF @ActiveProductCount < 200
    INSERT #Issues VALUES
        (N'Active product total', N'DEMO', N'Expected at least 200 active deterministic DEMO products; found ' + CONVERT(nvarchar(20), @ActiveProductCount) + N'.');

SELECT
    (SELECT COUNT_BIG(*) FROM #ExpectedCategories) AS ExpectedCategories,
    (SELECT COUNT_BIG(*) FROM #ExpectedProducts) AS ExpectedProducts,
    @ActiveProductCount AS ActiveDemoProducts,
    (SELECT COUNT_BIG(*) FROM #Issues) AS IssueCount;

SELECT IssueType, Identifier, Details
FROM #Issues
ORDER BY IssueType, Identifier;

IF EXISTS (SELECT 1 FROM #Issues)
    THROW 51201, 'Demo inventory verification failed. Review the issue result set.', 1;

SELECT N'PASS' AS VerificationResult,
       N'The seed is complete, consistent, and a rerun will reuse all deterministic identities.' AS Details;
