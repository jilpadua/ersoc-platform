# Testing

## Domain tests

```bash
cd backend
dotnet test tests/Ersms.Domain.Tests
```

Covers repair transition rules and totals calculation.

## API tests

```bash
dotnet test tests/Ersms.Api.Tests
```

Uses `WebApplicationFactory` + EF InMemory. Covers login, unauthorized access, repair create/status transition, invalid transition conflict, and audit presence.

## Manual smoke

1. Start Postgres + API + frontend.
2. Login as `owner@ersms.local` / `Owner123!`.
3. Create customer → device → repair → advance status → confirm audit log and dashboard counts.
