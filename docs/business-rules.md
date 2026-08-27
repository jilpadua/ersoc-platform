# Business rules

## Organization scope

All customer, device, service, repair, inventory, purchasing, and audit data is scoped by `OrganizationId`. Branch is recorded on repairs, stock ledger entries, purchase orders, and the signed-in user.

## Repair statuses

Statuses are stored in `RepairStatusDefinitions` (seeded, not hard-coded in UI logic beyond default transition graph).

Default codes: `RECEIVED`, `DIAGNOSIS`, `WAITING_FOR_APPROVAL`, `APPROVED`, `WAITING_FOR_PARTS`, `REPAIRING`, `TESTING`, `READY_FOR_PICKUP`, `COMPLETED`, `CANCELLED`.

Transitions are validated by `RepairWorkflow` in the domain. Each transition writes `RepairStatusHistory` (actor, timestamps, previous/new, reason) and an audit event.

## Soft deactivate

Customers, devices, services, parts, and suppliers use `IsActive` for soft deactivate. Default list queries return active records only (`includeInactive=true` to include inactive).

## Inventory ledger

Part quantity on hand is the sum of append-only `StockLedgerEntries` for `(OrganizationId, BranchId, PartId)`. Manual adjustments that would make on-hand negative are rejected. Purchase receiving posts positive `PurchaseReceive` ledger rows.

## Purchase orders

Statuses: `DRAFT` → `ORDERED` → `PARTIALLY_RECEIVED` / `RECEIVED`. Cancel allowed from `DRAFT` or `ORDERED` only (not after receiving has begun). Receive quantities cannot exceed remaining ordered quantity.

## Authorization

Permission codes (e.g. `inventory.write`, `purchasing.write`, `repairs.status`) are checked in application services. Controllers require authentication; missing permissions return `forbidden`.

## Audit

Create/update/status changes write `AuditLogs`. There is no update/delete API for audit rows.

## Dashboard honesty

Repair KPIs and low-stock part count are real. Sales, cash, expenses, and unpaid invoices remain labeled unavailable until later phases—never faked.
