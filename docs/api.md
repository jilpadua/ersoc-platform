# API conventions

Base path: `/api/v1/`

Auth: HTTP-only cookie `ersms_auth` after `POST /api/v1/auth/login`.

Errors:

```json
{ "error": { "code": "not_found", "message": "…" } }
```

Stable codes include: `validation_error`, `not_found`, `unauthorized`, `forbidden`, `conflict`, `invalid_transition`, `invalid_credentials`.

List endpoints accept `page`, `pageSize`, `search`, `sortBy`, `sortDesc`, and where noted `includeInactive`.

## Soft deactivate vs hard delete

Catalog entities (customers, devices, services, parts, suppliers) support **soft deactivate** (`IsActive=false`). Physical hard deletes of transactional data are out of scope.

## Inventory (Phase 2)

On-hand quantity is **ledger-derived** (`SUM(StockLedgerEntries.QuantityDelta)` per org/branch/part). Adjustments reject results that would go negative.

## Endpoints

| Method | Path | Notes |
|--------|------|-------|
| POST | `/auth/login` | Rate-limited |
| POST | `/auth/logout` | |
| GET | `/auth/me` | |
| GET/POST | `/customers` | `includeInactive` on GET |
| GET/PATCH | `/customers/{id}` | |
| POST | `/customers/{id}/deactivate` | Soft deactivate |
| POST | `/customers/{id}/activate` | |
| GET/POST | `/devices` | `includeInactive` on GET |
| GET | `/customers/{id}/devices` | |
| GET/PATCH | `/devices/{id}` | |
| POST | `/devices/{id}/deactivate` | |
| POST | `/devices/{id}/activate` | |
| GET/POST | `/services` | `includeInactive` on GET |
| PATCH | `/services/{id}` | |
| POST | `/services/{id}/deactivate` | |
| POST | `/services/{id}/activate` | |
| GET/POST | `/services/categories` | |
| GET/POST | `/parts` | Includes `unitCost`, `unitPrice`, on-hand; `includeInactive` |
| GET/PATCH | `/parts/{id}` | |
| GET | `/parts/{id}/ledger` | Paged ledger history |
| POST | `/parts/{id}/adjustments` | Append ledger row |
| POST | `/parts/{id}/deactivate` | |
| POST | `/parts/{id}/activate` | |
| GET/POST | `/suppliers` | `includeInactive` |
| GET/PATCH | `/suppliers/{id}` | |
| POST | `/suppliers/{id}/deactivate` | |
| POST | `/suppliers/{id}/activate` | |
| GET/POST | `/purchase-orders` | Optional `status` filter |
| GET/PATCH | `/purchase-orders/{id}` | PATCH draft only |
| POST | `/purchase-orders/{id}/submit` | DRAFT → ORDERED |
| POST | `/purchase-orders/{id}/receive` | Posts ledger receive rows |
| POST | `/purchase-orders/{id}/cancel` | DRAFT or ORDERED only |
| GET/POST | `/sales` | Optional `status`, `unpaidOnly` |
| GET | `/sales/{id}` | Lines, payments, invoice, returns |
| POST | `/sales/{id}/payments` | Idempotent; `Idempotency-Key` header or body |
| POST | `/sales/{id}/returns` | Restock + optional refund |
| POST | `/sales/{id}/void` | Unpaid completed only |
| GET | `/invoices` | Optional `unpaidOnly` |
| GET | `/invoices/{id}` | Read-only |
| GET | `/payment-methods` | Seeded CASH/CARD/TRANSFER |
| GET/POST | `/repairs` | |
| GET | `/repairs/statuses` | |
| GET | `/repairs/{id}` | Includes `allowedNextStatuses` |
| PATCH | `/repairs/{id}/status` | Workflow-gated |
| PATCH | `/repairs/{id}/technician` | |
| POST | `/repairs/{id}/notes` | |
| GET | `/dashboard` | Includes `lowStockParts`, `todaySalesTotal`, `unpaidInvoiceCount` |
| GET | `/search?q=` | |
| GET | `/audit-logs` | |
| GET | `/health` | |

## Sales (Phase 3)

Creating a sale completes immediately: validates stock, writes sale lines, posts negative `Sale` ledger rows, creates a 1:1 invoice (`TaxTotal = 0`), and optionally records the first payment. Payments require a unique org-scoped idempotency key; replays return the same sale state without double-posting.
