# API conventions (Phase 1)

Base path: `/api/v1/`

Auth: HTTP-only cookie `ersms_auth` after `POST /api/v1/auth/login`.

Errors:

```json
{ "error": { "code": "not_found", "message": "…" } }
```

Stable codes include: `validation_error`, `not_found`, `unauthorized`, `forbidden`, `conflict`, `invalid_transition`, `invalid_credentials`.

List endpoints accept `page`, `pageSize`, `search`, `sortBy`, `sortDesc`, and where noted `includeInactive`.

## Soft deactivate vs hard delete

Phase 1 catalog entities (customers, devices, services) support **soft deactivate** (`IsActive=false`) so repair history stays intact. Physical hard deletes of transactional data are out of scope.

## Endpoints

| Method | Path | Notes |
|--------|------|-------|
| POST | `/auth/login` | Rate-limited |
| POST | `/auth/logout` | |
| GET | `/auth/me` | |
| GET/POST | `/customers` | `includeInactive` on GET |
| GET/PATCH | `/customers/{id}` | Update includes email, notes, etc. |
| POST | `/customers/{id}/deactivate` | Soft deactivate |
| POST | `/customers/{id}/activate` | Reactivate |
| GET/POST | `/devices` | `includeInactive` on GET |
| GET | `/customers/{id}/devices` | |
| GET/PATCH | `/devices/{id}` | |
| POST | `/devices/{id}/deactivate` | Soft deactivate |
| POST | `/devices/{id}/activate` | |
| GET/POST | `/services` | `includeInactive` on GET |
| PATCH | `/services/{id}` | |
| POST | `/services/{id}/deactivate` | Soft deactivate |
| POST | `/services/{id}/activate` | |
| GET/POST | `/services/categories` | |
| GET/POST | `/repairs` | |
| GET | `/repairs/statuses` | Configurable definitions |
| GET | `/repairs/{id}` | Includes `allowedNextStatuses` |
| PATCH | `/repairs/{id}/status` | Workflow-gated; history + audit |
| PATCH | `/repairs/{id}/technician` | |
| POST | `/repairs/{id}/notes` | |
| GET | `/dashboard` | Real Phase 1 KPIs |
| GET | `/search?q=` | Repair #, customer, device |
| GET | `/audit-logs` | Read-only |
| GET | `/health` | |
