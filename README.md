# Electronics Repair Shop Management System (ERSMS)

Modular business-management platform for an electronics repair shop. Phase 1 delivers identity, customers, devices, service catalog, repair workflow, dashboard, audit, and search.

## Specs

- [docs/specs/electronics_repair_shop_technical_specification.pdf](docs/specs/electronics_repair_shop_technical_specification.pdf)
- [docs/specs/electronics_repair_shop_management_system_plan_original.pdf](docs/specs/electronics_repair_shop_management_system_plan_original.pdf)
- [docs/architecture.md](docs/architecture.md)
- [docs/development-plan.md](docs/development-plan.md)

## Stack

- **Frontend:** Next.js 15 + TypeScript + Tailwind
- **Backend:** ASP.NET Core 8 modular monolith
- **Database:** PostgreSQL 16

## Quick start

```bash
# 1. Database
docker compose up -d

# 2. API (http://localhost:5080)
cd backend
dotnet ef database update --project src/Ersms.Infrastructure --startup-project src/Ersms.Api
dotnet run --project src/Ersms.Api --launch-profile http

# 3. Web (http://localhost:3000)
cd frontend
npm install
npm run dev
```

Seed owner (development):

- Email: `owner@ersms.local`
- Password: `Owner123!`

## Tests

```bash
cd backend
dotnet test
```

## Phase status

**Phase 1 complete** (core platform). Do not start Phase 2 until Phase 1 is accepted as stable.
