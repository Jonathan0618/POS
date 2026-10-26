-- Read-only. Run against the intended POS database before deploying
-- AddCategoryNameUniqueness. The migration rechecks under a table write lock.
-- Case-insensitive, accent-sensitive; trims ordinary surrounding SQL spaces.
-- Active and inactive categories share names. Null/empty/space-only legacy
-- names are excluded from uniqueness, but reported separately for review.
-- Resolve duplicates through deliberate category renaming; preserve products
-- and their category references. This report does not modify data.
SELECT Id, Name
FROM dbo.Categories
WHERE Name IS NULL OR Name = N''
ORDER BY Id;

WITH Names AS
(
    SELECT Id, Name,
        LTRIM(RTRIM(Name)) COLLATE Latin1_General_100_CI_AS AS NormalizedName
    FROM dbo.Categories WHERE Name IS NOT NULL AND Name <> N''
), Conflicts AS
(
    SELECT *, COUNT_BIG(*) OVER (PARTITION BY NormalizedName) AS Matches
    FROM Names
)
SELECT Id, Name, NormalizedName, Matches
FROM Conflicts WHERE Matches > 1
ORDER BY NormalizedName, Id;
