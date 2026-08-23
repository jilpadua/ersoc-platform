# API conventions (Phase 1)

Base path: `/api/v1/`

Auth: HTTP-only cookie `ersms_auth` after `POST /api/v1/auth/login`.

Errors:

```json
{ "error": { "code": "not_found", "message": "…" } }
```

Stable codes include: `validation_error`, `not_found`, `unauthorized`, `forbidden`, `conflict`, `invalid_transition`, `invalid_credentials`.

List endpoints accept `page`, `pageSize`, `search`, `sortBy`, `sortDesc`.

## Endpoints

| Method | Path | Notes |
|--------|------|-------|
| POST | `/auth/login` | Rate-limited |
| POST | `/auth/logout` | |
| GET | `/auth/me` | |
| GET/POST | `/customers` | |
| GET/PATCH | `/customers/{id}` | |
| GET/POST | `/devices` | |
| GET | `/customers/{id}/devices` | |
| GET/PATCH | `/devices/{id}` | |
| GET/POST | `/services` | |
| GET/POST | `/services/categories` | |
| GET/POST | `/repairs` | |
| GET | `/repairs/statuses` | Configurable |
| GET | `/repairs/{id}` | |
| PATCH | `/repairs/{id}/status` | Records history + audit |
| PATCH | `/repairs/{id}/technician` | |
| POST | `/repairs/{id}/notes` | |
| GET | `/dashboard` | Real Phase 1 KPIs |
| GET | `/search?q=` | Repair #, customer, device |
| GET | `/audit-logs` | Read-only |
| GET | `/health` | |
