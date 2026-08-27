# Accounting (Phase 3 Hardening / Phase 4 prep)

This document records **stable accounting source identifiers** and cost rules so Phase 4 journal posting can map business events without inventing IDs.

Full double-entry posting (chart of accounts, journals, reports) is **Phase 4**. This file is the source-mapping contract.

## SourceType / SourceId map

| Business event | SourceType | SourceId | Notes |
| -------------- | ---------- | -------- | ----- |
| Sale completed | `SaleCompleted` | `Sale.Id` | Post revenue, AR/cash split, COGS/inventory |
| Payment succeeded | `PaymentSucceeded` | `Payment.Id` | Positive amount, `Status = Succeeded` |
| Sale return completed | `SaleReturnCompleted` | `SaleReturn.Id` | Reverse revenue/COGS; refunds are separate payments |
| Sale voided | `SaleVoided` | `Sale.Id` | Only unpaid sales with **no** returns |
| PO receive batch | `PurchaseReceived` | `PurchaseReceive.Id` | One receive header per Receive API call |
| Stock adjustment | `StockAdjusted` | `StockLedgerEntry.Id` | Manual adjustment ledger row |

Posting must be **idempotent** on `(OrganizationId, SourceType, SourceId)`.

## COGS cost basis

- At sale completion, each `SaleLine.UnitCost` is snapshotted from `Part.UnitCost`.
- Phase 4 COGS uses **`SaleLine.UnitCost × quantity`**, not the live part cost at posting time.
- Purchase valuation for receive journals uses `PurchaseOrderLine.UnitCost`.

## Purchase receive identity

- Each `POST .../purchase-orders/{id}/receive` creates a `PurchaseReceive` row.
- Stock ledger rows use `ReferenceType = "PurchaseReceive"` and `ReferenceId = PurchaseReceive.Id`.
- Do **not** use `PurchaseOrder.Id` as the accounting SourceId (partial receives would collide).

## Payments and balances

- `Sale.AmountPaid` / `BalanceDue` (and invoice mirrors) are maintained to match `Sum(Payments.Amount)` for that sale (refunds are negative amounts with `Status = Refunded`).
- Duplicate `(OrganizationId, IdempotencyKey)` never creates a second payment.

## Void vs return

- Void is allowed only for unpaid completed sales **with no returns**.
- If any return exists, void is rejected (conflict). Further stock correction uses returns only.
- This keeps restock and Phase 4 reversal sources unambiguous (return XOR void).

## Stock on sale complete

- Completing a sale re-checks on-hand inside a DB transaction (relational) before posting negative `Sale` ledger rows.
- Insufficient stock → conflict; sale is not committed.

## Business timestamps and posting dates

All financial timestamps are stored as `DateTimeOffset` (UTC). Display uses `Organization.TimeZoneId` (IANA, default `Asia/Manila`).

| Event | Business timestamp for Phase 4 journal `EntryDate` |
| ----- | -------------------------------------------------- |
| Sale completed / invoice issued | `Invoice.IssuedAt` (= `Sale.CompletedAt`) |
| Payment succeeded | `Payment.PaidAt` |
| Sale return | `SaleReturn.CompletedAt` |
| Refund payment | `SaleReturn.RefundedAt` / refund `Payment.PaidAt` |
| Sale voided | `Sale.VoidedAt` / `Invoice.VoidedAt` |
| PO receive | `PurchaseReceive.ReceivedAt` |
| Stock adjustment | `StockLedgerEntry.CreatedAt` (business time of adjustment) |

**Do not** use `CreatedAt` alone as the accounting posting date when a dedicated business stamp exists.

`Invoice.DueAt` is nullable (no payment-terms UI yet). Aging should use `DueAt ?? IssuedAt`.
