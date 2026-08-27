# Modules

| Module | Project area | Responsibility |
|--------|--------------|----------------|
| Identity | Domain/Identity, Infrastructure Auth | Organizations, branches, users, roles, permissions |
| Customers | Domain/Customers, Application/Customers | Customer profiles |
| Devices | Domain/Devices, Application/Devices | Device records |
| ServiceCatalog | Domain/ServiceCatalog, Application/ServiceCatalog | Categories and billable services |
| Repairs | Domain/Repairs, Application/Repairs | Tickets, workflow, notes, service lines |
| Inventory | Domain/Inventory, Application/Inventory | Parts catalog, stock ledger, adjustments |
| Purchasing | Domain/Purchasing, Application/Purchasing | Suppliers, purchase orders, receive batches (`PurchaseReceive`), receiving |
| Sales | Domain/Sales, Application/Sales | POS sales, payments, invoices, returns (void XOR return); posts GL journals |
| Accounting | Domain/Accounting, Application/Accounting | CoA, periods, journals, mappings, AP bills/payments, expenses, reports, reconciliation |
| Audit | Domain/Audit, Application/Audit | Append-only audit query |
| Dashboard | Application/Dashboard | Operational + cash/expense KPIs |
| Search | Application/Search | Global search |

Future modules (not implemented): Notifications/outbox (Phase 5), multi-branch transfers, repair billing checkout.
