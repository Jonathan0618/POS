/*
    Opt-in demo inventory seed for SQL Server.

    Run this only against a disposable development database or a restored backup:
      1. Change the query window to that database (do not use POSV1).
      2. Review the generated identities and safeguards below.
      3. Set @ApplyDemoInventory to 1 and execute the whole script once.
      4. Run scripts/verify-demo-inventory.sql, then rerun this script and verify again.

    The script never deletes rows and only reactivates records bearing the deterministic
    DEMO identities. Existing rows that collide with those identities cause a rollback.
*/

SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ApplyDemoInventory bit = 0;

IF @ApplyDemoInventory <> 1
    THROW 51100, 'Demo inventory seed is disabled. Use a disposable/backup database and set @ApplyDemoInventory to 1.', 1;

IF DB_NAME() = N'POSV1'
    THROW 51101, 'Refusing to seed POSV1. Restore or create a disposable database under a different name.', 1;

IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Products', N'U') IS NULL
   OR OBJECT_ID(N'dbo.InventoryBalances', N'U') IS NULL
   OR OBJECT_ID(N'dbo.StockMovements', N'U') IS NULL
    THROW 51102, 'Required inventory tables are missing. Apply the repository migrations first.', 1;

IF COL_LENGTH(N'dbo.Categories', N'IsActive') IS NULL
   OR COL_LENGTH(N'dbo.Products', N'Sku') IS NULL
   OR COL_LENGTH(N'dbo.Products', N'Unit') IS NULL
   OR COL_LENGTH(N'dbo.Products', N'BuyingThreshold') IS NULL
   OR COL_LENGTH(N'dbo.InventoryBalances', N'QuantityOnHand') IS NULL
   OR COL_LENGTH(N'dbo.StockMovements', N'MovementType') IS NULL
    THROW 51103, 'The inventory schema is older than this script expects. Apply all repository migrations first.', 1;

CREATE TABLE #ExpectedCategories
(
    Ordinal int NOT NULL PRIMARY KEY,
    Name nvarchar(100) NOT NULL UNIQUE,
    ProductStem nvarchar(40) NOT NULL,
    Unit nvarchar(30) NOT NULL
);

INSERT #ExpectedCategories (Ordinal, Name, ProductStem, Unit)
VALUES
    (1,  N'Beverages',              N'Beverage',       N'bottle'),
    (2,  N'Packaged Snacks',        N'Snack',          N'bag'),
    (3,  N'Bakery',                 N'Bakery Item',    N'piece'),
    (4,  N'Dairy',                  N'Dairy Item',     N'carton'),
    (5,  N'Fresh Produce',          N'Fresh Produce',  N'piece'),
    (6,  N'Pantry',                 N'Pantry Staple',  N'pack'),
    (7,  N'Frozen Foods',           N'Frozen Food',    N'pack'),
    (8,  N'Household',              N'Household Item', N'piece'),
    (9,  N'Personal Care',          N'Care Product',   N'piece'),
    (10, N'Stationery',             N'Stationery',     N'piece'),
    (11, N'Canned Goods',           N'Canned Good',    N'can'),
    (12, N'Condiments',             N'Condiment',      N'jar'),
    (13, N'Breakfast',              N'Breakfast Item', N'box'),
    (14, N'Confectionery',          N'Confectionery',  N'pack'),
    (15, N'Pet Supplies',           N'Pet Supply',     N'pack'),
    (16, N'Health Products',        N'Health Product', N'box');

CREATE TABLE #ExpectedProducts
(
    SequenceNumber int NOT NULL PRIMARY KEY,
    Sku nvarchar(50) NOT NULL UNIQUE,
    Barcode nvarchar(50) NOT NULL UNIQUE,
    Name nvarchar(100) NOT NULL,
    Description nvarchar(500) NOT NULL,
    CategoryName nvarchar(100) NOT NULL,
    CategoryId int NOT NULL,
    Price decimal(18, 2) NOT NULL,
    CostPrice decimal(18, 2) NOT NULL,
    Unit nvarchar(30) NOT NULL,
    BuyingThreshold int NOT NULL,
    Quantity int NOT NULL
);

SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRANSACTION;

BEGIN TRY
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'POS.DemoInventorySeed.v1',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;

    IF @LockResult < 0
        THROW 51104, 'Could not acquire the demo inventory seed application lock.', 1;

    IF EXISTS
    (
        SELECT ec.Name
        FROM #ExpectedCategories ec
        JOIN dbo.Categories c WITH (UPDLOCK, HOLDLOCK)
          ON LTRIM(RTRIM(c.Name)) COLLATE Latin1_General_100_CI_AS = ec.Name COLLATE Latin1_General_100_CI_AS
        GROUP BY ec.Name
        HAVING COUNT_BIG(*) > 1
    )
        THROW 51105, 'A deterministic DEMO category name resolves to multiple rows.', 1;

    INSERT dbo.Categories (Name, Description, IsActive)
    SELECT ec.Name, N'Deterministic demo inventory category.', 1
    FROM #ExpectedCategories ec
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Categories c WITH (UPDLOCK, HOLDLOCK)
        WHERE LTRIM(RTRIM(c.Name)) COLLATE Latin1_General_100_CI_AS = ec.Name COLLATE Latin1_General_100_CI_AS
    );

    -- These names are reserved for this seed, so reactivation does not affect unrelated categories.
    UPDATE c
       SET IsActive = 1
    FROM dbo.Categories c
    JOIN #ExpectedCategories ec
      ON LTRIM(RTRIM(c.Name)) COLLATE Latin1_General_100_CI_AS = ec.Name COLLATE Latin1_General_100_CI_AS
    WHERE c.IsActive = 0;

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
               5 + (n % 21) AS BuyingThreshold,
               CONVERT(decimal(18, 2), 2.50 + ((n * 137) % 9500) / 100.0) AS Price,
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
    INSERT #ExpectedProducts
        (SequenceNumber, Sku, Barcode, Name, Description, CategoryName, CategoryId,
         Price, CostPrice, Unit, BuyingThreshold, Quantity)
    SELECT pv.n,
           pv.Sku,
           pv.BarcodeBase + CONVERT(nvarchar(1), pv.CheckDigit),
           ec.ProductStem + N' ' + RIGHT(N'000' + CONVERT(nvarchar(3), pv.n), 3),
           N'Deterministic demo product ' + pv.Sku + N'.',
           ec.Name,
           c.Id,
           pv.Price,
           CONVERT(decimal(18, 2), ROUND(pv.Price * (55 + (pv.n % 21)) / 100.0, 2)),
           ec.Unit,
           pv.BuyingThreshold,
           CASE pv.n % 5
               WHEN 0 THEN 0
               WHEN 1 THEN pv.BuyingThreshold - 2
               WHEN 2 THEN pv.BuyingThreshold
               WHEN 3 THEN pv.BuyingThreshold + 7
               ELSE pv.BuyingThreshold + 20
           END
    FROM ProductValues pv
    JOIN #ExpectedCategories ec ON ec.Ordinal = pv.CategoryOrdinal
    JOIN dbo.Categories c
      ON LTRIM(RTRIM(c.Name)) COLLATE Latin1_General_100_CI_AS = ec.Name COLLATE Latin1_General_100_CI_AS
    OPTION (MAXRECURSION 200);

    IF (SELECT COUNT_BIG(*) FROM #ExpectedProducts) <> 200
        THROW 51106, 'The deterministic product set did not generate exactly 200 rows.', 1;

    IF EXISTS
    (
        SELECT ep.Sku
        FROM #ExpectedProducts ep
        JOIN dbo.Products p WITH (UPDLOCK, HOLDLOCK)
          ON UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS = ep.Sku COLLATE Latin1_General_100_CI_AS
        GROUP BY ep.Sku
        HAVING COUNT_BIG(*) > 1
    )
        THROW 51107, 'A deterministic DEMO SKU resolves to multiple product rows.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM #ExpectedProducts ep
        JOIN dbo.Products p WITH (UPDLOCK, HOLDLOCK)
          ON UPPER(LTRIM(RTRIM(p.Barcode))) COLLATE Latin1_General_100_CI_AS = ep.Barcode COLLATE Latin1_General_100_CI_AS
        WHERE p.Sku IS NULL
           OR UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS <> ep.Sku COLLATE Latin1_General_100_CI_AS
    )
        THROW 51108, 'A deterministic DEMO barcode is already assigned to another product.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM #ExpectedProducts ep
        JOIN dbo.Products p WITH (UPDLOCK, HOLDLOCK)
          ON UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS = ep.Sku COLLATE Latin1_General_100_CI_AS
        WHERE ISNULL(UPPER(LTRIM(RTRIM(p.Barcode))), N'') COLLATE Latin1_General_100_CI_AS <> ep.Barcode COLLATE Latin1_General_100_CI_AS
           OR p.Name COLLATE Latin1_General_100_CI_AS <> ep.Name COLLATE Latin1_General_100_CI_AS
           OR p.CategoryId <> ep.CategoryId
    )
        THROW 51109, 'A deterministic DEMO SKU exists with a conflicting barcode, name, or category.', 1;

    -- A reused product must be a complete prior seed row with a consistent balance and ledger.
    IF EXISTS
    (
        SELECT 1
        FROM #ExpectedProducts ep
        JOIN dbo.Products p
          ON UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS = ep.Sku COLLATE Latin1_General_100_CI_AS
        OUTER APPLY
        (
            SELECT COUNT_BIG(*) AS BalanceCount, MAX(b.QuantityOnHand) AS QuantityOnHand
            FROM dbo.InventoryBalances b WITH (UPDLOCK, HOLDLOCK)
            WHERE b.ProductId = p.Id
        ) balance
        OUTER APPLY
        (
            SELECT COUNT_BIG(*) AS SeedMovementCount
            FROM dbo.StockMovements m WITH (UPDLOCK, HOLDLOCK)
            WHERE m.ProductId = p.Id
              AND m.MovementType = 0
              AND m.ReferenceType = N'DemoInventorySeed'
              AND m.ReferenceId = ep.Sku
        ) opening
        OUTER APPLY
        (
            SELECT COALESCE(SUM(CONVERT(bigint, m.QuantityDelta)), 0) AS LedgerQuantity
            FROM dbo.StockMovements m WITH (UPDLOCK, HOLDLOCK)
            WHERE m.ProductId = p.Id
        ) ledger
        WHERE balance.BalanceCount <> 1
           OR opening.SeedMovementCount <> 1
           OR p.Quantity <> balance.QuantityOnHand
           OR CONVERT(bigint, p.Quantity) <> ledger.LedgerQuantity
    )
        THROW 51110, 'An existing DEMO product is not a complete, internally consistent prior seed row.', 1;

    DECLARE @InsertedProducts TABLE (ProductId int NOT NULL PRIMARY KEY, Sku nvarchar(50) NOT NULL UNIQUE);

    INSERT dbo.Products
        (Name, Sku, Description, Price, CostPrice, CategoryId, Quantity, Barcode,
         Unit, ExpiryDate, IsActive, BuyingThreshold)
    OUTPUT inserted.Id, inserted.Sku INTO @InsertedProducts (ProductId, Sku)
    SELECT ep.Name, ep.Sku, ep.Description, ep.Price, ep.CostPrice, ep.CategoryId, ep.Quantity,
           ep.Barcode, ep.Unit, NULL, 1, ep.BuyingThreshold
    FROM #ExpectedProducts ep
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Products p WITH (UPDLOCK, HOLDLOCK)
        WHERE UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS = ep.Sku COLLATE Latin1_General_100_CI_AS
    );

    INSERT dbo.InventoryBalances (ProductId, QuantityOnHand)
    SELECT ip.ProductId, ep.Quantity
    FROM @InsertedProducts ip
    JOIN #ExpectedProducts ep ON ep.Sku = ip.Sku;

    INSERT dbo.StockMovements
        (ProductId, MovementType, QuantityDelta, ReferenceType, ReferenceId, Reason, UserId, CreatedUtc)
    SELECT ip.ProductId, 0, ep.Quantity, N'DemoInventorySeed', ep.Sku,
           N'Deterministic demo opening balance.', NULL, SYSUTCDATETIME()
    FROM @InsertedProducts ip
    JOIN #ExpectedProducts ep ON ep.Sku = ip.Sku;

    -- These SKUs are reserved for this seed; no other existing products are changed.
    UPDATE p
       SET IsActive = 1
    FROM dbo.Products p
    JOIN #ExpectedProducts ep
      ON UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS = ep.Sku COLLATE Latin1_General_100_CI_AS
    WHERE p.IsActive = 0;

    IF (SELECT COUNT_BIG(*)
        FROM dbo.Products p
        JOIN #ExpectedProducts ep
          ON UPPER(LTRIM(RTRIM(p.Sku))) COLLATE Latin1_General_100_CI_AS = ep.Sku COLLATE Latin1_General_100_CI_AS
        WHERE p.IsActive = 1) <> 200
        THROW 51111, 'The seed did not produce exactly 200 active deterministic DEMO products.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM @InsertedProducts ip
        JOIN dbo.Products p ON p.Id = ip.ProductId
        JOIN dbo.InventoryBalances b ON b.ProductId = p.Id
        OUTER APPLY
        (
            SELECT COALESCE(SUM(CONVERT(bigint, m.QuantityDelta)), 0) AS LedgerQuantity
            FROM dbo.StockMovements m
            WHERE m.ProductId = p.Id
        ) ledger
        WHERE p.Quantity <> b.QuantityOnHand
           OR CONVERT(bigint, p.Quantity) <> ledger.LedgerQuantity
    )
        THROW 51112, 'A newly seeded product has inconsistent product, balance, and ledger quantities.', 1;

    DECLARE @InsertedCount int = (SELECT COUNT(*) FROM @InsertedProducts);
    COMMIT TRANSACTION;

    SELECT @InsertedCount AS ProductsInserted,
           200 - @InsertedCount AS ProductsReused,
           200 AS ActiveDemoProducts;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
