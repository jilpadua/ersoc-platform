# Business rules (Phase 1)

## Organization scope

All customer, device, service, repair, and audit data is scoped by `OrganizationId`. Branch is recorded on repairs and on the signed-in user.

## Repair statuses

Statuses are stored in `RepairStatusDefinitions` (seeded, not hard-coded in UI logic beyond default transition graph).

Default codes: `RECEIVED`, `DIAGNOSIS`, `WAITING_FOR_APPROVAL`, `APPROVED`, `WAITING_FOR_PARTS`, `REPAIRING`, `TESTING`, `READY_FOR_PICKUP`, `COMPLETED`, `CANCELLED`.

Transitions are validated by `RepairWorkflow` in the domain. Each transition writes `RepairStatusHistory` (actor, timestamps, previous/new, reason) and an audit event.

## Authorization

Permission codes (e.g. `repairs.status`) are checked in application services. Controllers require authentication; missing permissions return `forbidden`.

## Audit

Create/update/status changes write `AuditLogs`. There is no update/delete API for audit rows.

## Dashboard honesty

Phase 1 KPIs use repair data only. Sales, stock, cash, expenses, and unpaid invoices are labeled unavailable until later phases—never faked.
