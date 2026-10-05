# POS Remaining UI Implementation Guide

**Prepared:** 2026-09-15

**Baseline:** recovery commit `0c84efa` (`recovery/backend-completion-20260915`)

**Scope:** remaining U1 implementation and improvements after recovery of the completed B0-B6 backend

**UI framework:** DevExpress WinForms on .NET Framework 4.8

## 1. Purpose and source of truth

The backend implementation is complete, compiles in Release, passes the automated test suite, and has no pending Entity Framework migration. The remaining product work is the U1 application-wide UI phase.

Use these documents together:

1. `POS_IMPLEMENTATION_PLAN.md` defines module scope, business rules, milestones, and the B0-B6 to U1 delivery order.
2. `UI_IMPLEMENTATION_GUIDE.md` is the canonical designer-level UI contract, including exact control names, DTO fields, and service calls.
3. This document is the current-code delta: what exists, what needs improvement, what must be created, and the recommended implementation sequence.

This guide does not authorize recomputing business rules in forms. Services remain authoritative for validation, authorization, concurrency, transactions, audit, tax, discounts, stock, and document state.

## 2. Current UI inventory

The canonical guide names 47 `uc...` or `frm...` UI classes.

| Status | Count | Meaning |
|---|---:|---|
| Exists and compiled | 9 | Source exists and is included in `POS.csproj` |
| Exists but excluded | 1 | `ucDashboard` exists on disk but is not compiled |
| Not created | 37 | No matching source class currently exists |

`SignIn` also exists and compiles, but is not included in the count because it does not use the `uc` or `frm` prefix.

### Existing and compiled planned classes

- `frmMain`
- `frmChangePassword`
- `frmLockScreen`
- `frmStoreSettings`
- `ucAudit`
- `ucModules`
- `ucProducts`
- `ucRolesPermissions`
- `ucUsers`

### Existing but excluded

- `ucDashboard` and `ucDashboard.Designer.cs`

Do not merely add the current dashboard shell to the project. Finish its DTO binding, filters, states, permissions, and drill-down behavior before treating it as implemented.

### Legacy screens that should be replaced or absorbed

- `frmCategory` should be superseded by `ucCategories` and `frmCategoryEdit`.
- `frmAddProducts` should be superseded by `frmProductEdit`.
- `ucStock` should be absorbed into the broader `ucInventory` workspace.
- Numeric ribbon items such as `barButtonItem12` should be replaced with business names from the navigation specification.

Keep a legacy screen until its replacement is wired and verified. Remove it only in a later cleanup commit.

## 3. Existing screens that need improvement

| Screen | Current condition | Required improvement |
|---|---|---|
| `SignIn` | Uses standard WinForms text boxes/button and mixed DevExpress controls | Convert visible controls to DevExpress, remember username only, add Caps Lock and inline error states, prevent duplicate submits, and preserve the async authentication flow |
| `frmMain` | Uses legacy/numeric ribbon items and repeated event handlers | Implement the named ribbon structure, one reusable content-navigation method, status items, complete resource visibility, session/register/shift state, and clean disposal of the previous workspace |
| `ucDashboard` | Source exists but is excluded and incomplete | Implement against `ManagementReportService.GetDashboard`, add scope filters/cards/grids/drill-down, include it in `POS.csproj`, and wire it through composition |
| `ucProducts` | Legacy catalog grid exists | Add server paging, documented filters/columns, inactive state, missing-balance badge, identifiers, product editor, price history, and permission-aware actions |
| `ucStock` | Legacy stock list exists | Replace with `ucInventory`; retain only temporarily for compatibility |
| `ucUsers` | User management exists | Add search/status/role filters, paging, reset-password and activity workflows, loading/empty/error states, and action permissions |
| `ucRolesPermissions` | Basic role/claim management exists | Present a resource-grouped permission matrix, lock the System Administrator role visually, and add safe save/reload/conflict behavior |
| `ucModules` | Basic module management exists | Use stable resource codes, clear validation, permission-aware lifecycle actions, and safe refresh behavior |
| `ucAudit` | Operational audit browsing/archive UI exists | Align all documented filters and columns, provide readable old/new detail, and verify progress/cancellation and archive action states |
| `frmStoreSettings` | Core settings form exists | Add time-zone lookup, receipt preview, reload/revert, tax-history grid, dirty-state handling, and load-before-save protection |
| `frmChangePassword` | Functional DevExpress modal exists | Apply final visual consistency, validation summary, loading state, and keyboard/accessibility review |
| `frmLockScreen` | Functional DevExpress modal exists | Apply final visual consistency, retry/error state, and keyboard/accessibility review |

## 4. Primary workspaces to create

All primary workflows must be `XtraUserControl`s hosted inside `frmMain.pnlMain`.

| New control | Primary service | Main DTOs/contracts |
|---|---|---|
| `ucSalesRegister` | `SalesCartService`, `SalesService` | `SaleCartDTO`, `SaleCheckoutDTO`, `HeldSaleDTO` |
| `ucReceipts` | `SaleReceiptService` | `SaleReceiptSearchDTO`, `SaleReceiptPageDTO`, `SaleReceiptDTO` |
| `ucReturns` | `SaleReturnService` | `SaleReturnEligibilityDTO`, `SaleReturnPostDTO`, `SaleReturnResultDTO` |
| `ucCashierShifts` | `CashierShiftService` | `CashierShiftSearchDTO`, `CashierShiftPageDTO`, `CashierShiftDTO` |
| `ucCategories` | `InventoryService` | `CategorySearchDTO`, `CategoryPageDTO`, `CategorySummaryDTO` |
| `ucInventory` | `InventoryLedgerService` | balance, alert, movement, adjustment, and reconciliation DTOs |
| `ucStockCounts` | `StockCountService` | search, history, details, draft, posting, and row-version DTOs |
| `ucSuppliers` | `SupplierService` | supplier, supplier-product, and purchase-history DTOs |
| `ucPurchaseOrders` | `PurchaseOrderService` | search, page, detail, draft, update, and transition DTOs |
| `ucGoodsReceiving` | `GoodsReceiptService` | purchase-order receipt, direct receipt, and cost-proposal DTOs |
| `ucCustomers` | `CustomerService` | customer search/detail/history DTOs |
| `ucReports` | `ManagementReportService`, `ReportOutputService` | `ManagementFilterDTO`, `ManagementReportDTO`, report row DTOs |
| `ucMaintenance` | `DatabaseMaintenanceService` / `IBackupService` | `MaintenanceHealthDTO`, backup and restore results |
| `ucRegisters` | `RegisterStationService` | `RegisterStationPageDTO`, `RegisterStationDTO` |

`ucDashboard` is the fifteenth primary U1 workspace, but it is an improvement of an existing excluded class rather than a new class.

## 5. Modal forms to create

Focused edits and posting confirmations should be owned `XtraForm`s.

| New form | Purpose |
|---|---|
| `frmUserActivity` | Read-only user activity history |
| `frmRegisterEdit` | Create/edit a register station |
| `frmProductEdit` | Create/edit product details and identifiers |
| `frmProductPriceHistory` | Read-only price-change history |
| `frmCategoryEdit` | Create/edit category details |
| `frmStockAdjustment` | Post an idempotent manual inventory adjustment |
| `frmStockCount` | Create/edit/post/cancel a stock-count document |
| `frmSupplierEdit` | Create/edit supplier profile |
| `frmSupplierDetails` | Supplier profile, product links, and purchase history |
| `frmPurchaseOrderEdit` | Create/update/order/cancel a purchase order |
| `frmReceivePurchaseOrder` | Receive remaining lines from an order |
| `frmReceiveDirect` | Receive stock directly from a supplier |
| `frmReceiptCostProposal` | Review and explicitly apply received-cost changes |
| `frmPurchaseReturn` | Post a return against received goods |
| `frmCustomerEdit` | Create/edit customer profile |
| `frmCustomerDetails` | Customer details and transaction history |
| `frmOpenShift` | Open a cashier shift with starting cash |
| `frmCashMovement` | Post cash-in/cash-out against an open shift |
| `frmCloseShift` | Review expected cash and close a shift |
| `frmPayment` | Capture one or more valid tenders and submit checkout |
| `frmCheckoutComplete` | Show the committed receipt, totals, change, print, and new-sale actions |
| `frmHeldSales` | List, inspect, resume, and cancel held sales |
| `frmSaleReturn` | Select eligible lines, dispositions, refunds, and optional exchange sale |

## 6. Shared UI infrastructure first

Implement these foundations before building module screens:

- One `frmMain` workspace loader that docks the new control, disposes the old control, sets the title, and handles navigation exceptions.
- A common list-screen pattern: header, filter area, read-only grid, paging strip, reload action, and empty-state panel.
- A consistent async/busy helper that disables the relevant action area, prevents double clicks, and restores controls in `finally`.
- A user-safe error presenter for validation, authorization, stale-data conflicts, database failures, and unexpected errors. Never display stack traces.
- A local/store-time converter based on configured `TimeZoneId`.
- A PHP money display helper using configured `MoneyDecimalPlaces`.
- A permission binder that hides unavailable navigation but keeps viewable screens visible with forbidden actions disabled and explained.
- A reusable paging state that honors backend page limits; never load an entire operational table merely to page it in memory.
- A cancellation/lifetime pattern for long searches, audit archives, reports, health checks, backups, and restores.

Do not introduce a second set of business validators in this infrastructure. UI checks should provide quick feedback; service validation remains final.

## 7. Shell and session implementation

Rebuild the ribbon around these business areas:

- Home: Dashboard.
- Sales: Register, Held Sales, Receipts, Returns, and Shifts.
- Catalog: Products and Categories.
- Inventory: Inventory and Stock Counts.
- Purchasing: Suppliers, Purchase Orders, and Receiving.
- Customers: Customer management and history.
- Management: Reports.
- Administration: Users, Roles, Modules, Audit, Settings, Registers, and Maintenance.
- Account: Change Password, Lock, and Logout.

Add status items for current user, role, register, shift, database status, and store-local time. Navigation visibility is an early convenience check only; every service retains its own authorization boundary.

Replace repeated legacy event handlers with named handlers or command methods. Opening a second workspace must dispose the first one and its owned service/context.

## 8. Authentication and administration

### Sign-in

- Use `TextEdit` for username and a masked `ButtonEdit` or `TextEdit` for password.
- Use `CheckEdit` for remember-me and store only the username.
- Call `UserService.AuthenticateAsync`, then `RoleService.SetupClaims`.
- Disable submit while authenticating; Enter submits and Escape clears or closes according to the existing application flow.
- Show required-field, invalid-credential, locked/disabled-user, and unexpected-error states without revealing which credential was wrong.

### Users

- Bind list/detail state to `UserDTO`; do not bind Identity entities.
- Add search, enabled-state, role filtering, and backend paging.
- Support add, edit, enable/disable, reset password, and `frmUserActivity`.
- Bind activity rows from `UserActivityDTO`; keep internal IDs hidden.

### Roles, modules, and audit

- Group `RoleClaimDTO` permissions by stable resource/module and show View/Add/Edit/Delete explicitly.
- Prevent destructive changes to the System Administrator role.
- Keep stable `ResourceCodes` separate from editable display captions.
- Bind audit filters to `AuditSearchDTO` and results to `AuditEventDTO`.
- Show old/new values in a read-only detail area, not as large grid columns.
- Retain archive export, verification, and recovery progress/cancellation behavior.

## 9. Settings and registers

### Store settings

- Retain the complete loaded `StoreSettingsDTO`, including hidden IDs and `Revision`.
- Keep currency read-only as `PHP`.
- Replace free-text time zone with `lueTimeZone`.
- Add printer refresh, test print, receipt preview, reload, revert, and tax-history actions.
- Bind tax history to `TaxRateHistoryDTO`.
- Disable Save until the first successful load; after a conflict, require reload/review.

Use `StoreSettingsService.GetSettings`, `SaveSettings`, `GetTaxHistory`, `GetInstalledPrinters`, `BuildReceiptPreview`, and `PrintTestPage`.

### Register maintenance

- Search active/inactive registers using `RegisterStationService.Search`.
- Hide `Id` and `Revision` while retaining both in row/detail models.
- Use `GetDetails`, `Save`, and `SetActive(id, active, revision)`.
- Printer selection should come from installed-printer discovery rather than unrestricted text when possible.

## 10. Catalog and inventory

### Products

- Bind searches to `ProductSearchDTO` and rows to `ProductSummaryDTO`.
- Show name, SKU, barcode, category, unit, prices, quantity, reorder threshold, expiry, and active state.
- Distinguish a missing inventory balance from a real zero balance.
- Use `InventoryService.SearchProducts`, `AddProduct`, `UpdateProductDetails`, `UpdateProductIdentifiers`, `DeactivateProduct`, `ReactivateProduct`, and `GetProductPriceHistory`.
- Do not add an initial-quantity field to the product editor. Opening stock must be an inventory operation.

### Categories

- Use `SearchCategories`, `AddCategory`, `UpdateCategory`, `DeactivateCategory`, and `ReactivateCategory`.
- Retain category `Revision` across edit/lifecycle actions.
- Favor deactivation over deletion and explain reference protection when products exist.

### Inventory workspace

Provide Balances, Alerts, Movements, Adjustments, and Reconciliation pages:

- Balances: `SearchBalances(InventoryBalanceSearchDTO)`.
- Alerts: `SearchAlerts(InventoryAlertSearchDTO)` with visible severity.
- Movements: `SearchMovements(StockMovementSearchDTO)`; rows are immutable.
- Adjustment: `PostAdjustment(StockAdjustmentDTO)` with one generated `RequestId`, expected quantity, signed delta, reason, and resulting-quantity preview.
- Reconciliation: `GetReconciliation(InventoryReconciliationSearchDTO)`; diagnostic only, with no automatic repair button.

### Stock counts

- Use `SearchHistory`, `GetDetails`, `CreateDraft`, `UpdateDraft`, `PostDraft`, `CancelDraft`, and `PostCount`.
- Preserve `RequestId` and `RowVersion`; send the returned row version as `ExpectedRowVersion`.
- Make posted or cancelled counts entirely read-only.
- On stale data, require reload and review rather than automatically resubmitting.

## 11. Suppliers, purchasing, and receiving

### Suppliers

- Use `SupplierService.Search`, `GetDetails`, `Save`, and `SetActive`.
- Retain supplier revision tokens for edits and lifecycle actions.
- In `frmSupplierDetails`, implement supplier-product links with `SaveProductLink` and `UnlinkProduct` using the reviewed link revision.
- Add purchase-history filtering through `GetPurchaseHistory(SupplierPurchaseHistorySearchDTO)`.

### Purchase orders

- Search with `PurchaseOrderSearchDTO`; bind pages/details rather than entities.
- Draft lines use product, quantity, unit cost, and calculated display totals.
- Use `CreateDraft`, `UpdateDraft`, `OrderDraft`, `Cancel`, and `GetDetails`.
- Only Draft is editable. Ordered/partially received documents permit only service-approved transitions.
- Retain `Revision` across every update/order/cancel operation.

### Receiving and purchase returns

- Receive an order through `ReceivePurchaseOrder(PurchaseOrderReceiptDTO)`.
- Receive directly through `ReceiveDirect(DirectGoodsReceiptDTO)`.
- Review cost proposals with `GetCostProposal`; apply only after explicit confirmation using `ApplyReceivedCost`.
- Post purchase returns using `PurchaseReturnService.Post(PurchaseReturnPostDTO)`.
- Never rewrite the original goods receipt to represent a return.

## 12. Customers and cashier shifts

### Customers

- Use `CustomerService.Search`, `GetDetails`, `Save`, and `SetActive`.
- Preserve the customer revision for update/deactivation/reactivation.
- Implement paged history using `GetHistory(CustomerHistorySearchDTO)`.
- Double-clicking history should open the relevant receipt/return details without making history editable.

### Cashier shifts

- Search/detail with `CashierShiftService.Search` and `GetDetails`.
- Open with `OpenCashierShiftDTO` and a generated `RequestId`.
- Post cash movement with `PostCashMovementDTO`, retained shift revision, generated request ID, signed business meaning, amount, and required reason.
- Close with `CloseCashierShiftDTO`, expected cash display, counted cash input, variance preview, and retained revision.
- Do not allow checkout without the required active register/open shift state.

## 13. Sales, payments, held sales, and receipts

### Sales register

- Create cart state with `SalesCartService.Create`.
- Search or scan through `SearchProducts` and `AddByBarcode`.
- Use `AddProduct`, `UpdateQuantity`, `RemoveProduct`, and `Recalculate`; never calculate authoritative totals separately in the UI.
- Display current register, open shift, cashier, optional customer, configured tax, discount, stock state, and PHP totals.
- Use one generated checkout `RequestId` and keep it unchanged for an uncertain identical retry.

### Payment

- Build `SaleCheckoutDTO` with register, shift, optional customer, discount, optional held-sale identity/revision, product/quantity lines, and tender rows.
- Support Cash, Card, EWallet, and StoreCredit exactly as the service permits.
- Require external reference only where the backend requires it; StoreCredit requires a customer.
- Disable Pay on first submission. Call `SalesService.Checkout` once per attempt.
- On success, show `frmCheckoutComplete`; never infer success from a timeout.

### Held sales

- Use `SaveHeldSale`, `GetHeldSales`, `GetHeldSale`, and `CancelHeldSale`.
- Retain held-sale `Revision`; require review when resumed content is stale.
- Holding or cancelling must not be displayed as a completed sale or payment.

### Receipts

- Search through `SaleReceiptService.Search`, load stored facts with `Get`, and print with `Print`.
- Display historical store/product/tax/payment snapshots from `SaleReceiptDTO`; do not join current product values in the UI.
- Clearly label reprints and enforce the service permission for `Print(saleId, true)`.
- Include loading, empty, not-found, printer-failure, and successful-print states.

## 14. Returns, refunds, and exchanges

- Start from receipt lookup and call `SaleReturnService.GetEligibility(saleId)`.
- Display purchased, already returned, and eligible quantities for every line.
- Capture quantity and `ReturnDisposition` as Restock, Damaged, or Quarantine.
- Display eligible refund amounts by original payment and collect required non-cash references.
- Include generated `RequestId`, register, shift, reason, optional exchange sale, and any approval state in `SaleReturnPostDTO`.
- Call `SaleReturnService.Post` once and display the returned immutable result.
- Explain that only Restock increases sellable inventory.
- Never edit the original sale, receipt, or payment to represent a return.

## 15. Dashboard and reports

### Dashboard

- Include and finish `ucDashboard` only after it binds to `ManagementReportService.GetDashboard`.
- Use one `ManagementFilterDTO`, normally representing the current store-local day converted to a UTC half-open range.
- Show financial cards, top products, stock alerts, expiry count, open shifts, and shift issues.
- Every card must visibly retain its date/register scope and drill down through `frmMain` navigation.

### Reports

- Use a shared `ManagementFilterDTO` for preview, export, and print.
- Bind only the collection matching the selected report: sales, items/categories, tenders, inventory, movements, purchasing, shifts, or audit.
- Call `ManagementReportService.GetReport` once per refresh.
- Use `ReportOutputService.ExportCsv` and `PrintSummary`; do not regenerate totals in the form.
- Display exact filter criteria and generated time in previews and outputs.

## 16. Maintenance

- Bind health status from `DatabaseMaintenanceService.GetHealth`.
- Show application/database versions, latest application migration, log directory, database state, printer state, disk capacity, and backup age.
- Create backups only through `CreateBackup` with a validated rooted `.bak` destination.
- Restore only through `RestoreToNewDatabase`; clearly state that it creates a separate database and never overwrites the live database.
- Require explicit confirmation and prevent duplicate backup/restore submissions.
- Provide a safe open-log-folder action after validating the returned directory.

## 17. Composition-root and project wiring

Add factories to `ApplicationCompositionRoot` for:

- `CreateDashboardControl`
- `CreateSalesRegisterControl`
- `CreateReceiptsControl`
- `CreateReturnsControl`
- `CreateCashierShiftsControl`
- `CreateCategoriesControl`
- `CreateInventoryControl`
- `CreateStockCountsControl`
- `CreateSuppliersControl`
- `CreatePurchaseOrdersControl`
- `CreateGoodsReceivingControl`
- `CreateCustomersControl`
- `CreateReportsControl`
- `CreateMaintenanceControl`
- `CreateRegistersControl`

Add factories for modal editors where construction requires injected services or callbacks. A control and its cooperating services must share the intended context/current-user/clock/authorization scope. Dispose owned contexts and services with the owning form/control.

This is an old-style project: every new `.cs`, `.Designer.cs`, and `.resx` file must be explicitly included in `POS/POS.csproj`. A file existing on disk is not enough.

## 18. Permissions and state rules

Use the service's actual `ClaimActionType` mapping:

| UI behavior | Typical permission |
|---|---|
| Open, list, search, view detail | View |
| Create, post, receive, checkout, export, initial print | Add |
| Edit, approve, apply cost, reactivate | Edit |
| Deactivate, cancel, void, unlink | Delete when required by that service |

Required state behavior:

- Hide navigation the user cannot view.
- If the user can view but cannot act, show the screen and disable the action with an explanatory tooltip.
- Completed sales, receipts, payments, movements, returns, refunds, and posted counts are immutable.
- Draft-only actions must disappear or disable after transition.
- Never expose IDs, request hashes, revisions, or row-version bytes as editable values.

## 19. Concurrency, idempotency, and validation

- Preserve every returned `Revision` exactly and send it back on the next edit/lifecycle request.
- Preserve stock-count `RowVersion` and submit it as the expected row version.
- Generate a `RequestId` once per new posting session.
- Reuse the same request ID only for an uncertain retry with identical details.
- Generate a new request ID after a known rejection followed by any edit.
- Disable posting controls immediately to prevent double submission.
- On a conflict, prompt the operator to reload and review; never silently overwrite.
- Use `DXValidationProvider`/`DXErrorProvider` for field feedback, then display service errors in business language.
- Always re-enable controls in `finally`, unless the form closes after a confirmed successful commit.

## 20. Recommended implementation work packages

### U1.0 — Shell and shared presentation infrastructure

- [ ] Implement named ribbon/pages/groups/status items.
- [ ] Implement workspace navigation/disposal.
- [ ] Add busy, error, time, money, paging, and permission helpers.
- [ ] Convert SignIn to consistent DevExpress controls.

### U1.1 — Cashier-ready vertical workflow

- [ ] Register maintenance.
- [ ] Shift open, cash movement, and close workflow.
- [ ] Sales register/cart.
- [ ] Payment and checkout completion.
- [ ] Held sales.
- [ ] Receipt search/detail/print/reprint.

### U1.2 — Post-sale workflow

- [ ] Return eligibility and posting UI.
- [ ] Refund tender and disposition UI.
- [ ] Optional exchange linkage.

### U1.3 — Catalog and inventory

- [ ] Redesign product list/editor and price history.
- [ ] Replace category form with category workspace/editor.
- [ ] Implement balance, alert, movement, adjustment, and reconciliation pages.
- [ ] Implement stock-count history/editor/posting.

### U1.4 — Supply workflow

- [ ] Supplier list/editor/details/product links/history.
- [ ] Purchase-order list/editor/transitions.
- [ ] Ordered and direct receiving.
- [ ] Cost proposal confirmation.
- [ ] Purchase returns.

### U1.5 — Customers

- [ ] Customer list/editor/details.
- [ ] Customer purchase/return history navigation.

### U1.6 — Management visibility

- [ ] Finish and include dashboard.
- [ ] Implement reports workspace.
- [ ] Implement CSV export and summary print UI.

### U1.7 — Administration and maintenance completion

- [ ] Finish users, roles, modules, and audit consistency.
- [ ] Finish settings and tax-history experience.
- [ ] Implement health, backup, restore, and logs UI.

### U1.8 — System-wide acceptance

- [ ] Verify every role's navigation and action permissions.
- [ ] Verify loading, empty, success, validation, authorization, conflict, and unexpected-error states.
- [ ] Verify keyboard navigation, tab order, default/cancel buttons, readable scaling, and accessibility labels.
- [ ] Run checkout-to-receipt, return, stock-count, purchasing, reporting, backup, and restore acceptance scenarios.
- [ ] Verify physical receipt printing and reprint labeling.
- [ ] Run representative multi-workstation concurrency and soak tests.

## 21. Build and verification gate for every work package

Before marking a package complete:

1. Add every new source/designer/resource file to `POS.csproj`.
2. Build the full solution in Release.
3. Run `scripts/verify.ps1 -Configuration Release -FullSolution`.
4. Confirm no form constructs an operational service that should come from `ApplicationCompositionRoot`.
5. Confirm grids bind DTOs rather than EF/domain entities.
6. Confirm permissions and document states control every action.
7. Confirm revisions, row versions, and request IDs survive the UI round trip.
8. Exercise loading, empty, validation, authorization, conflict, database-failure, and success states.
9. Record screenshots or a short acceptance note for the completed workflow.
10. Update the U1 status in `POS_IMPLEMENTATION_PLAN.md` only after the relevant acceptance checks pass.

## 22. U1 definition of done

U1 is complete only when:

- All 15 primary workspaces exist, compile, are created through composition, and are reachable through named permission-aware navigation.
- All required modal workflows exist and are owned by the initiating screen.
- Existing security/settings screens meet the same interaction and visual-state standard.
- Every screen uses backend DTOs and service entry points without duplicating authoritative calculations.
- All transaction history is read-only and historical receipt/report values come from stored snapshots.
- Every list is bounded/server-paged and handles loading, empty, error, conflict, and retry states.
- PHP, configured decimal places, and store-local time are displayed consistently.
- The full Release build and automated suite pass.
- End-to-end cashier, manager, inventory, purchasing, reporting, and maintenance acceptance scenarios pass.
- Physical printer and representative multi-workstation checks are recorded separately from automated success.

Until these conditions are satisfied, the correct project status remains: **backend B0-B6 complete; U1 in progress or not started; production acceptance incomplete**.
