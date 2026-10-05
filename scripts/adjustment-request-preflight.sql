-- Read-only. Run before AddAdjustmentRequestUniqueness.
-- StockMovementType.Adjustment = 4. No rows means no duplicate request IDs
-- existed at query time; the migration checks again under a table write lock.
-- Preserve all movement facts. Investigate duplicates rather than deleting them.
WITH AdjustmentRequests AS
(
    SELECT Id, ProductId, ReferenceId, QuantityDelta, Reason, UserId, CreatedUtc,
        COUNT_BIG(*) OVER (PARTITION BY ReferenceId) AS Matches
    FROM dbo.StockMovements
    WHERE MovementType = 4 AND ReferenceId IS NOT NULL
)
SELECT Id, ProductId, ReferenceId, QuantityDelta, Reason, UserId, CreatedUtc, Matches
FROM AdjustmentRequests
WHERE Matches > 1
ORDER BY ReferenceId, Id;
