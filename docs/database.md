# Database (Phase 1)

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

## Critical constraints

- Unique `(OrganizationId, RepairNumber)` on `Repairs`
- Unique `(OrganizationId, Code)` on roles and repair status definitions
- Unique `(OrganizationId, NormalizedEmail)` on users
- Indexes on customer phone/name, device serial/IMEI, audit timestamp
