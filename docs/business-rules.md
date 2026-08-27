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

Statuses: `DRAFT` → `ORDERED` → `PARTIALLY_RECEIVED` / `RECEIVED`. Cancel allowed from `DRAFT` or `ORDERED` only (not after receiving has begun). Receive quantities cannot exceed remaining ordered quantity.

## Sales / POS

Part sales only (no repair checkout in Phase 3). Completing a sale validates stock, writes `Sale`/`SaleLine`, creates invoice 1:1, deducts inventory, and may accept an initial payment. `TaxTotal` is always `0`. Payment methods are org-seeded (`CASH`, `CARD`, `TRANSFER`). Payments require a unique `(OrganizationId, IdempotencyKey)`. Partial payments allowed. Returns cannot exceed sold quantity minus already returned; restock and optional refund payment. Void only for unpaid completed sales (restocks).

## Authorization

Permission codes (e.g. `sales.write`, `sales.refund`, `inventory.write`, `purchasing.write`, `repairs.status`) are checked in application services. Controllers require authentication; missing permissions return `forbidden`. Cashier is seeded with sales read/write/refund.

## Audit

Create/update/status changes write `AuditLogs`. There is no update/delete API for audit rows.

## Dashboard honesty

Repair KPIs, low-stock part count, today’s completed sales total, and unpaid invoice count are real. Expenses and cash balance remain labeled unavailable until Phase 4—never faked.
