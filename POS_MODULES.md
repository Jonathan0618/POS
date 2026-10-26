# POS System Module Roadmap

**Prepared:** 2026-09-04  
**Based on:** the current `POS` solution and its existing code

## Status legend

| Status | Meaning |
|---|---|
| Implemented | A usable foundation exists in the current project |
| Partial | Some entities, services, or screens exist, but the workflow is incomplete |
| Missing | No meaningful implementation was found |

## Recommended module map

```text
Foundation
├── Authentication and authorization
├── Users, roles, and permissions
├── Store and POS settings
└── Audit and activity logs

Catalog and inventory
├── Products and categories
├── Units, barcodes, and pricing
├── Stock management
├── Suppliers
├── Purchasing and receiving
└── Stock counts and adjustments

Selling
├── Register and cart
├── Payments
├── Receipts
├── Returns and refunds
├── Customers
├── Discounts and promotions
└── Cashier shifts and cash drawer

Management
├── Dashboard
├── Sales reports
├── Inventory reports
├── Financial summaries
└── Backup, restore, and maintenance
```

## Essential modules

These modules are needed for a reliable, production-usable POS.

### 1. Authentication and session management

**Current status:** Implemented, with improvements needed

Existing code provides sign-in, ASP.NET Identity, remembered credentials, and current-user state.

Required capabilities:

- Sign in and sign out
- Secure password hashing through Identity
- Remember username/session according to policy
- Lockout after repeated failed attempts
- Password change and administrator password reset
- Disabled/locked employee accounts
- Session timeout or register lock screen
- Clear current-user state on logout
- Safe authentication error handling and logging

### 2. Users, roles, and permissions

**Current status:** Partial to implemented

Existing screens and services manage users, roles, modules, and claims. UI actions can be protected with authorization attributes.

Required capabilities:

- Employee/user creation, editing, disabling, and deletion policy
- Role creation and assignment
- Permissions by module and action: view, add, edit, and delete
- Service-level authorization in addition to UI visibility
- Permission presets such as Administrator, Manager, Cashier, and Inventory Clerk
- Prevention of deleting the last administrator
- User activity history

### 3. Store and POS configuration

**Current status:** Missing

Required capabilities:

- Store name, address, phone, email, tax identifier, and receipt footer
- Currency and number formatting
- Time zone and business-date rules
- Tax rates and whether prices include tax
- Receipt numbering format
- Default printer and paper size
- Barcode settings
- Low-stock defaults
- Return/refund policy
- Rounding rules
- Optional multi-register identifiers

Do not hard-code VAT in `SalesService`; load the effective tax rule from configuration and store the applied rate and amount on each sale.

### 4. Product catalog

**Current status:** Partial

Existing code provides products, categories, barcode fields, prices, thresholds, and add/list/category screens.

Required capabilities:

- Add, edit, view, deactivate, and search products
- Categories and optional subcategories
- Unique SKU and unique barcode validation
- Selling price and cost price
- Unit of measure
- Tax category
- Product image, optional
- Active/discontinued state
- Price history
- Bulk price updates and CSV import/export, optional for first release
- Product variants, optional if the store sells sizes/colors

Important design decision: use one source of truth for available quantity. The current `Product.Quantity` and `Stock.Quantity` overlap.

### 5. Inventory and stock control

**Current status:** Partial

Existing code lists stock, tracks quantity, and displays low-stock alerts.

Required capabilities:

- Current quantity on hand
- Available, reserved, and damaged quantities if needed
- Stock-in and stock-out transactions
- Manual adjustments with mandatory reason
- Low-stock and out-of-stock alerts
- Reorder level per product
- Stock movement history/ledger
- Physical stock count and variance posting
- Expiry/batch tracking for applicable products
- Prevent negative inventory unless explicitly configured
- Concurrency protection so two cashiers cannot sell the final unit twice
- Inventory valuation using a selected costing rule

A stock ledger is recommended. Each sale, return, purchase receipt, adjustment, and count should create an immutable movement record.

### 6. Suppliers

**Current status:** Missing

Required capabilities:

- Supplier name and contact information
- Tax/business identifier
- Active/inactive status
- Products supplied
- Default cost and lead time
- Supplier purchase history
- Outstanding balance, only if supplier credit is supported

### 7. Purchasing and receiving

**Current status:** Missing

Required capabilities:

- Purchase order creation and approval
- Purchase-order line items
- Draft, ordered, partially received, received, and cancelled states
- Goods receiving against a purchase order
- Direct receiving for simple stores
- Supplier invoice/reference number
- Cost updates
- Partial delivery and back-order handling
- Automatic stock-ledger entries
- Purchase returns
- Purchase history and receiving report

For a small first release, purchase orders can be deferred, but a controlled stock-receiving workflow is essential.

### 8. Sales register and cart

**Current status:** Partial

`SalesService` contains initial cart calculations and sale persistence logic, but a complete cashier/register workflow was not found.

Required capabilities:

- Start a new sale
- Scan barcode or search products
- Add/remove cart items
- Change quantity with stock validation
- Display unit price, line discount, tax, and line total
- Sale-level discount with permission controls
- Suspend/hold and resume a sale
- Select customer, optional for anonymous cash sales
- Calculate subtotal, tax, discount, rounding, and total
- Accept payment and calculate change
- Commit the sale and stock deduction atomically
- Cancel/void a sale with reason and permission
- Reprint receipt with permission and audit record
- Prevent duplicate submission when the cashier clicks twice

Checkout must use one database transaction. Product quantities and the final sale must either all succeed or all roll back.

### 9. Payments and tenders

**Current status:** Missing or only represented by cash fields

Required capabilities:

- Cash payments
- Card payments recorded by reference number
- E-wallet/mobile payments recorded by provider/reference
- Split tender, if required
- Amount tendered and change
- Payment status
- Failed/cancelled payment handling
- Payment reversal connected to a refund
- Overpayment and underpayment validation
- Cashier and register attribution

Never store complete card numbers, CVV values, or payment credentials. A real card integration should use a compliant external payment provider and retain only safe references.

### 10. Receipts and invoices

**Current status:** Partial

The project contains a DevExpress receipt report and report viewer.

Required capabilities:

- Unique sequential receipt number
- Store details and tax identifier
- Sale date/time, cashier, and register
- Item descriptions, quantities, prices, discounts, and tax
- Subtotal, tax, discount, rounding, total, payment, and change
- Print preview and direct printing
- Reprint indicator
- Return/refund references
- PDF export, optional
- Invoice/customer details where legally required

A receipt must be reproducible from stored sale data even if product names, prices, or tax rates change later.

### 11. Returns, refunds, and exchanges

**Current status:** Missing

Required capabilities:

- Find the original sale by receipt number
- Select items and quantities to return
- Validate returnable quantity and return window
- Capture a return reason
- Refund to an allowed tender method
- Restock, quarantine, or mark damaged
- Create reversing stock movements
- Link return/refund to the original sale and payment
- Manager approval above configured limits
- Void versus refund distinction
- Exchange workflow, which may be implemented as a return plus a new sale

Never delete the original completed sale to represent a refund.

### 12. Cashier shifts and cash drawer

**Current status:** Missing

Required capabilities:

- Open shift/register with opening cash
- Cash-in and cash-out entries with reasons
- Expected cash calculation
- Blind or visible cash count at closing
- Cash over/short variance
- Shift close and manager approval
- Z-reading/end-of-day summary
- Prevent sales when no register shift is open, if required
- Optional physical drawer integration

This module is essential when accountability for physical cash matters.

### 13. Customers

**Current status:** Missing

Required capabilities:

- Walk-in customer support without mandatory registration
- Customer name and contact information
- Tax details for invoices
- Purchase history
- Notes and active/inactive state
- Optional loyalty points
- Optional store credit with a proper ledger and limits
- Privacy controls and data-retention policy

Customer management can be postponed for a strictly anonymous cash-sale first release.

### 14. Audit and activity logs

**Current status:** Partial

The EF context automatically creates audit records for data changes.

Required capabilities:

- Record user, timestamp, action, entity, and record identifier
- Record logins, failed logins, permission changes, voids, refunds, reprints, discounts, price overrides, and stock adjustments
- Use UTC timestamps and display them in local time
- Exclude passwords, password hashes, tokens, and other secrets
- Search and filter audit events
- Make logs append-only for normal users
- Define retention and archival rules
- Capture old/new values selectively instead of serializing every property blindly

### 15. Dashboard and alerts

**Current status:** Partial/shell present

Required capabilities:

- Sales today
- Transaction count
- Gross sales, discounts, tax, refunds, and net sales
- Estimated gross profit, if costs are reliable
- Low-stock and out-of-stock counts
- Products nearing expiry
- Top-selling products
- Recent activity
- Open shifts and cash variance alerts for managers

Dashboard queries should summarize data efficiently and should not load all transactions into memory.

### 16. Reports

**Current status:** Partial reporting infrastructure; business reports mostly missing

Minimum reports:

- Sales by date/time range
- Sales by product and category
- Sales by cashier/register
- Payment-method summary
- Tax summary
- Discounts, voids, returns, and refunds
- Product profit/margin estimate
- Current stock and low stock
- Stock movement and adjustment history
- Inventory valuation
- Purchasing and supplier history
- Shift and cash over/short report
- Audit/activity report

All reports should support filters, preview, printing, and export where useful. Totals should be calculated from stored transaction facts rather than current product values.

### 17. Backup, restore, and maintenance

**Current status:** Missing

Required capabilities:

- Documented SQL Server backup process
- Authorized manual backup
- Scheduled backup guidance or integration
- Restore procedure tested on a separate database
- Database migration/version check at deployment
- Data retention and archival
- Application and database health diagnostics
- Log-file location and rotation

Restore is more important than backup alone; a backup is not trustworthy until restoration has been tested.

## Optional growth modules

Implement these only when required by the business.

### Promotions and loyalty

- Percentage/fixed discounts
- Buy-one-get-one and quantity pricing
- Coupon codes
- Scheduled campaigns
- Loyalty points earning and redemption
- Promotion priority and stacking rules

### Multi-branch and multi-warehouse

- Branch/store records
- Warehouse/location-specific stock
- Stock transfers with send/receive states
- Branch-specific prices and taxes
- Consolidated reporting
- Per-branch users and permissions

This is a major architectural expansion. Add location identifiers to transactions from the beginning only if multi-branch support is genuinely planned.

### Accounts receivable and supplier payable

- Customer credit sales
- Credit limits and due dates
- Payment allocations
- Supplier balances
- Aging reports

Do not approximate accounting with editable balance fields; use a transaction ledger.

### Expense tracking

- Expense categories
- Register-paid expenses
- Attachments and approvals
- Expense summaries

This is useful operationally but is not a replacement for accounting software.

### Employee time and commission

- Clock-in/out
- Attendance
- Sales commission rules
- Employee performance summaries

### E-commerce or external integrations

- Accounting export
- Online catalog/order synchronization
- Payment terminal integration
- Barcode label printing
- Weighing scale integration
- Customer display
- Electronic invoicing/tax authority integration

Integration requirements depend heavily on country, hardware, and provider.

## Recommended implementation order

### Release 1: safe single-store cash POS

1. Store/tax/receipt configuration
2. Complete product catalog and choose one inventory quantity model
3. Stock receiving and adjustment ledger
4. Complete register/cart UI
5. Atomic checkout and concurrency protection
6. Cash payment
7. Reproducible receipt printing
8. Returns/refunds
9. Cashier shifts and cash reconciliation
10. Essential sales, stock, tax, and shift reports
11. Service-level permissions and hardened audit logging
12. Automated tests, backup, and restore procedure

### Release 2: operational control

1. Suppliers
2. Purchase orders and receiving
3. Physical stock count
4. Customer records
5. Card/e-wallet and split payments
6. Dashboard and management alerts
7. Exportable management reports

### Release 3: business-specific expansion

1. Promotions and loyalty
2. Expiry/batch or serial tracking
3. Multi-branch/warehouse support
4. Credit accounts
5. Hardware, accounting, or e-commerce integrations

## Core data records to add or refine

| Record | Purpose |
|---|---|
| `StoreSetting` | Store identity, currency, tax, receipt, and operational settings |
| `Product` | Sellable item and catalog information |
| `ProductPrice` | Effective-dated selling/cost price history |
| `StockMovement` | Immutable inventory increase/decrease ledger |
| `StockCount` / `StockCountLine` | Physical count and variance workflow |
| `Supplier` | Supplier master record |
| `Purchase` / `PurchaseLine` | Purchasing and receiving |
| `Sale` / `SaleLine` | Immutable completed-sale facts |
| `Payment` | Tender type, amount, state, and safe external reference |
| `Return` / `ReturnLine` | Reversal linked to original sale lines |
| `Customer` | Optional customer identity and contact data |
| `Register` | Physical/logical checkout station |
| `Shift` | Cashier register session and reconciliation |
| `CashMovement` | Opening cash, cash-in, cash-out, and closing entries |
| `TaxRate` | Effective tax definitions |
| `AuditEvent` | Security and operational event trail |

Completed transaction records should retain snapshots such as product description, unit price, cost used for margin, tax rate, discount, and cashier. They should not depend on the current product record to reconstruct history.

## Cross-cutting requirements

Every module should account for:

- Authorization at the service/use-case boundary
- Input and business-rule validation
- Atomic database transactions
- Concurrency conflicts
- Audit events without sensitive data
- Clear user-facing errors and recoverability
- UTC storage for timestamps
- Decimal/currency rounding policy
- Search, filtering, and pagination for growing data
- Deactivation instead of deleting referenced master data
- Automated tests for financial and inventory rules
- Database indexes for receipt number, barcode, SKU, dates, and foreign keys
- Backup and migration compatibility

## Immediate recommendation for this repository

Focus next on one complete vertical workflow:

```text
Open cashier shift
    -> scan/search products
    -> validate stock
    -> calculate subtotal, discount, tax, and total
    -> accept cash
    -> atomically save sale + payment + stock movements
    -> print a reproducible receipt
    -> include transaction in shift and sales reports
```

Finishing and testing this workflow will expose the correct boundaries for the rest of the system. Supplier, purchasing, customer, loyalty, and multi-branch functionality can then be added without building on an unreliable checkout core.
