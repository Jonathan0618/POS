# POS UI Assignment Distribution and Implementation Guide

**Updated:** 2026-09-17  
**Target:** DevExpress WinForms on .NET Framework 4.8  
**Scope:** U1 application UI only; backend services remain the source of business rules

## 1. Project analysis and distribution decision

The distributed UI is required. The repository has a completed service/backend layer, but the application UI is still the main unfinished phase. The canonical UI plan contains 47 `uc...`/`frm...` classes: 9 exist and compile, 1 (`ucDashboard`) exists but is excluded and incomplete, and 37 still need to be created. `SignIn` also exists and needs redesign, but it is outside that 47-class count because its name has no `uc` or `frm` prefix.

The original team ownership has been preserved. No valid UI was moved from one team to another. Misspelled class names were normalized to the canonical names so that contributors create the classes expected by `UI_IMPLEMENTATION_GUIDE.md` and `ApplicationCompositionRoot`.

### Removed assignment

- `RecieptReview` was removed from **Fayton, Layola and Labarentos**. A separate receipt-review form is not needed. Search, detail, stored receipt preview, initial print, and visibly marked reprint all belong in `ucReceipts`. Creating `RecieptReview` would duplicate state, printing rules, and permission handling.

### Normalized names (ownership unchanged)

| Original entry | Canonical class |
|---|---|
| `UcPurchaseOrder` | `ucPurchaseOrders` |
| `frmPurchaseEdit` | `frmPurchaseOrderEdit` |
| `UcGoodRecieving` | `ucGoodsReceiving` |
| `UcSalesRegister` | `ucSalesRegister` |
| `frmStockAdjust` | `frmStockAdjustment` |
| `UcStockCount` | `ucStockCounts` |
| `UcInventory` | `ucInventory` |
| `UcRegister` | `ucRegisters` |
| `UcCustomers` | `ucCustomers` |
| `UcSupplier` | `ucSuppliers` |
| `frmReciveDirect` | `frmReceiveDirect` |
| `frmRecieptCostProposal` | `frmReceiptCostProposal` |
| `frmPurchaseReturns` | `frmPurchaseReturn` |
| `frmCustomerDetail` | `frmCustomerDetails` |
| `UcCustomerShifts` | `ucCashierShifts` |
| `frmCloseShifts` | `frmCloseShift` |
| `UcReciepts` | `ucReceipts` |
| `UcReturns` | `ucReturns` |
| `UcDashboard` | `ucDashboard` |
| `UcReports` | `ucReports` |

### Added assignment coverage

The following required work was absent from the original distribution and has now been added without changing existing ownership: `SignIn`, `frmMain`, `ucProducts`, `frmProductPriceHistory`, `frmStockCount`, `frmStoreSettings`, `frmLockScreen`, `ucUsers`, `frmUserActivity`, `ucCategories`, `frmCategoryEdit`, `ucAudit`, `frmChangePassword`, `ucRolesPermissions`, `ucModules`, and `ucMaintenance`.

Legacy `frmCategory`, `frmAddProducts`, and `ucStock` are not new assignments. They remain only until `ucCategories`/`frmCategoryEdit`, `frmProductEdit`, and `ucInventory` are wired and verified, after which the legacy screens can be removed in a separate cleanup.

## 2. Rules that apply to every assignment

- A primary workspace is a DevExpress `XtraUserControl` hosted in `frmMain.pnlMain`. A focused edit, posting, payment, or confirmation flow is an owned modal `XtraForm`.
- Obtain services from `ApplicationCompositionRoot`; do not construct service instances inside controls. Dispose the service with its owning UI.
- Bind operation DTOs, not Entity Framework/domain entities. Services own authorization, totals, stock rules, tax, document transitions, and final validation.
- Preserve opaque `Revision`, `RowVersion`, and `RequestId` values. Never expose them as editable controls. On stale data, require reload and review rather than silently retrying.
- Disable action areas during calls, prevent duplicate submissions, restore state in `finally`, and provide loading, empty, populated, validation, authorization, conflict, and unexpected-error states.
- Use server paging (normally 50 rows), store-local dates converted from UTC, PHP money formatting using configured decimal places, and read-only views for completed/posted documents.
- Navigation visibility is permission-based, but services remain authoritative. If a screen is viewable but an action is forbidden, keep the screen visible and disable the action with an explanatory tooltip.
- Each team owns its designer, code-behind, composition-root factory/wiring, navigation hook where applicable, validation/error states, and verification of its assigned UI.

## 3. Assignment summary

| Team | Assigned UI classes |
|---|---|
| Norby & Rhogen | `ucPurchaseOrders`, `frmPurchaseOrderEdit`, `frmStockCount` |
| Siador, Boac and Lazaro | `ucGoodsReceiving`, `frmReceivePurchaseOrder`, `frmHeldSales`, `frmMain` |
| Bruno, Pumantoc and Barcelo | `ucSalesRegister`, `frmPayment`, `frmCheckoutComplete`, `frmProductPriceHistory` |
| Porin, Jeremy and Olipas | `frmStockAdjustment`, `ucStockCounts`, `ucInventory`, `frmProductEdit`, `ucProducts`|
| Wigan, Gano and Caoile | `ucRegisters`, `frmRegisterEdit`, `frmCashMovement`, `frmStoreSettings`, `frmLockScreen` |
| Tacio, Villanueva, Donato and Comising | `ucCustomers`, `frmCustomerEdit`, `frmOpenShift`, `ucUsers`, `frmUserActivity` |
| Javier, Micah and Ester | `ucSuppliers`, `frmSupplierEdit`, `frmSupplierDetails`, `frmReceiveDirect`|
| Mina, Meija and Melody (Triple M) | `frmReceiptCostProposal`, `frmPurchaseReturn`, `frmCustomerDetails`, `ucCashierShifts`, `ucAudit` |
| Fayton, Layola and Labarentos | `frmCloseShift`, `ucReceipts`, `frmSaleReturn`, `frmChangePassword`, `ucMaintenance` |
| Ballan, Balaoro and Estonelo | `ucReturns`, `ucDashboard`, `ucReports`, `ucRolesPermissions`, `ucModules`|

## 4. Detailed assignment guide

### Norby & Rhogen

#### `ucPurchaseOrders` - purchase-order workspace

- **Purpose:** Search, filter, page, and open purchase orders while making document status and remaining receipt work obvious.
- **Layout/data:** Use the shared list pattern. Filters map to `PurchaseOrderSearchDTO`: search, supplier, product, status, from date, and to date. Show order number, supplier code/name, status, created/ordered/cancelled dates, cancellation reason, total, and line count.
- **Actions/services:** Use `PurchaseOrderService.Search` and open `frmPurchaseOrderEdit` for add, view, edit, order, cancel, or receive. Refresh the row after the modal closes successfully.
- **Rules/acceptance:** Drafts may be edited. Ordered/partially received orders are read-only except for service-permitted receive/cancel actions. Use server paging and preserve the summary revision/detail identity.

#### `frmPurchaseOrderEdit` - purchase-order editor and transition form

- **Purpose:** Create or update a draft and perform explicit order/cancel transitions.
- **Layout/data:** Header contains `txtOrderNumber` and `slueSupplier`; line grid contains product, quantity, unit cost, and calculated line total. Detail mode also shows received and remaining quantities.
- **Actions/services:** Call `CreateDraft`, `UpdateDraft`, `OrderDraft`, `Cancel`, and `GetDetails`. A receive action opens the assigned `frmReceivePurchaseOrder` instead of posting receipt logic here.
- **Rules/acceptance:** Retain `Revision`; require a reason for cancellation; lock supplier and lines after Draft; do not calculate authoritative totals independently of the returned DTO; reload after successful transitions.

#### `frmStockCount` - stock-count editor/detail

- **Purpose:** Create, save, post, cancel, and review a stock-count document.
- **Layout/data:** Show read-only count ID, required reason, and lines with product/SKU, expected quantity, editable counted quantity, and calculated variance.
- **Actions/services:** Use `CreateDraft`, `UpdateDraft`, `PostDraft`, `CancelDraft`, `PostCount`, and `GetDetails` as appropriate.
- **Rules/acceptance:** Send returned `RowVersion` as `ExpectedRowVersion`; reload on conflict; posted/cancelled counts are fully read-only; posting is an explicit confirmed action.

### Siador, Boac and Lazaro

#### `ucGoodsReceiving` - receiving entry workspace

- **Purpose:** Give purchasing staff one navigation point for order-based receipts and direct supplier receipts.
- **Layout/data:** Show two clear entry actions, recent receiving context if available, and guidance distinguishing purchase-order receipt from direct receipt.
- **Actions/services:** Open `frmReceivePurchaseOrder` for ordered goods and the Javier/Micah/Ester-owned `frmReceiveDirect` for direct goods. Cost differences may open `frmReceiptCostProposal` after a successful receipt.
- **Rules/acceptance:** Do not duplicate either posting form in the workspace. Respect `Purchasing/View` and action permissions, prevent double-open/double-submit, and refresh relevant purchase-order state after receipt.

#### `frmReceivePurchaseOrder` - receive against an existing order

- **Purpose:** Post only eligible remaining quantities for an ordered or partially received purchase order.
- **Layout/data:** Use `txtReceiptNumber`, `sluePurchaseOrder`, `txtSupplierReference`, and a line grid showing product, ordered, previously received, remaining, and editable receipt quantity.
- **Actions/services:** Build `PurchaseOrderReceiptDTO` and call `GoodsReceiptService.ReceivePurchaseOrder` once per confirmed request.
- **Rules/acceptance:** Reject zero/negative or over-remaining quantities before submit, but leave final validation to the service. Disable submit immediately, show the resulting receipt ID, then offer cost proposal review only when applicable.

#### `frmHeldSales` - held-sale browser

- **Purpose:** List, inspect, resume, or cancel held carts without treating them as completed sales.
- **Layout/data:** Grid shows held time, register, customer, subtotal, tax, discount, total, and cashier. A detail pane shows product, SKU, quantity, unit price, tax, and line total.
- **Actions/services:** Use `SalesService.GetHeldSales`, `GetHeldSale`, and `CancelHeldSale(id, revision)`. Return the selected `HeldSaleDTO` to `ucSalesRegister` for resume; the register uses `SaveHeldSale` when holding.
- **Rules/acceptance:** Preserve `Revision`, confirm cancellation, prevent resuming stale/cancelled records, and do not create a second independent cart calculation path.

#### `frmMain` - application shell and navigation

- **Purpose:** Host every primary workspace and expose only authorized navigation while showing current session/register/shift/database state.
- **Layout/data:** Implement the named ribbon pages in the canonical guide: Home, Sales, Catalog, Inventory, Purchasing, Customers, Management, Administration, and Account. Add status items for current user, role, register, shift, database, and local time.
- **Actions/services:** Add one reusable workspace loader that disposes the old control, docks the new control, sets the title, and handles navigation errors. Add composition-root factories and permission mappings for all new workspaces.
- **Rules/acceptance:** Replace numeric item names/duplicate handlers; never leak undisposed controls/services; keep modal screens owned by the shell; keep logout, lock, and change-password flows working.

### Bruno, Pumantoc and Barcelo

#### `ucSalesRegister` - cashier selling workspace

- **Purpose:** Provide a scanner-first cart and checkout experience for an open cashier shift.
- **Layout/data:** Left side contains `txtBarcode`, product search/results, and optional customer. Right side contains cart lines, subtotal, tax, discount, total, Hold, Clear, and Pay. Bind returned `SaleCartDTO`/`SaleCartLineDTO` values.
- **Actions/services:** Use `SalesCartService.Create`, `SearchProducts`, `AddByBarcode`, `AddProduct`, `UpdateQuantity`, `RemoveProduct`, and `Recalculate`. Open `frmHeldSales` to resume and `frmPayment` to checkout.
- **Rules/acceptance:** Replace the bound cart after every service mutation; never recalculate authoritative totals in the control; require an active register/open shift; preserve resumed held-sale ID/revision into checkout.

#### `frmPayment` - tender entry and checkout submit

- **Purpose:** Capture one or more valid tenders and commit the sale exactly once.
- **Layout/data:** Show amount due, tendered, remaining, and change. Tender rows contain type, amount, and external reference. Card/EWallet require an external reference; cash omits it; store credit requires a customer.
- **Actions/services:** Build `SaleCheckoutDTO` with one generated `RequestId`, current register/shift, customer, discount, held-sale identity/revision, cart product/quantity lines, and tender rows; call `SalesService.Checkout`.
- **Rules/acceptance:** Disable Pay immediately. Reuse the same command/request ID only for an uncertain identical retry; generate a new one after the user changes rejected details. On success, open `frmCheckoutComplete`.

#### `frmCheckoutComplete` - committed-sale confirmation

- **Purpose:** Confirm that checkout committed and guide the cashier to printing or a fresh sale.
- **Layout/data:** Display receipt number, total, tender summary, change, and clear success state. Provide `btnPrintReceipt` and `btnNewSale`.
- **Actions/services:** Print through the receipt service/output path using the committed sale ID; signal the sales register to clear/recreate its cart only after confirmed success.
- **Rules/acceptance:** This form is read-only and must never resubmit checkout. Printing failure must not imply sale failure. Closing it must leave the completed sale immutable.

#### `SignIn` - authentication screen

- **Purpose:** Authenticate a user and establish role claims before opening the shell.
- **Layout/data:** Convert visible inputs to DevExpress controls: username, masked password, remember-username-only check box, sign-in button, Caps Lock warning, safe inline error, and logo/welcome panel.
- **Actions/services:** Call `UserService.AuthenticateAsync`, then `RoleService.SetupClaims` on success. Use `CredentialStore` only for the username preference.
- **Rules/acceptance:** Prevent duplicate submits, re-enable the form after failure, never store a password, never show technical exceptions, and preserve the existing async startup flow.

### Porin, Jeremy and Olipas

#### `ucInventory` - inventory operations workspace

- **Purpose:** Replace legacy `ucStock` with one workspace for balances, alerts, movements, adjustments, and reconciliation.
- **Layout/data:** Tabs/navigation pages bind `InventoryBalanceSearchDTO`, `InventoryAlertSearchDTO`, `StockMovementSearchDTO`, and `InventoryReconciliationSearchDTO`. Show missing balances and quantity mismatches explicitly.
- **Actions/services:** Use `InventoryLedgerService.SearchBalances`, `SearchAlerts`, `SearchMovements`, and `GetReconciliation`; open `frmStockAdjustment` for manual changes.
- **Rules/acceptance:** Positive/negative movements are visually distinct, reconciliation is diagnostic only, and there is no automatic repair button. Pages are server-paged and completed ledger facts are read-only.

#### `frmStockAdjustment` - manual stock adjustment

- **Purpose:** Post a traceable signed inventory delta for one product.
- **Layout/data:** Select product, show read-only expected quantity, enter signed quantity delta and required reason, and preview resulting quantity.
- **Actions/services:** Build `StockAdjustmentDTO` with one generated `RequestId` and call `InventoryLedgerService.PostAdjustment`.
- **Rules/acceptance:** Do not permit editing the expected quantity; warn about negative resulting stock while allowing the service/configuration to decide; reuse request ID only for an uncertain identical retry.

#### `ucStockCounts` - stock-count document list

- **Purpose:** Search stock-count documents and start/open count workflows.
- **Layout/data:** Filters include status, date range, creator, and product. Grid shows ID, status, creator, created/posted time, and product count while retaining request ID and row version in the row model.
- **Actions/services:** Use `StockCountService.SearchHistory` and open `frmStockCount` for new draft, draft edit, post, cancel, or read-only detail.
- **Rules/acceptance:** Use server paging; disable edit/post/cancel by document state; refresh after modal success; never display row version as editable text.

#### `ucProducts` - searchable product catalog

- **Purpose:** Redesign the existing catalog into a server-paged, status-aware product workspace.
- **Layout/data:** Filters are search, category, and active state. Show name, SKU, barcode, category, unit, selling/cost prices, quantity, threshold, expiry, and active state; render `MissingInventoryBalance` as an error badge.
- **Actions/services:** Use `InventoryService.SearchProducts`; open `frmProductEdit`; call deactivate/reactivate methods; open `frmProductPriceHistory`.
- **Rules/acceptance:** IDs remain hidden, permission/state controls action availability, PHP formatting is consistent, and the legacy add-product form is no longer opened after replacement verification.

#### `frmProductEdit` - product create/edit form

- **Purpose:** Create a product or update details and identifiers without posting opening stock.
- **Layout/data:** Product name, SKU, barcode, description, category, unit, selling price, cost price, buying threshold, and expiry date where supported.
- **Actions/services:** Use `InventoryService.AddProduct`, `UpdateProductDetails`, and `UpdateProductIdentifiers` with `InventoryDTO`/`ProductIdentifiersDTO`.
- **Rules/acceptance:** Do not include initial quantity; inventory changes belong to inventory operations. Validate unique identifiers through the service and do not partially claim success if one update fails.

#### `frmProductPriceHistory` - price audit history

- **Purpose:** Show immutable selling/cost price changes for a selected product.
- **Layout/data:** Read-only paged grid: changed time, user, register, previous/new selling price, previous/new cost, action, and correlation ID.
- **Actions/services:** Call `InventoryService.GetProductPriceHistory(productId, pageNumber, pageSize)`.
- **Rules/acceptance:** No editing or deletion; format money/time consistently; keep support IDs available but visually secondary.

### Wigan, Gano and Caoile

#### `ucRegisters` - register-station workspace

- **Purpose:** Search and manage physical/logical register stations.
- **Layout/data:** Search and active-state filters; grid fields are code, name, printer name, and active state while ID/revision stay hidden.
- **Actions/services:** Use `RegisterStationService.Search`, `GetDetails`, and `SetActive`; open `frmRegisterEdit` for create/edit.
- **Rules/acceptance:** Preserve revision for lifecycle actions, page on the server, and make active/inactive state unmistakable.

#### `frmRegisterEdit` - register-station editor

- **Purpose:** Create or edit register code, name, and printer assignment.
- **Layout/data:** `txtCode`, `txtName`, installed-printer lookup, and active checkbox for creation/context only.
- **Actions/services:** Bind `RegisterStationDTO` and call `RegisterStationService.Save`; obtain printer choices through settings/printer support rather than free-form assumptions.
- **Rules/acceptance:** Use `SetActive` from the workspace for later lifecycle changes, retain revision on edit, and reject blank/duplicate code through service validation.

#### `frmCashMovement` - cash-in/cash-out posting

- **Purpose:** Post a non-sale drawer movement against an open cashier shift.
- **Layout/data:** Show read-only register/shift, cash-in or cash-out choice, positive amount, required reason, and confirmation summary.
- **Actions/services:** Build `PostCashMovementDTO` with `ShiftRevision` and one generated `RequestId`; call `CashierShiftService.PostCashMovement`.
- **Rules/acceptance:** Require an open owned shift, prevent duplicate submit, preserve request identity for uncertain retry, and refresh the shift workspace after success.

#### `frmStoreSettings` - store, tax, register, printer, and receipt settings

- **Purpose:** Finish the existing settings form as a safe tabbed configuration editor.
- **Layout/data:** Store, Tax, Register/Printer, Receipt, and Tax History tabs. Include timezone lookup, installed-printer refresh, receipt preview, reload/revert, dirty marker, and tax-history grid.
- **Actions/services:** Use `StoreSettingsService.GetSettings`, `SaveSettings`, `GetTaxHistory`, `GetInstalledPrinters`, `BuildReceiptPreview`, and `PrintTestPage`.
- **Rules/acceptance:** Currency is read-only PHP; retain hidden IDs/revision; Save stays disabled until successful load and until changed; block accidental close with unsaved changes.

#### `frmLockScreen` - authenticated session lock

- **Purpose:** Protect an active session without treating lock as logout.
- **Layout/data:** Show current username, masked password, unlock, logout/cancel path, Caps Lock and safe retry/error state.
- **Actions/services:** Re-authenticate through `UserService`, restore claims through `RoleService`, and record successful unlock using the existing session service flow.
- **Rules/acceptance:** Keep the shell inaccessible while locked, prevent duplicate attempts, never reveal whether another account exists, and maintain keyboard/accessibility behavior.

### Tacio, Villanueva, Donato and Comising

#### `ucCustomers` - customer workspace

- **Purpose:** Search, page, maintain, and inspect customer records.
- **Layout/data:** Search and active-state filters; grid fields are code, name, phone, email, tax identifier, and active status, with address in detail and ID/revision hidden.
- **Actions/services:** Use `CustomerService.Search`, `GetDetails`, and `SetActive`; open `frmCustomerEdit` and the Triple-M-owned `frmCustomerDetails`.
- **Rules/acceptance:** Preserve revision, favor deactivate/reactivate over deletion, page on the server, and refresh after editor/detail actions.

#### `frmCustomerEdit` - customer profile editor

- **Purpose:** Create or update a customer profile.
- **Layout/data:** Code, name, phone, email, address, and tax identifier with clear required/format validation.
- **Actions/services:** Bind `CustomerDTO` and call `CustomerService.Save`.
- **Rules/acceptance:** Retain hidden ID/revision, handle stale edits with reload guidance, and do not mix sales-history editing into this form.

#### `frmOpenShift` - cashier-shift opening

- **Purpose:** Start a register shift with a declared opening cash amount.
- **Layout/data:** Active register lookup, opening cash, current user summary, and explicit confirmation.
- **Actions/services:** Build `OpenCashierShiftDTO` with register, opening cash, and one generated `RequestId`; call `CashierShiftService.Open`.
- **Rules/acceptance:** Prevent opening a conflicting shift, disable submit immediately, and return the new shift ID so shell and shift status can refresh.

#### `ucUsers` - user administration workspace

- **Purpose:** Extend existing user management with search, paging, account state, role assignment, password reset, and activity access.
- **Layout/data:** Search, enabled-state, and role filters; show username/full name, role, and active status. Add loading/empty/error states and permission-aware actions.
- **Actions/services:** Use `GetUsersPageAsync`, add/edit user flows, `SetUserEnabledAsync`, `ResetPasswordByAdministratorAsync`, and open `frmUserActivity`.
- **Rules/acceptance:** Never display password values; make enable/disable and reset actions explicit/confirmed; refresh after changes; keep async operations cancellable by control lifetime.

#### `frmUserActivity` - read-only user activity viewer

- **Purpose:** Give administrators a focused history for one user.
- **Layout/data:** Header identifies the user; read-only grid shows date logged, category, action, and details. Keep activity ID hidden unless support needs it.
- **Actions/services:** Call `UserService.GetUserActivityAsync` with the selected user and appropriate limit/page behavior supported by the service.
- **Rules/acceptance:** No mutation actions, user-safe empty/error states, local-time display, and cancellation when the modal closes.

### Javier, Micah and Ester

#### `ucSuppliers` - supplier workspace

- **Purpose:** Search, page, maintain, and inspect suppliers.
- **Layout/data:** Search and active-state filters; show code, name, contact, phone, email, tax ID, and active state. Keep address in details and revision hidden.
- **Actions/services:** Use `SupplierService.Search`, `GetDetails`, and `SetActive`; open `frmSupplierEdit` and `frmSupplierDetails`.
- **Rules/acceptance:** Preserve revision, page on the server, favor lifecycle state over deletion, and refresh after modal changes.

#### `frmSupplierEdit` - supplier profile editor

- **Purpose:** Create or update supplier master data.
- **Layout/data:** Code, name, contact name, phone, email, address, and tax identifier.
- **Actions/services:** Bind `SupplierDTO` and call `SupplierService.Save`.
- **Rules/acceptance:** Retain ID/revision, show field validation without replacing service validation, and give reload guidance on a stale edit.

#### `frmSupplierDetails` - supplier relationships and history

- **Purpose:** Combine read-only profile, supplier-product link maintenance, and purchase history.
- **Layout/data:** Tabs for Profile, Products, and Purchase History. Product links show product/SKU, supplier SKU, default cost, lead time, active state; history filters by document kind/number/date/product.
- **Actions/services:** Use `GetProducts`, `SaveProductLink`, `UnlinkProduct`, and `GetPurchaseHistory`.
- **Rules/acceptance:** Preserve each link revision, confirm unlink, page both grids, and do not edit purchase documents from this detail form.

#### `frmReceiveDirect` - direct supplier receipt

- **Purpose:** Receive stock without a purchase order while retaining supplier and cost evidence.
- **Layout/data:** Receipt number, supplier, supplier reference, and lines with product, positive quantity, and unit cost.
- **Actions/services:** Build `DirectGoodsReceiptDTO` and call `GoodsReceiptService.ReceiveDirect` once.
- **Rules/acceptance:** Disable duplicate submit, display posted receipt ID, update inventory only through the service, and hand any cost-change decision to `frmReceiptCostProposal`.

#### `ucCategories` - category workspace

- **Purpose:** Replace legacy `frmCategory` with searchable, paged category maintenance.
- **Layout/data:** Search and active-state filters; grid shows name, description, product count, and active state while ID/revision stay hidden.
- **Actions/services:** Use `InventoryService.SearchCategories`, deactivate/reactivate calls, and open `frmCategoryEdit`.
- **Rules/acceptance:** Do not destructively delete a category with products; prefer deactivation; refresh product/category lookups after successful changes.

#### `frmCategoryEdit` - category editor

- **Purpose:** Create or update category name and description.
- **Layout/data:** `txtName`, `memDescription`, Save, Cancel, validation summary, and busy state.
- **Actions/services:** Use `InventoryService.AddCategory` or `UpdateCategory` with the supported category model.
- **Rules/acceptance:** Retain ID/revision on edit, handle duplicates/service errors safely, and replace rather than extend legacy `frmCategory` after verification.

### Mina, Meija and Melody (Triple M)

#### `frmReceiptCostProposal` - received-cost review

- **Purpose:** Require explicit review before a received unit cost changes the product catalog cost.
- **Layout/data:** Show product, current catalog cost, received unit cost, difference, source receipt line, and Apply/Skip choices.
- **Actions/services:** Use `GoodsReceiptService.GetCostProposal` and submit `ApplyReceiptCostDTO` through `ApplyReceivedCost`.
- **Rules/acceptance:** Send `GoodsReceiptLineId` and `ExpectedCurrentCatalogCost`; never apply automatically; treat a changed expected cost as a conflict requiring reload/review.

#### `frmPurchaseReturn` - return goods to supplier

- **Purpose:** Post a return against quantities previously received from a supplier.
- **Layout/data:** Return number, goods-receipt lookup, required reason, and lines showing product, received, previously returned, eligible, and editable return quantity.
- **Actions/services:** Build `PurchaseReturnPostDTO` with retained goods-receipt-line IDs and call `PurchaseReturnService.Post`.
- **Rules/acceptance:** Do not exceed eligible quantity, do not mutate the original receipt, disable repeat submission, and show posted return ID/updated stock on success.

#### `frmCustomerDetails` - customer profile and transaction history

- **Purpose:** Show one customer and a paged history of sales and returns without turning the history into an editor.
- **Layout/data:** Profile tab plus History filters for document kind/number/date; show document number, status, event time, total, and related transaction context returned by customer history DTOs.
- **Actions/services:** Use `CustomerService.GetDetails` and `GetHistory(CustomerHistorySearchDTO)`.
- **Rules/acceptance:** History is read-only and server-paged; navigate to a receipt/return workspace for deeper action rather than editing facts here.

#### `ucCashierShifts` - shift operations workspace

- **Purpose:** Search shift history and present the current shift as an operational status card.
- **Layout/data:** Filters for register, cashier, status, and dates. Show opened/closed times, opening cash, sales, movements, expected cash, counted cash, and variance where authorized.
- **Actions/services:** Use `CashierShiftService.Search` and `GetDetails`; open `frmOpenShift`, `frmCashMovement`, and `frmCloseShift`.
- **Rules/acceptance:** Cash-in/out and close are unavailable without an appropriate open shift; preserve revision; refresh shell/status after every successful shift action.

#### `ucAudit` - audit event and archive workspace

- **Purpose:** Finish the existing append-only event browser and archive/recovery actions.
- **Layout/data:** Filters for register, unattributed events, category, local date range converted to UTC, search, entity, action, and correlation ID. Master grid shows event summary; detail pane shows readable old/new values.
- **Actions/services:** Use `AuditService.SearchAsync`/retention summary and existing archive export, verify, and restore services with progress and cancellation.
- **Rules/acceptance:** Events are never edited/deleted; long actions cancel safely; archive permissions/states are explicit; technical payloads are readable without exposing stack traces.

### Fayton, Layola and Labarentos

#### `frmCloseShift` - shift close and cash reconciliation

- **Purpose:** Review expected cash, capture counted cash, show variance, and close an open shift.
- **Layout/data:** Read-only opening/expected cash, counted cash (blind or visible per policy), calculated variance preview, reason/confirmation as required, and shift/register identity.
- **Actions/services:** Build `CloseCashierShiftDTO` with shift revision and one generated `RequestId`; call `CashierShiftService.Close`.
- **Rules/acceptance:** Disable duplicate close, preserve request/revision, handle conflict by reload, and refresh shell plus `ucCashierShifts` after success.

#### `ucReceipts` - receipt search, detail, preview, and printing

- **Purpose:** Own the complete receipt review workflow; no separate `RecieptReview` form is required.
- **Layout/data:** Filters for search, date range, register, and cashier; summary grid shows receipt number, sale time, status, register, cashier, customer, total, and currency. Detail displays stored header, lines, totals, payments, and footer from `SaleReceiptDTO`.
- **Actions/services:** Use `SaleReceiptService.Search`, `Get`, `Print(saleId, false)`, and permission-controlled `Print(saleId, true)`.
- **Rules/acceptance:** Never rebuild historical receipt values from current product data; clearly label reprints; printing failure does not change sale status; detail is read-only and server-paged search is used.

#### `frmSaleReturn` - return/refund posting form

- **Purpose:** Select eligible sold lines, disposition returned stock, allocate refunds, and optionally link an exchange sale.
- **Layout/data:** Show eligibility header and late-approval warning; line grid shows purchased/returned/eligible quantities and refund estimate with editable return quantity/disposition; refund grid shows eligible tender amounts.
- **Actions/services:** Use `SaleReturnService.GetEligibility` and `Post(SaleReturnPostDTO)` with generated request ID, current register/shift, reason, optional exchange sale, line selections, and refund rows.
- **Rules/acceptance:** Restock returns to sellable inventory; Damaged/Quarantine does not. Never exceed eligibility; require explicit approval where service rules demand it; show immutable result details after posting.

#### `frmChangePassword` - authenticated password change

- **Purpose:** Finish the existing modal for securely changing the current user's password.
- **Layout/data:** Current password, new password, confirmation, validation summary, show/hide controls if appropriate, and busy state.
- **Actions/services:** Call `UserService.ChangePasswordAsync` and display only safe operation-result messages.
- **Rules/acceptance:** Never log/store password text, clear sensitive controls on failure/close, prevent duplicate submit, and provide correct keyboard tab/default-button behavior.


#### `ucMaintenance` - database health, backup, restore, and logs

- **Purpose:** Provide authorized operational maintenance without allowing overwrite of the live database.
- **Layout/data:** Health header and checks grid, log-folder action, Create Backup, and Restore to New Database. Show application/database/migration versions and healthy/warning/failed states.
- **Actions/services:** Use `DatabaseMaintenanceService.GetHealth`, `CreateBackup`, and `RestoreToNewDatabase` through the maintenance/backup contract.
- **Rules/acceptance:** Validate rooted `.bak` paths and target database name; show the exact warning: **"Restore creates a new database and never overwrites the live POS database."** Provide progress/cancellation where supported and never add a live overwrite option.

### Ballan, Balaoro and Estonelo

#### `ucReturns` - returns entry workspace

- **Purpose:** Search/select a receipt, display return eligibility, and launch the focused posting form.
- **Layout/data:** Receipt lookup/result context, prior return summary, eligibility status, and clear action to open `frmSaleReturn`.
- **Actions/services:** Coordinate receipt selection with `SaleReturnService.GetEligibility`; pass the selected sale identity to `frmSaleReturn` and refresh after a successful return.
- **Rules/acceptance:** Do not duplicate posting grids or business calculations from the modal; disable action for ineligible receipts; explain deadline/approval status clearly.

#### `ucDashboard` - management overview

- **Purpose:** Finish the excluded existing source, compile it, and provide scoped operational/financial visibility.
- **Layout/data:** Date/register filters; cards for gross sales, refunds, net sales, tax, discounts, estimated cost/margin, transactions, and returns; grids for top products and stock alerts; alert cards for expiring products/open shifts/issues.
- **Actions/services:** Bind only `ManagementReportService.GetDashboard(ManagementFilterDTO)` output and wire drill-down to the relevant workspace.
- **Rules/acceptance:** Include the files in `POS.csproj` only after real binding/states are implemented; every card shows scope; red is reserved for actionable exceptions; do not recalculate totals in UI.

#### `ucReports` - management reporting workspace

- **Purpose:** Run sales, item/category, tender, inventory, movement, purchasing, shift, and audit-summary reports from one consistent filter.
- **Layout/data:** Shared date/register/user/category/product/supplier/customer/status/max-row filters, report selector, financial summary band, result grid, Export CSV, and Print Summary.
- **Actions/services:** Call `ManagementReportService.GetReport(filter)` once, bind the selected collection, and pass that same filter to `ReportOutputService.ExportCsv` or `PrintSummary`.
- **Rules/acceptance:** Cap rows at 500, show exact filter/generated time, never recompute backend totals, and use cancellation/busy states for long work.

#### `ucRolesPermissions` - role and permission matrix

- **Purpose:** Finish existing role management as a resource-grouped permission editor.
- **Layout/data:** Role list plus claims matrix for resource/module and View/Add/Edit/Delete; role name actions and Save Permissions.
- **Actions/services:** Use `RoleService.GetRoles`, add/update/delete role, `GetClaims`, and claim add/update operations.
- **Rules/acceptance:** Visually lock System Administrator, explain why it cannot be removed, preserve seeded resource meanings, and reload safely after saves/conflicts.

#### `ucModules` - authorization resource maintenance

- **Purpose:** Finish existing module/resource maintenance while protecting stable authorization codes.
- **Layout/data:** Module list/tree, name editor, Add, Rename, Delete, Reload, validation, empty, busy, and error states.
- **Actions/services:** Use `ModuleService.GetAllModules`, `AddModule`, `UpdateModule`, and `DeleteModule`.
- **Rules/acceptance:** Do not casually rename/delete seeded `ResourceCodes`; enforce permissions; show dependencies before destructive actions; refresh claims/navigation effects safely.

## 5. Integration order and completion gate

Implement shared shell/composition wiring first, then deliver in this order: cashier flow, post-sale flow, catalog/inventory, purchasing, customers/shifts, dashboard/reports, and administration/maintenance. Teams can build designers in parallel, but integration must respect those dependencies.

An assigned UI is complete only when:

1. It compiles and is included in `POS.csproj` where required.
2. Its composition-root factory owns and disposes services correctly.
3. Navigation/modal ownership and permission behavior are wired.
4. DTO fields, paging, revisions/row versions/request IDs, state transitions, and async busy/error states are verified.
5. Posted/completed facts remain read-only and no business totals/rules are reimplemented in the UI.
6. The solution builds in Release and applicable tests pass.

`UI_IMPLEMENTATION_GUIDE.md` remains the canonical control/DTO contract. `UI_REMAINING_IMPLEMENTATION_GUIDE.md` remains the current-code inventory and recommended implementation sequence. This file defines team ownership and the assignment-level acceptance guide.
