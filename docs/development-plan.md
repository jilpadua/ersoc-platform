# ERSMS Development Plan

This document is the implementation-ready roadmap for the Electronics Repair Shop Management System (ERSMS). A developer using Cursor should be able to follow it phase-by-phase and know exactly what to build, in what order, with which rules, data changes, tests, and exit criteria.

---

## Overall Specification Review

### What the existing plan already covers well

- Clear phased delivery (Core → Inventory → Sales → Accounting → Automation → Expansion).
- Organization/branch scoping and server-side authorization as non-negotiable constraints.
- Append-only inventory ledger and soft deactivate for master data.
- Sales/POS with idempotent payments, partial payments, returns, voids, and invoice projection.
- Domain + API testing expectations and a practical documentation set for Phases 1–3.
- Modular monolith stance (no premature microservices).

### What was previously under-specified

- Double-entry accounting depth (periods, immutability, source idempotency, AR/AP, expenses, reconciliation).
- Pre–Phase 4 hardening of payment, return/void, and stock integrity for accounting source mapping.
- Reliable asynchronous automation (outbox, retries, dead-letter, delivery tracking).
- Production concerns: backup/restore, monitoring, opening balances, deployment/rollback, operational admin.
- End-to-end testing of critical shop flows.
- File/photo storage maturity, branch isolation hardening, and future extensibility boundaries for integrations.

### What additional requirements are being added

- Phase 3 Hardening / Accounting Preparation gate before Phase 4.
- Full Phase 4 double-entry subsystem (CoA, journals, periods, mappings, AR/AP, expenses, reports, reconciliation).
- Phase 5 transactional outbox + Hangfire automation architecture.
- Phase 6 expansion build plans (portal, warranty, multi-branch transfers, mobile, booking, supplier portal, analytics, integrations).
- Phase 7 production hardening as a formal release gate.
- Explicit Definition of Done; expanded testing and documentation set (`accounting.md`, `events.md`, `deployment.md`, `operations.md`, `current-state.md`).

### Why those additions are necessary

A usable repair-shop system must keep money, stock, and books aligned; survive notification/job failures without corrupting core transactions; support opening balances and ops procedures; and prove integrity under real load. Checklist-only phases are not enough to start Phase 4 without inventing the accounting architecture.

---

## Principles

1. Build Phase 0 → 1 → 2 → 3 → 4 → 5 → 6 → 7; do not implement the entire system in one pass.
2. Each phase must be functional, testable, and production-quality for its scope.
3. Inspect before modifying; reuse existing code; keep domain logic out of controllers.
4. Prefer domain events for cross-module automation; core business commits must not depend on external side effects.
5. Do not hard-code configurable business rules (statuses, fees, roles, payment methods, account mappings where org-configurable).
6. Enforce organization/branch scope and authorization server-side; never rely on the frontend for authz.
7. Ledger-based inventory (Phase 2) and double-entry accounting (Phase 4) are sources of truth for stock and books respectively—do not store financial totals as the accounting source of truth.
8. Money uses `decimal` with fixed precision (existing `decimal(18,2)`); never floating-point for money.
9. Posted journal entries are immutable; corrections use reversal or adjustment entries.
10. Remain a modular monolith unless a concrete requirement cannot be met inside one host and one PostgreSQL database.
11. Analytics/reporting must not compromise transactional performance (use read-friendly queries or read models when needed).

---

## Phase roadmap

| Phase | Focus | Status | Exit criteria (summary) |
| ----- | ----- | ------ | ----------------------- |
| **0** | Discovery / Architecture | Completed | Architecture and development plan documented; specs in repo |
| **1** | Core platform | Completed | Identity, org/branch, customers, devices, services, repairs, dashboard, audit, tests |
| **2** | Inventory & purchasing | Completed | Stock ledger, suppliers, POs, receiving, low-stock metrics, tests |
| **3** | Sales / POS | Completed | Sales, payments (idempotent), invoices, returns, inventory deduction, tests |
| **4** | Accounting | Planned (next) | Chart of accounts, journals, AR/AP, expenses, balanced entries, reports, reconciliation, tests |
| **5** | Automation | Planned | Outbox, Hangfire jobs, notifications, retries, delivery tracking, job admin |
| **6** | Expansion | Planned | Portal, warranty, multi-branch transfers, mobile, booking, analytics, integrations |
| **7** | Production Hardening | Planned | Security, backups, observability, performance, integrity audits, operational readiness |

**Do not start Phase 4 until Phase 3 exit criteria are met and Phase 3 Hardening checks pass.**

---

## Definition of Done

A feature is **not** complete because its UI works.

It is complete only when:

- [ ] Business rules are implemented in the domain/application layer
- [ ] Authorization is enforced server-side (permission codes)
- [ ] Organization scope is enforced; branch is recorded/filtered where required
- [ ] Database integrity is protected (constraints, unique keys, no orphan critical links)
- [ ] Transaction boundaries are correct (all-or-nothing for related writes)
- [ ] Errors are handled with clear API results (no silent partial success)
- [ ] Important actions are auditable
- [ ] Idempotency exists where retries or duplicate submits are expected
- [ ] Unit and/or API tests covering the rules pass
- [ ] Documentation for the changed surface is updated
- [ ] Recovery behavior exists where appropriate (reversals, dead-letter, restore)
- [ ] No known critical data-integrity issue remains for that feature

---

## Testing strategy

Keep existing Domain + API test harnesses (`Ersms.Domain.Tests`, `Ersms.Api.Tests` with WebApplicationFactory + Testcontainers PostgreSQL). Expand coverage as phases grow.

### Unit tests

Cover:

- Domain rules and status transitions (repairs, POs, sales)
- Calculations (sale totals, balances, inventory on-hand sums)
- Accounting calculations (balanced journals, AR/AP aging, COGS mappings)
- Reversals and void/return invariants
- Authorization rule helpers where non-trivial

### Integration / API tests

Cover:

- Authentication and permission enforcement
- Organization isolation (and branch isolation where applicable)
- Transaction boundaries (sale+invoice+stock; receive+ledger; journal posting)
- Inventory, sales, payments, accounting postings
- Events/outbox and background job handlers (Phase 5+)

### E2E tests (critical flows)

Implement when UI and APIs for the flow are stable. Minimum real-world flows:

| Flow | Path |
| ---- | ---- |
| **Repair** | Customer → Device → Repair → Diagnosis → Approval → Completion |
| **Sales** | Sale → Inventory deduction → Invoice → Payment → Accounting → Report |
| **Purchasing** | PO → Receive → Inventory → Payable |
| **Accounting** | Sale → Journal → Payment → Ledger → Trial Balance → P&L |
| **Return** | Sale → Return → Restock → Refund/Reversal → Accounting |
| **Automation** | Business event → Outbox → Background job → Notification → Delivery status |

---

## Documentation set

| Doc | Owns |
| --- | ---- |
| `architecture.md` | Target architecture, stack, module boundaries, non-goals |
| `current-state.md` | What is implemented today vs planned (keep in sync each phase) |
| `development-plan.md` | Phases, build plans, exit criteria, DoD (this file) |
| `database.md` | Schema notes, constraints, migration commands |
| `api.md` | API conventions and endpoint catalog by phase |
| `modules.md` | Module ownership and dependencies |
| `business-rules.md` | Domain invariants and workflows |
| `accounting.md` | Chart of accounts defaults, posting mappings, period rules, report definitions (create in Phase 4) |
| `events.md` | Domain events, outbox contract, handlers, failure states (create in Phase 5; stub mappings in Phase 4) |
| `testing.md` | How to run unit/API/E2E tests |
| `deployment.md` | Environments, migrations, secrets, rollback (create by Phase 7; draft earlier as needed) |
| `operations.md` | Backups, restore drills, onboarding, opening balances, support procedures (Phase 7) |

Existing Phase 1–3 docs remain authoritative for completed work; expand them when Phase 4+ changes land.

---

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

---

## Local development

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

---

## Phase 0 — Discovery / Architecture

### Goal

Establish shared understanding of the product, modular monolith architecture, stack, and phased delivery so later phases do not reinvent foundations.

### Current Status

**Completed**

### Scope

Specs in repo, architecture documentation, development plan, local infrastructure (PostgreSQL via Docker), solution layout.

### Build Plan

#### 0.1 Specs and decisions

Capture technical specs and original product plan under `docs/specs/`. Document modular monolith, ASP.NET Core + Next.js + PostgreSQL, cookie auth, EF Core migrations.

#### 0.2 Architecture and module map

Document module ownership, dependency direction (API → Application → Domain; Infrastructure adapters), and non-goals (no microservices by default).

#### 0.3 Development plan baseline

Phase roadmap and exit criteria for Core through Expansion.

### Data Model Changes

None beyond deciding PostgreSQL + EF Core code-first.

### API / Application Layer

Conventions: versioned REST `/api/v1/`, Result-style errors, org claims on authenticated requests.

### UI / User Workflow

Admin UI shell planned (Next.js App Router); no production shop workflows yet.

### Business Rules

N/A beyond org-scoped multi-tenant intent.

### Events / Automation

MediatR / domain-event base types reserved for later phases.

### Testing

Document intended Domain + API test approach.

### Documentation

`architecture.md`, initial `development-plan.md`, related stubs.

### Implementation Order

1. Specs in repo → 2. Architecture doc → 3. Plan → 4. Solution bootstrap (executed in Phase 1).

### Exit Criteria

- [x] Architecture and development plan documented
- [x] Specs available in repo
- [x] Stack and modular monolith decision recorded

---

## Phase 1 — Core Platform

### Goal

Deliver a usable shop operations core: authenticate staff, manage org/branch-scoped customers/devices/services, run the repair workflow, see honest dashboard metrics, search, and audit sensitive actions.

### Current Status

**Completed**

### Scope

Identity (users, roles, permissions, cookie sessions), organization/branch, customers, devices, service catalog, repairs (statuses, history, lines, notes), dashboard (Phase 1 metrics only), audit logs, global search, domain/API tests, architecture docs aligned to implementation.

### Build Plan (completed)

#### 1.1 Identity and tenancy

Cookie sessions (`ersms_auth`); roles/permissions; `Organization` + `Branch`; `ICurrentUser` with `org_id` / `branch_id` claims.

#### 1.2 Master data

Customers, devices, service catalog CRUD with soft deactivate (`IsActive`).

#### 1.3 Repair workflow

Configurable `RepairStatusDefinitions`, validated transitions, status history, repair notes/service lines.

#### 1.4 Cross-cutting

Audit logs (immutable to normal users), global search (repair #, customer name/phone, device model/serial/IMEI), dashboard with real zeros (no fake sales/stock).

### Data Model Changes (completed)

Identity, customers, devices, services, repairs, status definitions/history, audit logs—all org-scoped as applicable. Soft deactivate preferred over hard delete for master data.

### API / Application Layer (completed)

Auth, CRUD endpoints, repair status transitions with permission checks; domain logic in application/domain services.

### UI / User Workflow (completed)

Login; manage customers/devices/services; create and progress repairs; dashboard; search; audit visibility for authorized roles.

### Business Rules (completed)

Org scope on business data; repair transitions via domain workflow; soft deactivate preserves history.

### Events / Automation

Domain event types exist for repairs; publishing/handlers deferred (Phase 5). Cross-module side effects use direct service calls + audit.

### Testing (completed)

Domain + API tests for authz, repair transitions, audit.

### Documentation (completed)

`architecture.md`, `database.md`, `api.md`, `modules.md`, `business-rules.md`, `testing.md` match Phase 1.

### Implementation Order (historical)

Identity → org/branch seed → customers/devices/services → repairs → audit/search/dashboard → tests/docs.

### Exit Criteria

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

---

## Phase 2 — Inventory / Purchasing

### Goal

Make parts stock trustworthy via an append-only ledger, and support supplier POs with partial/full receiving that posts inventory correctly.

### Current Status

**Completed**

### Scope

Parts catalog, stock ledger, adjustments, suppliers, purchase orders, receiving, low-stock dashboard metric, `inventory.*` / `purchasing.*` permissions, tests, docs.

### Build Plan (completed)

#### 2.1 Parts and ledger

Parts with soft deactivate; on-hand = `SUM(StockLedgerEntries.QuantityDelta)` per `(OrganizationId, BranchId, PartId)`.

#### 2.2 Adjustments

Manual adjustments that would make on-hand negative are rejected; ledger history per part.

#### 2.3 Suppliers and POs

Supplier CRUD; PO workflow draft → ordered → partial/full receive → cancel rules; receiving posts `PurchaseReceive` ledger credits.

#### 2.4 Metrics and permissions

Dashboard low-stock count; seed permissions for Owner, Admin, Inventory staff.

### Data Model Changes (completed)

`Part`, `StockLedgerEntry`, `Supplier`, `PurchaseOrder`, `PurchaseOrderLine` with org/branch as designed.

### API / Application Layer (completed)

Parts adjust/receive flows; PO submit/receive/cancel; permission checks.

### UI / User Workflow (completed)

Parts, stock history, suppliers, PO create/receive, low-stock visibility on dashboard.

### Business Rules (completed)

Append-only ledger; no negative on-hand from adjustments; receive cannot exceed remaining ordered qty; cancel not after receiving started.

### Events / Automation

Low-stock alerts deferred to Phase 5; metric is synchronous query.

### Testing (completed)

Domain + API tests for stock math, PO workflow, adjust/receive.

### Documentation (completed)

`api`, `database`, `modules`, `architecture`, `business-rules` updated.

### Implementation Order (historical)

Parts + ledger → adjustments → suppliers → POs/receiving → dashboard → tests/docs.

### Exit Criteria

- [x] Parts catalog with soft deactivate; on-hand derived from append-only stock ledger
- [x] Stock adjustments reject negative on-hand; ledger history available per part
- [x] Suppliers CRUD with soft deactivate
- [x] Purchase orders: draft → submit → receive (partial/full) → cancel rules; receiving posts ledger credits
- [x] Dashboard low-stock count (on hand < reorder level) is real
- [x] `inventory.*` / `purchasing.*` permissions seeded for Owner, Admin, Inventory staff
- [x] Domain + API tests for stock math, PO workflow, adjust/receive flows
- [x] Docs updated (`api`, `database`, `modules`, `architecture`, `business-rules`)

**Do not start Phase 3 until Phase 2 is stable and the above checklist is met.**

---

## Phase 3 — Sales / POS

### Goal

Support branch part sales with invoices, idempotent/partial payments, inventory deduction, returns/refunds, and void of eligible unpaid sales—honest dashboard sales metrics.

### Current Status

**Completed** (with hardening required before Phase 4)

### Scope

Part sales (optional customer); complete-at-checkout sale + invoice + optional payment; stock deduction via ledger `Sale`; payments with `(OrganizationId, IdempotencyKey)` uniqueness; returns/`SaleReturn` restock; void unpaid completed sales; `sales.*` permissions; dashboard today’s sales and unpaid invoice count.

**Out of scope for Phase 3:** repair checkout billing, tax calculation (`TaxTotal` remains `0`), full accounting journals.

### Build Plan (completed)

#### 3.1 Sale completion

Create `Sale` + `SaleLine`s, 1:1 `Invoice`, deduct stock (`Sale` ledger rows); reject insufficient stock.

#### 3.2 Payments

Idempotent payments; partial payments; invoice status from balances (`UNPAID` / `PARTIAL` / `PAID`).

#### 3.3 Returns and voids

Returns cannot exceed sold minus already returned; restock via `SaleReturn` ledger; optional refund payment. Void only unpaid completed sales; reverse stock.

#### 3.4 Permissions and dashboard

Seed `sales.read` / `sales.write` / `sales.refund`; real today’s sales total and unpaid invoice count.

### Data Model Changes (completed)

`PaymentMethod`, `Sale`, `SaleLine`, `Invoice`, `Payment`, `SaleReturn`, `SaleReturnLine`.

### API / Application Layer (completed)

Sales, invoices, payment methods controllers/services; transaction boundaries for complete/pay/return/void.

### UI / User Workflow (completed)

POS checkout, unpaid invoices, payments, returns, voids (authorized).

### Business Rules (completed)

Part sales only; payment methods org-seeded (`CASH`, `CARD`, `TRANSFER`); soft rules as in `business-rules.md`.

### Events / Automation

Accounting posting deferred to Phase 4; notifications deferred to Phase 5.

### Testing (completed)

Domain + API tests for stock reject, payment idempotency, return/restock, dashboard metrics.

### Documentation (completed)

`api`, `database`, `modules`, `architecture`, `business-rules` updated.

### Implementation Order (historical)

Payment methods → complete sale → payments → returns/void → dashboard → tests/docs.

### Exit Criteria

- [x] Part sales at a branch (optional customer); complete-at-checkout creates sale + invoice + optional payment
- [x] Completing a sale deducts stock via ledger `Sale` entries; insufficient stock rejected
- [x] Idempotent payments (`IdempotencyKey` unique per org); partial payments allowed
- [x] Invoice is 1:1 auto projection of completed sale; unpaid/partial list available
- [x] Returns restock via `SaleReturn` ledger and can record refund payments
- [x] Void unpaid completed sales reverses stock
- [x] `sales.read` / `sales.write` / `sales.refund` seeded (Owner/Admin/Cashier; Inventory staff read)
- [x] Dashboard today’s sales total and unpaid invoice count are real
- [x] Domain + API tests for stock reject, payment idempotency, return/restock, dashboard metrics
- [x] Docs updated (`api`, `database`, `modules`, `architecture`, `business-rules`)

**Do not start Phase 4 until Phase 3 is stable and the above checklist is met.**

### Phase 3 Hardening / Accounting Preparation

Before building Phase 4, verify these integrity gates so accounting sources are trustworthy. This is **not** a rebuild of Sales—only checks and small fixes if gaps are found.

#### H.1 Payment integrity

- Duplicate `IdempotencyKey` within an org never creates a second successful payment.
- `Sale.AmountPaid` / `BalanceDue` and invoice balances always match sum of succeeded payments minus refunds.
- Partial payment cannot overpay total; paid sale cannot accept another succeeded payment without explicit overpayment policy (default: reject).

#### H.2 Return / void correctness

- Returned quantities never exceed sold − previously returned.
- Void only when unpaid and completed; voided sales/invoices cannot accept new payments.
- Restock ledger rows exist for every return/void line quantity; on-hand increases match restock deltas.

#### H.3 Stock interaction

- Completing a sale never leaves invoice without matching negative `Sale` ledger rows (same org/branch/part/qty).
- Concurrent complete-sale attempts cannot drive on-hand negative (transaction + check).

#### H.4 Accounting source mapping inventory

Document stable source IDs for Phase 4 posting:

| Business event | SourceType (proposed) | SourceId |
| -------------- | --------------------- | -------- |
| Sale completed | `SaleCompleted` | `Sale.Id` |
| Payment succeeded | `PaymentSucceeded` | `Payment.Id` |
| Sale return | `SaleReturnCompleted` | `SaleReturn.Id` |
| Sale voided | `SaleVoided` | `Sale.Id` |
| PO receive batch | `PurchaseReceived` | receive transaction / PO receive id (introduce stable id if missing) |
| Stock adjustment | `StockAdjusted` | `StockLedgerEntry.Id` or adjustment batch id |

Record cost basis for COGS: use part cost fields available at sale time (snapshot on line or current average/standard cost—document choice in `accounting.md` during Phase 4; default: unit cost stored/snapshotted on sale line or part cost at posting time with explicit rule).

#### H.5 Regression testing

- Re-run full Sales + Inventory API suites.
- Add/confirm cases: double-submit payment, return then void attempt, over-return, concurrent stock.

#### Hardening exit checklist

- [x] Payment, balance, and invoice invariants verified (tests green)
- [x] Return/void/stock invariants verified (tests green)
- [x] SourceType/SourceId mapping table written into `docs/accounting.md` (or draft section)
- [x] No known critical integrity bugs blocking journal posting

---

## Phase 4 — Accounting

### Goal

Introduce a proper **double-entry accounting subsystem** so every material money and inventory valuation movement from Sales and Purchasing (plus expenses and supplier payments) posts balanced, immutable, source-traceable journals; support AR/AP, periods, financial reports, and automated reconciliation checks.

### Current Status

**Planned** (next major phase)

### Scope

Chart of Accounts, accounts, account types, journal entries/lines, accounting periods, posting engine, business→GL mappings, AR, AP, expenses, financial reports, reconciliation checks, accounting permissions, dashboard cash/expense metrics (real), domain/API tests, `accounting.md`.

**Out of scope:** multi-currency, external tax engines, payroll, full bank feed import (adapters may be stubbed for Phase 6/7), repair billing checkout (unless product later extends Sales).

### Conceptual posting workflow

```text
Business Transaction
    → Accounting Source Event (SourceType + SourceId, org-unique)
    → Journal Entry (header: period, date, memo, status Posted)
    → Journal Lines (debits/credits)
    → General Ledger (query over posted lines)
```

### Build Plan

#### 4.1 Accounting Core — Chart of Accounts and Accounts

**What to build:** Org-scoped chart of accounts with seedable default template (Assets, Liabilities, Equity, Revenue, COGS, Expenses). Each `Account` has code, name, type, normal balance (debit/credit), `IsActive`, optional parent for hierarchy (keep flat initially if simpler; support parent id for grouping in reports).

**Entities:** `Account`, optional `AccountType` enum/codes (`Asset`, `Liability`, `Equity`, `Revenue`, `CostOfGoodsSold`, `Expense`).

**Business rules:** Unique `(OrganizationId, Code)`; system/seeded accounts may be protected from delete; soft deactivate only; cannot deactivate if required by active mapping.

**Dependencies:** Identity org scope.

#### 4.2 Accounting Periods

**What to build:** `AccountingPeriod` per org (e.g. calendar month): `StartDate`, `EndDate`, `Status` (`Open` / `Closed`). Posting allowed only into Open periods (or admin “post to closed” forbidden by default).

**Workflows:** Create periods (generate year), close period (requires balanced books / reconciliation gate optional), reopen only with `accounting.periods` permission and audit.

**Edge cases:** Transaction date maps to exactly one period; refuse post if no open period covers the date.

#### 4.3 Journal Entries, Lines, and Source References

**What to build:**

- `JournalEntry`: OrganizationId, BranchId (nullable or required—default: branch of source transaction), PeriodId, EntryNumber, EntryDate, PostedAt, PostedByUserId, Memo, Status (`Posted` only for v1—no draft journals unless needed for manual entries), SourceType, SourceId, optional ReversesJournalEntryId.
- `JournalLine`: JournalEntryId, AccountId, Debit, Credit, Description; exactly one of Debit/Credit > 0 per line (other zero).
- Unique index `(OrganizationId, SourceType, SourceId)` so duplicate source events never create duplicate accounting entries.
- Manual journal entry command for adjustments (still must balance; still gets a synthetic SourceType `ManualJournal` + new SourceId).

**Rules:**

- Sum(Debits) == Sum(Credits) or reject.
- Posted entries are **immutable** (no update/delete of lines).
- Corrections: post reversing entry (swap debits/credits) linked via `ReversesJournalEntryId`, then optional new adjustment entry.
- Every posted entry remains traceable to source.

#### 4.4 Journal Posting Engine

**What to build:** Application service `AccountingPostingService` (name flexible) that:

1. Accepts a typed posting request (or domain handler) with source + computed lines.
2. Begins DB transaction (or participates in ambient transaction with the business write when posting synchronously).
3. Checks uniqueness of source; if exists, return existing entry (idempotent).
4. Validates period open, accounts active, balances, org scope.
5. Inserts entry + lines; writes audit log.
6. Returns journal id.

**Sync vs async:** Phase 4 posts **synchronously in the same transaction** as the business commit when the business module calls the posting service (simplest integrity). Phase 5 may also emit outbox events for notifications without changing GL posting ownership.

**Dependencies:** Sales, Purchasing/Inventory for source data; do not reverse-call UI.

#### 4.5 Accounting Mappings (business → GL)

Configurable org account mappings table `AccountingAccountMapping` (or settings): keys such as `InventoryAsset`, `Cogs`, `SalesRevenue`, `AccountsReceivable`, `AccountsPayable`, `Cash`, `CardClearing`, `Bank`, `RefundExpense` (if needed). Seed defaults to seeded CoA accounts.

##### Sale completed

For each completed sale (source `SaleCompleted` / Sale.Id):

- Debit **Cash** (amount paid at completion) and/or **Accounts Receivable** (balance due)
- Credit **Sales Revenue** (sale total; tax stays 0 until tax phase)
- Debit **COGS**, Credit **Inventory** (cost × qty for lines; cost rule documented)

If fully unpaid: full total to AR. If fully paid at checkout: full total to cash/bank by method. If partial: split cash vs AR.

##### Payment succeeded (subsequent)

Source `PaymentSucceeded` / Payment.Id:

- Debit Cash/Bank/Card clearing (by `MethodCode` mapping)
- Credit Accounts Receivable
- Amount = payment amount

##### Purchase receive

Source `PurchaseReceived` / stable receive id:

- Debit Inventory (received value = qty × unit cost)
- Credit Accounts Payable

Introduce `SupplierBill` (or AP bill) linked to receive if not present: bill total, balance, supplier, status.

##### Supplier payment

Source `SupplierPayment` / payment id:

- Debit Accounts Payable
- Credit Cash/Bank
- Allocate to bills (partial allocation allowed)

##### Expense

Source `ExpensePosted` / expense id:

- Debit Expense account (category mapping)
- Credit Cash/Bank or AP (if unpaid)

##### Return

Source `SaleReturnCompleted`:

- Reverse revenue for returned line totals (Debit Revenue / Credit AR or Cash depending on refund path)
- Reverse COGS/Inventory (Debit Inventory / Credit COGS)
- If refund payment recorded: Debit AR or reduce cash appropriately—keep AR/cash consistent with sale balances after return

##### Void

Source `SaleVoided`:

- Full reversing journal of the original sale posting (and any related payments if void allowed only unpaid—payments should not exist; reverse sale AR/revenue/COGS/inventory only)

##### Stock adjustment (valuation)

If adjustment affects inventory value: Debit/Credit Inventory vs Adjustment expense/gain account. Quantity-only adjustments with no unit cost may skip GL or use configured cost—document rule (default: post value using part cost × qty delta).

#### 4.6 Accounts Receivable

**What to build:**

- Invoice balance already on `Invoice`; accounting AR subledger must reconcile to sum of open invoice `BalanceDue` for customers.
- Payment allocation: payments already tied to `SaleId`; treat as allocation to that invoice (1:1 sale↔invoice).
- Customer balance = sum of open invoice balances for customer (walk-in/null customer tracked under a system “Counter sale” bucket or excluded from customer statements).
- Aging buckets: Current, 1–30, 31–60, 61–90, 90+ based on `IssuedAt` vs as-of date.
- Customer statement query: open invoices, payments in period, ending balance.

#### 4.7 Accounts Payable

**What to build:**

- `SupplierBill` from receives (and manual bills).
- `SupplierPayment` + `SupplierPaymentAllocation` (bill id, amount).
- Supplier balance and bill balance; aging similar to AR.
- Permissions: `accounting.ap` / `purchasing.pay` (choose codes; seed Owner/Admin).

#### 4.8 Expenses

**What to build:**

- `ExpenseCategory` (maps to expense account)
- `Expense`: org, branch, category, amount (`decimal(18,2)`), date, payee, payment method or payable flag, status (`Draft` / `Approved` / `Posted` / `Voided`), notes
- Approval workflow for amounts over configurable threshold (or always require approve for non-Owner)
- Attachments via existing `IFileStorage` (metadata table `ExpenseAttachment`)
- On approve/post: journal as in mapping; audit

#### 4.9 Financial Reports

Read models/queries over posted journal lines (do not maintain separate mutable “total” tables as source of truth):

| Report | Definition |
| ------ | ---------- |
| General Ledger | Lines by account + date range, running balance |
| Trial Balance | Per account sum debit/credit as of date; must balance |
| Profit & Loss | Revenue − COGS − Expenses for period |
| Balance Sheet | Assets = Liabilities + Equity as of date |
| Cash Flow | Period cash account movements (indirect or direct from cash lines—pick direct from cash account lines for MVP) |
| AR Aging | Customer open invoices by bucket |
| AP Aging | Supplier open bills by bucket |

#### 4.10 Reconciliation

Automated check commands/queries (Admin):

- Trial balance debits == credits
- Sum of completed sale totals in period ≈ sales revenue credits (allow documented timing differences)
- Inventory GL balance vs sum(part cost × on-hand) within tolerance or exact under chosen cost method
- AR GL vs sum(invoice BalanceDue)
- AP GL vs sum(bill balances)
- Payments total vs cash/AR movement samples

Failed checks produce a reconciliation report row (`RequiresAttention`) without mutating GL.

#### 4.11 Opening balances

Command to post opening balance journal (balanced) into first open period: seed AR/AP/inventory/cash/equity. Required for production cutover (also referenced in Phase 7).

#### 4.12 Permissions and dashboard

Seed `accounting.read`, `accounting.write`, `accounting.post`, `accounting.periods`, `accounting.approve_expense` (adjust names consistently). Owner/Admin full; others read as needed. Dashboard: real expense totals / cash movement metrics—never fake.

### Data Model Changes

New tables (names indicative):

| Entity | Important fields / constraints |
| ------ | ------------------------------ |
| `Accounts` | OrgId, Code (unique per org), Name, Type, NormalBalance, ParentAccountId?, IsSystem, IsActive |
| `AccountingPeriods` | OrgId, Name, StartDate, EndDate, Status; unique non-overlap per org |
| `JournalEntries` | OrgId, BranchId?, PeriodId, EntryNumber (unique per org), EntryDate, PostedAt, SourceType, SourceId, ReversesJournalEntryId?; **unique (OrgId, SourceType, SourceId)** |
| `JournalLines` | EntryId, AccountId, Debit, Credit, Description; check Debit≥0, Credit≥0, not both >0 |
| `AccountingAccountMappings` | OrgId, MappingKey, AccountId |
| `SupplierBills` | OrgId, SupplierId, BillNumber, SourceReceiveId?, Total, AmountPaid, Balance, Status, IssuedAt |
| `SupplierPayments` | OrgId, BranchId, SupplierId, Amount, MethodCode, PaidAt, IdempotencyKey unique per org |
| `SupplierPaymentAllocations` | PaymentId, BillId, Amount |
| `ExpenseCategories` | OrgId, Name, AccountId, IsActive |
| `Expenses` | OrgId, BranchId, CategoryId, Amount, ExpenseDate, Status, … |
| `ExpenseAttachments` | ExpenseId, StorageKey, FileName, ContentType |

Indexes: journal lines by (AccountId, EntryDate); AR/AP by supplier/customer and status; periods by date range.

Migrations: additive EF migration(s); seed CoA + mappings per org (on org create and for existing seed org).

### API / Application Layer

**Commands / use cases:**

- Create/update/deactivate account; generate/close periods
- Post manual journal; post/reverse via engine
- Upsert mappings
- Create supplier bill (from receive hook); record supplier payment (idempotent)
- Create/approve/void expense; upload attachment
- Run reconciliation checks; post opening balances

**Queries:**

- CoA list, period list, journal by id/source, GL, trial balance, P&L, balance sheet, cash flow, AR/AP aging, statements

**Validation / authz:** permission attributes/checks; org isolation; money precision; balanced entry validation.

**Transaction boundaries:** business write + journal post in one DB transaction for sale/payment/receive/return/void/expense post.

**Idempotency:** source unique key; supplier payment IdempotencyKey; expense post once.

### UI / User Workflow

- Chart of accounts browser; period management
- Journal entry list/detail (read-mostly); manual journal form
- Account mapping settings (Owner/Admin)
- Expenses list/create/approve/attach
- Supplier bills & pay bills
- Reports pages: Trial Balance, P&L, Balance Sheet, GL, AR/AP aging
- Reconciliation results panel
- Dashboard widgets for accounting-ready metrics

### Business Rules

- Debits == credits on every posted entry
- Posted journals immutable
- Duplicate SourceType+SourceId forbidden (idempotent return)
- Post only to Open periods
- Money `decimal(18,2)` only
- Org scope on all accounting rows
- AR/AP subledgers must be reconcilable to GL control accounts
- Sales/Inventory modules remain sources for operational data; GL is books of record for financial position

### Events / Automation

Produce domain events (even if handlers come in Phase 5): `JournalEntryPosted`, `ExpenseApproved`, `SupplierPaymentRecorded`, `AccountingPeriodClosed`. Sales/Purchasing call posting service directly in Phase 4.

Document event payloads in draft `events.md` / `accounting.md`.

### Testing

**Unit:** balance validation, mapping line builders (sale/payment/return/void/receive), aging buckets, period assignment, reversal line swap.

**API:** post sale → journal exists and balances; double complete/post is idempotent; payment posts AR reduction; return/void reverse correctly; close period blocks posts; unauthorized access forbidden; org isolation; expense approve posts; trial balance endpoint balances; reconciliation detects induced mismatch.

**E2E (when UI ready):** Sale → Journal → Payment → Ledger → Trial Balance → P&L; Return path.

### Documentation

Create `docs/accounting.md`. Update `database.md`, `api.md`, `modules.md`, `business-rules.md`, `architecture.md`, `testing.md`, `current-state.md`.

### Implementation Order

1. Accounts + CoA seed + periods  
2. Journal entry/line model + posting engine + source uniqueness  
3. Account mappings + Sale completed + Payment postings  
4. Return + Void reversals  
5. Purchase receive → AP bill + Inventory/AP journals  
6. Supplier payments + allocations  
7. Expenses + attachments + approval  
8. Financial report queries  
9. Reconciliation checks + opening balances  
10. UI screens  
11. Permissions/dashboard  
12. Tests + docs  

### Exit Criteria

- [ ] Chart of accounts seeded per org; accounts CRUD/soft deactivate with permissions
- [ ] Open/closed accounting periods enforced on post
- [ ] Journal posting engine enforces balance, immutability, source idempotency, traceability
- [ ] Sale, payment, return, void, purchase receive, supplier payment, and expense mappings post correct journals
- [ ] AR aging/statements and AP aging/bills/payments work
- [ ] GL, Trial Balance, P&L, Balance Sheet, Cash Flow available via API (and UI for primary reports)
- [ ] Reconciliation checks run and surface failures without corrupting data
- [ ] Opening balance journal supported
- [ ] Money remains `decimal(18,2)`; no float; no silent mutation of posted entries
- [ ] Domain + API tests green for posting, idempotency, reversals, isolation
- [ ] Docs updated including `accounting.md`

---

## Phase 5 — Automation

### Goal

Deliver reliable asynchronous side effects (notifications, reminders, scheduled work) that **never fail a core business transaction** when email/SMS or workers are down.

### Current Status

**Planned**

### Scope

Transactional outbox, background processing (Hangfire), notification delivery tracking, repair/approval/pickup notifications, payment reminders, low-stock alerts, recurring expenses, scheduled reports, background job administration.

### Reliable async model

```text
Business Transaction
    → Database Commit (includes Outbox row in same transaction)
    → Outbox/Event Record (EventId, CorrelationId, Type, Payload, Status)
    → Background Handler (Hangfire)
    → Side Effect (email/SMS/report)
    → Delivery Status update
```

### Build Plan

#### 5.1 Outbox and event persistence

- `OutboxMessage`: Id (EventId), OrganizationId, CorrelationId, Type, Payload (JSON), OccurredAt, Status (`Pending` / `Processing` / `Processed` / `Failed` / `DeadLetter`), AttemptCount, LastError, NextAttemptAt.
- Write outbox rows in the **same** DB transaction as the business change.
- Dispatcher polls/claims pending messages (or Hangfire continuation after commit).

#### 5.2 Handler reliability

- Handlers idempotent on EventId (store `ProcessedEvent` or mark outbox Processed once).
- Retries with backoff; after N failures → `DeadLetter` / RequiresAttention.
- CorrelationId flows into logs and notification records.

#### 5.3 Notification delivery tracking

- `Notification`: OrgId, Channel (`Email`/`Sms`), Template, Recipient, RelatedEntityType/Id, OutboxMessageId, Status (`Queued`/`Sent`/`Failed`), ProviderMessageId?, error text.
- Provider adapters behind interfaces (`IEmailSender`, `ISmsSender`); failures update notification + outbox, do not throw into API request path.

#### 5.4 Automation features

| Feature | Trigger | Behavior |
| ------- | ------- | -------- |
| Repair notifications | Status changes | Notify customer/staff per template config |
| Approval notifications | Enter `WAITING_FOR_APPROVAL` | Notify customer with approve link/instructions |
| Ready-for-pickup | `READY_FOR_PICKUP` | Notify customer |
| Payment reminders | Scheduled job | Unpaid invoices older than N days |
| Low-stock alerts | On-hand < reorder (scheduled or on ledger post) | Notify inventory roles |
| Recurring expenses | Cron | Create draft/post expenses from `RecurringExpense` definitions |
| Scheduled reports | Cron | Generate P&L/AR snapshot; email Owner or store file |
| Job administration | UI/API | List jobs, outbox dead-letters, requeue, inspect failures |

#### 5.5 Background job monitoring

Hangfire dashboard (secured), metrics: pending outbox count, dead-letter count, job failures. Health check endpoint includes DB + worker heartbeat.

### Data Model Changes

`OutboxMessages`, `Notifications`, `NotificationTemplates` (org-configurable), `RecurringExpenses`, optional `ProcessedEvents`.

### API / Application Layer

- Internal dispatcher only for outbox claim.
- Admin APIs: list outbox, requeue dead-letter, notification history, recurring expense CRUD, report schedule CRUD.
- Permissions: `automation.admin`, `notifications.read`.

### UI / User Workflow

- Notification settings/templates
- Failed deliveries / dead-letter queue
- Recurring expenses
- Scheduled reports
- Job/outbox admin (Owner/Admin)

### Business Rules

- Core API success independent of provider availability
- Idempotent handlers
- Org scope on all messages
- PII in payloads minimized; templates server-side

### Events / Automation

Consume Phase 1–4 domain events via outbox: repair status, sale/payment, low stock, period close, expense approved, etc.

### Testing

Unit: retry/idempotency state machine. API/integration: business commit creates outbox; handler success marks Processed; handler failure retries then DeadLetter; notification row updated. E2E: Business event → Outbox → Job → Notification → Delivery status.

### Documentation

Create `events.md`; update `architecture.md`, `operations.md` draft, `testing.md`.

### Implementation Order

1. Outbox table + write helper in DbContext SaveChanges path or explicit unit-of-work  
2. Hangfire host + dispatcher  
3. Email adapter (dev = pickup folder / console)  
4. Repair status notification handlers  
5. Payment reminders + low-stock jobs  
6. Recurring expenses + scheduled reports  
7. Admin UI + dead-letter requeue  
8. Tests + docs  

### Exit Criteria

- [ ] Outbox written atomically with business commits
- [ ] Handlers retry, idempotent, dead-letter on exhaustion
- [ ] Notifications tracked with delivery status
- [ ] Repair/approval/pickup, payment reminders, low-stock alerts working in non-prod
- [ ] Recurring expenses and scheduled reports runnable
- [ ] Job/outbox admin usable by Owner/Admin
- [ ] Killing email provider does not fail sale/repair APIs
- [ ] Tests + `events.md` updated

---

## Phase 6 — Expansion

### Goal

Extend ERSMS beyond the counter admin app into customer/supplier touchpoints, multi-branch operations, mobile technicians, booking, analytics, and integration adapters—without rewriting Phase 1–4 domain rules.

### Current Status

**Planned**

### Scope

Customer portal, warranty management, multi-branch transfers, mobile technician app, online booking, supplier portal, advanced analytics, external integrations.

### Build Plan

#### 6.1 Customer Portal

**Build:** Separate auth (customer identity or magic-link tied to customer record); scoped APIs that only return that customer’s data.

**Workflows:** view repair status + history; approve estimates; view invoices/payment status; download receipts (PDF via file storage); list past repairs.

**Rules:** strict customer↔resource authorization; no staff permission leakage; org isolation still applies.

#### 6.2 Warranty Management

**Entities:** `Warranty` (repair/sale/part, start/end, coverage rules), `WarrantyClaim` (claim, diagnosis, outcome: covered rework / denied / goodwill).

**Workflows:** start warranty on completed repair/sale line; claim → rework repair link → outcome; parts/services covered flags.

**Rules:** expired claims rejected; claim does not silently mutate original sale accounting without Phase 4-compliant journals (warranty rework cost expense mapping).

#### 6.3 Multi-Branch

**Build:** Branch-scoped users already exist; harden list filters for inventory, repairs, sales, expenses, reporting by branch. Inter-branch **stock transfer** workflow:

`Requested` → `Approved` → `Dispatched` → `Received` → `Completed`

**Entities:** `StockTransfer`, lines, status history. Ledger: negative at source branch on dispatch/receive rules (document: deduct source on Dispatch, credit destination on Receive); accounting: inventory relocation (no P&L) or in-transit account.

**Reports:** branch P&L / stock by branch.

#### 6.4 Mobile Technician App

**Build:** Mobile-friendly UI or separate app consuming same API. Features: assigned jobs, diagnosis notes, photos (`IFileStorage`), parts used (inventory rules), status changes via existing `RepairWorkflow`.

**Rules:** reuse backend domain; offline is optional later—online-first MVP.

#### 6.5 Online Booking

**Build:** public booking flow: service selection → device details → schedule slot → confirmation → cancel/reschedule. Converts to internal appointment/`Repair` draft (`RECEIVED` or `BOOKED` status if added to status definitions).

**Rules:** capacity per branch/day; confirmation notifications via Phase 5.

#### 6.6 Supplier Portal

**Build:** supplier user login scoped to their `SupplierId`; view POs, acknowledge, maybe ASN/ship notices; no access to other suppliers or sales data.

#### 6.7 Advanced Analytics

**Build:** read models / materialized summaries updated asynchronously (outbox → projector) for repair TAT, sales trends, margin, technician productivity. Analytics DB queries must not lock hot transactional tables—use replicas or summary tables.

#### 6.8 Integrations

Adapter boundaries (interfaces already preferred):

| Integration | Port |
| ----------- | ---- |
| Payments | `IPaymentCapture` (card terminals / online) |
| Email/SMS | existing Phase 5 ports |
| Barcode hardware | scanner input conventions / label print |
| Receipt printers | `IReceiptPrinter` |
| Accounting/tax export | `IAccountingExport` |
| External reporting | webhook/CSV export |

### Data Model Changes

Portal identities; warranties/claims; stock transfers; booking slots/appointments; supplier portal users; analytics summary tables; integration credentials (secrets in config, not code).

### API / Application Layer

Versioned portal/mobile routes; same domain services; rate limiting on public booking; idempotency on booking create.

### UI / User Workflow

Portal pages; warranty admin; transfer UI; mobile repair screens; booking widget; supplier PO views; analytics dashboards; integration settings.

### Business Rules

No bypass of inventory/accounting invariants; branch transfers append ledger; portal authz separate from staff RBAC.

### Events / Automation

Booking confirmed, transfer status changed, warranty claim opened—via outbox.

### Testing

API isolation tests for portal/supplier; transfer ledger correctness; booking conversion; analytics projector idempotency.

### Documentation

Update modules, api, business-rules, architecture, events; add portal/security notes to `operations.md`.

### Implementation Order

1. Multi-branch reporting filters + stock transfer  
2. Warranty  
3. Customer portal  
4. Online booking  
5. Mobile tech surfaces  
6. Supplier portal  
7. Analytics read models  
8. Integration adapters as needed  
9. Tests + docs  

### Exit Criteria

- [ ] Customer portal supports status, approval, invoices, receipts, history securely
- [ ] Warranty claim/rework lifecycle works with accounting-safe cost handling
- [ ] Branch transfers follow Requested→…→Completed with correct stock
- [ ] Mobile tech can progress assigned repairs using backend rules
- [ ] Online booking creates internal records and notifications
- [ ] Supplier portal is isolated per supplier
- [ ] Analytics do not degrade transactional latency under load tests
- [ ] Integration ports exist for payments/messaging/print/export
- [ ] Tests + docs updated

---

## Phase 7 — Production Hardening

### Goal

Make ERSMS safely operable in production: secure, recoverable, observable, performant, integrity-checked, and operable by staff (onboarding, opening balances, rollback).

### Current Status

**Planned** (formal release gate; hardening work may begin earlier but Phase 7 signs off production readiness)

### Scope

Security review, reliability/DR, observability, performance testing, data integrity audits, operational readiness (config, imports, opening balances, migration, training).

### Build Plan

#### 7.1 Security

- Authorization matrix review (every endpoint permission + org filter)
- Tenant isolation tests (cross-org IDOR attempts fail)
- Branch isolation where required
- Rate limiting on auth and public endpoints
- Secrets in env/vault; no secrets in git
- Secure cookie flags (HTTP-only, Secure, SameSite) verified for production
- File upload validation (type, size, malware policy as appropriate)
- Dependency vulnerability review

#### 7.2 Reliability

- Automated PostgreSQL backups; documented retention
- Restore testing (restore to staging successfully)
- Disaster recovery runbook (RPO/RTO targets documented)
- Outbox/event recovery (replay dead-letter)
- Hangfire/worker recovery (restart, stuck Processing reclaim)

#### 7.3 Observability

- Structured logs with CorrelationId
- Error monitoring (e.g. exception telemetry)
- Health checks: API, DB, worker
- Metrics: request latency, job failures, outbox depth, accounting reconciliation failures
- API/DB monitoring dashboards

#### 7.4 Performance

Load/performance tests for:

- Large repair histories
- Large inventory ledgers
- Large journal ledgers
- Long-range financial reports
- Global search
- Concurrent stock operations
- Concurrent payments

Add indexes/read models where tests fail targets.

#### 7.5 Data Integrity

Scheduled or on-demand integrity jobs:

- Orphan detection (lines without headers, etc.)
- Duplicate business identifiers (sale/invoice/repair numbers per org)
- Unbalanced journals (should be zero)
- Invalid inventory (negative on-hand)
- Incorrect sale/invoice balances
- Missing audit for critical actions
- Missing accounting source mappings / unposted sources

#### 7.6 Operational Readiness

- Initial org/branch/user configuration checklist
- Product/parts CSV import
- Opening inventory (adjustments or import + ledger)
- Opening accounting balances (Phase 4 command)
- Data migration from legacy tools (mapping guide)
- Rollback procedures (migration down / backup restore)
- Onboarding + training/support procedures in `operations.md`

### Data Model Changes

Minimal: health/metrics config; optional integrity finding tables; import job records.

### API / Application Layer

Admin integrity report endpoints; import endpoints; health/readiness probes; no weakening of authz for convenience.

### UI / User Workflow

Ops admin: import wizards, integrity report, system health summary (Owner).

### Business Rules

Production config distinct from Development; seed credentials disabled or forced change.

### Events / Automation

Alert on dead-letter threshold and reconciliation failure.

### Testing

Security IDOR suite; backup restore drill documented; performance baselines recorded; integrity job tests with seeded anomalies.

### Documentation

Create/finalize `deployment.md` and `operations.md`; update `testing.md`, `architecture.md`, `current-state.md`.

### Implementation Order

1. Security + cookie/secrets hardening  
2. Backups + restore drill  
3. Observability + health  
4. Performance tests + fixes  
5. Integrity jobs  
6. Import/opening balance tooling  
7. Runbooks + training docs  
8. Sign-off checklist  

### Exit Criteria

- [ ] Security review items closed or accepted with documented risk
- [ ] Backup + successful restore demonstrated
- [ ] Observability and health checks live in target environment
- [ ] Performance targets met for listed scenarios (or waivers documented)
- [ ] Integrity audit clean or only known non-critical findings
- [ ] Opening inventory/accounting + import path documented and tested
- [ ] Deployment and rollback runbooks published
- [ ] Onboarding/support procedures published
- [ ] Phase 4–6 critical regressions still green

---

## Constraints (do not violate)

- Do not rebuild completed Phase 1–3 modules without a documented integrity bug or Phase 3 Hardening finding.
- Do not split into microservices by default.
- Do not store financial totals as the accounting source of truth.
- Do not silently mutate posted financial entries.
- Do not use floating-point values for money.
- Do not rely on the frontend for authorization.
- Do not let external notifications block core transactions.
- Do not let analytics queries compromise transactional performance.
- Do not hard-code business rules that should be configurable.
- Do not remove completed exit criteria merely to simplify this document.
