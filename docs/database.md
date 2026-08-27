# Database

## Engine

PostgreSQL 16 via Docker Compose (`postgres` service). Connection string in `backend/src/Ersms.Api/appsettings.json`.

## Migrations

EF Core migrations live in `backend/src/Ersms.Infrastructure/Persistence/Migrations`.

```bash
dotnet ef migrations add <Name> --project src/Ersms.Infrastructure --startup-project src/Ersms.Api
dotnet ef database update --project src/Ersms.Infrastructure --startup-project src/Ersms.Api
```

## Phase 1 tables

Identity: `Organizations`, `Branches`, `AppRoles`, `AppPermissions`, `RolePermissions`, `AppUserRoles`, ASP.NET Identity tables (`AspNetUsers`, …)

CRM / devices / catalog: `Customers`, `Devices`, `DevicePhotos`, `ServiceCategories`, `Services`

Repairs: `RepairStatusDefinitions`, `Repairs`, `RepairServiceLines`, `RepairStatusHistories`, `RepairNotes`, `RepairPhotos`

Audit: `AuditLogs` (append-only)

## Phase 2 tables

Inventory: `Parts`, `StockLedgerEntries` (append-only quantity deltas)

Purchasing: `Suppliers`, `PurchaseOrders`, `PurchaseOrderLines`, `PurchaseReceives` (one row per receive API call; ledger `ReferenceId`)

## Phase 3 tables

Sales: `PaymentMethods`, `Sales`, `SaleLines` (includes `UnitCost` snapshot at sale), `Invoices`, `Payments`, `SaleReturns`, `SaleReturnLines`

`Parts.UnitPrice` (sell price); `Parts.UnitCost` / `SaleLines.UnitCost` for COGS prep (see `accounting.md`).

## Phase 3 hardening migration

`Phase3HardeningAccountingPrep`: adds `SaleLines.UnitCost`, `PurchaseReceives` table.

## Critical constraints

- Unique `(OrganizationId, RepairNumber)` on `Repairs`
- Unique `(OrganizationId, PoNumber)` on `PurchaseOrders`
- Unique `(OrganizationId, SaleNumber)` on `Sales`
- Unique `(OrganizationId, InvoiceNumber)` on `Invoices`
- Unique `SaleId` on `Invoices` (1:1 with sale)
- Unique `(OrganizationId, IdempotencyKey)` on `Payments`
- Unique `(OrganizationId, ReturnNumber)` on `SaleReturns`
- Unique `(OrganizationId, Sku)` on `Parts`
- Unique `(OrganizationId, Code)` on roles, repair status definitions, and payment methods
- Unique `(OrganizationId, NormalizedEmail)` on users
- Indexes on customer phone/name, device serial/IMEI, stock ledger `(OrganizationId, BranchId, PartId)`, audit timestamp
