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

Uses `WebApplicationFactory` + EF InMemory. Covers:

- Login, unauthorized access, repair create/status transition, invalid transition, audit
- Inventory adjust, PO submit/partial receive, low-stock dashboard hooks
- Sales: complete sale + stock ledger, payment idempotency, overpay reject, return/refund balances, void restock, pay-after-void reject, over-return, return-then-void reject, `voidedAt`/`refundedAt`/`issuedAt` stamps
- Auth `me` includes `timeZoneId`
- Purchase receive ledger `ReferenceType`/`ReferenceId` = distinct `PurchaseReceive` ids

## Manual smoke

1. Start Postgres + API + frontend.
2. Login as `owner@ersms.local` / `Owner123!`.
3. Create customer → device → repair → advance status → confirm audit log and dashboard counts.
