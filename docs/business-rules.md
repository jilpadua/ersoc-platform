# Business rules

## Organization scope

All customer, device, service, repair, inventory, purchasing, sales, and audit data is scoped by `OrganizationId`. Branch is recorded on repairs, stock ledger entries, purchase orders, sales/payments/returns, and the signed-in user.

## Repair statuses

Statuses are stored in `RepairStatusDefinitions` (seeded, not hard-coded in UI logic beyond default transition graph).

Default codes: `RECEIVED`, `DIAGNOSIS`, `WAITING_FOR_APPROVAL`, `APPROVED`, `WAITING_FOR_PARTS`, `REPAIRING`, `TESTING`, `READY_FOR_PICKUP`, `COMPLETED`, `CANCELLED`.

Transitions are validated by `RepairWorkflow` in the domain. Each transition writes `RepairStatusHistory` (actor, timestamps, previous/new, reason) and an audit event.

## Soft deactivate

Customers, devices, services, parts, and suppliers use `IsActive` for soft deactivate. Default list queries return active records only (`includeInactive=true` to include inactive).

## Inventory ledger

Part quantity on hand is the sum of append-only `StockLedgerEntries` for `(OrganizationId, BranchId, PartId)`. Manual adjustments that would make on-hand negative are rejected. Purchase receiving posts positive `PurchaseReceive` ledger rows. Completing a sale posts negative `Sale` rows; returns/voids post positive restock rows (`SaleReturn` or void reason).

## Purchase orders

Statuses: `DRAFT` → `ORDERED` → `PARTIALLY_RECEIVED` / `RECEIVED`. Cancel allowed from `DRAFT` or `ORDERED` only (not after receiving has begun). Receive quantities cannot exceed remaining ordered quantity. Each receive call creates a `PurchaseReceive` header; stock ledger references that receive id (`ReferenceType = PurchaseReceive`).

## Sales / POS

Part sales only (no repair checkout in Phase 3). Completing a sale validates stock (re-check inside a DB transaction), writes `Sale`/`SaleLine` (with `UnitCost` snapshotted from `Part.UnitCost`), creates invoice 1:1, deducts inventory, and may accept an initial payment in the same transaction. `TaxTotal` is always `0`. Payment methods are org-seeded (`CASH`, `CARD`, `TRANSFER`). Payments require a unique `(OrganizationId, IdempotencyKey)`. Partial payments allowed; overpay rejected. `AmountPaid` / `BalanceDue` track `Sum(Payments.Amount)` (refunds are negative payment rows).

Business timestamps (UTC): invoice `IssuedAt`, payment `PaidAt`/`CreatedAt`, sale `CompletedAt`/`VoidedAt`, return `CompletedAt`/`RefundedAt`, invoice `DueAt` (nullable), invoice `VoidedAt`. Display uses org `TimeZoneId`. Accounting posting dates use these business stamps—not `CreatedAt` alone (see `accounting.md`).

Returns: quantities cannot exceed sold minus already returned (duplicate lines in one request are aggregated before validation); restock via `SaleReturn` ledger; optional refund payment. **Void only for unpaid completed sales with no returns** (restocks full sold qty). A sale that has returns cannot be voided.

See `docs/accounting.md` for Phase 4 source mapping.

## Authorization

Permission codes (e.g. `sales.write`, `sales.refund`, `inventory.write`, `purchasing.write`, `repairs.status`) are checked in application services. Controllers require authentication; missing permissions return `forbidden`. Cashier is seeded with sales read/write/refund.

## Audit

Create/update/status changes write `AuditLogs`. There is no update/delete API for audit rows.

## Dashboard honesty

Repair KPIs, low-stock part count, today’s completed sales total, unpaid invoice count, posted expenses today, and cash/bank GL balance are real.
