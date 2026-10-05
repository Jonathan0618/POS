# POS Implemented Features Guide

**Prepared:** 2026-09-07  
**Purpose:** a plain-language explanation of the features and infrastructure added to the POS project  
**Current position:** backend phases B0-B6 are implemented; the remaining application-wide UI phase U1 has not started

## 1. How to read this guide

This document explains what was added, why it exists, and how it works. It is intended for developers, operators, and project owners who do not want to work through the much more detailed implementation log.

Three status words are important:

- **Implemented** means the required application code exists.
- **Deployed** means a database migration or permission seed was applied to the current `POSV1` database and inspected.
- **Verified** means behavior was exercised through tests or runtime checks.

These words are not interchangeable. The B0-B6 backend is implemented and relevant migrations were deployed, but testing was placed on hold. Anything specifically listed as deferred must not be treated as production-verified.

## 2. System structure

The solution remains a C#/.NET Framework 4.8 WinForms application using DevExpress, Entity Framework 6, SQL Server, and ASP.NET Identity.

The main responsibilities are separated as follows:

```text
POS                 User interface and application startup
POS.Services        Business workflows and validation
POS.Models          DTOs used to move safe data between layers
POS.Data            EF context, mappings, migrations, and auditing
POS.Domains         Database entities
POS.Core            Current-user, authorization, clock, and result contracts
POS.Common          Shared constants and enumerations
POS.Tests           Automated test projects (currently held by user direction)
```

`ApplicationCompositionRoot` is the central place that creates services, forms, and shared dependencies. This avoids each screen silently creating unrelated database contexts and services.

## 3. Shared foundations

### One transaction per business operation

Checkout, receiving, returns, stock adjustments, stock counts, and shift operations use a shared `POSContext` and explicit database transaction.

This means all related records succeed together or are rolled back together. For example, checkout cannot permanently reduce stock if saving the sale fails.

### Typed operation results

`OperationResult` and `OperationResult<T>` represent expected outcomes such as invalid input, missing permission, or stale data. They allow the UI to show understandable messages without exposing technical exception details.

### Current user and authorization

`ICurrentUser` provides the authenticated identity from claims. `IAuthorizationService` checks stable resource codes and the existing View, Add, Edit, and Delete action flags.

Permission checks exist inside services. Hiding a button is only a convenience; it is not the security boundary.

### UTC time

`IClock` supplies timestamps. Stored operational timestamps use UTC, while the UI is expected to convert them to the configured store time zone.

### Safe concurrency

Mutable records use either row versions or opaque content revisions.

- A **row version** is a database-generated token that changes when a record changes.
- A **content revision** is a hash of the values the user reviewed.
- A stale token means somebody else changed the data, so the caller must reload and review it.

The application does not silently overwrite newer changes.

### Idempotent posting

Critical commands use a caller-generated `RequestId` and a hash of the request contents.

If the same request is retried after an uncertain result, the service returns the already-created transaction rather than creating a duplicate. Reusing the ID with changed details is rejected.

### Money and currency

Money uses `decimal`. The configured currency is fixed to Philippine peso (`PHP`). The number of decimal places can be configured from zero to four, and midpoint values use away-from-zero rounding.

Completed transactions store the currency, tax, rounding, prices, names, and other facts used at posting time. Later configuration or catalog edits do not rewrite history.

## 4. Authentication and sessions

The authentication baseline now includes:

- required-field validation and duplicate-submit protection;
- ASP.NET Identity password hashing;
- failed-attempt tracking and a 15-minute lock after five failures;
- disabled-account handling;
- username-only remember-me behavior;
- claims as the single current-user source;
- logout, register lock, and same-user unlock;
- self-service password change and administrator password reset;
- safe audit events for login, failure, lockout, logout, lock/unlock, and password operations.

Passwords, hashes, security stamps, and tokens are excluded from audit values. A previous proxy-name audit issue affecting legacy sign-in was corrected by recording the real entity name.

## 5. Users, roles, modules, and permissions

Stable resource codes replace permissions based on display names. Baseline roles include System Administrator, Manager, Cashier, and Inventory Clerk.

Added behavior includes:

- server-paged user search by user details and role;
- account enable/disable;
- safe role assignment in one transaction;
- role creation, rename, and protected deletion;
- a permission matrix for View/Add/Edit/Delete;
- prevention of self-disablement and self-demotion;
- protection for the final active System Administrator;
- safe module parent validation and cycle prevention;
- protection against deleting referenced or parent modules;
- user activity queries backed by audit events;
- permission-change and role-assignment audit records.

Permission changes are defined to take effect at the next sign-in.

## 6. Store, tax, register, printer, and receipt settings

Store settings now cover store identity, contact information, tax identity, PHP formatting, time zone, receipt footer, negative-stock policy, tax rules, register details, printer, and receipt numbering.

Important behavior:

- only one store configuration may exist;
- currency is always PHP;
- tax may be inclusive or exclusive;
- changing tax creates a new effective-dated version instead of rewriting history;
- receipt numbers are allocated under a serializable database lock;
- receipt numbers have a unique database index;
- a stale settings form cannot move the receipt sequence backwards;
- failed saves roll back and clear tracked changes before reuse;
- printer discovery and test printing use `IReceiptPrinter`;
- receipt preview creates a sample marked `SAMPLE - NOT A SALE` and consumes no receipt number.

Store settings remain an owned DevExpress modal form by explicit project decision.

## 7. Product catalog and categories

The catalog backend supports:

- product creation and editing;
- deactivation and reactivation instead of destructive deletion;
- unique SKU and barcode enforcement;
- category assignment, unit, selling price, cost, reorder level, and expiry date;
- server-side search and bounded paging;
- price-change history;
- stale-edit protection;
- category deactivation/reactivation;
- unique category names;
- prevention of category deletion cascading into products.

Initial inventory is not entered as a product field. It belongs to controlled inventory operations so the stock ledger remains explainable.

## 8. Inventory and stock control

`InventoryBalance` is the current quantity, while `StockMovement` is the permanent history explaining every increase or decrease.

The inventory backend includes:

- opening balances;
- sale, receipt, return, purchase-return, adjustment, and count movements;
- negative-stock policy enforcement;
- row-version concurrency checks;
- balance-versus-ledger reconciliation queries;
- paged balance and movement searches;
- low-stock, out-of-stock, expiring-soon, and expired queries;
- manual adjustments with required reasons;
- adjustment request idempotency;
- draft, posted, and cancelled stock counts;
- stock-count request idempotency and stale-version protection;
- structured audit events for stock operations.

Checkout and other stock writers refuse to proceed when the stored balance disagrees with the movement ledger. They do not silently repair inventory.

Product-level expiry is currently supported. Batch- or lot-specific expiry quantities are not yet implemented.

## 9. Suppliers

Supplier services support:

- create, edit, search, details, deactivate, and reactivate;
- unique normalized supplier codes;
- contact, address, and tax information;
- stale-edit protection;
- supplier-to-product links;
- supplier SKU, default cost, and lead time;
- safe unlinking without deleting either master record;
- paged supplier purchase history.

Sensitive supplier contact values participate in conflict detection but are excluded from automatic audit details.

## 10. Purchasing, receiving, and purchase returns

The purchasing backend supports:

- purchase-order drafts;
- draft editing with revision checks;
- Draft, Ordered, Partially Received, Received, and Cancelled states;
- order and cancellation transitions;
- direct goods receipts;
- partial receiving against purchase orders;
- duplicate-receipt protection;
- atomic inventory movement posting;
- proposed product-cost changes that require explicit review;
- purchase returns as separate correcting documents;
- prevention of returning more than the eligible received quantity.

Drafts and cancellations do not change inventory. Completed receipts are not rewritten to represent a return.

## 11. Customers

Customer services provide create, edit, search, details, deactivate/reactivate, and purchase/return history.

A sale may still use a walk-in customer without creating a customer record. Referenced customers are deactivated instead of being destructively deleted.

## 12. Registers, cashier shifts, and cash movements

Register maintenance supports search, details, save, and active-state changes with revision protection.

Cashier shift functionality includes:

- opening a shift with opening cash;
- preventing conflicting open shifts for a register or cashier;
- cash-in and cash-out with required reasons;
- request-id protection against duplicate cash movements;
- expected-cash calculation from recorded facts;
- closing count and over/short variance;
- shift concurrency protection;
- shift/register ownership validation for sales and returns.

Expected cash is calculated and is not an editable number.

## 13. Sales cart, held sales, and checkout

`SalesCartService` provides product search, exact barcode lookup, add/remove/update operations, current stock visibility, and configured totals without depending on WinForms controls.

Checkout validates:

- an active register and open owned shift;
- active products and positive, unique quantities;
- current balance and ledger agreement;
- available stock and negative-stock policy;
- configured tax and rounding;
- discount permission and bounds;
- customer requirements;
- tender sufficiency and references.

One transaction saves the sale, line snapshots, payments, receipt sequence, stock balances, stock movements, held-sale transition, and audit event.

Held sales persist the reviewed cart but do not reserve or reduce stock. They can be listed, resumed, updated, cancelled, and consumed by checkout using request and revision checks.

## 14. Payments and tenders

Supported tender types are Cash, Card, EWallet, and StoreCredit.

The backend supports multiple tender rows, validates underpayment, calculates change only from eligible cash, requires safe external references for non-cash tenders, and requires a customer for StoreCredit.

Complete card numbers, CVV values, credentials, and tokens must never be stored. Only safe provider references are retained. Completed non-cash references have database uniqueness protection.

## 15. Receipts and printing

Receipts are reconstructed from stored sale snapshots rather than current product/settings data. They include store, tax, register, cashier, optional customer, items, prices, discounts, tax, rounding, payments, cash, change, and footer information.

Receipt services support paged search, details, initial printing, and permission-controlled reprinting. Reprints are visibly marked and audited. Printer access goes through the receipt-printer abstraction.

## 16. Returns, refunds, and exchanges

The return service provides eligibility lookup and atomic posting.

It enforces:

- a default 30-day return window;
- cumulative item return limits;
- proportional refund calculations;
- original-payment refund limits;
- active register and open-shift attribution;
- manager approval for late returns, non-restock dispositions, or returns above PHP 1,000;
- unique non-cash refund references;
- optional linkage to a separate completed exchange sale.

Restock increases sellable inventory. Damaged and Quarantine dispositions do not. The original sale and receipt remain unchanged; return documents and reversing movements preserve the history.

## 17. Audit and activity history

Audit records are append-only through normal EF application saves. Attempts to edit or delete them through the application context are rejected.

Added audit behavior includes:

- allowlisted structured values rather than dumping every property;
- exclusion of credentials, tokens, contact details, payment references, and row versions;
- UTC timestamps;
- user, record, category, outcome, and register attribution;
- correlation IDs that link events from one business operation;
- bounded, paged search;
- category, date, text, entity, action, register, and correlation filters;
- a retention summary;
- ZIP/JSONL archive export with a manifest and SHA-256 integrity digest;
- archive verification;
- restore into a new audit-only database.

The current retention policy keeps all audit records. Export does not purge source records. An integrity digest detects accidental or malicious file changes but does not prove who created the archive.

## 18. Dashboard and reports

Management services use one shared UTC date/register/user/customer filter and calculate results from completed sale snapshots and posted returns.

The backend provides:

- gross sales, refunds, net sales, tax, discounts, cost, and estimated margin;
- transaction and return counts;
- top products;
- stock and expiry alerts;
- open and long-running shift alerts;
- sales, items/categories, tender, inventory, movement, purchasing, shift, and audit report rows;
- UTF-8 CSV export with spreadsheet-formula injection protection;
- printable management summaries with the applied filters and generation time.

Dashboard and report calculations share the same financial rules so equivalent filters can reconcile.

## 19. Backup, restore, diagnostics, and health

`DatabaseMaintenanceService` implements `IBackupService` and requires Maintenance permission.

It can:

- create a full SQL Server `COPY_ONLY` backup with `CHECKSUM`;
- run `RESTORE VERIFYONLY` on the new backup;
- restore a checked backup only into a new database;
- relocate restored data and log files to SQL Server default locations;
- run `DBCC CHECKDB` after restore;
- report application/database migration versions and connectivity;
- report printer configuration, log-drive free space, and latest full-backup age;
- write safe backup/restore audit events.

The backend cannot overwrite the live `POSV1` database. Live replacement is deliberately a manual DBA operation requiring coordinated approval.

Diagnostic logs default to `%LOCALAPPDATA%\POS\Logs`, rotate at 5 MiB, retain five archives, bound field lengths, and redact common credential assignments. Callers must still avoid logging customer, payment, token, credential, or connection-string data.

## 20. Database migrations and deployment work

The implementation added migrations for the operations schema, inventory backfill, receipt and identifier uniqueness, audit correlation/register attribution, category protection, request-id safeguards, purchasing corrections, shift guards, receipt snapshots, return guards, exchange linkage, and reporting indexes.

Before material deployments, checked `COPY_ONLY` backups were created. Generated SQL and resulting metadata were reviewed, and inventory totals were checked for reconciliation where recorded.

Application startup applies pending non-destructive EF migrations and refuses to continue if migration application fails. Automatic data-loss migrations are not allowed.

The empty experimental `MyInfoMigration` files were removed from the working tree. They were not included in `POS.Data.csproj`, contained no schema operations, and were not part of the production migration chain.

## 21. Build, tests, and automation

The repository includes:

- `.editorconfig` for code conventions;
- a Windows GitHub Actions workflow;
- `scripts/verify.ps1` for local verification;
- framework-free and LocalDB integration test projects.

Some early slices were tested before the hold. From 2026-09-05 onward, user direction paused adding and running tests. Testing resumed on 2026-09-07 by user direction. The complete automated suite and full Release solution build pass after the non-UI cleanup. Coverage was expanded for checkout replay safety, return replay/restocking, dashboard/report reconciliation, cashier-shift lifecycle calculations, and diagnostic-log redaction.

Nineteen unused Syncfusion assembly references were removed because the application contains no Syncfusion code and the UI standard is DevExpress. The conflicting `ucProducts.ProductName` designer field was renamed to `colProductName` without changing its caption or data binding. The subsequent full Release build reports no warnings, and all existing tests pass.

## 22. What remains

The next planned phase is U1, the remaining UI implementation across all 17 modules.

U1 includes the DevExpress shell/navigation redesign, sales register and payment dialogs, shift screens, receipts, returns, catalog and inventory screens, purchasing, customers, dashboard, reports, register maintenance, backup/restore UI, and final security/settings consistency.

Testing resumed on 2026-09-07. The current EF migration chain matches `POSV1`, with all 36 migrations applied and no new migration required. A fresh checked backup was restored into the separate `POS_Verification_20260907` database and passed `DBCC CHECKDB`; live and restored product, balance, ledger, and migration counts match. Existing and expanded automated integration tests pass. Physical printer checks, production-scale performance checks, UI user acceptance, and broader multi-workstation soak testing still require their corresponding hardware or representative environment.

## 23. Quick glossary

| Term | Simple meaning |
|---|---|
| Audit event | A permanent record of who did what and when |
| Correlation ID | An ID joining audit events from the same operation |
| DTO | A purpose-built object used to move data safely between layers |
| EF6 | Entity Framework 6, used to read and write SQL Server data |
| Idempotent | Safe to retry without creating the same transaction twice |
| Immutable snapshot | Historical values saved so later edits cannot change the past |
| Ledger | An append-only history explaining a balance |
| Migration | A versioned database schema change |
| Opaque revision | A conflict token the UI preserves but does not interpret |
| Row version | A database token used to detect concurrent edits |
| Serializable transaction | A strict transaction used to protect critical shared data |
| Service boundary | The business layer where validation and permission checks occur |
| Soft delete/deactivation | Hiding a referenced record without erasing its history |
| UTC | A common stored time standard converted to local time for display |

## 24. Related documents

- `POS_IMPLEMENTATION_PLAN.md` contains the detailed delivery history and acceptance criteria.
- `UI_IMPLEMENTATION_GUIDE.md` is the exact designer-facing specification for U1.
- `POS_MODULES.md` describes the complete module roadmap.
- `PROJECT_ANALYSIS.md` records the original architecture and risks.
- `OPERATIONS.md` contains deployment, backup, restore, and recovery procedures.
