using Ersms.Domain.Accounting;
using Ersms.SharedKernel;

namespace Ersms.Application.Accounting;

public sealed record JournalLineInput(Guid AccountId, decimal Debit, decimal Credit, string? Description = null);

public sealed record PostJournalRequest(
    Guid OrganizationId,
    Guid? BranchId,
    DateTimeOffset EntryDate,
    string SourceType,
    Guid SourceId,
    string? Memo,
    IReadOnlyList<JournalLineInput> Lines,
    Guid? ReversesJournalEntryId = null);

public sealed record JournalLineDto(Guid Id, Guid AccountId, string AccountCode, string AccountName, decimal Debit, decimal Credit, string? Description);

public sealed record JournalEntryDto(
    Guid Id,
    string EntryNumber,
    Guid PeriodId,
    string PeriodName,
    DateTimeOffset EntryDate,
    DateTimeOffset PostedAt,
    Guid PostedByUserId,
    string? Memo,
    string Status,
    string SourceType,
    Guid SourceId,
    Guid? ReversesJournalEntryId,
    IReadOnlyList<JournalLineDto> Lines);

public sealed record AccountDto(
    Guid Id,
    string Code,
    string Name,
    string AccountType,
    string NormalBalance,
    Guid? ParentAccountId,
    bool IsSystem,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateAccountRequest(string Code, string Name, string AccountType, string NormalBalance, Guid? ParentAccountId = null);
public sealed record UpdateAccountRequest(string Name, bool IsActive);

public sealed record AccountingPeriodDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, string Status);
public sealed record GeneratePeriodsRequest(int Year);
public sealed record MappingDto(string MappingKey, Guid AccountId, string AccountCode, string AccountName);
public sealed record UpsertMappingRequest(string MappingKey, Guid AccountId);

public sealed record ManualJournalRequest(
    DateTimeOffset EntryDate,
    Guid? BranchId,
    string? Memo,
    IReadOnlyList<JournalLineInput> Lines);

public sealed record OpeningBalanceRequest(
    DateTimeOffset EntryDate,
    Guid? BranchId,
    string? Memo,
    IReadOnlyList<JournalLineInput> Lines);

public interface IAccountingPostingService
{
    Task<Result<JournalEntryDto>> PostAsync(PostJournalRequest request, CancellationToken ct = default);
    Task<Result<JournalEntryDto>> ReverseAsync(Guid journalEntryId, DateTimeOffset entryDate, string sourceType, Guid sourceId, string? memo, CancellationToken ct = default);
}

public interface IAccountService
{
    Task<Result<IReadOnlyList<AccountDto>>> ListAsync(bool? activeOnly, CancellationToken ct = default);
    Task<Result<AccountDto>> CreateAsync(CreateAccountRequest request, CancellationToken ct = default);
    Task<Result<AccountDto>> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<MappingDto>>> ListMappingsAsync(CancellationToken ct = default);
    Task<Result<MappingDto>> UpsertMappingAsync(UpsertMappingRequest request, CancellationToken ct = default);
}

public interface IAccountingPeriodService
{
    Task<Result<IReadOnlyList<AccountingPeriodDto>>> ListAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<AccountingPeriodDto>>> GenerateYearAsync(GeneratePeriodsRequest request, CancellationToken ct = default);
    Task<Result<AccountingPeriodDto>> CloseAsync(Guid id, CancellationToken ct = default);
    Task<Result<AccountingPeriodDto>> ReopenAsync(Guid id, CancellationToken ct = default);
}

public interface IJournalQueryService
{
    Task<Result<PagedResult<JournalEntryDto>>> ListAsync(PagedQuery query, string? sourceType, CancellationToken ct = default);
    Task<Result<JournalEntryDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<JournalEntryDto>> GetBySourceAsync(string sourceType, Guid sourceId, CancellationToken ct = default);
    Task<Result<JournalEntryDto>> PostManualAsync(ManualJournalRequest request, CancellationToken ct = default);
    Task<Result<JournalEntryDto>> PostOpeningBalancesAsync(OpeningBalanceRequest request, CancellationToken ct = default);
}

public static class PaymentMethodAccounts
{
    public static string MappingKeyFor(string methodCode) => methodCode.ToUpperInvariant() switch
    {
        "CASH" => MappingKeys.Cash,
        "CARD" => MappingKeys.CardClearing,
        "TRANSFER" => MappingKeys.Bank,
        _ => MappingKeys.Cash
    };
}
