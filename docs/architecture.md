# ERSMS Architecture

## Current state

Phase 0?3 are implemented: modular ASP.NET Core API, Next.js admin UI, PostgreSQL, cookie auth with RBAC, customers, devices, service catalog, configurable repair workflow, inventory ledger, suppliers/POs/receiving, **sales/POS** (payments, invoices, returns), dashboard (including low-stock, today?s sales, unpaid invoices), audit, and search.

## Source of truth

- [docs/specs/electronics_repair_shop_technical_specification.pdf](specs/electronics_repair_shop_technical_specification.pdf)
- [docs/specs/electronics_repair_shop_management_system_plan_original.pdf](specs/electronics_repair_shop_management_system_plan_original.pdf)

## Target architecture

```text
Web Client (Next.js)
    ?
API (ASP.NET Core /api/v1)
    ?
Application Modules
    ?
Domain Layer
    ?
Persistence / External Services (PostgreSQL, local/S3 file storage)
```

### Style

**Modular monolith** ? modules share one PostgreSQL database and one API host, with clear ownership boundaries and in-process domain events for cross-module automation. Microservices are out of scope unless a concrete requirement cannot be met inside the monolith.

## Technology stack

| Layer | Choice |
|-------|--------|
| Frontend | Next.js (App Router), TypeScript, React, Tailwind CSS |
| Backend | ASP.NET Core 8, C# |
| ORM | EF Core 8 (code-first migrations) |
| Database | PostgreSQL 16 |
| Auth | ASP.NET Core Identity + HTTP-only cookie sessions |
| API | Versioned REST `/api/v1/` |
| Cross-module | MediatR domain events (in-process) |
| Files | `IFileStorage` abstraction; local disk in Phase 1 |
| Jobs / Redis | Deferred (Phase 5+ / as needed) |

## Solution layout

```text
backend/
  src/
    Ersms.Api/              # Host, controllers, middleware
    Ersms.SharedKernel/     # Result, paging, events base, ICurrentUser
    Ersms.Infrastructure/   # DbContext, Identity, file storage, seeding
    Modules/
      Identity/
      Customers/
      Devices/
      ServiceCatalog/
      Repairs/
      Audit/
      Dashboard/
  tests/
    Ersms.Domain.Tests/
    Ersms.Api.Tests/
frontend/                   # Next.js admin UI
docs/
docker-compose.yml          # PostgreSQL for local development
```

## Module boundaries (Phase 1-3)

| Module | Owns | Depends on |
|--------|------|------------|
| Identity | Organizations, branches, users, roles, permissions, sessions | SharedKernel |
| Customers | Customer profiles, contacts, addresses, notes | Identity (org scope) |
| Devices | Device records, identifiers, photos metadata | Customers |
| ServiceCatalog | Categories, services, pricing, warranty days | Identity (org scope) |
| Repairs | Tickets, status definitions/history, services lines, notes, photos | Customers, Devices, ServiceCatalog, Identity |
| Inventory | Parts, append-only stock ledger, adjustments | Identity (org/branch) |
| Purchasing | Suppliers, purchase orders, receiving (posts inventory ledger) | Inventory, Identity |
| Sales | Sales, sale lines, payments, invoices, returns (posts inventory ledger) | Inventory, Customers, Identity |
| Audit | Append-only audit logs | All (via events/interceptors) |
| Dashboard | Operational KPI read queries | Repairs, Inventory, Sales |

Later phases add Accounting, Notifications, Reporting expansions, Employees extras, Settings expansions.

## Database strategy

- Single PostgreSQL database shared by modules.
- Relational model with foreign keys, unique constraints, and indexes.
- Critical invariants enforced in the database (e.g. unique `(OrganizationId, RepairNumber)`).
- Transactions for multi-step writes within a module.
- Optimistic concurrency where high-conflict updates appear (later stock/accounting).
- Inventory quantity and accounting balances will be **ledger-based** in Phases 2 and 4 ? not mutable totals alone.

## API strategy

- Base path: `/api/v1/`
- Controllers validate input, authorize, then call application services.
- Stable application error codes in ProblemDetails extensions for the frontend.
- Pagination, filtering, sorting, and search on list endpoints.
- Organization (and branch where applicable) scope enforced **server-side**.
- Idempotency keys for payment and other retry-sensitive operations (Phase 3+).

## Authentication and authorization

- Local passwords with ASP.NET Identity (adaptive hashing).
- HTTP-only cookie authentication for the web client.
- Configurable roles seeded: `OWNER`, `ADMIN_MANAGER`, `CASHIER`, `TECHNICIAN`, `INVENTORY_STAFF`.
- Permission codes attached to roles; `HasPermission` policies.
- Rate limiting on authentication endpoints.
- Audit of security-sensitive actions.

## Domain events (Phase 1+)

Examples: `RepairCreated`, `RepairStatusChanged` (consumers: Audit; later Inventory/Accounting/Notifications).

External notification failures must never roll back core business transactions (Phase 5).

## Repair workflow

Statuses are **configurable** (table-driven), seeded with:

`RECEIVED` ? `DIAGNOSIS` ? `WAITING_FOR_APPROVAL` ? `APPROVED` ? `WAITING_FOR_PARTS` ? `REPAIRING` ? `TESTING` ? `READY_FOR_PICKUP` ? `COMPLETED` / `CANCELLED`

Every transition records actor, timestamp, previous status, new status, optional reason.

## Frontend principles

Operational business UI: clear tables, filters, pagination, status badges, form validation, empty/loading/error states. Optimize for daily shop workflows, not decorative marketing layouts.

## Explicit non-goals (through Phase 2)

POS/payments, double-entry accounting, Hangfire, Redis-as-default, customer portal, multi-branch transfers, Elasticsearch, consuming parts on repair tickets (deferred).
