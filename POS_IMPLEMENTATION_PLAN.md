# POS Full Implementation Plan

**Created:** 2026-09-04  
**Scope:** implementation of all 17 modules defined in `POS_MODULES.md`  
**Target:** single-store, production-ready desktop POS with an upgrade path to multiple registers  
**Current baseline:** C#/.NET Framework 4.8, WinForms, DevExpress, EF6, SQL Server, ASP.NET Identity

**UI standard:** all new and redesigned screens must use DevExpress visual components. Standard WinForms remains acceptable only for nonvisual framework infrastructure required by WinForms itself (for example `Application`, `DialogResult`, and interfaces/base types).

**Navigation standard:** use `frmMain` as the single application shell and host primary workflows as `UserControl`s in its content panel. Use separate modal forms only for focused edits or interactions that must interrupt the workflow, such as credential changes, confirmations, payment, and compact record editors.

**Store settings exception (2026-09-05):** per user direction, store and POS settings open in a separate, owned DevExpress form from the main shell.

**Currency requirement (2026-09-05):** use Philippine peso (`PHP`). Store settings default to PHP, expose it as read-only, and reject other currencies at service validation. Existing completed-sale currency snapshots remain historical facts.

**Testing hold (2026-09-05):** per user direction, do not add or run tests until the user explicitly resumes testing. Application builds may continue; deferred regression and migration checks must not be reported as passed.

**Delivery order (2026-09-05):** per user direction, implement the backend across all 17 modules first. UI implementation, redesign, navigation wiring, and visual polish come last, in phase U1. This supersedes the earlier module-by-module vertical delivery order. Existing UI remains in place; completed UI work is retained. Backend contracts must remain independent of forms and controls. Minimal caller compatibility edits are permitted only when needed to keep existing code compiling after a backend contract change; they must not expand into UI feature work.

**Completion notification:** explicitly notify the user when the planned B0–B6 backend implementation is finished. Distinguish implementation completion from deferred testing, migration deployment, and runtime verification; do not announce completion while required backend scope remains.

## Purpose

This file is the master change specification and delivery checklist for implementing all 17 POS modules. It describes intended application changes; creating this document does not mean those code changes have already been made.

Implementation must be incremental. Each backend slice should compile, preserve existing user data, and document its migration and verification requirements. While testing is on hold, continue backend implementation and application builds, record unverified behavior explicitly, and defer test creation/execution and runtime acceptance. Do not mark deferred checks as passed or treat implemented code as production-verified.

## Completion status

The table below records overall module delivery, including work completed before the backend-first decision. M0–M7 references are historical grouping labels, not the current execution order. Completed modules are retained; the new backend phases may still identify integration work in them. All remaining UI work belongs to U1.

| # | Module | Current state | Target milestone | Delivery status |
|---:|---|---|---|---|
| 1 | Authentication and session management | Implemented | M1 | Complete |
| 2 | Users, roles, and permissions | Implemented | M1 | Complete |
| 3 | Store and POS configuration | Partial | M1 | In progress |
| 4 | Product catalog | Partial | M2 | In progress |
| 5 | Inventory and stock control | Partial | M2 | In progress |
| 6 | Suppliers | Partial | M3 | In progress |
| 7 | Purchasing and receiving | Partial | M3 | In progress |
| 8 | Sales register and cart | Backend implemented | M4 | UI and verification deferred |
| 9 | Payments and tenders | Backend implemented | M4 | UI and verification deferred |
| 10 | Receipts and invoices | Backend implemented | M4 | UI and verification deferred |
| 11 | Returns, refunds, and exchanges | Backend implemented | M5 | UI and verification deferred |
| 12 | Cashier shifts and cash drawer | Backend implemented | M4 | UI and verification deferred |
| 13 | Customers | Partial | M5 | In progress |
| 14 | Audit and activity logs | Partial | M1–M5 | In progress |
| 15 | Dashboard and alerts | Backend implemented | M6 | UI and verification deferred |
| 16 | Reports | Backend implemented | M6 | UI and verification deferred |
| 17 | Backup, restore, and maintenance | Backend implemented | M7 | UI and verification deferred |

## Mandatory architectural changes

These changes are prerequisites for safe implementation.

### Dependency and transaction boundaries

- Stop constructing a new `POSContext` inside every generic repository.
- Create one shared context for the generic repositories participating in each business operation.
- Inject the shared context into repositories or use EF `DbSet` directly in application services.
- Commit once at the end of checkout, receiving, refund, adjustment, and shift-close operations.
- Wrap multi-record financial and stock operations in explicit database transactions.
- Implement `IDisposable` for context-owning scopes.
- Keep WinForms code responsible for presentation; put business rules in services.

### Suggested dependency flow

```text
POS UI
  -> POS.Services interfaces and DTOs
  -> POS.Data shared-context repository/query implementations
  -> POS.Domains entities

POS.Core
  -> current-user, clock, authorization, and result contracts

POS.Common
  -> shared enums and constants
```

### New common abstractions

Add these contracts, with names adjusted to the project's final conventions:

- `ICurrentUser` — authenticated user, role, and claims without mutable duplicated static fields.
- `IClock` — supplies UTC timestamps and enables deterministic tests.
- `IAuthorizationService` — checks permissions inside application services.
- `IReceiptPrinter` — separates receipt generation from printer hardware.
- `IBackupService` — performs validated database backup/restore operations.
- `OperationResult<T>` — represents expected validation/conflict failures without generic exceptions.

### Data rules

- Store timestamps in UTC; convert to local time only for display.
- Use `decimal`, never floating point, for money.
- Define a single currency-rounding method and apply it consistently.
- Use immutable transaction lines/snapshots for completed sales, purchases, and refunds.
- Deactivate referenced master data instead of deleting it.
- Add row-version concurrency columns to mutable stock and shift records.
- Add unique indexes for SKU, barcode, receipt number, payment reference where applicable, and register code.
- All migrations must have descriptive names and a tested rollback/recovery approach.

## Proposed domain model changes

### Store and configuration

- `StoreSetting`: store identity, tax number, currency, time zone, receipt footer, default tax, rounding, negative-stock policy.
- `Register`: name/code, store relationship, active state, receipt printer configuration.
- `TaxRate`: name, rate, inclusive/exclusive flag, effective dates, active state.
- `NumberSequence`: atomic next values for receipts, returns, purchases, and shifts.

### Catalog and inventory

- Refine `Product`: SKU, unique barcode, unit, tax rate, selling price, cost, active state, optional image path.
- Keep `Category`, prevent deletion when referenced, and support deactivation.
- Replace overlapping product/stock quantities with `InventoryBalance` plus an immutable `StockMovement` ledger.
- `InventoryBalance`: product, location/register-independent stock location, quantity on hand, row version.
- `StockMovement`: product, type, quantity delta, reference type/id, reason, user, UTC timestamp.
- `StockAdjustment` and lines: controlled manual adjustment workflow.
- `StockCount` and lines: draft, counted, posted, and cancelled states.

### Supplier and purchasing

- `Supplier`: code, name, contacts, address, tax ID, active state.
- `SupplierProduct`: product, supplier SKU, last/default cost, lead time.
- `PurchaseOrder` and `PurchaseOrderLine`: order lifecycle and expected quantities.
- `GoodsReceipt` and `GoodsReceiptLine`: received quantities, costs, and stock movements.
- `PurchaseReturn` and lines: supplier returns and reversal movements.

### Sales and payments

- Refine `Sale`: receipt number, status, register, shift, cashier, customer, subtotal, discount, tax, rounding, total, UTC date.
- Consolidate `Sale.Items` and `Sale.SaleItems` into one collection.
- Refine `SaleItem`: product ID plus product/SKU/barcode description snapshots, quantity, unit price, cost snapshot, discount, tax rate, tax, line total.
- `Payment`: sale, tender type, amount, status, safe provider/reference value, UTC timestamp.
- `HeldSale`: optional persistent suspended cart or a draft `Sale` status.
- `Return` and `ReturnLine`: original sale/line links, reason, disposition, totals, approver.
- `RefundPayment`: refund tender and safe payment reference.

### Customers, shifts, and audit

- `Customer`: code, name, contacts, tax information, active state, notes.
- `Shift`: register, cashier, opening/closing time, opening cash, expected cash, counted cash, variance, state, row version.
- `CashMovement`: shift, cash-in/out/opening/closing type, amount, reason, user, time.
- Refine `AuditLog`: event category, entity ID, safe structured details, correlation ID, user, register, UTC timestamp.

## Module-by-module changes

All **Code changes** below belong to the backend phases where applicable. All remaining **UI changes** are deferred to U1. Receipt/report data construction, calculations, printer/backup adapters, and export services are backend work; report layout/designers, viewers, forms, controls, and navigation are UI work. Acceptance criteria involving user interaction or visual behavior are retained for U1 and later verification.

## 1. Authentication and session management

### Code changes

- Replace duplicated `CurrentUser` fields with a claims-backed `ICurrentUser` implementation.
- Add login attempt tracking and configurable lockout through Identity.
- Add logout, lock-screen, password-change, and administrator-reset services.
- Re-enable the sign-in form after failed login; handle exceptions without leaving the UI disabled.
- Decide whether remember-me retains an encrypted password or only remembers the username. Prefer username-only or a revocable token.
- Record successful login, failed login, logout, lockout, and password reset audit events.

### UI changes

- Update `SignIn` with progress, validation, Caps Lock warning, lockout feedback, and safe error messages.
- Add `frmChangePassword` and administrator password-reset action.
- Add logout/lock buttons to `frmMain`.

### Acceptance criteria

- Disabled or locked users cannot sign in.
- Failed authentication restores an interactive login screen.
- Logging out clears all session data and returns to sign-in.
- No password or token appears in logs or audit values.
- Authentication behaviors have service/integration tests.

## 2. Users, roles, and permissions

### Code changes

- Define stable module/resource codes instead of deriving security from display names.
- Seed Administrator, Manager, Cashier, and Inventory Clerk roles with explicit permissions.
- Enforce permission checks in services for every state-changing use case.
- Add rules preventing removal of the final administrator or self-lockout during an active session.
- Complete module deletion/deactivation and correct parent-module update behavior.
- Add pagination/search for users and role assignments.

### UI changes

- Improve user list, role assignment, enable/disable, reset-password, and activity views.
- Build a permission matrix grouped by application module.
- Hide or disable unauthorized navigation while retaining service-level enforcement.

### Acceptance criteria

- Permissions apply even if a UI event is invoked indirectly.
- Role changes take effect at a defined point (immediately or next login).
- The last active administrator cannot be removed or demoted.
- Permission changes are audited.

## 3. Store and POS configuration

### Code changes

- Add store, tax, register, number-sequence, currency, and receipt configuration entities/services.
- Replace hard-coded `VatRate` with an effective tax configuration.
- Centralize money rounding and receipt-number generation.
- Validate that exactly one applicable default configuration exists.

### UI changes

- Add settings screens for store profile, tax, register, receipt, currency, stock policy, and printer.
- [x] Add printer test and receipt preview.

### Acceptance criteria

- A manager can configure store and register without editing `App.config`.
- Every sale stores the tax and rounding rules actually applied.
- Receipt numbers remain unique under concurrent checkout.

## 4. Product catalog

### Code changes

- Complete product create/read/update/deactivate operations.
- Add SKU, barcode uniqueness, unit, tax rate, active state, and optional image.
- Add category deactivation and reference protection.
- Move nested view models out of `InventoryService` into consistent DTO files.
- Replace repeated category queries with one server-side projection.
- Add paginated search by name, SKU, barcode, category, and active state.
- Add price-change audit/history.

### UI changes

- Complete product editor and product details.
- Add edit/deactivate actions, filters, validation, and duplicate-barcode feedback.
- Add optional barcode-label printing and import/export after core CRUD is stable.

### Acceptance criteria

- Duplicate SKU/barcode is rejected by both validation and database constraint.
- Referenced products are deactivated, not deleted.
- Product search remains responsive with a realistic catalog size.

## 5. Inventory and stock control

### Code changes

- Choose `InventoryBalance` as the current quantity and `StockMovement` as its immutable history.
- Remove or migrate the overlapping `Product.Quantity`/`Stock.Quantity` design.
- Add atomic movement posting for receive, sale, return, adjustment, purchase return, and count variance.
- Add optimistic concurrency and negative-stock policy.
- Add stock counts, adjustments, reasons, and approval rules.
- Add low-stock, zero-stock, and expiring-item queries.

### UI changes

- Add inventory balances, movement history, adjustments, stock count, and alerts screens.
- Show source document links for each movement.

### Acceptance criteria

- Balance equals the sum of posted movements after migration/opening balance.
- A failed transaction leaves balance and movements unchanged.
- Two simultaneous sales cannot consume the same final unit.
- Every manual adjustment requires user, reason, and audit event.

## 6. Suppliers

### Code changes

- Add supplier and supplier-product entities, DTOs, configurations, services, and permissions.
- Enforce unique supplier codes and safe deactivation.
- Add supplier purchase-history query.

### UI changes

- Add supplier list, editor, details, supplied products, and purchase history.

### Acceptance criteria

- Referenced suppliers cannot be destructively deleted.
- Supplier contact and product-source data are searchable.
- Changes are validated and audited.

## 7. Purchasing and receiving

### Code changes

- Add purchase-order, goods-receipt, and purchase-return workflows with explicit statuses.
- Support partial receiving without exceeding ordered quantities unless authorized.
- Post received inventory and costs in one transaction.
- Capture supplier invoice/reference and prevent accidental duplicate receiving.
- Define whether receiving updates product cost automatically or proposes a price change.

### UI changes

- Add purchase order list/editor, receiving screen, outstanding order view, and purchase-return screen.
- Provide printable purchase order and receiving document.

### Acceptance criteria

- Partial receipts update remaining quantities correctly.
- Cancelling a draft changes no stock.
- Posting or reversing a receipt atomically updates documents and stock ledger.
- Duplicate supplier references trigger a warning or constraint according to policy.

## 8. Sales register and cart

### Code changes

- Implement a register/cart application service independent of WinForms controls.
- Add barcode lookup, product search, line add/remove/update, hold/resume, and totals calculation.
- Validate positive quantities, active products, valid price/tax, payment sufficiency, and stock.
- Implement idempotent checkout to prevent duplicate sale submission.
- Commit sale, lines, payments, stock movements, number sequence, and audit event in one transaction.
- Use explicit sale states: Draft/Held, Completed, Voided, PartiallyReturned, Returned.

### UI changes

- Add a cashier-focused POS screen with barcode input, searchable product grid, cart, totals, payment action, hold/resume, and keyboard shortcuts.
- Keep barcode focus ready for scanner input and show clear stock/error feedback.

### Acceptance criteria

- A completed sale is all-or-nothing.
- Double-clicking checkout cannot create two sales.
- A held sale changes no stock until completion.
- Totals match tested tax, discount, and rounding rules.

## 9. Payments and tenders

### Code changes

- Add payment records and tender types: Cash, Card, EWallet, and optional StoreCredit.
- Support one or multiple tenders according to configuration.
- Validate tender totals and calculate change only from eligible tenders.
- Store safe external references, never full card data or credentials.
- Add reversal/refund association and payment status transitions.

### UI changes

- Add payment dialog with amount due, tender entry, remaining amount, change, and references.
- Restrict manual overrides and offline-payment recording by permission.

### Acceptance criteria

- Underpayment cannot complete a normal sale.
- Payment total, sale total, and change reconcile.
- Sensitive payment data is never persisted.
- Reversals and refunds remain linked to original payments.

## 10. Receipts and invoices

### Code changes

- Refactor receipt report to consume an immutable receipt DTO built from stored sale snapshots.
- Add receipt number, store, register, cashier, payment, tax, discount, return, and reprint fields.
- Add printer abstraction and configurable printer/paper settings.
- Audit reprints and restrict them by permission where required.

### UI changes

- Add print preview, direct print, reprint search, and optional PDF export.
- Add invoice/customer fields where required by the business jurisdiction.

### Acceptance criteria

- Old receipts remain identical after product prices/names or tax settings change.
- Every completed sale has one unique receipt number.
- Reprints are visibly marked and audited.

## 11. Returns, refunds, and exchanges

### Code changes

- Add return/refund entities and service linked to original sale lines and payments.
- Prevent returned quantity from exceeding eligible purchased quantity.
- Add return reasons, item disposition (restock/damaged/quarantine), approval thresholds, and return window.
- Post refund, return document, stock movements, sale status, and audit in one transaction.
- Model exchange as a return plus a separate new sale linked by reference.

### UI changes

- Add receipt lookup, eligible-items grid, disposition, reason, refund tender, approval, and confirmation screens.

### Acceptance criteria

- Original sales are never deleted or edited to fake a return.
- Returned quantities and refunded money cannot exceed their originals.
- Restocked items increase inventory; damaged items do not enter sellable stock.
- Partial and full returns update sale status correctly.

## 12. Cashier shifts and cash drawer

### Code changes

- Add register, shift, and cash-movement services and entities.
- Require an open shift before checkout when configured.
- Compute expected cash from opening amount, cash sales/refunds, and cash movements.
- Add open, cash-in/out, count, close, reopen/adjust-with-approval state transitions.
- Ensure a user/register cannot have conflicting open shifts.

### UI changes

- Add open-shift, cash-in/out, shift status, closing count, variance, and manager-approval screens.

### Acceptance criteria

- Expected cash is derived, not manually editable.
- Shift closure records counted amount and variance.
- All cash movements require a reason and user.
- Sales and refunds appear in the correct shift.

## 13. Customers

### Code changes

- Add customer entity, DTO, configuration, service, deactivation, and privacy-aware audit rules.
- Allow anonymous/walk-in sale without creating a customer.
- Store optional customer snapshot/details on invoices as required.
- Add purchase and return history queries.

### UI changes

- Add customer list/editor/details and quick customer selection/creation from POS.

### Acceptance criteria

- Walk-in checkout remains fast and requires no customer record.
- Customer history accurately links sales and returns.
- Referenced customers are deactivated rather than deleted, subject to privacy policy.

## 14. Audit and activity logs

### Code changes

- [x] Replace automatic EF property dumping with allowlisted, structured audit details.
- [x] Standardize existing explicit business/security event details on the structured format.
- Add entity ID, category, correlation ID, register, UTC timestamp, user, and outcome.
- Add explicit operational/security events not captured by EF state changes.
- Exclude passwords, hashes, security stamps, tokens, credentials, and sensitive payment data.
- [x] Make normal application operations append-only for audit records through EF saves.
- [x] Define the baseline retention policy: retain all events, with automatic deletion disabled.
- [x] Implement authorized archive export without removing source records.
- [x] Implement archive verification and restoration into a new audit-only database.
- [ ] Validate archive round trips and perform a separate-database restore drill when testing resumes; keep purge disabled.

### UI changes

- [x] Add authorized audit viewer with date, username/user ID/record ID, entity, and action filters.
- [x] Add category filtering derived from recorded entity/event names.
- [x] Add register attribution and register filtering for audit records.

### Acceptance criteria

- Critical actions are traceable to a user, time, register, and source record.
- No secret appears in the audit database.
- Normal users cannot alter or delete audit records.
- One business operation can be followed through its correlation ID.

## 15. Dashboard and alerts

### Code changes

- Add optimized aggregate queries for daily sales, refunds, tax, discount, profit estimate, transaction count, and stock alerts.
- Apply permission and date/register scope to dashboard data.
- Add configurable alert thresholds and refresh behavior.

### UI changes

- Complete dashboard cards/charts for today, trend, top products, low/out-of-stock, expiry, recent activity, and open-shift issues.
- Add drill-through from alerts to relevant screens.

### Acceptance criteria

- Dashboard values reconcile with reports for the same filters.
- Queries do not load all transaction rows into memory.
- Unauthorized financial data is not displayed.

## 16. Reports

### Code changes

- Add report query services and typed report DTOs.
- Implement sales, items/categories, cashier/register, tenders, taxes, discounts/voids/refunds, margin, inventory balance, movement, valuation, purchasing, shifts, and audit reports.
- Derive historical reports from immutable transaction snapshots.
- Add common date, user, register, status, category, product, supplier, and customer filters.

### UI changes

- Add report navigation, filter panels, preview, print, and safe export.
- Display filter criteria and generated timestamp on output.

### Acceptance criteria

- Report totals reconcile with transaction records and dashboard.
- Cancelled/draft records are excluded unless explicitly requested.
- Exports respect the same authorization and filters as previews.

## 17. Backup, restore, and maintenance

### Code changes

- Add database version/startup compatibility check.
- Add authorized SQL Server backup service with validated destination and clear progress/error reporting.
- Add restore workflow that requires explicit confirmation, exclusive access, and a pre-restore backup where possible.
- Add diagnostic logging with rotation and no secrets.
- Add health checks for database connectivity, schema version, printer configuration, disk space, and backup age.
- Document deployment, upgrade, backup schedule, restore drill, and recovery ownership.

### UI changes

- Add maintenance screen showing application/database version, connection health, last backup, log location, backup action, and protected restore action.

### Acceptance criteria

- A backup can be restored into a separate test database and passes basic integrity checks.
- Only authorized administrators can backup/restore.
- Failed migrations do not silently start the application against an incompatible schema.
- Logs support diagnosis without exposing credentials or customer/payment secrets.

## Migration plan

Create small, ordered migrations rather than one giant schema change:

1. `AddStoreRegisterTaxAndSequences`
2. `RefineProductsAndCategories`
3. `AddInventoryBalancesAndStockMovements`
4. `MigrateExistingInventoryQuantities`
5. `AddSuppliersAndSupplierProducts`
6. `AddPurchasingAndReceiving`
7. `RefineSalesAndSaleLines`
8. `AddPaymentsAndTenderTypes`
9. `AddShiftsAndCashMovements`
10. `AddReturnsAndRefundPayments`
11. `AddCustomers`
12. `HardenAuditEvents`
13. `AddConcurrencyAndPerformanceIndexes`

Before each destructive or data-transforming migration:

- Back up a representative database.
- Validate row counts and monetary totals before and after.
- Supply deterministic defaults/backfill logic.
- Test on a copy of production-like data.
- Do not remove legacy columns until the new workflow has been verified.

The current experimental `MyInfoMigration` should be removed from the production plan or moved to a separate practice project before new production migrations are generated.

## Permission catalog

Each module should expose stable resource codes with the actions it supports:

| Resource | Typical actions |
|---|---|
| Authentication | Login, Logout, ResetPassword, UnlockUser |
| Users | View, Add, Edit, Disable, ResetPassword |
| Roles | View, Add, Edit, Delete, AssignPermissions |
| Settings | View, Edit |
| Products | View, Add, Edit, Deactivate, ChangePrice, Import, Export |
| Inventory | View, Receive, Adjust, Count, ViewCost |
| Suppliers | View, Add, Edit, Deactivate |
| Purchasing | View, Create, Approve, Receive, Cancel, Return |
| Sales | View, Create, Hold, Void, OverridePrice, Discount, Reprint |
| Payments | TakePayment, Reverse, ViewReferences |
| Returns | View, Create, Approve, Refund |
| Shifts | Open, CashInOut, Close, ApproveVariance |
| Customers | View, Add, Edit, Deactivate, ViewHistory |
| Audit | View, Export |
| Dashboard | ViewOperational, ViewFinancial |
| Reports | View, Export, Print |
| Maintenance | ViewHealth, Backup, Restore, ManageLogs |

Permissions should be enforced by services and reflected by the UI. UI hiding alone is insufficient.

## Test plan

### Unit tests

- Tax, discount, rounding, subtotal, total, tender, and change calculations.
- Sale/return quantity eligibility.
- Stock movement effects and negative-stock rules.
- Shift expected-cash calculation.
- Permission policies and status transitions.
- Receipt/invoice DTO construction.

### Integration tests

- EF mappings and pending migrations.
- Unique SKU, barcode, receipt, and sequence behavior.
- Atomic checkout, receiving, refund, adjustment, and shift close.
- Rollback after failure at each critical write step.
- Optimistic concurrency on final stock units and shift close.
- Identity lockout, role assignment, and service authorization.
- Audit secrecy and completeness.

### End-to-end/manual scenarios

- Configure a new store and register.
- Create users/roles and verify restricted navigation/actions.
- Create supplier/category/product and receive stock.
- Open shift, sell using each tender, print/reprint receipt, and close shift.
- Hold/resume and cancel a cart.
- Partially return and fully return sales.
- Count stock and post variance.
- Reconcile dashboard/report totals.
- Back up and restore into a clean test environment.

## Active delivery sequence — backend first

Implement the phases below in dependency order. A backend phase can be recorded as implemented with verification deferred while testing is held; this does not complete the corresponding overall module. Do not start U1 until the planned backend scope across B0–B6 is implemented. Resume testing only when the user explicitly says so, independently of the implementation phase.

| Phase | Backend scope | Completion requirements | Current status |
|---|---|---|---|
| B0 | Catalog and inventory — modules 4 and 5 | Safe product/category writes and deactivation; SKU/barcode rules and reviewed database constraints; price history; searchable/paged DTO queries; inventory balance/ledger integration; adjustments and stock counts; concurrency and reference protection | Implemented; verification deferred |
| B1 | Suppliers and purchasing — modules 6 and 7 | Supplier maintenance, supplier-product relationships, order lifecycle, direct/partial receiving, purchase returns, transactional movements and cost updates | Implemented; verification deferred |
| B2 | Customer, register, and shift prerequisites — modules 3, 12, and 13 | Customer maintenance/history contracts, walk-in support, register/configuration rules, open/close shift, cash movements, expected cash and variance, shift concurrency | Implemented; verification deferred |
| B3 | Sales, tenders, and receipt data — modules 8, 9, and 10 | Cart/hold/resume services, configured calculations, idempotent checkout, atomic sale/payment/ledger/sequence writes, tender validation, historical receipt DTOs and authorized print/reprint services | Implemented; verification deferred |
| B4 | Returns, refunds, and exchanges — module 11 | Eligibility and quantity limits, approval rules, return disposition, refund tenders, linked exchange operations, transactional reversals | Implemented; verification deferred |
| B5 | Management queries — modules 15 and 16 | Authorized dashboard aggregates and report DTO/query/export services derived from historical facts; consistent date/register filters and reconciliation rules | Implemented; verification deferred |
| B6 | Maintenance and cross-module completion — modules 1, 2, 3, 14, and 17 | Backup/recovery services, diagnostics, migration/startup compatibility, configuration gaps, complete authorization/audit coverage for all new workflows, backend integration review and deferred verification register | Implemented; verification deferred |
| U1 | All remaining UI work — modules 1–17 | DevExpress screens, main-shell navigation, focused dialogs, binding to completed services, validation/error/loading states, dashboard/report visuals, receipt layouts and operator workflows | Ready; not started |

Authorization, validation, audit, UTC handling, money rules, transaction boundaries, and migration compatibility are required within every backend phase; B6 is a final coverage review rather than a reason to postpone these safeguards.

### Immediate backend slice

1. Complete supplier maintenance, stale-write protection, supplier-product linking/unlinking, and purchase-history queries.
2. Implement purchase-order lifecycle and authorization.
3. Implement idempotent direct/partial receiving with atomic inventory movements and reviewed cost updates.
4. Implement purchase returns as correcting documents and movements without rewriting completed receipts.
5. Update this plan after each backend slice. Do not add UI actions, editors, grids, dialogs, or navigation for these capabilities until U1.

### Backend slice completion checklist

- [ ] Business rules and service/DTO contracts are implemented independently of WinForms.
- [ ] Authorization and input validation are enforced before reads/writes as appropriate.
- [ ] Multi-record writes, concurrency, retry/recovery, and audit behavior are addressed.
- [ ] Entity/configuration/migration changes preserve existing data and document deployment requirements.
- [ ] Application build passes; only necessary caller compatibility changes accompany backend work.
- [ ] Deferred unit, integration, migration, and runtime scenarios are recorded while tests remain held.
- [ ] Implementation status and remaining backend work are updated without claiming overall module completion.

## Historical milestones and implementation notes

The original M0–M7 checklists below preserve scope and development history. They do not override the active B0–B6 → U1 sequence. Outstanding UI items in these checklists are deferred to U1; testing items remain subject to the testing hold.

### M0 — Engineering foundation

- [x] Add an initial framework-free test project for sales calculations and `.editorconfig`.
- [x] Introduce dependency composition and shared repository context.
- [x] Add result, clock, current-user, and authorization abstractions.
- [x] Register the intended sale EF configuration.
- [x] Remove the duplicate `Sale.Items`/`SaleItems` collection.
- [x] Establish CI build/test checks.

Progress note (2026-09-04): `SalesService` now shares one injected context across its repositories, defers repository saves, and commits stock changes and the sale through one explicit transaction. Repository context injection and owned-context disposal were added without changing existing callers. Broader repository/context composition remains open.

Repository composition note (2026-09-04): per the selected architecture, the existing generic repository remains the data-access abstraction rather than adding a separate unit-of-work type. `InventoryService`, `ModuleService`, and `RoleService` now inject one `POSContext` into all repositories used by each service; Identity managers in `RoleService` share that same context. Operational and security services support injected contexts and explicitly dispose contexts they create.

UI composition note (2026-09-04): `ApplicationCompositionRoot` is now the runtime construction point for services, forms, and injected dependencies. Forms retain parameterless constructors for WinForms designer compatibility, while normal application navigation uses injected services and deterministic disposal. No external DI container or separate unit-of-work abstraction was introduced.

Cross-cutting abstractions note (2026-09-04): `OperationResult`/`OperationResult<T>`, `IClock`, `ICurrentUser`, and `IAuthorizationService` now have concrete implementations and focused tests. Sales, inventory movements, store settings, and EF auditing consume injected clock/current-user values; the existing static UI authorization entry point delegates to the injectable claims implementation. Gradual conversion of expected service failures from exceptions to operation results remains module-level work.

CI note (2026-09-04): the Windows GitHub Actions workflow restores packages, builds the `POS.Tests` dependency graph in Release mode, and runs all framework-free and LocalDB integration tests. `scripts/verify.ps1` provides the same local check and supports `-FullSolution` where licensed DevExpress/Syncfusion assemblies are installed. Hosted CI intentionally excludes the proprietary UI project because those assemblies are not available on standard runners.

Database note (2026-09-04): application startup now applies pending EF6 migrations. Non-destructive automatic migrations are enabled; EF refuses automatic changes that could lose data. Migration failure is shown to the user and prevents startup against an incompatible schema.

Schema foundation note (2026-09-04): domain records and EF mappings now exist for store settings, tax rates, registers, number sequences, inventory balances/movements/counts, suppliers, purchase orders/receipts, customers, payments, returns/refunds, shifts, and cash movements. Sale records now include historical financial fields and links needed by these modules. Workflows, screens, explicit code migrations, and deeper tests remain open, so the 17 modules are not yet marked complete.

Database execution note (2026-09-04): `FullOperationsFoundation` was generated, reviewed, corrected for SQL Server cascade-path rules, and applied directly to `POSV1`. The resulting schema and migration history were queried successfully. `BackfillInventoryLedger` was then applied: all 5 existing products received inventory-balance rows and 3 nonzero opening quantities received auditable opening movements.

Module 5 progress note (2026-09-04): `InventoryLedgerService` now supports validated, transactional stock movements, optimistic-concurrency balances, negative-stock policy, movement history, UTC audit details, and transitional synchronization to `Product.Quantity`. Validator tests pass. Stock adjustment/count DevExpress workflows and checkout/receiving integration remain open.

Module 3 progress note (2026-09-04): an atomic store/tax/register settings service, validated settings DTO, DevExpress-only settings form, ribbon navigation, safe defaults, and validator tests are implemented. Printer discovery/testing, permission enforcement, tax history management, and receipt-sequence management remain open.

Module 3 settings-authorization/UI note (2026-09-05): store configuration now opens as a service-injected `UserControl` inside the main shell rather than a separate management form. `StoreSettingsService` receives authorization through dependency injection and enforces Settings View/Edit at its read/write boundaries; the control disables Save without Edit permission and provides an in-place revert action. Bypass-resistance tests cover unauthorized reads and writes. Printer discovery/testing, tax history management, and receipt-sequence management remain open.

Module 3 printer note (2026-09-05): `IReceiptPrinter` now separates printer discovery/test output from settings and UI code. The Windows adapter enumerates installed printers, validates the selected device, and produces a minimal test page with store/register context while returning typed failures. The settings control provides a printer list, refresh, and permission-gated test action. Automated tests inject a fake adapter, verifying discovery and selected-printer routing without sending a physical print job. Tax history and receipt-sequence management remain open.

Module 3 tax-history note (2026-09-05): changing the configured tax name, rate, or inclusive mode now closes the current `TaxRate` with an effective-to UTC timestamp and inserts a new active version instead of rewriting historical data. The generated tax ID is established inside the settings transaction before the store default is switched, preserving atomic foreign-key correctness. The in-shell settings control shows read-only, newest-first tax history. LocalDB integration coverage verifies version closure, replacement rate, active/default selection, and history ordering. Receipt-sequence management remains open.

Module 3 receipt-sequence note (2026-09-05): `NumberSequenceService` now configures the receipt prefix/next value through the existing generic repository and allocates numbers with a serializable SQL row lock inside the checkout transaction. First-time allocation uses a direct transactional insert so it cannot flush unrelated tracked stock before sale validation. Completed sales receive fixed-width receipt numbers; rollback does not consume the sequence, and a unique `Sales.ReceiptNumber` index provides database enforcement. Settings expose prefix/next value, integration tests verify sequential allocation and rollback tracking, and `AddUniqueSaleReceiptNumber` was applied to `POSV1`.

Module 3 checkout-tax note (2026-09-05): checkout no longer uses `SalesCalculator.DefaultVatRate`. It requires exactly one store configuration and an active effective default tax, calculates exclusive or inclusive tax with centralized midpoint-away-from-zero currency rounding, and snapshots the applied rate and amount on every sale line plus aggregate sale totals. Store-setting reads reject duplicate configurations and creation cannot silently add a second singleton row. Calculator and LocalDB integration tests verify inclusive/exclusive math, line snapshots, totals, and configured-tax selection.

Module 3 sale-snapshot note (2026-09-05): money decimal precision is now configurable from zero to four places and used by line tax and sale-total rounding. Each completed sale snapshots currency code, tax name, inclusive mode, decimal places, and `AwayFromZero` rounding method so later configuration changes cannot reinterpret historical totals. Calculator and checkout integration tests verify configurable precision and persisted snapshot values. `AddSaleConfigurationSnapshot` was applied to `POSV1` with safe defaults for existing records.

Module 3 receipt-concurrency note (2026-09-05): receipt configuration now locks the current database sequence through validation and saving, rejects any backward next-number change with reload guidance, and refreshes tracked sequence state before updating. A settings screen opened before a completed checkout can no longer reset the number that checkout consumed. Receipt allocation validates formatted length before committing its own transaction. LocalDB regression tests verify stale configuration rejection, rollback of unrelated settings, and distinct consecutive persisted receipt numbers for two checkouts synchronized immediately before allocation. `scripts/verify.ps1 -FullSolution` passes, including all tests; the existing `ucProducts.ProductName` member-hiding warning remains. Module 3 remains in progress: receipt preview and the remaining UI/manual acceptance checks are still open.

### M1 — Secure system foundation

Module 3 concurrent-settings note (2026-09-05): `GetSettings` now supplies an opaque revision covering persisted store, tax, register, and receipt-sequence fields. Callers must retain the revision while editing and reload after saving. `SaveSettings` serializes settings writers with an update/range lock on store settings, reads configuration under a serializable transaction, and rejects missing/stale revisions or mismatched entity IDs before mutation. This covers competing first-time setup as well as updates without a schema migration. Receipt allocation also invalidates an open settings revision, requiring reload before saving. Existing UI reads preserve the revision; preview remains independent of revision validation. Longer-held configuration locks can briefly delay concurrent operations. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests were added or run and no database operations were executed; simultaneous-save, first-setup, and checkout interaction verification remain deferred. Existing test fixtures that construct DTOs directly will need to follow the load/edit/save contract when testing resumes.

Module 3 unsaved-settings note (2026-09-05): settings now compare editor values with the last loaded/saved values and indicate unsaved changes in the form title. Closing through Close, Escape, or the window close action prompts authorized editors to save, discard, or keep editing; validation/save failures cancel closure. Users without Edit permission can discard or keep editing. Revert asks before discarding edits, and a failed reload preserves the current editor values. Receipt preview does not reset the saved baseline. A successful commit followed by a reload failure is now reported as saved with reload required, and further saves are disabled until fresh settings/IDs load. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests were added or run; close/revert interaction verification remains deferred under the testing hold.

Module 3 settings save-recovery note (2026-09-05): settings saves now require a context without pending changes or an active transaction, clear previously tracked values before loading current settings, and explicitly roll back and detach all tracked entries after failure. Recovery includes entries marked unchanged by intermediate saves, generated audit entries, and IDs allocated inside a rolled-back transaction. This prevents a reused settings service from carrying failed tax/register/store changes into a later save. Store existence and an already-changed default tax are checked before tax mutation; stale tax selection requests a reload. Receipt-setting reads already use the generic repository's no-tracking query and required no change. This is not full optimistic concurrency protection for simultaneous settings edits. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests were added or run and no database changes were applied; failed-save retry and stale-settings regression scenarios remain deferred under the testing hold.

Module 3 settings usability note (2026-09-05): the owned settings form now provides a DevExpress selector for installed time-zone IDs, and service validation rejects missing or invalid system time zones before saving. Tax-history dates display in the configured store time zone with explicit column labels while stored UTC values remain unchanged. Save, printer test, and preview remain disabled until settings load; Reload settings offers recovery after an initial load failure. Printer discovery, printer actions, tax-history refresh, and revert failures receive dedicated feedback, and printer/history failures no longer prevent loaded settings from being displayed. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests were added or run, no print jobs were sent, and no database migration was required. Module 3 remains in progress pending manual acceptance and deferred verification.

Module 14 archive-recovery note (2026-09-05): the audit viewer now exposes Verify archive and Restore to new database through DevExpress file dialogs. Verification demands Audit View and checks the ZIP entry set, format/version, SHA-256, field schema, UTC timestamps, ascending unique IDs, count, maximum ID, and date coverage. Reading is bounded to 2 GiB uncompressed data, 64 KiB manifest text, and 4 Mi characters per record. Restore additionally demands Audit Add/Edit, verifies before creating anything, and holds the source file without write sharing through import. It generates a new `POS_AuditRestore_<guid>` database on the configured SQL Server and imports original audit IDs/details plus manifest metadata in one transaction, using `datetime2(7)` and UTC DataTable values. The live database is never a selectable restore target. Failure after database creation can leave the named empty database; the error identifies it, and source records remain untouched. SQL Server database-creation permission is required. Recovery databases contain audit records and archive metadata only. The Release application build passes; no archive verification, restoration, or tests were executed under the user's testing hold. Runtime validation and a restore drill remain deferred. SHA-256 detects data/manifest mismatch; it does not authenticate the archive's author.

Module 14 archive-export note (2026-09-05): the audit viewer now offers Export full archive with a DevExpress save dialog, event-count progress, cancellation, and navigation-away cancellation. The service independently requires Audit View plus Add (create archive) under the current four-action permission model. It streams all retained events in batches of 500 into `audit-events.jsonl` inside a ZIP, preserving IDs, exact old/new values, UTC timestamps, actor IDs, correlations, and register snapshots. `manifest.json` records format/version, archive ID, export actor/time, event count, highest event ID, date coverage, encoding, and SHA-256 of the uncompressed JSONL bytes. A serializable read transaction provides consistent coverage across batches and may delay audit writers while copying. Output is finalized through a unique temporary file and a non-overwriting move; cancellation/failure attempts to remove only that temporary file. No source records are modified or deleted. The application Release build passes; no tests were added or run, and no actual archive was exported during this work. Archive verification/restoration remains open.

Module 14 retention-summary note (2026-09-05): the audit viewer now offers an independently permission-protected Retention summary showing total retained events and oldest/newest local timestamps across the entire audit history. The service calculates count/min/max in one SQL aggregate query, handles an empty history, and supports cancellation. The current application policy retains all events with no automatic expiry or deletion. The summary is loaded on demand and leaves current search results intact on failure. The Release application build passes; no tests were added or run under the user's testing hold. No migration is required.

Audit retention/archival baseline: audit records stay in the operational database indefinitely until the business approves a retention duration and a separate archival workflow exists. Database backups preserve audit records together with their related transactions; the application's backup/restore module remains scheduled for M7. A future archive must preserve original IDs, UTC timestamps, attribution, correlations, and details; record its coverage and integrity information; restrict access; and support restoration into a separate database before any deletion is considered. No archive, purge, or restore action is implemented by the retention summary. This is the application's current operational behavior, not a jurisdiction-specific retention determination.

Module 14 register-attribution note (2026-09-05): automatic and explicit audit events now snapshot nullable register ID/code. Async operation scopes accept an explicit register, including checkout's supplied register; nested scopes inherit it and restore the previous register on disposal. Saves validate explicitly supplied registers as active. Without an explicit register, the current single-register deployment uses the sole active register; zero or multiple active registers produce unattributed events rather than guessed origins. The viewer displays register ID/code and supports exact historical-code filtering or unattributed-only filtering. `AddAuditRegisterAttribution` adds nullable ID/code columns and a register/date index, with no foreign key or backfill that could alter historical events. It is registered for startup migration and was not applied by this work. The Release application build passes; tests remain paused per user direction. Explicit workstation/register selection is still required before supporting multiple active registers reliably.

Module 14 category-filter note (2026-09-05): audit search now provides Security, Configuration, Catalog, Inventory, Purchasing, Sales, Customers, Cash management, and Other categories. A shared explicit entity/event-name map classifies existing and new audit records without modifying historical data or requiring another migration. The service validates the selected category and filters its entity names in SQL before counting and paging; unknown or missing entity names are included in Other. The DevExpress viewer provides an All categories option and shows the derived category on each event. The Release application build passes; no tests were added or run under the user's testing hold. Register attribution/filtering and retention/archival remain open.

Module 14 correlation note (2026-09-05): nullable indexed `AuditLog.CorrelationId` links automatic and explicit events. `AuditOperation` uses async-flow-local scopes; nested calls reuse the current ID and disposal restores the previous scope. User/security, role/module, store/sequence, inventory-movement, and checkout operations now establish scopes, including workflows with multiple Identity/EF saves. Unscoped saves get one fresh ID for their batch. The audit viewer displays and filters the exact ID. `AddAuditCorrelationId` was scaffolded and reviewed: it adds only the nullable GUID column and index, leaving historical rows null. It is registered for normal startup migration and has not been applied by this work. Application Release build verification is used; correlation behavior, async isolation, and migration tests are deferred under the user's testing hold.

Module 14 explicit-event JSON note (2026-09-05): existing authentication/session/account/password, role-permission, and user-role assignment events now use typed `AuditEventDetails` entry points and the same JSON serializer/value limits as automatic audits. Previous roles are represented as a JSON array instead of a delimiter-joined string; authentication contains only username/outcome, permissions contain role/module IDs and action flags, and assignment contains role ID/name. Event names, UTC timestamps, actor/record identifiers, transaction boundaries, and historical records are preserved. Tests parse persisted permission and authentication details, verify role transitions, and exercise quotes/newlines/separators and value bounds. Future workflow events still need explicit safe field definitions when those workflows are implemented.

Module 14 structured automatic-audit note (2026-09-05): `AuditValuePolicy` now defines explicit per-entity fields for automatic EF audits. Unknown entity types and newly added properties produce no value details until reviewed. Values use JSON with invariant string formatting, escaped text, null preservation, and a 512-character per-value limit with a truncation marker. Added/deleted events capture approved current/original fields; modified events capture only approved fields whose values changed. Excluded-only changes retain event metadata with empty JSON objects. Credentials, Identity claim values/login keys, contact details, free-form notes, payment references, and row versions are excluded. Tests verify escaping, unknown fields/types, payment-reference exclusion, invariant money, nulls, bounds, and persisted approved/excluded changes through asynchronous and synchronous saves. Historical records are retained; explicit service events still use their existing safe details format and await standardization.

Module 14 audit-viewer note (2026-09-05): a read-only DevExpress `ucAudit` now opens inside the main shell under Audit View navigation permission. Its injected `AuditService` independently demands that permission before opening a per-query context, uses the existing generic repository, and applies SQL date/text filters, bounded paging, and deterministic newest-first date/ID ordering. The viewer accepts local date bounds, displays local timestamps, and shows selected old/new details. It handles loading, empty/error states and cancels queries on disposal. Tests cover access denial before database creation, inclusive start/exclusive end, combined filters, stable page ties, missing users, details, empty results, page clamping, and invalid date ranges. Category/register filtering awaits corresponding audit schema fields; structured allowlisted details, correlation IDs, and retention remain open. Manual UI verification remains required.

Module 14 append-only note (2026-09-05): `POSContext` now rejects modified or deleted `AuditLog` entries before staging generated audit records or saving any accompanying business changes. The guard applies to synchronous and asynchronous EF saves, including generic repository callers. LocalDB regression coverage exercises edit/delete attempts through both save paths, verifies the original event and accompanying product remain unchanged, and checks that rejected saves do not stage extra audit entries. This is application-level enforcement; direct SQL/database administrator operations are outside this guard. Audit viewer/filtering, structured allowlisted details, correlation identifiers, and retention/archival remain open.

Module 3 receipt-preview note (2026-09-05): the settings form now opens an owned DevExpress report viewer with an approximately 80 mm sample receipt built from current, including unsaved, form values. It displays store/contact/tax details, register, proposed number format, sample item, inclusive/exclusive tax, configured decimal precision, PHP totals, cash/change, and footer. The document is explicitly marked `SAMPLE - NOT A SALE`. Preview requires Settings View permission, validates input, and performs no database calls, receipt allocation, save, or automatic printing. Tests cover both tax modes, precision, current settings, PHP validation, authorization denial, and generation against an unusable database endpoint without printing. Manual visual and printer verification remain open. Verification uses the Release configuration because the active Visual Studio debugging session locks Debug output files.

Store settings form note (2026-09-05): per user direction, `frmStoreSettings` now derives from `XtraForm` and opens as an owned modal dialog through `ApplicationCompositionRoot`. The current main-panel workflow stays in place. Save, revert, printer actions, tax history, and service authorization are retained; Close and Escape dismiss the form, and the composition root disposes its settings service with the form.

- [ ] Complete modules 1, 2, and 3.
- [ ] Harden module 14 for authentication, permissions, and configuration.
- [ ] Seed default roles/resources safely.
- [ ] Test login, lockout, settings, and service authorization.

Authentication progress note (2026-09-04): sign-in now uses typed authentication results, validates input, tracks failed attempts through ASP.NET Identity, locks accounts for 15 minutes after five failures, resets the counter after success, and fully populates session identity fields. The sign-in form reliably re-enables after expected failures and exceptions. Remember-me now retains only the protected username and no longer restores or rewrites passwords. Identity password hashes, security stamps, tokens, and credentials are excluded from EF audit value serialization. An isolated LocalDB integration test verifies repeated-failure lockout and audit secrecy.

Session lifecycle note (2026-09-04): claims are now the single source for current user ID, username, display name, role ID, and role name; duplicated mutable session fields were removed. The main ribbon exposes a logout action that clears the principal, closes the application shell, and returns to a fresh sign-in dialog in the same process. Focused tests verify claim projection and complete session clearing.

Password lifecycle note (2026-09-04): `UserService` now provides typed self-service password change and administrator reset operations using Identity password validation. Administrator reset replaces the hash and rotates the security stamp in one user update instead of relying on the previously unconfigured token provider. A focused DevExpress password-edit dialog is available from the main ribbon, and integration tests verify current-password checks, old-password invalidation, and administrator reset behavior.

Authentication audit note (2026-09-04): login success, invalid login, lockout, logout, password change, and administrator password reset now create explicit UTC audit records through `BaseRepository<AuditLog, long>`. Audit details contain only username/outcome metadata and never passwords, hashes, stamps, or tokens. The main-shell logout path calls the service so session clearing and audit recording cannot diverge. Integration tests verify lockout, password lifecycle, and logout audit events.

Register lock note (2026-09-04): the main shell now provides a lock action backed by a focused DevExpress credential dialog. Locking clears the active claims principal immediately, keeps the main shell inaccessible, and allows only the same username to reauthenticate; choosing logout returns to the normal sign-in loop. Lock and unlock are explicit UTC audit events, with integration coverage for session clearing and event persistence.

Account-state note (2026-09-04): administrators can now enable or disable accounts through a typed service operation. Disabled state uses ASP.NET Identity's existing lockout fields with a permanent lockout marker, avoiding a risky schema default that could disable existing users; temporary failed-login lockout remains distinct. Authentication rejects disabled users before password validation, re-enabling clears lockout counters, and disable/re-enable/rejected-login events are audited. Integration tests verify the full disable/re-enable lifecycle. User-management UI exposure remains part of module 2's single-shell redesign.

Module 1 completion note (2026-09-04): the sign-in screen now validates required credentials before database access, prevents duplicate submissions, displays DevExpress marquee progress, warns when Caps Lock is active, and always restores its controls after failed authentication. Together with lockout, disabled accounts, claims-only sessions, username-only remember-me, logout/register lock, password change/reset, safe audit events, and integration tests, the authentication/session module's planned baseline is complete.

Module 2 user-management UI note (2026-09-04): runtime user navigation now hosts `ucUsers` inside the main shell instead of opening the legacy list form. The DevExpress grid provides filtering, selection-aware actions, loading/error states, add/edit dialogs, and audited enable/disable operations. Add and edit remain focused modal interactions under the single-shell navigation standard. Stable resource codes and service-level authorization are the next module 2 slice.

Module 2 permission foundation note (2026-09-05): stable resource codes now replace form/display names for Users, Roles, Modules, Settings, Products, Inventory, Sales, and Audit. The migration seed renames legacy resources in place, consolidates duplicates without losing granted flags, idempotently creates Administrator/Manager/Cashier/Inventory Clerk roles, and applies explicit baseline permissions. `UserService`, `RoleService`, and `ModuleService` now enforce injected authorization at their read/write boundaries, with bypass-resistance tests; `ucUsers` reflects Add/Edit claims in its action states. The seed was applied to `POSV1` and verified with eight full Administrator resource grants.

Module 2 administrator-safety note (2026-09-05): user role selection is now resolved by stable role ID and passed to Identity by the corresponding role name. The custom-role Identity manager uses the application's injected `POSContext`, preventing stock `IdentityRole` queries. Service rules reject self-disablement, self-demotion, and disabling or demoting the final active System Administrator. Isolated LocalDB integration coverage verifies each rejection and permits the operation once a second active administrator exists.

Module 2 module-maintenance note (2026-09-05): module creation and editing now return typed validation results, persist the selected parent ID correctly, reject missing/self/cyclic parents, and prevent duplicate resource names. Deletion is limited to unused leaf modules; modules with children or role-permission references are protected. The existing module editor surfaces confirmation and validation feedback, and isolated LocalDB tests cover parent persistence, cycle rejection, protected deletion, and successful deletion of an unused module.

Module 2 user-query note (2026-09-05): `UserService` now exposes permission-protected, server-side user search and pagination with bounded page sizes, stable username ordering, total counts, and batched role-name resolution. The single-shell `ucUsers` view provides username/name/role search plus previous/next navigation while retaining modal add/edit interactions. Isolated LocalDB tests verify filtering, deterministic page boundaries, totals, and role-name search.

Module 2 permission-matrix UI note (2026-09-05): roles/permissions and modules now open as injected `UserControl` views inside the main application shell instead of separate management forms. The matrix presents one resource row with View/Add/Edit/Delete flags, is read-only without Edit permission, and states that claim changes apply at next sign-in. Loading a role is now side-effect free and strictly role-scoped; a missing claim is created only when an authorized edit is saved. Integration coverage verifies role isolation, read-only loading, and persistence of an edited permission.

Module 2 navigation-authorization note (2026-09-05): the main shell now receives its authorization service through the composition root and applies stable resource View claims to every existing Users, Roles, Modules, Settings, Products, Inventory, and Sales ribbon entry, including duplicated legacy buttons. Unauthorized entries are hidden while session lock, password change, and logout remain available. A framework-free policy test verifies that View exposes a resource and that Edit alone does not expose navigation; service guards remain the authoritative enforcement boundary.

Module 2 permission-audit note (2026-09-05): permission creation and updates now append explicit UTC `RolePermission` audit events through the existing generic audit repository. Claim data and its audit event share one injected context and one `SaveChanges` commit; audit values contain only role/module identifiers and View/Add/Edit/Delete booleans. Upserts prevent duplicate role/module claim rows. Integration tests verify the initial grant and a subsequent change preserve safe old/new permission values.

Module 2 role-lifecycle note (2026-09-05): role creation and renaming now return typed validation results, trim names, reject duplicates, and surface Identity failures. The stable System Administrator role cannot be renamed or deleted, and any role assigned to a user is protected from deletion; unused roles can be removed safely. The main-shell role control provides focused rename input and delete confirmation popups based on Edit/Delete claims. Integration tests cover protected administrator and assigned roles plus valid rename/deletion of an unused role.

Module 2 role-assignment note (2026-09-05): replacing a user's role now executes all Identity removals, the new assignment, and an explicit `UserRoleAssigned` audit record inside one database transaction. Selecting the already-assigned role is an idempotent no-op, and failed Identity results roll back the replacement instead of leaving a user without a role. Integration coverage verifies persisted reassignment and safe old/new role audit details.

Module 2 activity/completion note (2026-09-05): the single-shell user view now includes a recent-activity panel for the selected account. Activity queries are bounded, newest-first, filtered to events performed by or recorded against that user, and independently require Audit View permission at both UI and service boundaries. Tests verify indirect access denial, relevant event retrieval, and absence of Identity secrets. Together with stable resources, seeded roles, service guards, administrator safety, search/paging, role lifecycle, permission matrix/auditing, and authorized navigation, Module 2's planned baseline is complete.

Authentication regression note (2026-09-05): signing in a legacy account that still had Identity lockout tracking disabled attempted a user update, and EF auditing used the generated proxy class name as `AuditLog.TableName`; that exceeded the schema's 20-character limit and surfaced as the generic “sign in could not be completed” message. Auditing now unwraps EF proxies to the real entity type (`User`). A regression test covers enabling lockout during login, and the configured `admin` account was verified through authentication and claim setup against `POSV1`.

### M2 — Catalog and inventory foundation

Module 5 stock-count request idempotency/deployment note (2026-09-06): new stock-count drafts and direct postings now require a caller-generated nonempty `RequestId` retained across uncertain retries. A SHA-256 request hash covers operation kind (draft/direct post), normalized reason, and product-sorted ID/expected/counted quantities. Under the serializable write transaction, an exact same-user replay returns the original count ID without creating lines, movements, or audits; reuse with changed details, a different operation kind, or a different actor is rejected. Draft posting continues to use the persisted draft ID and reviewed row version rather than creating another request identity. History/detail DTOs expose RequestId but never the internal hash. `AddStockCountRequestId` adds nullable RequestId/RequestHash columns to preserve legacy documents and a filtered unique request index; automatic audits include RequestId but exclude the hash. Named concurrent index conflicts become reload-and-retry guidance after rollback. There were no existing stock-count rows to backfill. A COPY_ONLY CHECKSUM backup completed at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_stockcount_request_20260906_151303.bak`; generated SQL was reviewed, migration/Seed completed on `POSV1`, and metadata confirms nullable uniqueidentifier/nvarchar(64) fields plus the enabled filtered unique index. Inventory totals remain 5 products/62, 5 balances/62, and 3 movements/62. No pending explicit migrations remain. The Release application build passes with the existing `ucProducts.ProductName` warning. These are build/deployment observations, not behavioral tests. No UI changes or tests were performed. Exact/conflicting/concurrent retries, uncertain commits, audit deduplication, hash stability, rollback, and migration restore/rollback remain unverified under the testing hold. Existing test fixtures/direct backend callers must supply a retained RequestId when testing resumes. B0 remains in progress.

Module 5 adjustment request uniqueness/deployment note (2026-09-06): `AddAdjustmentRequestUniqueness` adds a filtered unique index on `StockMovements.ReferenceId` for `StockMovementType.Adjustment` only, providing database enforcement for caller-generated adjustment request IDs without restricting other movement references. The migration holds a table write lock, rejects existing duplicate adjustment request IDs with cleanup guidance, and never rewrites movement facts. `scripts/adjustment-request-preflight.sql` reports duplicates read-only. The service translates a named index conflict after rollback into guidance to reload movement history and retry the same request ID/details, allowing the existing idempotent lookup to return the committed movement on the next call. The preflight returned no conflicts. A COPY_ONLY CHECKSUM backup completed at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_adjustment_index_20260906_150828.bak`; generated SQL was reviewed, the migration and normal Seed method completed on `POSV1`, and metadata confirms the unique filtered index is enabled with the intended predicate. Post-deployment totals remain 5 products/62 product quantity, 5 balances/62 balance quantity, and 3 movements/62 ledger quantity. No pending explicit migrations remain. The Release application build passes with the existing `ucProducts.ProductName` warning. These are build/deployment observations, not behavioral tests. No UI changes or tests were performed. Concurrent duplicate submission, uncertain commit retry, conflict translation, rollback, and migration restore/rollback remain unverified under the testing hold. B0 remains in progress.

Module 5 expiry-alert query note (2026-09-06): the authorized paged inventory-alert contract now supports `ExpiringSoon` and `Expired` in addition to low/out-of-stock alerts. ExpiringSoon includes today and uses a validated 1–365 calendar-day UTC window with an exclusive end; Expired selects expiry values before current UTC date. Both include only active products with positive inventory balances and a recorded expiry date, excluding inactive products, zero stock, missing balances, and products without expiry metadata. Expiry results retain existing search/category filters, return the current expiry value, and order earliest expiry then product ID. This models one product-level expiry date; batch/lot-specific expiry quantities and store-time-zone business-date rules remain future design work. Count/page remain separate live queries. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests, UI changes, migrations, or database operations were performed. UTC/date boundaries, SQL translation, positive-stock filtering, missing dates, paging, and authorization remain unverified under the testing hold. B0 remains in progress.

Module 5 paged balance browsing note (2026-09-06): `SearchBalances(InventoryBalanceSearchDTO)` requires Inventory View and returns separate DTO pages with balance/product IDs, current product/category labels, identifiers, unit, active state, quantity, and reorder threshold. Optional trimmed name/SKU/barcode, category, and product active-state filters run in SQL with no tracking. Default scope includes active and inactive products with balances. Missing balances are excluded rather than fabricated and remain visible in reconciliation. Pages are bounded to 1–200, clamped, and ordered by product name/ID; empty results use page 1 of 1. Count/page are separate live queries and do not certify ledger reconciliation. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests, UI changes, migrations, or database operations were performed. Filter composition, paging, missing balances, SQL translation, and authorization verification remain deferred under the testing hold. B0 remains in progress.

Module 5 quantity-range validation note (2026-09-06): stock-count posting now calculates candidate variances as long values and rejects values outside the persisted movement integer range before opening its write transaction. Draft creation retains valid integer expected/count quantities even if their difference is too large to post; drafting no longer overflows while computing that difference. Posting still revalidates current inventory and uses a checked conversion when staging movements. Adjustment/opening posting now evaluates the resulting balance as a long and gives validation guidance before mutation if the result exceeds the integer balance range, instead of exposing an arithmetic overflow. Negative-stock policy remains a separate check. No quantity is clamped, split, or repaired automatically. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests, UI changes, migrations, or database operations were performed. Integer boundaries, negative expected stock, extreme draft variance, posting rejection, and rollback behavior remain unverified under the testing hold. B0 remains in progress.

Checkout conflict feedback note (2026-09-06): checkout now uses the inventory conflict classifier to translate nested EF concurrency errors and SQL deadlock/lock-timeout errors (1205/1222) into reload-stock/review-cart validation guidance after successful rollback and tracked-state cleanup. Original exceptions remain attached for diagnostics. No automatic resubmission is performed; command/connection timeouts and rollback failures propagate without implying a safe retry. Callers must create a fresh submission from the reviewed cart, since the failed entity graph may retain generated IDs, receipt numbers, and calculated fields after EF detachment. Idempotent checkout and a dedicated retry-safe cart contract remain B3 work. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests, UI changes, migrations, or database operations were performed. Concurrency/deadlock paths, failed-save identity values, rollback failure, and caller recovery remain unverified under the testing hold. B0 remains in progress.

Checkout ledger-reconciliation guard note (2026-09-06): checkout now compares each distinct product's persisted movement sum with its inventory balance before that product's first deduction, inside the existing serializable transaction. SQL sums use bigint, and disagreement produces reconciliation guidance before checkout can commit. Repeated lines for the same product reuse the validated state and continue deducting from the tracked balance; pending sale movements are not incorrectly compared against the persisted ledger on subsequent lines. Existing legacy-product/balance checks, stock sufficiency, ordered product processing, and rollback cleanup remain in place. No stock repair or movement backfill is performed. This adds one ledger aggregate per distinct product and may increase lock duration; performance/concurrency verification remains deferred. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests, UI changes, migrations, or database operations were performed. Mismatched ledgers, repeated lines, later-line rollback, empty ledgers, and concurrent stock activity remain unverified under the testing hold. Checkout payments, shifts, and idempotency remain future backend scope; B0 remains in progress.

Module 5 movement conflict feedback note (2026-09-06): adjustment and opening-balance posting now recognize nested EF concurrency exceptions and SQL deadlock/lock-timeout errors (1205/1222) after successful rollback and tracked-state cleanup. Validation feedback directs callers to reload inventory/movement history; adjustment retries must retain the original request ID and details, while changed requests require fresh review. Opening retries remain subject to the existing zero-balance/no-history guard. Original exceptions are retained for diagnostics. No automatic retry or quantity rewrite is introduced, and command/connection timeouts or rollback failures are not reported as recovered conflicts. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests, UI changes, migrations, or database operations were performed. Concurrent adjustment/opening, deadlock/timeout paths, replay validation, and context recovery remain unverified under the testing hold. B0 remains in progress.

Module 5 stock-count conflict feedback note (2026-09-06): stock-count create/post, draft editing, and cancellation now translate EF optimistic-concurrency conflicts and SQL deadlock/lock-timeout errors (1205/1222) into reload/review validation guidance after successful rollback and tracked-state cleanup. Nested SQL errors are inspected, original exceptions remain attached for diagnostics, and other failures propagate unchanged. Rollback failures still propagate after cleanup rather than being reported as recovered conflicts. No automatic replay is introduced; command/connection timeouts are not classified as retry-safe. Draft editing also copies the supplied eight-byte revision at entry and uses the shared reviewed-version guard, matching post/cancel behavior. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests, UI changes, migrations, or database operations were performed. Concurrent edit/post/cancel, deadlock/timeout paths, rollback failure, reused-context recovery, and version-copy behavior remain unverified under the testing hold. B0 remains in progress.

Module 5 stock-count reason audit note (2026-09-06): draft creation, direct posting, and draft posting now append a structured `StockCount` operational audit event before the final save, inside the document/stock transaction. `CountDraftCreated`/`CountPosted` events retain count ID, validated trimmed reason, line count, resulting state, authenticated actor, and UTC operation time; the existing audit save pipeline supplies correlation/register attribution. Drafts and all-zero-variance counts therefore retain their reason even without a movement. Posting an existing draft records the supplied posting reason separately from the original draft reason; prior events remain immutable. No zero-value movements or historical backfill are created. This uses existing audit storage, not a new document reason field; reason access remains through authorized audit queries. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests, UI changes, migrations, or database operations were performed. Atomic reason persistence/rollback, zero variance, draft/post attribution, and structured detail verification remain deferred under the testing hold. B0 remains in progress.

Module 5 stock-count history filters note (2026-09-06): `SearchHistory(StockCountSearchDTO)` requires Inventory View and combines optional Draft/Posted/Cancelled status, inclusive UTC creation start/exclusive end, exact creator ID, and contained-product filters in SQL. Unknown/numeric statuses, non-UTC or reversed date ranges, invalid product IDs, and overlong creator IDs are rejected before querying. Product filtering selects documents without reducing their reported total line count. Existing bounded pages, newest-first creation/ID ordering, UTC output, and row-version tokens are retained; `GetHistory` delegates to the new contract with no filters. Count and page reads remain separate live queries. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests, UI changes, migrations, or database operations were performed. SQL translation, combined filters, date boundaries, paging, and authorization remain unverified under the testing hold. B0 remains in progress.

Module 5 paged stock-alert note (2026-09-06): `InventoryLedgerService.SearchAlerts(InventoryAlertSearchDTO)` requires Inventory View and returns model DTO pages from inventory balances for active products. LowStock means quantity at or below the product reorder threshold; OutOfStock means quantity at or below zero, including negative inventory. Optional category and trimmed name/SKU/barcode filters execute in SQL; missing balances are excluded rather than represented as zero and remain discoverable through reconciliation. Inactive categories do not suppress their active products. Queries return current labels, quantity, unit, threshold, and category, ordered by quantity then product ID with page sizes bounded to 1–200 and clamped page numbers. Empty results use page 1 of 1. Count/page are separate live queries; these alerts do not certify ledger reconciliation. Legacy alerts remain available for existing callers. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests, UI changes, migrations, or database operations were performed. SQL translation, threshold/negative/zero boundaries, filters, paging, missing balances, and permission checks remain unverified under the testing hold. Expiry/batch workflows and broader inventory completion remain open; B0 remains in progress.

Module 4 category stale-edit protection note (2026-09-06): category list and paged-search results now include an opaque content revision covering ID, name, description, and active state. Update/delete operations compare the supplied revision against freshly loaded state inside the serializable catalog transaction; missing/stale revisions require reload before mutation. Deactivate/reactivate now require `(categoryId, revision)` and check revision before same-state no-op handling. Callers must retain the loaded revision and reload after writes. The existing category editor already round-trips its model and reloads after writes; no UI file changes were needed, and the compatibility model revision is marked non-browsable. Hash inputs use length-prefixed, null-aware encoding. This protects current field values, not all intervening history: an edit reverted to identical values produces the same revision; product-reference changes are separately checked during deletion. No schema migration is needed. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests or database operations were performed. Stale edit/delete/state changes, missing revisions, concurrent operations, no-op behavior, and existing caller binding remain unverified under the testing hold; direct-construction test fixtures must retain revisions when testing resumes. B0 remains in progress.

Catalog migration deployment note (2026-09-06): per explicit user authorization, applied `AddProductIdentifierUniqueness`, `AddCategoryActiveState`, `PreventCategoryProductCascadeDelete`, and `AddCategoryNameUniqueness` to `POSV1` on `localhost\SQLEXPRESS` using EF6 with an explicit connection and latest named migration target. Both read-only preflight reports returned no conflicts or blank category names. A COPY_ONLY database backup with CHECKSUM completed before deployment at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_catalog_20260906_120503.bak`. Reviewed generated SQL before applying; EF completed all four migrations and its normal Seed method. Post-deployment metadata confirms all four migration-history entries, three enabled unique indexes, and an enabled/trusted category foreign key with NO_ACTION deletion. All 3 categories are active; 5 products, quantity 62, selling-price sum 213.00, cost-price sum 204.00, 0 sales, inventory balance sum 62, and 3 movements totaling 62 remain unchanged. These are deployment observations, not behavioral test results. The Release application build passed. No UI changes or tests were performed; restore drills, migration rollback, concurrency, and service/runtime regression checks remain deferred. This deployment supersedes earlier unapplied notes for these four migrations only; B0 remains in progress.

Module 4 paged category-query note (2026-09-06): `SearchCategories(CategorySearchDTO)` requires Products View before database access and returns separate model DTOs with category details, active state, and product-reference counts including inactive products. Optional trimmed name search and active-state filters execute in SQL; name matching follows database collation. Search terms are limited to 100 characters, page sizes are bounded to 1–200, pages are clamped, and results use deterministic name/ID ordering. Empty results return page 1 of 1. Product counts are projected in SQL rather than loading each category's products. Count and page queries are separate live reads; reference counts do not replace transactional deletion checks. Legacy category APIs remain available for existing callers. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed. SQL translation, filters, paging boundaries, reference counts, and authorization verification remain deferred under the testing hold. B0 remains in progress, including stale-edit protection and broader inventory completion.

Module 4 category-name constraint note (2026-09-06): `AddCategoryNameUniqueness` adds a persisted normalized name and filtered unique index across active and inactive categories, using case-insensitive, accent-sensitive comparison after trimming ordinary SQL spaces. Null/empty/space-only legacy names remain excluded; normal service writes still require a name. The migration checks duplicates while holding a table write lock and stops with cleanup guidance without renaming/deleting records. `scripts/category-name-preflight.sql` reports blank names and duplicate groups without modifying data and can run before the category active-state migration. Service duplicate checks now explicitly use the same collation inside the catalog transaction, independent of database default collation; named index violations become validation feedback after rollback. The SQL-only migration retains the preceding EF snapshot and is registered for startup deployment, but neither migration nor preflight was executed. Down removes only the added index/computed column. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes or tests were added/run and no database operations were performed. SQL/index compatibility, collation/whitespace boundaries, concurrent create/rename, migration rollback, and validation recovery remain unverified under the testing hold. Database enforcement remains pending deployment; stale-edit protection and other B0 work remain open.

Module 4 category foreign-key protection note (2026-09-06): the Category–Product EF relationship now disables cascade deletion. `PreventCategoryProductCascadeDelete` replaces the existing Products.CategoryId foreign key with a non-cascading constraint and includes the corresponding conceptual/store model snapshot change. After deployment, database deletion of a category referenced by any product is rejected, including references from inactive products. No product/category rows or indexes are removed; existing service reference checks and explicit deactivation remain in place. The migration is registered after `AddCategoryActiveState` for normal startup deployment but was not applied. Deployment may require schema locks and will fail if existing references are invalid; review against a backed-up target when verification resumes. Down restores the former cascade behavior, so prefer forward recovery and review rollback carefully. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests, UI changes, or database operations were performed. Referenced/unreferenced deletion, concurrent assignment, EF model/schema compatibility, migration application, and rollback verification remain deferred. Category-name uniqueness constraints and stale-edit protection remain open; B0 is still in progress.

Module 4 category lifecycle note (2026-09-06): `DeactivateCategory` and `ReactivateCategory` require Products Delete/Edit respectively and update only category active state in the shared serializable transaction/audit scope. Repeated same-state requests are no-ops; missing IDs require reload. `GetCategories(bool?)` exposes optional active-state filtering; the legacy all-category query retains all records and now returns their state. Creation always starts active and ordinary category edits cannot change state. Product creation and reassignment require an active category; editing an existing product may retain its inactive category. Deactivating a category preserves its products, stock, sale eligibility, and references. Existing protected deletion behavior remains available for unreferenced categories. Automatic audit allowlists include category state. `AddCategoryActiveState` adds a required Boolean column defaulting all existing categories to active, with an updated EF model snapshot. It is registered for startup migration but was not applied. Down removes lifecycle state, so export that state before rollback; database cascade removal, category-name constraints, and stale-edit protection remain open. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI files or tests were changed, and no database operations were executed. Permissions, concurrent assignment/deactivation, no-op audits, rollback, model/schema compatibility, and migration recovery verification remain deferred. B0 remains in progress.

Module 4 category-write transaction note (2026-09-06): category creation and renaming now check duplicate trimmed names inside the same serializable transaction that persists the change. Required-field and ID validation remains before database access. Updates save tracked property changes rather than marking the entire category modified. This closes the service check/write gap and retains automatic audit and rollback cleanup. Concurrent writers may block or receive a deadlock failure; automatic retry, database-enforced category-name uniqueness, stale-edit protection, category deactivation, and removal of the cascading category foreign key remain open. Existing duplicate data is not repaired. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed. Concurrent create/rename, no-op audit, and rollback verification remain deferred under the testing hold. B0 remains in progress.

Module 5 paged movement-history note (2026-09-05): `SearchMovements(StockMovementSearchDTO)` requires Inventory View and combines optional product/type, inclusive UTC start/exclusive UTC end, and exact source-reference filters in SQL. It validates date kinds/ranges, movement types, IDs, and reference lengths, bounds pages to 1–200 rows, and orders by timestamp/ID newest-first. DTOs expose immutable movement facts and attribution alongside explicitly current product labels. Count and data are separate live queries; existing bounded `GetMovements` remains available. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed. EF translation, combined filters, date boundaries, permissions, and paging verification remain deferred.

Module 5 inventory-query source note (2026-09-05): catalog projections and paged product search now read quantity from `InventoryBalance`. Product quantity DTOs are nullable and expose `MissingInventoryBalance`, preserving the distinction between zero stock and a missing balance. Low-stock queries use active products with actual balances and no longer fall back to legacy product quantities. `GetAllStocks` now projects inventory balances with product names, units, and reorder thresholds instead of the legacy Stock table; returned row IDs are inventory-balance IDs. The legacy database fields/tables remain for compatibility and reconciliation, without migration or cleanup. Missing balances remain visible through catalog/reconciliation queries and are excluded from stock/low-stock lists. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI files or tests were changed and no database operations were performed. EF projection, missing-balance, low-stock, and existing UI binding verification remain deferred.

Module 5 movement-entry restrictions note (2026-09-05): generic `PostMovement` now accepts only controlled opening inventory. Adjustment and stock-count callers must use their dedicated services; sale, receiving, and return movement kinds are rejected outside their document workflows. Opening inventory requires a positive quantity, reason/source reference, existing zero balance, and no prior movements, checked inside the serializable transaction. All remaining movement-core writes verify ledger/balance agreement. This prevents using a generic movement type to bypass reviewed adjustments or count documents. Opening inventory does not repair preexisting mismatches and is not a recurring receiving workflow. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed. Workflow-bypass, repeated/concurrent opening, and ledger consistency verification remain deferred.

Module 5 adjustment retry note (2026-09-05): manual adjustments now require a caller-generated nonempty `RequestId`, retained for retries. The movement stores it as ReferenceId and encodes the reviewed expected quantity in `ManualAdjustment:<quantity>` ReferenceType. Before reading/changing stock, posting looks for the request under the same serializable transaction. An exact replay of product, delta, reason, actor, and expected quantity returns the existing movement without saving; conflicting reuse is rejected. Existing unkeyed movements are not retroactively deduplicated. This is service-transaction enforcement, not a new database unique constraint; concurrent submissions may block or yield a deadlock victim, which must retry with the same ID. Request indexing and broader workflow idempotency remain open. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed. Retry, conflicting reuse, uncertain-commit recovery, and concurrency verification remain deferred.

Module 5 reviewed-count transitions note (2026-09-05): `PostDraft` and `CancelDraft` now require the eight-byte row version returned by count history/details. They validate and copy the supplied token, compare it with the persisted header inside the serializable write transaction, and reject a changed document before transition. Posting also checks authentication before reading lines. Cancellation no longer treats an already-cancelled document as a successful no-op; non-draft transitions require reload/status review. No UI callers currently use these backend methods. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed. Reviewed-version rejection and concurrent edit/post/cancel verification remain deferred.

Module 5 draft-count editing note (2026-09-05): `UpdateDraft(StockCountDraftUpdateDTO)` now accepts bounded partial updates to existing draft lines, requires Inventory Edit/an authenticated actor, and compares the supplied eight-byte document row version before changing counted quantities. Posted/cancelled counts, duplicate/missing products, negative counts, and attempts to rewrite expected quantities are rejected. Changes touch the header so its existing row-version column advances together with line edits and audit records in one transaction; no-op edits do not save. History/detail DTOs expose the version for later callers, which must reload after saving. Stock and movements remain unchanged. Product membership/expected snapshots are intentionally fixed; a count whose expected stock is stale must be cancelled and recreated after review. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed. Stale-version, partial-edit, post/edit concurrency, and audit/rollback checks remain deferred.

Module 5 stock-count draft lifecycle note (2026-09-05): `CreateDraft` persists a count header and reviewed/count quantities without changing balances or creating movements. `PostDraft` uses persisted lines, checks that the draft and its lines still match, revalidates current balances/ledger, and posts variances against the same count ID in one transaction. Posted/cancelled drafts cannot be posted again. `CancelDraft` changes only Draft to Cancelled, preserves lines, leaves stock unchanged, and treats an already-cancelled count as a no-op. Operations require Inventory Edit and an authenticated actor; original creator/time are retained on posting, with posting attribution in movements/automatic audit. Drafts are visible through history/detail queries. The existing schema has no document-level reason or dedicated posting-user field; a reason is supplied again at posting and persisted on nonzero movements. Draft editing and approval remain pending. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed. State transitions, concurrent post/cancel, stale draft rejection, and rollback verification remain deferred.

Module 5 stock-count query note (2026-09-05): `StockCountService.GetHistory` and `GetDetails` now require Inventory View and return bounded, no-tracking DTO pages. History includes status, actor ID, UTC timestamps, and line count, ordered newest-first with ID ties. Detail pages return stored expected/physical quantities and long-valued variance, ordered by product/line ID. Product names/SKUs are explicitly labeled current catalog values rather than historical snapshots. Missing count IDs produce validation feedback. Count/page reads are separate live queries. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed. Query translation, paging, authorization, and quantity-history verification remain deferred.

Module 5 stock-count posting note (2026-09-05): `StockCountService.PostCount` accepts 1–500 distinct product lines with reviewed expected quantities, nonnegative physical quantities, and a required reason. It demands Inventory Edit and an authenticated actor before database access. Under one serializable transaction it creates a posted count document/lines, rejects stale or unreconciled stock, sets balance and transitional product quantities to the count, and creates nonzero variance movements linked by count ID. Equal quantities retain count lines without zero-value movements; the existing schema stores the reason on variance movements, so an all-zero count has no persisted reason field. Failure clears all tracked state after rollback, including the intermediate header save. Both active and inactive products can be counted. This slice implements direct posting only; persisted drafts, cancellation, count retrieval, approval, and request idempotency remain open. Expected-quantity checks do not detect intervening activity returning to the same quantity. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes or tests were added and no migrations or database operations were executed. Multi-line rollback, zero variance, stale counts, concurrent sales, and ledger reconciliation verification remain deferred.

Module 5 manual-adjustment note (2026-09-05): `PostAdjustment(StockAdjustmentDTO)` now posts a nonzero delta with a mandatory reason and expected reviewed quantity under Inventory Edit. Inside the movement transaction it rejects changed quantities and disagreement among product quantity, balance, and summed ledger; then it updates both quantity records and appends an Adjustment movement attributed to the authenticated user. The generic movement entry point directs Adjustment callers to this contract. All movement posting now requires an authenticated actor and exactly one store configuration for negative-stock policy. Expected quantity detects a changed balance, not intervening activity that returns to the same quantity; operation idempotency/approval thresholds remain future work. The generic posting entry point still supports other movement kinds pending workflow-specific restrictions. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed. Adjustment success/rollback, stale review, ledger mismatch, policy, and concurrent posting verification remain deferred.

Module 5 reconciliation-query note (2026-09-05): `GetReconciliation(InventoryReconciliationSearchDTO)` requires Inventory View and returns bounded pages comparing each product's legacy quantity, nullable inventory balance, and summed ledger quantity. It defaults to mismatches only, supports an optional product ID and all-row mode, includes inactive products, and identifies missing balances without substituting zero. SQL sums use bigint; DTO differences use long to avoid integer overflow. Results order by product ID and include page/count metadata. The query performs no repairs, opening movements, or writes. Count and page are separate live queries, so concurrent changes may affect the returned page; this is diagnostic information, not a transactional reconciliation certificate. Orphan ledger rows outside valid product foreign keys are outside this product-based query. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed. EF translation, mismatch coverage, permissions, paging, and performance checks remain deferred.

Module 5 append-only ledger note (2026-09-05): `POSContext` rejects modified or deleted `StockMovement` entries before generating audit records or saving accompanying business changes. The shared save hook covers synchronous and asynchronous EF saves and generic repository callers. Corrections must be new movements so the original ledger history remains intact. This is application-level enforcement; raw SQL, database administrator actions, and database cascades are outside the tracked-entry guard. No schema migration or UI change was made. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests were added or run and no database operations were executed; edit/delete bypass attempts, both save paths, and accompanying-write rollback checks remain deferred.

Checkout authorization/attribution note (2026-09-05): `SalesService` now accepts injected `ICurrentUser` and `IAuthorizationService` alongside its context/clock. `CreateSale` demands Sales Add before validating input or opening a transaction and independently requires an authenticated, nonempty cashier ID. The cashier is captured once at entry and assigned to both sale and stock movements, replacing caller-supplied attribution. Default constructors retain claims-backed behavior; receipt-sequence dependency uses the same authorization instance without imposing Settings permission on checkout. Injected composition must provide matching user/clock dependencies to `POSContext` for automatic audit attribution. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed. Denied-call, anonymous-cashier, forged-cashier, and audit-consistency verification remains deferred. This completes authorization for the current checkout entry point; future payment/shift/return workflows still require their own guards.

Module 5 checkout-ledger note (2026-09-05): checkout now reads the inventory balance as its stock source, rejects missing balances or disagreement with the transitional product quantity, decrements the balance, synchronizes product quantity, and stages negative Sale movements per sale line. Movements reference the unique receipt number (`SaleReceipt`), share the sale UTC timestamp/current-session cashier, and commit with sale/lines, sequence, and automatic audits inside the existing serializable transaction. Product IDs are processed in order and EF persists only changed product fields. Checkout rejects a context with pending changes or an active transaction, clears stale tracking before loading, and detaches all operation state after rollback, including entries marked unchanged by an attempted save. Persisted sale IDs and invalid/null lines are rejected. Checkout still disallows overselling; payments, shift enforcement, service authorization, injected cashier attribution, idempotency, deadlock/conflict feedback, and reconciliation of older mismatches remain open backend work. No legacy stock was repaired or invented. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes or tests were added, and no database operations or migrations were executed. Atomic rollback, duplicate lines, final-unit concurrency, and ledger reconciliation verification remain deferred.

Module 5 balance-lifecycle note (2026-09-05): new products now receive a zero-quantity inventory balance in the same catalog transaction; no zero-value movement is created. `InventoryLedgerService` now enforces injected Inventory View for reads and Inventory Edit for posting, validates movement types/reference lengths, returns deterministic movement ordering, and reports missing balances instead of treating them as zero or silently copying legacy stock. Posting rejects disagreement between legacy product quantity and the balance, uses a serializable transaction, and clears rolled-back tracked state after failure. Existing data is not backfilled by this change. Checkout still writes legacy product quantity only and remains the next ledger-integration gap; after such a checkout the new consistency check can reject movements until reconciliation. Workflow-specific movement rules, idempotency, reconciliation, and append-only ledger enforcement remain unfinished. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes or tests were added, no migrations were required, and no database operations were executed. Permission, missing-balance, creation rollback, and concurrent-movement verification remain deferred.

Module 4 backend price-history note (2026-09-05): `GetProductPriceHistory` exposes existing append-only Product audit records through separate price-history DTOs, requiring both Products View and Audit View before querying. It returns initial Added events and structured Modified events mentioning Price/CostPrice, newest-first by UTC timestamp/ID, with bounded pages, before/after monetary values, actor ID, register code, and correlation ID. Automatic audits already persist catalog price changes in the catalog transaction, so no duplicate history table or migration was added. Missing unchanged values remain null rather than being reconstructed from current prices. Parsing is bounded and uses invariant decimal values; included records with unreadable/incomplete details are marked unavailable. Legacy modified events without the structured JSON field names are not included, so this is not a guaranteed complete historical ledger or effective-dated pricing system. Count/page reads are live separate queries. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes or tests were added, and no database operations were executed. Persistence, history parsing, permissions, and paging verification remain deferred.

Module 4 identifier-constraint migration note (2026-09-05): `AddProductIdentifierUniqueness` is registered as a SQL-only EF migration retaining the preceding conceptual model snapshot. It adds persisted normalized SKU/barcode keys with filtered unique indexes using case-insensitive, accent-sensitive comparison after trimming ordinary surrounding spaces. Active and inactive records share the identifier namespace; null/empty/space-only legacy identifiers remain excluded. A SKU length check prevents truncation beyond 50 characters. The migration checks overlong SKUs and duplicate keys under a table write lock and fails with cleanup guidance instead of modifying product values or transaction snapshots. `scripts/product-identifier-preflight.sql` reports affected rows without writing. Down removes only the added indexes, computed columns, and length constraint. Catalog writes translate the named index conflicts into validation errors after rollback. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes or tests were added, and no SQL report or migration was executed. Database enforcement is pending deployment; SQL/index compatibility, concurrent writes, and migration rollback must be verified when testing resumes. Normal application startup will attempt this migration, so review the preflight against a backed-up target before deployment. Whitespace normalization is SQL space trimming, not arbitrary Unicode/control-character normalization.

Module 4 backend SKU/unit note (2026-09-05): `UpdateProductIdentifiers(ProductIdentifiersDTO)` now requires Products Edit, validates a required SKU/unit with 50/30-character limits and no control characters, normalizes SKU to uppercase, and rejects an SKU already used by another active or inactive product. Identifier writes change only SKU/unit and participate in the catalog transaction, automatic audit, and rollback recovery. Product creation accepts optional SKU/unit fields, defaults an omitted unit to `piece`, and now validates/writes inside the same catalog transaction; omitted SKU remains supported for the existing UI until U1. Unit is a label only and does not convert existing quantities. Ordinary catalog updates continue to preserve identifiers. Duplicate checks run inside serializable writes but database-enforced uniqueness and existing-data migration remain pending. Existing product rows are not backfilled or altered by this work. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed; SKU normalization, duplicate/concurrent updates, authorization, and audit verification remain deferred.

Module 4 backend search note (2026-09-05): `SearchProducts(ProductSearchDTO)` now enforces Products View before querying and combines trimmed name/SKU/barcode text search with optional category and active-state filters in SQL. Page sizes are bounded to 1–200, page numbers are clamped to the available range, and results use deterministic name/ID ordering. Separate model DTOs return a bounded product page, total count, page metadata, category name, catalog identifiers, pricing, status, expiry, and transitional product quantity. Empty results use page 1 of 1 with zero items. Count and page reads are separate queries, so concurrent writes may change results between them; this is a live catalog view, not a snapshot. Legacy UI queries remain available until U1. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, database execution, or migrations were performed. Filter, boundary, permission, and query-performance verification remains deferred.

Module 4 backend product-lifecycle note (2026-09-05): `UpdateProductDetails(InventoryDTO)` loads the persisted product and assigns only name, description, selling/cost prices, barcode, category, and reorder threshold. The legacy entity-based update delegates to that contract; supplied stock quantity, active state, SKU, unit, and expiry cannot overwrite persisted values. EF saves only changed fields inside the shared catalog transaction/recovery scope. `DeleteProduct` now delegates to deactivation, preserving the product and all references; explicit deactivate/reactivate operations require Products Delete/Edit respectively and are idempotent. Low-stock alerts exclude inactive products and checkout rejects inactive products it loads. Existing inactive records are not automatically reactivated. No UI controls were added or changed. No schema migration was required. Runtime/concurrent checkout-deactivation verification and legacy-data review remain deferred under the testing hold; broader checkout concurrency remains unfinished. SKU/unit editing and price history are upcoming backend work.

Module 4 category-safety note (2026-09-05): category writes now validate IDs, required names and the 100-character name limit, trim names/descriptions, and reject an already-used name through an application-level check. Missing update/delete targets report reload guidance. Deletion rejects every category referenced by a product, including inactive products, with the reference check and deletion held inside a serializable transaction. Category writes establish an audit scope and discard rolled-back tracked state after failure; category reads are ordered and no-tracking. The legacy cascading foreign key remains unchanged, so direct SQL deletion is outside this service protection; a reviewed schema change and category deactivation remain open. Name uniqueness is not yet database-enforced. The Release application build passes with the existing `ucProducts.ProductName` warning. A stray character after a using directive was also removed to restore compilation. No tests were added or run and no database operations were executed; deletion/concurrent-assignment and rollback verification remain deferred.

Module 4 product-validation note (2026-09-05): product creation and update now validate required fields, mapped text limits, positive prices within SQL decimal(18,2) capacity, two-decimal precision, nonnegative reorder thresholds, existing categories, and duplicate trimmed barcodes before repository writes. Creation rejects supplied existing IDs; updates require an existing product. Names/barcodes/descriptions are normalized and new products start active. The add dialog closes successfully after persistence so the catalog refreshes and the same form cannot accidentally resubmit that product. Duplicate-barcode validation is currently an application check, not a concurrent uniqueness guarantee; a reviewed database constraint and existing-data cleanup remain open. SKU maintenance, safe update field boundaries, deactivation, and category validation remain open. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests were added or run and no database operations were executed.

Module 4 catalog-authorization note (2026-09-05): `InventoryService` now accepts injected authorization and demands Products View/Add/Edit/Delete before the corresponding product/category repository operations. Product-based low-stock alerts require Products View; the legacy stock list requires Inventory View. Product add/save controls and category add/edit/delete affordances reflect claims, including separate permissions for new versus existing category rows. Product/category/stock screens now catch permission and operation failures and show DevExpress feedback. The main-shell stock alert no longer triggers a redundant product-list reload. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests were added or run; bypass-resistance and restricted-user UI checks remain deferred. Product/category validation, safe deactivation/reference protection, and inventory-write rules remain separate unfinished slices.

Module 4 catalog-query note (2026-09-05): the catalog, category-filtered catalog, and low-stock list now share an ordered, no-tracking EF projection with category navigation instead of materializing products and repeatedly loading each category. The projection populates category ID/name, barcode, SKU, unit, cost, selling price, quantity, active state, and reorder threshold. The product grid now binds previously unbound barcode/cost/status columns, displays SKU/unit, and provides the hidden threshold field used by low-stock highlighting. It is read-only, loads once on entry, shows load failures, and refreshes after its owned/disposed add-product dialog closes. This begins M2 implementation while M1 verification remains deferred under the testing hold; modules 3 and 14 are not marked complete. Pagination, catalog write validation/authorization, uniqueness, editing/deactivation, price history, and ledger quantity integration remain open. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests were added or run and no database operations were executed.

- [ ] Complete modules 4 and 5.
- [ ] Migrate existing quantities to inventory balance/ledger.
- [ ] Complete product maintenance, adjustments, counts, and stock alerts.
- [ ] Test stock concurrency and reconciliation.

### M3 — Supply workflow

Module 12 checkout-shift enforcement/B2 transition note (2026-09-06): `SalesService.CreateSale` now requires positive register and shift identifiers before checkout and verifies the selected register is active, the shift exists and is Open, the shift belongs to that register, and the authenticated cashier owns it. An optional linked customer requires Customers View and must still be active; a null customer remains the supported walk-in representation. These checks execute inside the existing serializable checkout transaction before stock or financial mutations, so a concurrent shift close must serialize with checkout. The service continues to force the persisted sale to Completed and assigns cashier attribution from the authenticated session. Refund-to-shift enforcement remains part of B4 when the refund posting workflow is implemented. No schema migration or database write was required. The Release application build passes with 20 existing warnings (19 missing Syncfusion references and `ucProducts.ProductName` hiding its inherited member) and zero errors. No UI changes or tests were performed. Register/shift/customer rejection, checkout-versus-close concurrency, permissions, audit attribution, and rollback remain unverified under the testing hold. The planned B2 customer, register, and shift prerequisite backend scope is now implemented with verification deferred; active backend delivery advances to B3 sales, tenders, and receipt data.

Module 12 shift-query/close note (2026-09-06): `CashierShiftService.GetDetails` and bounded `Search` now require Shifts View and return register/cashier labels, UTC lifecycle facts, financial values, and the EF row-version as an opaque base64 revision. Open-shift reads calculate live expected cash from Opening and CashIn less CashOut movements, completed Cash payments, and cash refunds belonging to Posted/Completed sale-return documents; closed rows retain their stored closing snapshot. `Close` requires Shifts Edit, an authenticated actor, reusable request ID, reviewed row version, and bounded nonnegative two-decimal counted cash. Inside one serializable transaction it recomputes expected cash, derives variance, requires Shifts Delete as the current variance-approval permission when variance is nonzero, records expected/counted/variance/ClosedUtc/status, and appends a register-attributed Closing movement with the physical count. Exact close retries return successfully; changed request reuse, stale/closed shifts, concurrent movement IDs, and EF row-version conflicts are rejected. Closing does not create sales, payments, or refunds. Search currently issues live expected-cash aggregates separately for each open row on its bounded page; batching is a future performance refinement. No migration or database write was required beyond the already deployed cash-movement request foundation. The Release application and Services builds pass with existing application warnings only. No UI changes or tests were performed. Query translation, payment/refund reconciliation, pagination performance, closing/variance permissions, retry/concurrency, audit, and rollback remain unverified under the testing hold. B2 remains in progress; checkout and future refund operations must enforce the selected open shift/register before this phase transitions.

Module 12 cash-in/out/deployment note (2026-09-06): `CashierShiftService.PostCashMovement` now requires Shifts Edit and an authenticated actor, a reusable request ID, current base64 row-version review token, positive bounded two-decimal amount, and a required reason up to 250 characters. Only Open shifts accept activity. Cash-in increases expected cash; cash-out decreases it and cannot reduce expected cash below zero. The movement and expected-cash update commit together inside the shift serializable transaction, with the audit explicitly attributed to the shift register. Exact request retries return the original movement; changed reuse and stale shifts are rejected, and EF row-version plus the unique movement request index handle concurrent writes. `AddCashMovementRequestId` adds a nullable RequestId, assigns unique NEWID values to legacy movements, makes it required, and creates a unique index. Generated SQL was reviewed. Preflight found zero shifts and cash movements. A COPY_ONLY CHECKSUM backup was created and verified at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_cash_movement_request_20260906_202345.bak`; migration/Seed completed, required uniqueidentifier/index metadata was verified, and shift/cash counts plus reconciled inventory totals remained unchanged. The Release application and final Services build pass with existing application warnings only. No UI changes or tests were performed. Cash-in/out behavior, revision refresh, exact/concurrent retries, cash-out boundaries, audit attribution, rollback, and migration restore remain unverified under the testing hold. B2 remains in progress; shift queries, sales/refund-derived expected cash, and close/variance are next.

Module 12 shift-opening/deployment note (2026-09-06): `CashierShiftService.Open` now requires Shifts Add and an authenticated cashier, validates a reusable nonempty request ID, active register, and bounded nonnegative two-decimal opening cash, and opens the shift under a register-attributed audit scope. One serializable transaction creates the Open shift, snapshots cashier/register/time, initializes expected cash to the opening float, and records an Opening cash movement even when the float is zero. Exact request retries return the original shift; changed request reuse is rejected. Application checks plus filtered unique indexes enforce at most one Open shift per register and per cashier, while a unique request-ID index supplies the concurrent idempotency boundary. The stable Shifts permission resource grants System Administrator all actions, Manager View/Add/Edit/Delete, and Cashier View/Add/Edit. `AddShiftOpeningGuards` adds nullable RequestId, assigns a unique NEWID to every legacy shift, makes it required, and creates `IX_RequestId`, `IX_OpenShiftRegister`, and `IX_OpenShiftCashier`; rollback removes only those indexes and the request column. Preflight found zero shifts and no open duplicates. Generated SQL was reviewed. A COPY_ONLY CHECKSUM backup was created and verified at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_shift_opening_guards_20260906_201716.bak`; migration/Seed completed, column/index/permission metadata was verified, and inventory totals remain reconciled at 62. The Release application build passes with the existing missing Syncfusion-reference warnings and `ucProducts.ProductName` warning. No UI changes or tests were performed. Opening behavior, zero/nonzero movements, exact/concurrent retries, exclusivity, audit attribution, rollback, and migration restore remain unverified under the testing hold. B2 remains in progress; cash-in/out, shift queries, expected cash, and close/variance are next.

Module 3 register-station backend note (2026-09-06): `RegisterStationService` now provides Settings-authorized paged search, details, create/update, and revision-protected activate/deactivate operations independently of WinForms. Codes are trimmed, uppercased, and reserved across active and inactive registers; name/printer lengths and control characters are validated. Writes execute inside a serializable audit transaction with tracked-state cleanup, application duplicate checks, and translation of concurrent `IX_Code` violations. New registers start active. Deactivation is rejected while the register owns any Open cashier shift, and all referenced historical sales, shifts, cash movements, and audits remain intact. This service permits multiple active registers; explicit workstation/register selection and removal of the existing StoreSettings first-register assumption remain required before multi-register runtime use. No schema migration or database write was required. The Release application build passes with the existing missing Syncfusion-reference warnings and `ucProducts.ProductName` warning. No UI changes or tests were performed. Validation, revisions, duplicate races, permissions, open-shift protection, audit attribution, and multi-register behavior remain unverified under the testing hold. B2 remains in progress; cashier-shift open/close and cash movements are next.

Module 13 customer foundation/history/permission note (2026-09-06): `CustomerService` now provides Customers-authorized backend create/update, deterministic paged search across code/name/phone/email, details, and revision-protected deactivate/reactivate operations. Customer codes are normalized and remain unique across active and inactive records; writes use serializable transactions, rollback cleanup, non-destructive lifecycle changes, and automatic audits whose reviewed value policy records only customer ID/code/active state, excluding contact, address, tax, and name values. New customers start active, while optional null `Sale.CustomerId` continues to represent walk-in sales without creating a master record. `GetHistory` independently requires Customers View and Sales View and returns a bounded, newest-first combined timeline of linked sales and sale returns with optional document kind, product, receipt/return number, and exact UTC range filters; product filters select whole documents and preserve document-wide amounts/counts. The stable Customers resource is now seeded: System Administrator has all actions, Manager View/Add/Edit, and Cashier View/Add/Edit, all without destructive delete for baseline roles. A COPY_ONLY CHECKSUM backup was created and verified at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_customer_permissions_20260906_200616.bak`; no explicit migration was pending, Seed created Customers module ID 15 and its intended grants, the database still has zero customers, and inventory totals remain reconciled at 62. The Release application build passes with the existing missing Syncfusion-reference warnings and `ucProducts.ProductName` warning. No UI changes or tests were performed. Validation, revision, uniqueness races, permission combinations, audit privacy, SQL union translation, history reconciliation, paging, and seed idempotency remain behaviorally unverified under the testing hold. B2 remains in progress; register and shift prerequisites are next.

Module 7 reviewed-cost/B1 transition note (2026-09-06): receiving now has an explicit cost policy rather than silently changing product costs. `GetCostProposal` requires Purchasing View and Products View and returns the receipt-line snapshot alongside the current catalog cost. `ApplyReceivedCost` separately requires Purchasing View, Products Edit, and an authenticated actor; it accepts the reviewed current cost, reloads the receipt line and product in a serializable transaction, rejects stale catalog values or inactive products, and applies only the immutable received unit cost. A retry succeeds without another write when that exact cost is already current. The product update uses normal automatic audit/price-history capture and does not change selling price, supplier defaults, receipt facts, stock, or ledger movements. No migration or database write was required. The Release application build succeeded with the existing missing Syncfusion-reference warnings and `ucProducts.ProductName` warning. No UI changes or tests were performed. Proposal reads, permission combinations, stale/concurrent writes, audit output, retries, and price-history visibility remain unverified under the testing hold. The planned B1 supplier/purchasing backend scope is now implemented with verification deferred; active backend delivery advances to B2 customer, register, and shift prerequisites.

Module 7 purchase-return/deployment note (2026-09-06): purchase returns are now persisted as append-only correcting documents linked to a posted goods receipt and its exact receipt lines. `PurchaseReturnService.Post` requires Purchasing Edit and an authenticated actor, validates a normalized return number, required bounded reason, and 1-500 distinct positive receipt-line quantities, and rejects cumulative returns above the originally received quantity. It also requires available on-hand stock plus agreement among product quantity, inventory balance, and summed ledger. Under one serializable transaction it snapshots original receipt costs, calculates the return total, reduces both quantity stores, appends negative PurchaseReturn movements, updates the receipt to PartiallyReturned or Returned, and commits automatic audit records without rewriting the original receipt lines or receipt movements. Exact unchanged return-number retries return the original document; changed reuse is rejected and the unique return-number index handles concurrent commits. Inactive products remain returnable when referenced by an existing receipt. `AddPurchaseReturns` creates PurchaseReturns and PurchaseReturnLines with non-cascading source/product/supplier foreign keys and cascading ownership only from a return header to its lines. Generated SQL was reviewed before deployment. A COPY_ONLY CHECKSUM backup was created and verified at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_purchase_returns_20260906_162213.bak`; migration/Seed completed on `POSV1`, both tables and migration history were verified, and inventory remained 5 products/62, 5 balances/62, and 3 movements/62 with zero existing receipts. No UI changes or test execution occurred. Behavioral, concurrency, rollback, cumulative-return, status, audit, and restore verification remain deferred under the testing hold. B1 remains in progress; reviewed cost-update policy and purchasing query/document gaps are next.

Module 7 direct-receiving note (2026-09-06): `GoodsReceiptService.ReceiveDirect` now provides a Purchasing Add-protected backend operation for inventory received from an active supplier without a purchase order. The request requires a normalized receipt number, optional supplier reference, and 1-500 distinct active products with positive quantities and positive two-decimal unit costs within the database monetary range. Under one serializable transaction it creates a Posted goods receipt with a null purchase-order link, snapshots actual unit costs, updates inventory balances and transitional product quantities, appends attributed PurchaseReceipt movements, and commits automatic audit records. It first requires product quantity, balance, and summed ledger to reconcile, and rejects quantity overflow. Exact unchanged receipt-number retries return the original receipt; changed reuse and duplicate supplier references are rejected, with the existing unique receipt-number index handling concurrent receipt-number commits. Direct receipt costs do not alter catalog or supplier default costs pending the reviewed cost-update policy. No schema migration or database write was required. The Release application build succeeded with the existing missing Syncfusion-reference warnings and `ucProducts.ProductName` warning. Tests were neither built as a top-level project nor executed, and no UI changes were made. Authorization, validation, idempotency, concurrency, rollback, audit, duplicate supplier-reference behavior, and inventory reconciliation remain behaviorally unverified under the testing hold. B1 remains in progress; purchase returns and cost-update policy are next.

Module 7 purchase-order receiving note (2026-09-06): `GoodsReceiptService.ReceivePurchaseOrder` now provides a Purchasing Edit-protected backend operation for partial and final receipts against ordered purchase orders. It validates a caller-assigned receipt number, optional supplier reference, and 1-500 distinct positive product quantities; rejects products outside the order and quantities above each remaining amount; and records inactive catalog products when they belong to an existing order so master-data deactivation does not strand fulfillment. Under one serializable transaction it creates a Posted goods receipt with stored purchase-order costs, advances received quantities, completes a fully received order or leaves a partial order Pending, updates both the canonical inventory balance and transitional product quantity, appends attributed PurchaseReceipt movements, and commits automatic audit records. Before each update it requires product quantity, balance, and summed ledger to agree. Exact unchanged retries return the original receipt; changed receipt-number reuse and duplicate supplier references are rejected, while the existing unique receipt-number index remains the concurrent retry boundary. Receipt costs are immutable transaction snapshots and this slice deliberately leaves product catalog cost and supplier default cost unchanged pending a reviewed cost-update policy. Direct receiving and purchase returns remain open. No schema migration or database write was required. The Release solution build succeeded with the existing missing Syncfusion-reference warnings and `ucProducts.ProductName` warning; this compiled the test project but did not execute tests. No UI changes were made. Authorization, validation boundaries, exact/concurrent retries, partial/final progress, rollback, audit output, duplicate supplier references, and inventory reconciliation remain behaviorally unverified under the testing hold. B1 remains in progress; direct receiving and purchase returns are next.

Module 7 purchase-order transition/deployment note (2026-09-06): revision-protected `OrderDraft` and `Cancel` operations now require Purchasing Edit and execute inside the purchase-order serializable transaction/audit scope. Ordering permits only Draft, revalidates a nonempty line set plus active supplier/products, sets generic status Pending as the current persisted representation of Ordered, and records OrderedUtc. Cancellation requires a trimmed reason up to 250 characters and permits Draft or Pending only when every received quantity is zero and no goods receipt references the order; it sets Cancelled, CancelledUtc, and CancelReason. Completed/received or already-cancelled records require review/correcting workflows rather than destructive changes. Neither transition changes inventory, costs, supplier links, or order lines. Search/details and revision hashes now include cancellation facts. `AddPurchaseOrderCancellation` adds nullable datetime/reason fields, preserving existing orders. An initial generated-snapshot validation found an invalid datetime Precision facet; it was corrected before any database update, then the build and generated SQL check passed. A COPY_ONLY CHECKSUM backup completed at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_po_cancellation_20260906_155818.bak`; migration/Seed completed on `POSV1`, metadata confirms the nullable datetime/nvarchar(250) fields, there remain 0 purchase orders, and inventory totals remain 5 products/62, 5 balances/62, and 3 movements/62. No pending explicit migrations remain. The Release application build passes with the existing `ucProducts.ProductName` warning. These are build/deployment observations, not behavioral tests. No UI changes or tests were performed. All transition, stale revision, cancellation protection, audit, concurrency, rollback, and migration restore/rollback behavior remains unverified under the testing hold. B1 remains in progress; receiving is next.

Module 7 purchase-order draft editing note (2026-09-06): `UpdateDraft(PurchaseOrderDraftUpdateDTO)` now requires Purchasing Edit, a loaded order ID/revision, active supplier, and the same bounded line/cost rules as creation. It loads the header/lines inside a serializable transaction, permits only Draft state, recomputes the opaque header/line revision, and rejects stale/missing values before mutation. Supplier and the complete line set may be replaced; order number, creation time, status, and received quantities cannot be supplied or overwritten. Replacement lines start with zero received quantity, and the recalculated total plus header/line/audit changes commit atomically. Failure rolls back and clears tracked state; recognized EF/deadlock/lock-timeout conflicts produce reload guidance. This implementation replaces persisted line identities on every successful edit, so downstream references must not exist while Draft; later ordered/received states are immutable through this method. No inventory, supplier defaults, or product costs change. No schema migration or database write was required. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests or UI changes were performed. Stale/current revisions, line replacement, audit output, rollback, concurrent edits, no-op edits, and identity behavior remain unverified under the testing hold. B1 remains in progress; order/cancel transitions are next.

Module 7 purchase-order query/detail note (2026-09-06): `PurchaseOrderService.Search` and `GetDetails` now require Purchasing View and expose backend DTOs independent of UI. Search combines bounded order-number/supplier text, optional supplier/product, explicit Draft/Pending/Completed/Cancelled status, and inclusive UTC creation start/exclusive end filters in SQL; numeric or unrelated generic document states are rejected. Pages are bounded to 1–200 and ordered deterministically by creation time/ID newest first. Product filtering selects whole orders without reducing their line count or total. Details return current supplier/product labels, stored order facts, ordered/received/remaining quantities, unit costs, and line totals. An opaque SHA-256 revision covers header state/timestamps/total and every persisted line identity/product/quantity/cost for later edit/transition concurrency checks; current catalog labels are intentionally excluded. Count/page and header/line detail reads are separate live queries, so the revision represents the completed detail read rather than a transactionally locked snapshot. No migration or database write was required. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests or UI changes were performed. SQL translation, filters, paging, concurrent detail reads, revision stability, permissions, and growing-data performance remain unverified under the testing hold. B1 remains in progress; draft editing and order/cancel transitions are next.

Module 7 purchase-order draft foundation note (2026-09-06): `PurchaseOrderService.CreateDraft` now provides a backend-only, Purchasing Add-protected draft operation independent of forms. It validates a required normalized order number, active supplier, 1–500 distinct active products, positive integer quantities, positive decimal(18,2) costs, and aggregate monetary range before persistence. It snapshots ordered quantity/unit cost, initializes received quantity to zero, calculates/stores the document total, timestamps creation in UTC, and commits header/lines/audit atomically in a serializable transaction with rollback tracking cleanup. The existing unique `IX_OrderNumber` is the database retry boundary. An exact order-number retry matching supplier, Draft state, total, and every unchanged line returns the original ID without another write; changed reuse is rejected. Named concurrent index conflicts request reload/retry of the unchanged request. Order numbers are caller-assigned in this slice; centralized purchase numbering remains future work. Drafts do not alter inventory, supplier defaults, or product costs. The Release application build passes with the existing `ucProducts.ProductName` warning, and database metadata confirms the expected unique order-number index name. No tests, UI changes, migrations, or database writes were performed. Validation boundaries, exact/conflicting/concurrent retries, audit, rollback, totals, and permission enforcement remain unverified under the testing hold. Draft query/edit/order/cancel transitions and receiving remain upcoming B1 work.

Module 6 supplier purchase-history/permission note (2026-09-06): stable `Purchasing` resource authorization is now part of the backend permission catalog. Normal seeding grants Administrator all actions and Manager/Inventory Clerk View/Add/Edit without Delete. `SupplierService.GetPurchaseHistory` independently requires both Suppliers View and Purchasing View, validates supplier, optional document kind, exact UTC date range semantics, optional product, and bounded document-number search before querying. It returns one newest-first paged timeline combining purchase orders and goods receipts with document/status/date, stored order total or receipt line-derived total, line count, purchase-order link, and supplier reference. Product filters select documents while totals/counts remain document-wide. Draft order activity uses CreatedUtc; ordered orders use OrderedUtc; receipts use ReceivedUtc. Pages are bounded to 1–200 and deterministic by event time/kind/ID; count/page are separate live queries. A COPY_ONLY CHECKSUM backup completed at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_purchasing_permissions_20260906_153049.bak`; the database was already at the latest migration, then the normal idempotent Seed method created Purchasing module ID 14 and verified the three intended role grants. There remain 0 purchase orders and 0 goods receipts; inventory totals remain 5 products/62, 5 balances/62, and 3 movements/62. The Release application build passes with the existing `ucProducts.ProductName` warning. These are build/seed observations, not behavioral tests. No UI changes, schema migrations, or tests were performed. SQL UNION translation, totals, filters, ordering ties, permissions, seed idempotency, and growing-data performance remain unverified under the testing hold. B1 remains in progress; purchase-order/receiving workflows are next.

Module 6 supplier stale-edit note (2026-09-06): supplier search/detail DTOs now carry an opaque SHA-256 content revision covering ID, active state, code/name, and all contact/address/tax fields with null-aware encoding. Existing supplier updates compare the supplied revision with freshly loaded state inside the serializable supplier transaction before applying changes; missing/stale revisions require reload. Activation/deactivation now requires `(supplierId, active, revision)` and checks the revision before same-state no-op handling. Creation remains revision-free and always starts active. Callers must retain revisions from reads and reload after writes. The revision represents current field content, not all intervening history; a change later reverted exactly produces the same revision. Sensitive contact values remain excluded from automatic audit details even though they participate in concurrency protection. No schema migration is required. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests, UI changes, migrations, or database operations were performed. Revision round-trip, stale updates/state changes, no-op behavior, concurrent edits, rollback recovery, and future caller compatibility remain unverified under the testing hold. B1 remains in progress; supplier purchase history and purchasing workflows remain next.

B0 transition note (2026-09-06): the planned catalog/inventory backend scope for B0 is implemented with testing and runtime acceptance still deferred. This status covers service-level authorization/validation, non-destructive lifecycle and reference protection, deployed SKU/barcode/category constraints, paged catalog/balance/alert/history/reconciliation queries, balance/append-only ledger integration, controlled opening/adjustment/count workflows, request idempotency, checkout ledger guards, and concurrency/recovery handling. It does not mark modules 4/5 fully complete: UI remains U1, automated/runtime/concurrent verification remains held, product-level expiry is not a batch ledger, legacy compatibility columns remain until verified removal, and deployment restore drills remain open. Active backend delivery moves to B1.

Module 6 supplier-product unlink/stale-edit note (2026-09-06): supplier-product pages now return an opaque SHA-256 content revision covering link identity, supplier/product IDs, supplier SKU, default cost, and lead time. Existing-link updates require the current revision inside the supplier serializable transaction; a create request encountering an existing pair is rejected for reload instead of blindly overwriting it. `UnlinkProduct` requires Suppliers Edit, validates the reviewed identity/revision, and deletes only the relationship in the same audit/rollback scope. Inactive suppliers/products may be unlinked so obsolete sourcing relationships can be cleaned up; creation/update still requires both masters active. Product and supplier records, purchase documents, catalog costs, inventory, and transaction history are unchanged. The existing unique supplier/product index remains the concurrent-creation enforcement boundary. No schema migration is required. The Release application build passes with the existing `ucProducts.ProductName` warning. No tests, UI changes, or database operations were performed. Revision round-trip, stale update/unlink, inactive unlink, concurrent create, audit, rollback recovery, and existing caller compatibility remain unverified under the testing hold. B1 remains in progress; supplier master stale-edit protection and purchase history are next gaps.

Module 6 supplier-product note (2026-09-05): `SaveProductLink` now validates supplier SKU, decimal(18,2) nonnegative default cost, and nonnegative lead time, requires Suppliers Edit, and upserts the supplier/product pair inside the supplier transaction/audit scope. Only active suppliers/products can be maintained; an explicit mismatched link ID is rejected. Supplier prices remain separate from product selling/cost prices. `GetProducts` requires Suppliers View plus Products View and returns bounded pages including inactive linked products and their current names/SKUs. The existing unique supplier/product index remains in place. Unlinking, supplier-specific purchase history, and stale-edit protection remain open. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes, tests, migrations, or database operations were performed. Link creation/update, validation, permissions, and paging verification remain deferred.

Module 6 supplier-foundation note (2026-09-05): `SupplierService` now provides validated create/update, non-destructive deactivate/reactivate, detail retrieval, and bounded code/name/status search using injected context/authorization. Supplier codes are normalized and checked for duplicates, including inactive records; existing schema indexes remain the database enforcement baseline. Writes preserve relationships, use serializable transactions and rollback cleanup, and leave sensitive contact fields outside automatic audit value allowlists. `Suppliers` is added to stable resources and normal migration seeding: Administrator gets all flags, Manager View/Add/Edit, and Inventory Clerk View. Seeding was not executed. This starts the supplier prerequisite using the existing catalog/inventory foundation; B0 remains open for outstanding implementation/hardening and deferred verification and is not declared complete. Supplier-product links, purchase history, stale-edit protection, and receiving remain upcoming work. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes or tests were added, no schema migration was needed, and no database operations were executed.

- [ ] Complete modules 6 and 7.
- [ ] Implement supplier, purchasing, partial receiving, and purchase returns.
- [ ] Reconcile purchasing with stock movements and cost updates.

### M4 — Sell and collect money

M4 backend completion/deployment note (2026-09-06): the Sell and collect money backend milestone is implemented across modules 8, 9, 10, and the previously completed module 12. `SalesCartService` provides authorized paged product search, exact barcode lookup, add/update/remove operations, live inventory visibility, and configured tax/discount totals without depending on WinForms. `SalesService` now accepts a typed checkout command; validates bounded unique lines, manager-authorized discounts, Cash/Card/EWallet/StoreCredit tenders, non-cash references, StoreCredit customer linkage, payment sufficiency, and cash-only change; and persists completed payments atomically with the sale, receipt sequence, stock balance/ledger, held-cart transition, and audit. Held sales are persisted without stock or payments, use request idempotency and row-version review, can be listed/resumed/updated/cancelled, and must match reviewed contents when checkout consumes them. Receipt search/detail DTOs read stored product, store, tax, register, cashier, customer, payment, and financial snapshots; authorized printing uses the configured register printer, visibly labels reprints, and appends explicit print/reprint audit events. Completed non-cash provider references have a filtered unique database constraint. `AddSaleReceiptSnapshotsAndRevision` was generated, reviewed, and augmented with safe legacy snapshot backfill and duplicate-payment-reference preflight enforcement. Preflight found zero sales, zero payments, and no duplicate references. A COPY_ONLY CHECKSUM backup was created and verified at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_m4_sales_20260906_211031.bak`; migration/Seed completed on `POSV1`, snapshot/row-version columns, payment index, and migration history were verified, and inventory totals remain reconciled at 62. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI work or tests were performed. Checkout/tender math, held-sale lifecycle, concurrency/idempotency, printing hardware/failure audit behavior, receipt SQL projections, rollback, and migration restore remain unverified under the testing hold. B3 and the M4 backend milestone are implemented with verification deferred; active backend delivery advances to B4 returns, refunds, and exchanges.

B4 backend completion/deployment note (2026-09-06): module 11 now has an authorized return eligibility query and an idempotent, serializable posting workflow. `SaleReturnService` enforces the 30-day return window, cumulative source-line quantities, proportional refundable values, original-payment refund ceilings, active register/open owned shift attribution, and manager approval for late, non-restock, or returns above PHP 1,000. Posting writes the return, refund tenders, sale status, restocked inventory balance/product quantity, stock movements, and audit atomically; damaged and quarantined dispositions do not increase sellable stock. Refunds reference their original completed payment, cash capacity excludes original change, non-cash refunds require unique provider references, and an optional completed exchange sale is linked through a unique foreign key. Return numbering uses the persisted `RETURN` sequence. The Returns resource is seeded for Administrator, Manager, and Cashier roles at their intended access levels. Generated migration SQL was reviewed before deployment. Preflight found no existing returns or refund payments. A COPY_ONLY CHECKSUM backup was created and verified at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_b4_returns_20260906_212451.bak`; `AddSaleReturnPostingGuards` and the corrective `AddExchangeSaleForeignKey` migration completed on `POSV1`. Required columns, unique indexes, enabled foreign keys, migration history, and reconciled inventory totals of 62 were verified. No UI work or tests were performed. Behavioral, concurrency, rollback, approval, tender reconciliation, and restore verification remain deferred under the testing hold. B4 and the M5 post-sale backend milestone are implemented; active backend delivery advances to B5 management queries.

B5 backend completion/deployment note (2026-09-06): modules 15 and 16 now use typed, authorization-gated management contracts with one validated UTC half-open date/register/user/customer scope. `ManagementReportService` computes dashboard and report financials from completed immutable sale snapshots and posted return facts, including gross sales, refunds, net sales, tax, discounts, estimated cost/margin, and transaction counts. Its bounded SQL queries cover sales, product/category performance, tender collection/refunds, current inventory and valuation, stock movements, supplier purchasing/returns, cashier/register shifts, and grouped audit activity. Dashboard results reuse the same financial calculation and add top products, configurable per-product low/out-of-stock thresholds, 30-day expiry counts, and open/long-running shift alerts. `ReportOutputService` provides permission-controlled UTF-8 CSV output with formula-injection protection and configured-register summary printing; generated time and exact filter criteria are embedded in output. Dashboard and Reports resources are restricted to Administrator and Manager, with export/print represented by Reports Add. `AddManagementQueryIndexes` adds seven reviewed indexes for the primary transaction date/status/register/supplier predicates. A COPY_ONLY CHECKSUM backup was created and verified at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_b5_reporting_20260906_214000.bak`; migration and permission seed completed on `POSV1`. No UI work or tests were performed. Query translation, performance at production scale, numerical reconciliation, CSV round-trip, physical printing, and restore verification remain deferred under the testing hold. B5 and the M6 management-visibility backend milestone are implemented; active backend delivery advances to B6 maintenance and cross-module completion.

Module 8 checkout-request idempotency/deployment note (2026-09-06): completed-sale submission now requires a caller-generated nonempty `Sale.RequestId` retained unchanged across uncertain retries. A SHA-256 request hash canonically covers the register, shift, optional customer/walk-in choice, cash received, and product/quantity multiset. Inside the serializable checkout transaction, an exact same-cashier replay returns the existing completed sale with its lines and payments without allocating another receipt, reducing stock, or writing another audit; changed details or reuse by another cashier are rejected. A unique Sales request-ID index supplies the concurrent database boundary. Legacy sales receive unique request IDs and deterministic legacy hashes before both columns become required. The migration was generated and its SQL reviewed; the initial scaffold timestamp and unsafe empty-value backfill were corrected before deployment. Preflight found zero existing sales. A COPY_ONLY CHECKSUM backup was created and verified at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_sale_request_20260906_204949.bak`; `AddSaleCheckoutRequestId` was applied to `POSV1`, required column/index metadata and migration history were verified, and inventory totals remain reconciled at 62. The Release application build passes with the existing `ucProducts.ProductName` warning. No UI changes or tests were performed. Exact/conflicting/concurrent retries, uncertain commits, hash stability, rollback, and migration restore remain unverified under the testing hold. This idempotency foundation is incorporated into the completed B3 backend scope described above.

- [x] Complete modules 8, 9, 10, and 12 backend scope.
- [x] Deliver open-shift-to-receipt backend workflow.
- [x] Make checkout atomic and idempotent.
- [x] Reconcile payments, cash, stock, receipts, and audit in the backend implementation.

### M5 — Post-sale operations

- [x] Complete modules 11 and 13 backend implementation.
- [x] Deliver returns/refunds/exchanges and customer history backend services.
- [ ] Verify return eligibility, stock disposition, and refund reconciliation.

### M6 — Management visibility

- [x] Complete modules 15 and 16 backend implementation.
- [x] Use shared financial calculations for dashboard and essential reports.
- [x] Add authorization-controlled CSV export and summary printing.

### M7 — Production readiness

- [x] Complete module 17 backend implementation.
- [ ] Perform performance, security, migration, backup, and restore validation.
- [x] Write deployment and operator documentation.
- [ ] Run a complete user-acceptance test and restore drill.

B6 backend completion/deployment note (2026-09-07): the planned B0–B6 backend implementation is finished, with testing, runtime acceptance, and all remaining UI work still deferred. Module 17 now exposes an authorization-gated `IBackupService` implementation for checked `COPY_ONLY` SQL Server backups and restore into a new, non-live database with relocated data/log files and `DBCC CHECKDB`. The backend intentionally cannot overwrite the configured live database; production replacement remains a coordinated DBA operation. A typed maintenance health contract reports application/database migration versions, connectivity, printer configuration, log-disk capacity, and SQL full-backup age. Diagnostic logging uses bounded, rotating local files and redacts common credential assignments. Backup/restore outcomes append safe operational audit events, and Maintenance is included in audit categorization. Database migration metadata can now be queried independently of startup updates. A stable `Maintenance` resource was added to normal idempotent seeding and granted only to System Administrator. Cross-module service entry points were reviewed for authorization coverage; pure calculations and in-memory cart mutations remain non-persisting helpers, while operational reads/writes retain resource guards. No schema migration was required. Before seeding, a `COPY_ONLY` checksum backup was created and verified at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_before_b6_20260907_105700.bak`. The normal migration path reported no pending explicit migrations, ran Seed, and database metadata confirmed Maintenance module ID 20 with full System Administrator permissions; the latest migration remains `AddManagementQueryIndexes`. The Release application build passes with the existing missing Syncfusion-reference warnings and `ucProducts.ProductName` warning. No tests were run, no restore was performed, and no UI was changed. Backup path/SQL service permissions, restore behavior, health-query permissions, logging rotation/redaction, cross-module runtime behavior, performance/security checks, and the restore drill remain explicitly unverified under the testing hold. U1 is now the next implementation phase.

## Final definition of done for every module — after backend and UI delivery

Non-UI verification resumption note (2026-09-07): testing resumed by user direction. The full Release solution and complete automated test executable pass. Integration coverage now additionally verifies idempotent checkout without duplicate stock movement, idempotent return posting with restock, dashboard/report financial reconciliation, idempotent shift opening/cash movement/close with derived expected cash, and diagnostic-log credential redaction. Nineteen unused Syncfusion references were removed after a repository-wide source/resource search found no usage, and the `ucProducts.ProductName` designer member was renamed to `colProductName`; the following full build reports no warnings. The empty, untracked `MyInfoMigration` artifact set was removed; it was not compiled into `POS.Data`, contained no Up/Down operations, and was not part of the production migration chain. EF tooling reports all 36 application migrations applied to `POSV1`, so no new migration was required. A fresh `COPY_ONLY` checksum backup was created and verified at `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\Backup\POSV1_nonui_verification_20260907.bak`, restored to the separate `POS_Verification_20260907` database, and passed `DBCC CHECKDB`. Live/restored counts match at 5 products, 5 balances, balance/ledger quantity 62, and 36 migrations. Physical printing, production-scale performance, UI acceptance, and broader multi-workstation soak testing still require their corresponding hardware or representative environment.

This is the final module/release checklist, not a gate requiring UI work during backend phases. Test-related items remain unchecked until testing resumes and the relevant checks actually pass.

- [ ] Domain rules and acceptance criteria are documented.
- [ ] Entity/configuration/migration changes are reviewed and tested.
- [ ] Service behavior is independent of WinForms controls.
- [ ] Authorization is enforced in the service/use-case layer.
- [ ] Multi-record writes are atomic.
- [ ] Expected validation and concurrency errors are user-safe.
- [ ] Audit coverage exists and contains no secrets.
- [ ] Unit and integration tests cover critical success/failure paths.
- [ ] UI supports loading, empty, success, validation, and failure states.
- [ ] Navigation and permissions are wired.
- [ ] Reports affected by the module reconcile correctly.
- [ ] Build, tests, and migration smoke tests pass.
- [ ] Documentation and status table are updated.

## Change-control rules

- Follow B0–B6 in dependency order, then U1; implement small, reviewable backend slices before beginning remaining UI work.
- Do not combine experimental entities or migrations with production features.
- Do not edit generated designer code unless the WinForms designer requires it.
- Never rewrite or delete completed financial transactions; create correcting records.
- Do not introduce multi-branch, loyalty, credit, or external payment complexity until the core backend through B4 is stable unless business requirements explicitly demand it.
- Update this file after each completed slice by changing delivery status and checking acceptance items.
- Use DevExpress controls exclusively for new or redesigned visual UI components, including buttons, editors, grids, navigation, dialogs, layout, validation, and reports.

## Historical first implementation slice — already superseded

The M0 slice below records the original starting point. Do not restart it; the current next work is the immediate backend slice under the active delivery sequence above.

Start with M0, specifically:

1. Add test projects.
2. Register `POSConfiguration` in `POSContext`.
3. Remove the duplicate `Sale.Items`/`SaleItems` concept.
4. Refactor context ownership so one checkout uses one context and transaction.
5. [x] Add tests proving that failed checkout does not decrement stock.

This slice addresses the highest-risk correctness issue before the system gains more modules and data.

Checkout rollback test note (2026-09-04): the isolated LocalDB integration test now verifies that insufficient payment persists neither the sale nor a stock decrement. Rollback also restores EF tracked state so a reused service cannot later persist the failed checkout's in-memory quantity change.

