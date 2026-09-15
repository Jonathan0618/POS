namespace POS.Common.Enumerations
{
    public enum DocumentStatus { Draft, Pending, Posted, Completed, Cancelled, Voided, PartiallyReturned, Returned, Held }
    public enum StockMovementType { OpeningBalance, PurchaseReceipt, Sale, SaleReturn, Adjustment, StockCount, PurchaseReturn }
    public enum TenderType { Cash, Card, EWallet, StoreCredit }
    public enum PaymentStatus { Pending, Completed, Reversed, Refunded, Failed }
    public enum CashMovementType { Opening, CashIn, CashOut, Closing }
    public enum ShiftStatus { Open, Closed, Reopened }
    public enum ReturnDisposition { Restock, Damaged, Quarantine }
}
