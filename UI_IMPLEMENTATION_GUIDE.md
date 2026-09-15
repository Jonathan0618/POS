# POS UI Implementation Guide

**Prepared:** 2026-09-07  
**Purpose:** designer-facing specification for implementing U1 against the existing backend  
**UI framework:** DevExpress WinForms only for new or redesigned visual controls

## 1. Non-negotiable implementation rules

- Keep `frmMain` as the application shell. Put primary workflows in `UserControl`s hosted in `pnlMain`.
- Keep focused edits, confirmations, payment, shift opening/closing, and restore operations in owned modal `XtraForm`s.
- Store settings remain an owned `frmStoreSettings` exception.
- Use service instances supplied by `ApplicationCompositionRoot`; do not construct services inside controls.
- Dispose the service when its owning form/control is disposed.
- Check navigation permission before showing an item, but rely on the service as the real authorization boundary.
- Never bind domain entities directly when an operations DTO exists.
- Never allow editing of completed sales, posted receipts, stock movements, returns, refunds, or audit events.
- Display money as `PHP` using the configured `MoneyDecimalPlaces`.
- Convert stored UTC timestamps to the configured store time zone for display. Label UTC values explicitly if no conversion is made.
- Use `DXValidationProvider` or `DXErrorProvider`; show business exceptions in `XtraMessageBox` without exposing stack traces.
- Disable the action area and show a `SplashScreenManager`/wait overlay during database calls. Restore controls in `finally`.
- Every list needs loading, empty, populated, validation-error, authorization-error, and unexpected-error states.

### Concurrency and retry fields

These values are backend tokens, not user-editable fields:

- Preserve every `Revision` string returned by a detail/search DTO and send it unchanged on update, deactivate, cancel, post, or unlink.
- Preserve `byte[] RowVersion` from a stock count and send it as `ExpectedRowVersion` when editing/posting/cancelling.
- Generate a nonempty `Guid RequestId` once when a new posting/edit session begins. Reuse that same value if the result is uncertain and the user retries the identical operation.
- Generate a new `RequestId` after the user changes transaction details following a known rejection.
- After every successful write, reload the record to obtain the new revision.
- On stale/conflict feedback, do not silently resubmit. Ask the user to reload and review.

## 2. Naming standard

| Purpose | Prefix | Examples |
|---|---|---|
| User control | `uc` | `ucSalesRegister`, `ucInventory` |
| Modal form | `frm` | `frmPayment`, `frmSupplierEdit` |
| Grid / view | `gc` / `gv` | `gcProducts`, `gvProducts` |
| Text / memo | `txt` / `mem` | `txtSearch`, `memReason` |
| Lookup / search lookup | `lue` / `slue` | `lueStatus`, `slueSupplier` |
| Date range | `de` | `deFrom`, `deTo` |
| Number / money | `spn` | `spnQuantity`, `spnAmount` |
| Toggle | `chk` / `tgl` | `chkActive`, `tglMismatchesOnly` |
| Button | `btn` | `btnSave`, `btnReload` |
| Label | `lbl` | `lblTotal`, `lblEmptyState` |
| Layout / group | `lc` / `lcg` | `lcMain`, `lcgFilters` |
| Tabs | `tc` / `tp` | `tcSupplier`, `tpHistory` |
| Ribbon items | `bbi` | `bbiProducts`, `bbiSalesRegister` |
| Error/validation | `dxError` / `dxValidation` | `dxError`, `dxValidation` |

Rename existing numeric controls when redesigning them. New names must describe intent rather than designer creation order.

## 3. Shell and navigation

Use these ribbon pages, groups, and item names:

| Ribbon page | Group | Item name | Opens | Permission |
|---|---|---|---|---|
| `rpHome` | `rpgOverview` | `bbiDashboard` | `ucDashboard` | `Dashboard/View` |
| `rpSales` | `rpgRegister` | `bbiSalesRegister` | `ucSalesRegister` | `Sales/View` + `Sales/Add` |
| `rpSales` | `rpgRegister` | `bbiHeldSales` | held-sales panel in register | `Sales/View` |
| `rpSales` | `rpgAfterSale` | `bbiReceipts` | `ucReceipts` | `Sales/View` |
| `rpSales` | `rpgAfterSale` | `bbiReturns` | `ucReturns` | `Returns/View` |
| `rpSales` | `rpgCash` | `bbiShifts` | `ucCashierShifts` | `Shifts/View` |
| `rpCatalog` | `rpgCatalog` | `bbiProducts` | `ucProducts` | `Products/View` |
| `rpCatalog` | `rpgCatalog` | `bbiCategories` | `ucCategories` | `Products/View` |
| `rpInventory` | `rpgStock` | `bbiInventory` | `ucInventory` | `Inventory/View` |
| `rpInventory` | `rpgStock` | `bbiStockCounts` | `ucStockCounts` | `Inventory/View` |
| `rpPurchasing` | `rpgSuppliers` | `bbiSuppliers` | `ucSuppliers` | `Suppliers/View` |
| `rpPurchasing` | `rpgOrders` | `bbiPurchaseOrders` | `ucPurchaseOrders` | `Purchasing/View` |
| `rpPurchasing` | `rpgOrders` | `bbiReceiving` | `ucGoodsReceiving` | `Purchasing/View` |
| `rpCustomers` | `rpgCustomers` | `bbiCustomers` | `ucCustomers` | `Customers/View` |
| `rpManagement` | `rpgReports` | `bbiReports` | `ucReports` | `Reports/View` |
| `rpAdministration` | `rpgSecurity` | `bbiUsers` | existing `ucUsers` | `Users/View` |
| `rpAdministration` | `rpgSecurity` | `bbiRoles` | existing `ucRolesPermissions` | `Roles/View` |
| `rpAdministration` | `rpgSecurity` | `bbiModules` | existing `ucModules` | `Modules/View` |
| `rpAdministration` | `rpgAudit` | `bbiAudit` | existing `ucAudit` | `Audit/View` |
| `rpAdministration` | `rpgConfiguration` | `bbiStoreSettings` | existing `frmStoreSettings` | `Settings/View` |
| `rpAdministration` | `rpgMaintenance` | `bbiMaintenance` | `ucMaintenance` | `Maintenance/View` |
| `rpAccount` | `rpgSession` | `bbiChangePassword` | existing `frmChangePassword` | authenticated user |
| `rpAccount` | `rpgSession` | `bbiLock` | existing `frmLockScreen` | authenticated user |
| `rpAccount` | `rpgSession` | `bbiLogout` | logout flow | authenticated user |

Status bar fields: `bsiCurrentUser`, `bsiRole`, `bsiRegister`, `bsiShift`, `bsiDatabaseStatus`, and `bsiLocalTime`.

## 4. Shared list-screen pattern

Use a `LayoutControl` with:

1. `lcgHeader`: title, one-line description, primary action.
2. `lcgFilters`: search/filter editors plus `btnApplyFilters`, `btnClearFilters`, and `btnReload`.
3. `gc...` + `gv...`: read-only results with focused-row actions.
4. `pnlPaging`: `btnFirstPage`, `btnPreviousPage`, `lblPage`, `btnNextPage`, `btnLastPage`, `luePageSize`.
5. `lblEmptyState`: centered message shown only for zero rows.

Default page size is 50. Offer 25, 50, 100, and 200 only when the backend permits it. Do not fetch all records for a grid.

## 5. Authentication, users, roles, and audit

### Sign-in — existing `SignIn`

Replace the remaining standard visual controls with DevExpress equivalents:

- `txtUserName`: `TextEdit`, bound to username input.
- `txtPassword`: `ButtonEdit` or `TextEdit` with password masking.
- `chkRememberMe`: `CheckEdit`; it remembers username only.
- `btnSignIn`: `SimpleButton`.
- `lblCapsLock`: warning label.
- `lblError`: user-safe authentication error.
- `peLogo`: `PictureEdit` replacing the standard `PictureBox` if redesigned.

Call `UserService.AuthenticateAsync(username, password)`, then `RoleService.SetupClaims(roleId, username)`. Failed authentication must re-enable the form. Visual outcome: a clean two-column welcome panel, prominent sign-in action, inline validation, and no technical error text.

### Users — extend existing `ucUsers`

Filters: `txtSearch`, `lueEnabledState`, `slueRole`, paging controls. Bind rows to `UserDTO` fields. Primary actions: `btnAddUser`, `btnEditUser`, `btnEnableDisable`, `btnResetPassword`, `btnViewActivity`.

Activity modal `frmUserActivity` grid fields from `UserActivityDTO`: `DateLogged`, `Category`, `Action`, `Details`. Keep `ActivityId` hidden unless needed for support.

### Roles and permission matrix — extend existing `ucRolesPermissions`

- Role grid fields: `Id` hidden, `Name` visible.
- Permission grid fields from `RoleClaimDTO`: resource/module name, `CanView`, `CanAdd`, `CanEdit`, `CanDelete`.
- Controls: `txtRoleName`, `btnAddRole`, `btnRenameRole`, `btnDeleteRole`, `btnSavePermissions`.
- Visually lock the System Administrator row and explain why it cannot be removed.

### Modules — extend existing `ucModules`

Bind `ModuleDTO`. Use `txtModuleName`, `btnAddModule`, `btnRenameModule`, and `btnDeleteModule`. Prefer stable resource codes already defined in `ResourceCodes`; do not casually rename seeded resources.

### Audit — extend existing `ucAudit`

Filters map to `AuditSearchDTO`:

| Control | DTO field |
|---|---|
| `lueRegister` | `RegisterCode` |
| `chkUnattributedOnly` | `UnattributedOnly` |
| `lueCategory` | `Category` |
| `deFrom` | `FromUtc` |
| `deTo` | convert inclusive local end date to `ToUtcExclusive` |
| `txtSearch` | `Search` |
| `txtEntity` | `Entity` |
| `txtAction` | `Action` |
| `txtCorrelationId` | parsed `CorrelationId` |

Grid fields from `AuditEventDTO`: `LocalTime`, `Username`, `RegisterCode`, `Category`, `Entity`, `RecordId`, `Action`, `CorrelationId`. Put `OldValue` and `NewValue` in a read-only detail panel, not wide grid columns. Existing archive actions remain `btnExportArchive`, `btnVerifyArchive`, and `btnRestoreAuditArchive` with progress/cancellation. Visual outcome: append-only event browser with a master row and readable detail pane.

## 6. Store and register configuration

### Store settings — retain existing `frmStoreSettings`

Existing names already align with `StoreSettingsDTO`: `txtStoreName`, `memAddress`, `txtPhone`, `txtEmail`, `txtTaxIdentifier`, `txtCurrencyCode`, `spnMoneyDecimalPlaces`, `txtTimeZone`, `memReceiptFooter`, `chkAllowNegativeStock`, `txtTaxName`, `spnTaxRate`, `chkTaxInclusive`, `txtRegisterCode`, `txtRegisterName`, `txtPrinterName`, `txtReceiptPrefix`, and `spnNextReceiptNumber`.

Required adjustments:

- `txtCurrencyCode` is read-only and displays `PHP`.
- Use a time-zone lookup named `lueTimeZone`, even if the current field is named `txtTimeZone`.
- Do not display `Revision`, `StoreSettingId`, `TaxRateId`, or `RegisterStationId`; retain them in the loaded DTO.
- Keep `btnRefreshPrinters`, `btnTestPrinter`, `btnSave`, `btnCancel`, and add `btnPreviewReceipt`, `btnReload`, `btnRevert` if absent.
- Add `gcTaxHistory`/`gvTaxHistory` bound to `TaxRateHistoryDTO`: `Name`, `Rate`, `IsInclusive`, `IsActive`, `EffectiveFromUtc`, `EffectiveToUtc`.

Visual outcome: tabbed owned settings form with Store, Tax, Register/Printer, Receipt, and Tax History pages; unsaved-change marker in the title; Save disabled until a successful load.

### Register maintenance — `ucRegisters` + `frmRegisterEdit`

Use `RegisterStationService`.

- Filters: `txtSearch`, `lueActiveState` -> `Search(search, active, pageNumber, pageSize)`.
- Grid fields from `RegisterStationDTO`: `Code`, `Name`, `PrinterName`, `IsActive`; hide `Id`, `Revision`.
- Editor controls: `txtCode`, `txtName`, `luePrinterName`, `chkIsActive` (create only; lifecycle actions thereafter).
- Calls: `GetDetails`, `Save`, `SetActive(id, active, revision)`.

## 7. Product catalog and categories

### Products — redesign/extend existing `ucProducts`

Filters map to `ProductSearchDTO`: `txtSearch` -> `Search`, `slueCategory` -> `CategoryId`, `lueActiveState` -> `IsActive`.

Grid fields from `ProductSummaryDTO`:

`Name`, `Sku`, `Barcode`, `CategoryName`, `Unit`, `SellingPrice`, `CostPrice`, `Quantity`, `BuyingThreshold`, `ExpiryDate`, `IsActive`. Hide `Id` and `CategoryId`. Show `MissingInventoryBalance` as a red “Missing balance” badge rather than displaying zero.

Actions: `btnAddProduct`, `btnEditProduct`, `btnDeactivateReactivate`, `btnPriceHistory`, `btnReload`. Use `InventoryService.SearchProducts`.

Product editor `frmProductEdit` fields:

- `txtProductName` -> `InventoryDTO.ProductName`
- `txtSku` -> `InventoryDTO.Sku` / `ProductIdentifiersDTO.Sku`
- `txtBarcode` -> `InventoryDTO.Barcode`
- `memDescription` -> `InventoryDTO.Description`
- `slueCategory` -> `InventoryDTO.CategoryId`
- `lueUnit` -> `InventoryDTO.Unit`
- `spnSellingPrice` -> `InventoryDTO.SellingPrice`
- `spnCostPrice` -> `InventoryDTO.CostPrice`
- `spnBuyingThreshold` -> `InventoryDTO.BuyingThreshold`
- `deExpiryDate` -> product expiry value when exposed by the editor model

Use `AddProduct`, `UpdateProductDetails`, `UpdateProductIdentifiers`, `DeactivateProduct`, and `ReactivateProduct`. Do not offer an initial-quantity field here; opening inventory belongs to inventory operations.

Price history modal `frmProductPriceHistory`: grid fields `ChangedUtc`, `UserId`, `RegisterCode`, `PreviousSellingPrice`, `SellingPrice`, `PreviousCostPrice`, `CostPrice`, `Action`, `CorrelationId` from `ProductPriceChangeDTO`.

Visual outcome: searchable catalog with status chips, money formatting, stock badge, and a compact right-side action menu.

### Categories — `ucCategories` + `frmCategoryEdit`

Replace the old list form when convenient. Filters map to `CategorySearchDTO.Search` and `IsActive`. Grid fields: `Name`, `Description`, `ProductCount`, `IsActive`; hide `Id`, `Revision`. Editor fields: `txtName`, `memDescription`. Calls: `SearchCategories`, `AddCategory`, `UpdateCategory`, `DeactivateCategory(id, revision)`, `ReactivateCategory(id, revision)`. Disable destructive deletion when `ProductCount > 0` and favor deactivation.

## 8. Inventory

### Inventory workspace — `ucInventory`

Use a `NavigationFrame` or tabs: Balances, Alerts, Movements, Adjustments, and Reconciliation.

#### Balances

Filters map to `InventoryBalanceSearchDTO`: `txtBalanceSearch`, `slueBalanceCategory`, `lueProductActiveState`. Grid fields: `ProductName`, `Sku`, `Barcode`, `CategoryName`, `Unit`, `QuantityOnHand`, `ReorderLevel`, `IsActive`. Use `InventoryLedgerService.SearchBalances`.

#### Alerts

Controls: `lueAlertKind` -> `InventoryAlertKind`, `txtAlertSearch`, `slueAlertCategory`, `spnExpiringWithinDays`. Grid fields: `ProductName`, `Sku`, `CategoryName`, `QuantityOnHand`, `ReorderLevel`, `ExpiryDate`. Use red for out-of-stock/expired and amber for low/expiring. Use `SearchAlerts`.

#### Movements

Filters map exactly to `StockMovementSearchDTO`: `slueMovementProduct`, `lueMovementType`, `deMovementFrom`, `deMovementTo`, `txtReferenceType`, `txtReferenceId`. Grid fields: `CreatedUtc`, `CurrentProductName`, `CurrentSku`, `MovementType`, `QuantityDelta`, `Reason`, `ReferenceType`, `ReferenceId`, `UserId`. Positive deltas green, negative deltas red. Use `SearchMovements`.

#### Manual adjustment — modal `frmStockAdjustment`

Fields for `StockAdjustmentDTO`: hidden generated `RequestId`, `slueProduct` -> `ProductId`, read-only `spnExpectedQuantity` -> `ExpectedQuantityOnHand`, signed `spnQuantityDelta` -> `QuantityDelta`, required `memReason` -> `Reason`. Show calculated resulting quantity before confirmation. Call `PostAdjustment`.

#### Reconciliation

Controls: `slueReconciliationProduct`, `tglMismatchesOnly`. Grid fields from `InventoryReconciliationItemDTO`: `ProductName`, `Sku`, `ProductQuantity`, `BalanceQuantity`, `LedgerQuantity`, `BalanceMinusLedger`, `ProductMinusBalance`, `IsReconciled`, `MissingBalance`. This page is diagnostic and must not include an automatic repair button.

### Stock counts — `ucStockCounts`

Filters map to `StockCountSearchDTO`: `lueStatus`, `deFrom`, `deTo`, `slueCreatedBy`, `slueProduct`. Summary grid: `Id`, `Status`, `CreatedByUserId`, `CreatedUtc`, `PostedUtc`, `ProductCount`; retain `RequestId` and `RowVersion` in the row model.

Editor/detail `frmStockCount`:

- `txtCountId` read-only.
- `memReason` required for create/post.
- `gcCountLines` fields: `CurrentProductName`, `CurrentSku`, `ExpectedQuantity` read-only, `CountedQuantity` editable, calculated `Variance` read-only.
- Buttons: `btnSaveDraft`, `btnPost`, `btnCancelCount`, `btnReload`, `btnClose`.

Use `CreateDraft`, `UpdateDraft`, `PostDraft`, `CancelDraft`, `PostCount`, and `GetDetails`. Posted/cancelled documents are entirely read-only.

## 9. Suppliers and purchasing

### Suppliers — `ucSuppliers` + `frmSupplierEdit`

Filters: `txtSearch`, `lueActiveState`. Grid from `SupplierDTO`: `Code`, `Name`, `ContactName`, `Phone`, `Email`, `TaxIdentifier`, `IsActive`; hide `Id`, `Revision`, and show address in details.

Editor controls map exactly: `txtCode`, `txtName`, `txtContactName`, `txtPhone`, `txtEmail`, `memAddress`, `txtTaxIdentifier`. Calls: `SupplierService.Search`, `GetDetails`, `Save`, `SetActive`.

Supplier detail `frmSupplierDetails` tabs:

- Profile.
- Products: grid from `SupplierProductDTO` with `ProductName`, `ProductSku`, `SupplierSku`, `DefaultCost`, `LeadTimeDays`, `ProductIsActive`. Editor names: `slueProduct`, `txtSupplierSku`, `spnDefaultCost`, `spnLeadTimeDays`; calls `SaveProductLink` and `UnlinkProduct` using the retained link revision.
- Purchase history: filters map to `SupplierPurchaseHistorySearchDTO.Kind`, `DocumentNumber`, dates, and `ProductId`; grid fields `Kind`, `DocumentNumber`, `Status`, `EventUtc`, `TotalAmount`, `LineCount`, `SupplierReference`.

### Purchase orders — `ucPurchaseOrders` + `frmPurchaseOrderEdit`

Filters map to `PurchaseOrderSearchDTO`: `txtSearch`, `slueSupplier`, `slueProduct`, `lueStatus`, `deFrom`, `deTo`. Grid fields from `PurchaseOrderSummaryDTO`: `OrderNumber`, `SupplierCode`, `SupplierName`, `Status`, `CreatedUtc`, `OrderedUtc`, `CancelledUtc`, `CancelReason`, `TotalAmount`, `LineCount`.

Editor header: `txtOrderNumber`, `slueSupplier`. Line grid uses `PurchaseOrderLineInputDTO`: `ProductId` via repository lookup, `Quantity`, `UnitCost`, calculated line total. Detail rows use `CurrentProductName`, `CurrentSku`, `OrderedQuantity`, `ReceivedQuantity`, `RemainingQuantity`, `UnitCost`, `LineTotal`.

Actions and calls:

- `btnSaveDraft` -> `CreateDraft` or `UpdateDraft` with retained `Revision`.
- `btnMarkOrdered` -> `OrderDraft(PurchaseOrderTransitionDTO)`.
- `btnCancelOrder` -> prompt `memReason`, then `Cancel`.
- `btnReceive` -> open purchase-order receiving form.

Only Draft is editable. Ordered and partially received records are read-only except receiving/cancellation allowed by service rules.

### Goods receiving — `ucGoodsReceiving`

Provide two actions.

Purchase-order receipt `frmReceivePurchaseOrder` fields: `txtReceiptNumber`, `sluePurchaseOrder`, `txtSupplierReference`, and line grid with `ProductId`, product label, remaining quantity, editable `Quantity`. Submit `PurchaseOrderReceiptDTO` through `ReceivePurchaseOrder`.

Direct receipt `frmReceiveDirect` fields: `txtReceiptNumber`, `slueSupplier`, `txtSupplierReference`, and line grid with `ProductId`, `Quantity`, `UnitCost`. Submit `DirectGoodsReceiptDTO` through `ReceiveDirect`.

Cost proposal `frmReceiptCostProposal`: display `ProductName`, `CurrentCatalogCost`, `ReceivedUnitCost`; `btnApplyCost` submits `GoodsReceiptLineId` and `ExpectedCurrentCatalogCost` to `ApplyReceivedCost`. Never apply silently.

### Purchase returns — `frmPurchaseReturn`

Fields map to `PurchaseReturnPostDTO`: `txtReturnNumber`, `slueGoodsReceipt`, required `memReason`, line grid containing retained `GoodsReceiptLineId`, product label, received/previously-returned/eligible quantities, and editable `Quantity`. Call `PurchaseReturnService.Post`. Result should show the posted return ID and updated stock; the original receipt stays unchanged.

## 10. Customers

### Customers — `ucCustomers` + `frmCustomerEdit`

Filters: `txtSearch`, `lueActiveState`. Grid from `CustomerDTO`: `Code`, `Name`, `Phone`, `Email`, `TaxIdentifier`, `IsActive`; hide `Id`, `Revision`, show `Address` in details.

Editor controls: `txtCode`, `txtName`, `txtPhone`, `txtEmail`, `memAddress`, `txtTaxIdentifier`. Calls: `CustomerService.Search`, `GetDetails`, `Save`, `SetActive`.

Customer detail `frmCustomerDetails` contains a History tab. Filters map to `CustomerHistorySearchDTO.Kind`, `ProductId`, `ReceiptNumber`, `FromUtc`, `ToUtcExclusive`; `CustomerId` is retained from the selected customer. Grid fields: `Kind`, `ReceiptNumber`, `Status`, `EventUtc`, `Amount`, `LineCount`. Double-click opens the receipt or return details.

## 11. Cashier shifts

### Shift workspace — `ucCashierShifts`

Filters map to `CashierShiftSearchDTO`: `slueRegister`, `slueCashier`, `lueStatus`, `deFrom`, `deTo`. Grid fields: `RegisterCode`, `RegisterName`, `CashierName`, `Status`, `OpenedUtc`, `ClosedUtc`, `OpeningCash`, `ExpectedCash`, `CountedCash`, `Variance`. Retain `Id`, `RequestId`, `Revision`.

Modals:

- `frmOpenShift`: `slueRegister` -> `RegisterStationId`, `spnOpeningCash` -> `OpeningCash`, generated `RequestId`; call `CashierShiftService.Open`.
- `frmCashMovement`: read-only shift/register, `rgMovementType` -> `IsCashIn`, `spnAmount`, required `memReason`, retained `ShiftRevision`, generated `RequestId`; call `PostCashMovement`.
- `frmCloseShift`: read-only `OpeningCash` and `ExpectedCash`, blind/visible `spnCountedCash` according to policy, calculated variance preview, retained `ShiftRevision`, generated `RequestId`; call `Close`.

Visual outcome: the current shift appears as a prominent status card. Cash-in/out buttons are unavailable without an open owned shift.

## 12. Sales register, payment, held sales, and receipts

### Sales register — `ucSalesRegister`

Recommended layout:

```text
+---------------------------+--------------------------------+
| Barcode/search            | Cart                           |
| Product results           | Qty | Item | Price | Total     |
|                           |                                |
|                           | Subtotal                       |
|                           | Tax                            |
|                           | Discount                       |
|                           | TOTAL                          |
|                           | Hold | Clear | PAY             |
+---------------------------+--------------------------------+
```

Controls:

- `txtBarcode`: scanner-first input; Enter calls `SalesCartService.AddByBarcode`.
- `txtProductSearch`, `btnSearchProducts`, `gcSaleProducts`/`gvSaleProducts`: bind `SaleCartProductPageDTO.Items` fields `ProductName`, `Sku`, `Barcode`, `QuantityAvailable`, `UnitPrice`.
- `slueCustomer`: optional active customer; null means walk-in.
- `gcCart`/`gvCart`: bind `SaleCartLineDTO` fields `ProductName`, `Sku`, `Quantity`, `QuantityAvailable`, `UnitPrice`, `TaxRate`, `TaxAmount`, `LineTotal`.
- Repository actions: `riQty` edits quantity through `UpdateQuantity`; `riRemove` calls `RemoveProduct`.
- Totals labels: `lblSubtotal`, `lblTax`, `lblDiscount`, `lblTotal`, `lblCurrency` bound to `SaleCartDTO`.
- Actions: `btnHoldSale`, `btnResumeHeldSale`, `btnClearCart`, `btnPayment`.

Create the cart using `SalesCartService.Create(registerStationId, customerId)`. Every mutation returns/recalculates a `SaleCartDTO`; replace the bound cart with the returned object. Disable checkout when no current open shift exists.

### Payment — modal `frmPayment`

Header: `lblAmountDue`, `lblTendered`, `lblRemaining`, `lblChange`. Tender grid maps to `SaleTenderDTO`: `lueTenderType`, `spnAmount`, `txtExternalReference`. `ExternalReference` is required for Card/EWallet and omitted for Cash. StoreCredit requires a selected customer. Support multiple rows if desired because the backend accepts a list.

Checkout command `SaleCheckoutDTO` must contain generated `RequestId`, current `RegisterStationId`, current `CashierShiftId`, optional `CustomerId`, `DiscountAmount`, optional resumed `HeldSaleId` and `HeldRevision`, cart lines mapped only to `ProductId`/`Quantity`, and tender rows. Call `SalesService.Checkout` once, disable the Pay button immediately, and reuse the command/request ID for an uncertain identical retry.

After success, show `frmCheckoutComplete` with receipt number, total, change, `btnPrintReceipt`, and `btnNewSale`.

### Held sales — modal `frmHeldSales`

Grid fields from `HeldSaleDTO`: `HeldUtc`, `RegisterCode`, `CustomerName`, `Subtotal`, `TaxAmount`, `DiscountAmount`, `TotalAmount`, `CashierUserId`. Detail lines: `ProductName`, `Sku`, `Quantity`, `UnitPrice`, `TaxAmount`, `LineTotal`. Actions: `btnResume`, `btnCancelHeldSale`. Use `GetHeldSales`, `GetHeldSale`, `SaveHeldSale`, and `CancelHeldSale(id, revision)`.

### Receipts — `ucReceipts`

Filters map to `SaleReceiptSearchDTO`: `txtSearch`, `deFrom`, `deTo`, `slueRegister`, `slueCashier`. Grid fields: `ReceiptNumber`, `SaleDateUtc`, `Status`, `RegisterCode`, `CashierName`, `CustomerName`, `TotalAmount`, `CurrencyCode`.

Receipt detail/preview uses `SaleReceiptDTO`:

- Header: `StoreName`, `StoreAddress`, `StoreTaxIdentifier`, `ReceiptNumber`, `SaleDateUtc`, `RegisterName`, `RegisterCode`, `CashierName`, optional `CustomerCode`/`CustomerName`.
- Lines: `ProductName`, `Sku`, `Barcode`, `Quantity`, `UnitPrice`, `DiscountAmount`, `TaxRate`, `TaxAmount`, `LineTotal`.
- Totals: `Subtotal`, `DiscountAmount`, `TaxAmount`, `RoundingAmount`, `TotalAmount`, `CashReceived`, `Change`.
- Payments: `TenderType`, `Status`, `Amount`, `ExternalReference`, `CreatedUtc`.
- Footer: `TaxName`, `TaxInclusive`, `ReceiptFooter`.

Use `SaleReceiptService.Get`, `Print(saleId, false)` for initial printing, and permission-controlled `Print(saleId, true)` for reprints. A reprint must be visibly labeled.

## 13. Returns, refunds, and exchanges

### Returns workspace — `ucReturns` + `frmSaleReturn`

Start with receipt lookup from `ucReceipts`, then call `SaleReturnService.GetEligibility(saleId)`.

Eligibility header: `ReceiptNumber`, `SaleDateUtc`, `CustomerName`, `OriginalTotal`, `PreviouslyRefunded`, `ReturnDeadlineUtc`, and an approval warning when `RequiresLateApproval`.

Return line grid maps eligibility to posting:

- Read-only: `SaleItemId` hidden, `ProductName`, `PurchasedQuantity`, `ReturnedQuantity`, `EligibleQuantity`, `ApproximateRefundPerUnit`.
- Editable: `spnReturnQuantity` -> `SaleReturnLinePostDTO.Quantity`, `lueDisposition` -> `ReturnDisposition` (`Restock`, `Damaged`, `Quarantine`).

Refund grid:

- Read-only eligibility: `OriginalPaymentId` hidden, `TenderType`, `OriginalAppliedAmount`, `RefundedAmount`, `EligibleAmount`.
- Editable posting: `Amount`, `ExternalReference`.

Other fields: generated `RequestId`, current `RegisterStationId`, current `CashierShiftId`, optional `slueExchangeSale` -> `ExchangeSaleId`, required `memReason`.

Call `SaleReturnService.Post(SaleReturnPostDTO)`. Success view displays `ReturnNumber`, `TotalAmount`, `ApprovedByUserId`, and `CreatedUtc`. Explain disposition visually: Restock returns to sellable inventory; Damaged/Quarantine does not.

## 14. Dashboard

### Dashboard — implement/finish existing `ucDashboard`

Filters use `ManagementFilterDTO`: `deFrom`, `deTo`, optional `slueRegister`; dashboard should normally use today in store-local time converted to UTC.

Cards from `FinancialSummaryDTO`: `GrossSales`, `Refunds`, `NetSales`, `Tax`, `Discounts`, `EstimatedCost`, `EstimatedMargin`, `TransactionCount`, `ReturnCount`.

Use DevExpress `TileControl` or layout cards plus grids for `DashboardDTO.TopProducts` and `DashboardDTO.StockAlerts`. Display `ExpiringProductCount`, `OpenShiftCount`, and `OpenShiftIssueCount` as alert cards that drill into their respective workspaces. Use neutral colors for totals, green for net sales/margin, amber for warnings, and red only for actionable exceptions. Every card should show its date/register scope.

## 15. Reports

### Reports workspace — `ucReports`

Shared filter panel maps to `ManagementFilterDTO`:

`deFrom`, `deTo`, `slueRegister`, `slueUser`, `slueCategory`, `slueProduct`, `slueSupplier`, `slueCustomer`, `chkIncludeInactiveOrCancelled`, `spnMaximumRows` (cap at 500).

Report selector `lueReportType` should offer:

- Sales: `SalesReportRowDTO`
- Products/categories: `ItemSalesReportRowDTO`
- Tenders: `TenderReportRowDTO`
- Inventory: `InventoryReportRowDTO`
- Stock movements: `MovementReportRowDTO`
- Purchasing/suppliers: `PurchasingReportRowDTO`
- Shifts: `ShiftReportRowDTO`
- Audit summary: `AuditActivityReportRowDTO`

Call `ManagementReportService.GetReport(filter)` once and bind the selected DTO collection from `ManagementReportDTO`. Show `FinancialSummaryDTO` above applicable reports. Use `ReportOutputService.ExportCsv(filter)` for `btnExportCsv` and `PrintSummary(filter, registerStationId)` for `btnPrintSummary`. Display exact filters and generated time in preview/output. Do not recalculate report totals in the UI.

Visual outcome: filter panel on the left or top, summary band, tabular result, and clear Export/Print actions that use the same filter object as the preview.

## 16. Maintenance

### Maintenance — `ucMaintenance`

Use `DatabaseMaintenanceService` through `IBackupService`.

Health header fields from `MaintenanceHealthDTO`: `CheckedUtc`, `ApplicationVersion`, `DatabaseName`, `DatabaseVersion`, `LatestApplicationMigration`, `LogDirectory`. Health grid fields from `HealthCheckItemDTO`: `Description`, `State`, `Detail`; show Healthy green, Warning amber, Failed red.

Actions:

- `btnRefreshHealth` -> `GetHealth()`.
- `btnOpenLogFolder` -> open `LogDirectory` after validating it exists.
- `btnCreateBackup` -> DevExpress save dialog limited to rooted `.bak`; call `CreateBackup(path)` and display the returned path.
- `btnRestoreToNewDatabase` -> select `.bak`, enter `txtTargetDatabaseName`, show explicit confirmation, call `RestoreToNewDatabase(backupPath, targetDatabaseName)`.

The restore UI must state: **“Restore creates a new database and never overwrites the live POS database.”** Do not add a live-database overwrite option.

## 17. Composition-root additions

Add factory methods to `ApplicationCompositionRoot` for each new UI owner. Suggested names:

`CreateDashboardControl`, `CreateSalesRegisterControl`, `CreateReceiptsControl`, `CreateReturnsControl`, `CreateCashierShiftsControl`, `CreateInventoryControl`, `CreateStockCountsControl`, `CreateSuppliersControl`, `CreatePurchaseOrdersControl`, `CreateGoodsReceivingControl`, `CreateCustomersControl`, `CreateReportsControl`, `CreateMaintenanceControl`, and `CreateRegistersControl`.

For workflows requiring multiple services, create one shared `POSContext` for the control/form scope and inject the same `IClock`, `ICurrentUser`, and `IAuthorizationService` instances. Do not let two cooperating services silently own separate contexts for one posted operation.

## 18. Permission behavior

The four current action flags are `View`, `Add`, `Edit`, and `Delete` (`ClaimActionType`). Use the service’s existing mapping even when the business caption differs.

| UI action | Typical flag |
|---|---|
| Open/list/details/search | View |
| Create/post/receive/checkout/export/print | Add |
| Edit/approve/apply cost/reactivate | Edit |
| Deactivate/cancel/void/unlink | Delete where the service requires it |

Do not guess solely from this table when wiring an action: service checks remain authoritative. If a user can view but cannot act, keep the screen visible and disable the action with a tooltip explaining the missing permission.

## 19. Final visual acceptance checklist

- All new visible controls are DevExpress controls.
- Primary workflows appear inside `frmMain.pnlMain`; focused actions are owned modal forms.
- One consistent header, filter, grid, paging, and empty-state pattern is used.
- Field captions use business language while `FieldName` values exactly match DTO property names.
- IDs, revisions, request hashes, and row versions are hidden.
- Opaque revisions/request IDs survive binding and are submitted correctly.
- Dates show the store time zone; money shows PHP and configured decimals.
- Completed transaction facts are read-only.
- Buttons reflect permission and document state.
- Loading blocks duplicate clicks; uncertain posting retries retain the original request ID.
- Stale data produces reload/review guidance.
- Grid pages are server-paged and do not load entire tables.
- Empty, loading, validation, authorization, conflict, success, and failure states are visually distinct.
- Receipt/report output comes from stored backend DTOs and is not recomputed from current product data.

## 20. Recommended build order for the UI

1. Rename and organize `frmMain` ribbon/navigation.
2. Sales register, payment, open/close shift, and receipt preview/printing.
3. Returns/refunds.
4. Products/categories and inventory balances/movements/adjustments/counts.
5. Suppliers, purchase orders, receiving, purchase returns.
6. Customers and history.
7. Dashboard and reports.
8. Register maintenance and maintenance/backup/restore.
9. Finish security/audit/settings consistency and accessibility polish.

This order produces a usable cashier flow first while retaining the backend’s transaction, authorization, audit, concurrency, and idempotency guarantees.
