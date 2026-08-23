# Modules (Phase 1)

| Module | Project area | Responsibility |
|--------|--------------|----------------|
| SharedKernel | `Ersms.SharedKernel` | Result, paging, permissions, current user |
| Identity | Domain + Infrastructure Identity / seed | Org, branch, users, roles, permissions, cookies |
| Customers | `Application/Customers` | Customer profiles |
| Devices | `Application/Devices` | Device records |
| ServiceCatalog | `Application/ServiceCatalog` | Categories and priced services |
| Repairs | `Application/Repairs` + `Domain/Repairs` | Tickets, configurable statuses, transitions |
| Audit | `Application/Audit` + `Infrastructure/Audit` | Append-only audit trail |
| Dashboard | `Application/Dashboard` | Operational KPIs |
| Search | `Application/Search` | Indexed DB search |
| Infrastructure | `Ersms.Infrastructure` | EF Core, file storage, seeding |
| API | `Ersms.Api` | Controllers `/api/v1` |

Future modules (not implemented): Inventory, Purchasing, Sales, Accounting, Notifications, Reporting expansions.
