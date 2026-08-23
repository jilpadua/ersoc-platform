# ERSMS Development Plan

## Principles

1. Build Phase 1 ? 2 ? 3 ? 4 ? 5 ? 6; do not implement the entire system in one pass.
2. Each phase must be functional, testable, and production-quality.
3. Inspect before modifying; reuse existing code; keep domain logic out of controllers.
4. Prefer domain events for cross-module automation.
5. Do not hard-code configurable business rules (statuses, fees, roles, payment methods).
6. Enforce organization/branch scope and authorization server-side.
7. Ledger-based inventory (Phase 2) and double-entry accounting (Phase 4) when those phases begin.

## Phase roadmap


| Phase | Focus                  | Exit criteria (summary)                                                              |
| ----- | ---------------------- | ------------------------------------------------------------------------------------ |
| **0** | Discovery + docs       | Architecture and development plan documented; specs in repo                          |
| **1** | Core platform          | Identity, org/branch, customers, devices, services, repairs, dashboard, audit, tests |
| **2** | Inventory & purchasing | Stock ledger, suppliers, POs, receiving, low-stock alerts, tests                     |
| **3** | Sales / POS            | Sales, payments (idempotent), invoices, returns, inventory deduction, tests          |
| **4** | Accounting             | Chart of accounts, journals, balanced entries, financial reports, tests              |
| **5** | Automation             | Domain-event notifications, Hangfire jobs, reminders, delivery tracking              |
| **6** | Expansion              | Portal, multi-branch transfers, mobile, analytics, integrations (as needed)          |




## Phase 1 exit criteria

- [x] Users can authenticate with cookie sessions; roles/permissions enforced on APIs
- [x] Organization and branch seeded; all business data org-scoped
- [x] Customers, devices, service catalog CRUD working (create, update, soft deactivate; UI includes email)
- [x] Full repair workflow with configurable statuses and status history (UI lists only allowed next statuses)
- [x] Dashboard shows real Phase 1 metrics (zeros when empty; no fake sales/stock)
- [x] Audit log records create/update/status/permission-sensitive actions; immutable to normal users
- [x] Global search covers repair #, customer name/phone, device model/serial/IMEI
- [x] Domain + API tests green (authz, repair transitions, audit)
- [x] `docs/architecture.md` and related docs match implementation

Hard deletes of customers/devices/services (and posted transactional rows) are deferred; Phase 1 uses soft deactivate (`IsActive`) so repair history stays intact.

**Do not start Phase 2 until Phase 1 is stable and the above checklist is met.**

## Increment workflow

For every implementation task:

1. Inspect relevant files
2. Explain what will change
3. Implement
4. Run type checking / build
5. Run tests
6. Fix failures
7. Review for architectural violations
8. Update documentation
9. Summarize completed work



## Local development (Phase 1)

```bash
# Infrastructure
docker compose up -d

# Backend
cd backend
dotnet restore
dotnet ef database update --project src/Ersms.Infrastructure --startup-project src/Ersms.Api
dotnet run --project src/Ersms.Api

# Frontend
cd frontend
pnpm install
pnpm dev
```

Default seed credentials are documented in `.env.example` / `appsettings.Development.json` (never commit production secrets).

## Testing strategy

- **Unit:** domain rules (repair transitions, totals)
- **Integration / API:** WebApplicationFactory + Testcontainers PostgreSQL
- **E2E (later):** critical shop flows once UI stabilizes



## Documentation set


| Doc                   | Purpose                               |
| --------------------- | ------------------------------------- |
| `architecture.md`     | Target architecture and decisions     |
| `development-plan.md` | Phases and exit criteria              |
| `database.md`         | Schema notes (evolve with migrations) |
| `api.md`              | API conventions and Phase endpoints   |
| `modules.md`          | Module ownership                      |
| `business-rules.md`   | Repair workflow and invariants        |
| `testing.md`          | How to run tests                      |


