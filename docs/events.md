# Events (draft)

Phase 5 will own the outbox and notification handlers. Phase 4 GL posting remains synchronous inside business transactions.

## Planned events

| Event | When | Suggested payload |
| ----- | ---- | ----------------- |
| `JournalEntryPosted` | After journal insert | `OrganizationId`, `JournalEntryId`, `SourceType`, `SourceId`, `EntryNumber` |
| `ExpenseApproved` | Expense approved/posted | `OrganizationId`, `ExpenseId`, `Amount` |
| `SupplierPaymentRecorded` | AP payment saved | `OrganizationId`, `SupplierPaymentId`, `SupplierId`, `Amount` |
| `AccountingPeriodClosed` | Period closed | `OrganizationId`, `PeriodId`, `Name` |

Handlers must not mutate posted journals.
