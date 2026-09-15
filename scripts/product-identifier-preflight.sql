-- Read-only. Run against the intended POS database before deploying
-- AddProductIdentifierUniqueness. No results means no conflicts were found
-- at the time of this query; the migration checks again under a write lock.
-- Empty/space-only legacy identifiers are excluded. Inactive products count.
-- Resolve conflicts by editing the original product records; do not delete
-- products or rewrite completed sale snapshots to satisfy these indexes.
SELECT Id, Name, Sku, LEN(LTRIM(RTRIM(Sku))) AS SkuLength
FROM dbo.Products WHERE LEN(LTRIM(RTRIM(Sku))) > 50 ORDER BY Id;

WITH Identifiers AS
(
    SELECT Id, Name, IsActive, N'SKU' AS IdentifierType, Sku AS OriginalValue,
        CONVERT(nvarchar(50), UPPER(LTRIM(RTRIM(Sku)))) COLLATE Latin1_General_100_CI_AS AS NormalizedValue
    FROM dbo.Products WHERE Sku IS NOT NULL AND Sku <> N''
    UNION ALL
    SELECT Id, Name, IsActive, N'Barcode', Barcode,
        CONVERT(nvarchar(50), UPPER(LTRIM(RTRIM(Barcode)))) COLLATE Latin1_General_100_CI_AS
    FROM dbo.Products WHERE Barcode IS NOT NULL AND Barcode <> N''
), Conflicts AS
(
    SELECT *, COUNT_BIG(*) OVER (PARTITION BY IdentifierType, NormalizedValue) AS Matches
    FROM Identifiers
)
SELECT Id, Name, IsActive, IdentifierType, OriginalValue, NormalizedValue, Matches
FROM Conflicts WHERE Matches > 1
ORDER BY IdentifierType, NormalizedValue, Id;
