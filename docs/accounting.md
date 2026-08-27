# Accounting

Double-entry accounting for ERSMS (Phase 4). Operational sales/purchasing remain sources of truth for stock and invoices; the GL is the books of record.

## SourceType / SourceId map

| Business event | SourceType | SourceId | Notes |
| -------------- | ---------- | -------- | ----- |
| Sale completed | `SaleCompleted` | `Sale.Id` | Revenue, cash/AR split, COGS/inventory |
| Payment succeeded | `PaymentSucceeded` | `Payment.Id` | Subsequent payments only (checkout cash is in sale journal) |
| Sale return completed | `SaleReturnCompleted` | `SaleReturn.Id` | Reverse revenue/COGS; refund cash and/or AR |
| Sale voided | `SaleVoided` | `Sale.Id` | Reversal of original sale journal |
| PO receive batch | `PurchaseReceived` | `PurchaseReceive.Id` | Inventory Dr / AP Cr; creates `SupplierBill` |
| Stock adjustment | `StockAdjusted` | `StockLedgerEntry.Id` | Value = `Part.UnitCost × qty delta` |
| Supplier payment | `SupplierPayment` | `SupplierPayment.Id` | AP Dr / cash-bank Cr |
| Expense posted | `ExpensePosted` | `Expense.Id` | Posted on approve (MVP) |
| Expense voided | `ExpenseVoided` | `Expense.Id` | Reversing entry |
| Manual journal | `ManualJournal` | new Guid | Balanced adjustment |
| Opening balance | `OpeningBalance` | new Guid | Cutover balances |

Posting is **idempotent** on `(OrganizationId, SourceType, SourceId)`.

## COGS cost basis

- At sale completion, each `SaleLine.UnitCost` is snapshotted from `Part.UnitCost`.
- COGS uses **`SaleLine.UnitCost × quantity`**, not live part cost at posting time.
- Purchase valuation for receive journals uses `PurchaseOrderLine.UnitCost`.

## Chart of accounts (seed defaults)

| Code | Name | Mapping key |
| ---- | ---- | ----------- |
| 1000 | Cash | `Cash` |
| 1010 | Bank | `Bank` |
| 1020 | Card Clearing | `CardClearing` |
| 1100 | Accounts Receivable | `AccountsReceivable` |
| 1200 | Inventory | `InventoryAsset` |
| 2000 | Accounts Payable | `AccountsPayable` |
| 3000 | Opening Equity | `OpeningEquity` |
| 4000 | Sales Revenue | `SalesRevenue` |
| 5000 | Cost of Goods Sold | `Cogs` |
| 5100 | Inventory Adjustment | `InventoryAdjustment` |
| 6000 | Operating Expense | `OperatingExpense` |

Mappings live in `AccountingAccountMappings` (Owner/Admin upsert).

## Periods

Monthly `AccountingPeriod` rows per org (`Open` / `Closed`). Journals post only into an **Open** period that covers `EntryDate`. Seed generates the current calendar year.

## Journal rules

- Sum(debits) == sum(credits); money `decimal(18,2)`.
- Posted entries are immutable; corrections use reversing journals (`ReversesJournalEntryId`).
- `EntryDate` uses business stamps below (not `CreatedAt` alone when a stamp exists).

## Business timestamps and posting dates

| Event | Journal `EntryDate` |
| ----- | ------------------- |
| Sale completed | `Sale.CompletedAt` / `Invoice.IssuedAt` |
| Payment succeeded | `Payment.PaidAt` |
| Sale return | `SaleReturn.CompletedAt` |
| Sale voided | `Sale.VoidedAt` |
| PO receive | `PurchaseReceive.ReceivedAt` |
| Stock adjustment | `StockLedgerEntry.CreatedAt` |
| Supplier payment | `SupplierPayment.PaidAt` |
| Expense | `Expense.ExpenseDate` |

Display uses `Organization.TimeZoneId` (default `Asia/Manila`). Aging uses `DueAt ?? IssuedAt`.

## Permissions

`accounting.read`, `accounting.write`, `accounting.post`, `accounting.periods`, `accounting.approve_expense`, `accounting.ap`.

## Reports (API)

Under `/api/v1/accounting/reports`: general-ledger, trial-balance, profit-and-loss, balance-sheet, cash-flow (direct cash lines), ar-aging, ap-aging, customer-statement, reconciliation.

## Domain events (Phase 5 handlers)

Payload stubs: `JournalEntryPosted`, `ExpenseApproved`, `SupplierPaymentRecorded`, `AccountingPeriodClosed` — produced conceptually when those actions succeed; outbox/handlers arrive in Phase 5.
