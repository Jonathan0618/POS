namespace POS.Models.Operations
{
    public sealed class StockAdjustmentDTO
    {
        // Generate once per adjustment and retain unchanged for retries.
        public System.Guid RequestId { get; set; }
        public int ProductId { get; set; }
        public int ExpectedQuantityOnHand { get; set; }
        public int QuantityDelta { get; set; }
        public string Reason { get; set; }
    }
}
