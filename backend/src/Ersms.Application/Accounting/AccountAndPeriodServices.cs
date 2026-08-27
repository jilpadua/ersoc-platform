using Ersms.Application.Common;
using Ersms.Domain.Accounting;
using Ersms.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Accounting;

public sealed class CreateAccountValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountType).NotEmpty();
        RuleFor(x => x.NormalBalance).Must(x => x is NormalBalances.Debit or NormalBalances.Credit);
    }
}

public sealed class AccountService : IAccountService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IValidator<CreateAccountRequest> _validator;

    public AccountService(IApplicationDbContext db, ICurrentUser user, IAuditService audit, IValidator<CreateAccountRequest> validator)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _validator = validator;
    }

    public async Task<Result<IReadOnlyList<AccountDto>>> ListAsync(bool? activeOnly, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<IReadOnlyList<AccountDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var q = _db.Accounts.AsNoTracking().Where(a => a.OrganizationId == orgId);
        if (activeOnly == true) q = q.Where(a => a.IsActive);
        var items = await q.OrderBy(a => a.Code).ToListAsync(ct);
        return Result<IReadOnlyList<AccountDto>>.Success(items.Select(ToDto).ToList());
    }

    public async Task<Result<AccountDto>> CreateAsync(CreateAccountRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingWrite);
        if (!auth.IsSuccess) return Result<AccountDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<AccountDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var orgId = _user.OrganizationId!.Value;
        var exists = await _db.Accounts.AnyAsync(a => a.OrganizationId == orgId && a.Code == request.Code.Trim(), ct);
        if (exists) return Result<AccountDto>.Failure(ErrorCodes.Conflict, "Account code already exists.");

        var entity = new Account
        {
            OrganizationId = orgId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            AccountType = request.AccountType.Trim(),
            NormalBalance = request.NormalBalance,
            ParentAccountId = request.ParentAccountId,
            IsSystem = false,
            IsActive = true
        };
        _db.Accounts.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("create", "Account", entity.Id.ToString(), null, ToDto(entity), ct);
        return Result<AccountDto>.Success(ToDto(entity));
    }

    public async Task<Result<AccountDto>> UpdateAsync(Guid id, UpdateAccountRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingWrite);
        if (!auth.IsSuccess) return Result<AccountDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var entity = await _db.Accounts.FirstOrDefaultAsync(a => a.Id == id && a.OrganizationId == orgId, ct);
        if (entity is null) return Result<AccountDto>.Failure(ErrorCodes.NotFound, "Account not found.");

        if (!request.IsActive)
        {
            var mapped = await _db.AccountingAccountMappings.AnyAsync(m => m.OrganizationId == orgId && m.AccountId == id, ct);
            if (mapped)
                return Result<AccountDto>.Failure(ErrorCodes.Conflict, "Cannot deactivate an account used by an active mapping.");
        }

        var before = ToDto(entity);
        entity.Name = request.Name.Trim();
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("update", "Account", entity.Id.ToString(), before, ToDto(entity), ct);
        return Result<AccountDto>.Success(ToDto(entity));
    }

    public async Task<Result<IReadOnlyList<MappingDto>>> ListMappingsAsync(CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<IReadOnlyList<MappingDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var items = await (
            from m in _db.AccountingAccountMappings.AsNoTracking()
            join a in _db.Accounts.AsNoTracking() on m.AccountId equals a.Id
            where m.OrganizationId == orgId
            orderby m.MappingKey
            select new MappingDto(m.MappingKey, m.AccountId, a.Code, a.Name)
        ).ToListAsync(ct);
        return Result<IReadOnlyList<MappingDto>>.Success(items);
    }

    public async Task<Result<MappingDto>> UpsertMappingAsync(UpsertMappingRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingWrite);
        if (!auth.IsSuccess) return Result<MappingDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var account = await _db.Accounts.FirstOrDefaultAsync(a =>
            a.Id == request.AccountId && a.OrganizationId == orgId && a.IsActive, ct);
        if (account is null) return Result<MappingDto>.Failure(ErrorCodes.NotFound, "Account not found.");

        var entity = await _db.AccountingAccountMappings
            .FirstOrDefaultAsync(m => m.OrganizationId == orgId && m.MappingKey == request.MappingKey, ct);
        if (entity is null)
        {
            entity = new AccountingAccountMapping
            {
                OrganizationId = orgId,
                MappingKey = request.MappingKey.Trim(),
                AccountId = request.AccountId
            };
            _db.AccountingAccountMappings.Add(entity);
        }
        else
        {
            entity.AccountId = request.AccountId;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("upsert", "AccountingAccountMapping", entity.Id.ToString(), null,
            new { entity.MappingKey, entity.AccountId }, ct);
        return Result<MappingDto>.Success(new MappingDto(entity.MappingKey, account.Id, account.Code, account.Name));
    }

    private static AccountDto ToDto(Account a) =>
        new(a.Id, a.Code, a.Name, a.AccountType, a.NormalBalance, a.ParentAccountId, a.IsSystem, a.IsActive, a.CreatedAt);
}

public sealed class AccountingPeriodService : IAccountingPeriodService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;

    public AccountingPeriodService(IApplicationDbContext db, ICurrentUser user, IAuditService audit)
    {
        _db = db;
        _user = user;
        _audit = audit;
    }

    public async Task<Result<IReadOnlyList<AccountingPeriodDto>>> ListAsync(CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<IReadOnlyList<AccountingPeriodDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var items = await _db.AccountingPeriods.AsNoTracking()
            .Where(p => p.OrganizationId == orgId)
            .OrderByDescending(p => p.StartDate)
            .Select(p => new AccountingPeriodDto(p.Id, p.Name, p.StartDate, p.EndDate, p.Status))
            .ToListAsync(ct);
        return Result<IReadOnlyList<AccountingPeriodDto>>.Success(items);
    }

    public async Task<Result<IReadOnlyList<AccountingPeriodDto>>> GenerateYearAsync(GeneratePeriodsRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingPeriods);
        if (!auth.IsSuccess) return Result<IReadOnlyList<AccountingPeriodDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        if (request.Year < 2000 || request.Year > 2100)
            return Result<IReadOnlyList<AccountingPeriodDto>>.Failure(ErrorCodes.Validation, "Invalid year.");

        var orgId = _user.OrganizationId!.Value;
        for (var month = 1; month <= 12; month++)
        {
            var start = new DateOnly(request.Year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            var exists = await _db.AccountingPeriods.AnyAsync(p =>
                p.OrganizationId == orgId && p.StartDate == start && p.EndDate == end, ct);
            if (exists) continue;
            _db.AccountingPeriods.Add(new AccountingPeriod
            {
                OrganizationId = orgId,
                Name = $"{request.Year}-{month:D2}",
                StartDate = start,
                EndDate = end,
                Status = PeriodStatuses.Open
            });
        }
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("generate", "AccountingPeriod", orgId.ToString(), null, new { request.Year }, ct);
        return await ListAsync(ct);
    }

    public async Task<Result<AccountingPeriodDto>> CloseAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingPeriods);
        if (!auth.IsSuccess) return Result<AccountingPeriodDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId, ct);
        if (period is null) return Result<AccountingPeriodDto>.Failure(ErrorCodes.NotFound, "Period not found.");
        period.Status = PeriodStatuses.Closed;
        period.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("close", "AccountingPeriod", period.Id.ToString(), null, new { period.Name }, ct);
        return Result<AccountingPeriodDto>.Success(new AccountingPeriodDto(period.Id, period.Name, period.StartDate, period.EndDate, period.Status));
    }

    public async Task<Result<AccountingPeriodDto>> ReopenAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingPeriods);
        if (!auth.IsSuccess) return Result<AccountingPeriodDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var period = await _db.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == orgId, ct);
        if (period is null) return Result<AccountingPeriodDto>.Failure(ErrorCodes.NotFound, "Period not found.");
        period.Status = PeriodStatuses.Open;
        period.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("reopen", "AccountingPeriod", period.Id.ToString(), null, new { period.Name }, ct);
        return Result<AccountingPeriodDto>.Success(new AccountingPeriodDto(period.Id, period.Name, period.StartDate, period.EndDate, period.Status));
    }
}

public sealed class JournalQueryService : IJournalQueryService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAccountingPostingService _posting;

    public JournalQueryService(IApplicationDbContext db, ICurrentUser user, IAccountingPostingService posting)
    {
        _db = db;
        _user = user;
        _posting = posting;
    }

    public async Task<Result<PagedResult<JournalEntryDto>>> ListAsync(PagedQuery query, string? sourceType, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<PagedResult<JournalEntryDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var q = _db.JournalEntries.AsNoTracking().Where(j => j.OrganizationId == orgId);
        if (!string.IsNullOrWhiteSpace(sourceType))
            q = q.Where(j => j.SourceType == sourceType);
        q = q.OrderByDescending(j => j.EntryDate).ThenByDescending(j => j.EntryNumber);
        var total = await q.CountAsync(ct);
        var ids = await q.Skip(query.Skip).Take(query.Take).Select(j => j.Id).ToListAsync(ct);
        var items = new List<JournalEntryDto>();
        foreach (var id in ids)
        {
            var one = await GetAsync(id, ct);
            if (one.IsSuccess && one.Value is not null) items.Add(one.Value);
        }
        return Result<PagedResult<JournalEntryDto>>.Success(new PagedResult<JournalEntryDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        });
    }

    public async Task<Result<JournalEntryDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<JournalEntryDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var entry = await _db.JournalEntries.AsNoTracking()
            .Include(j => j.Lines)
            .Include(j => j.Period)
            .FirstOrDefaultAsync(j => j.Id == id && j.OrganizationId == orgId, ct);
        if (entry is null) return Result<JournalEntryDto>.Failure(ErrorCodes.NotFound, "Journal not found.");
        return Result<JournalEntryDto>.Success(await MapAsync(entry, ct));
    }

    public async Task<Result<JournalEntryDto>> GetBySourceAsync(string sourceType, Guid sourceId, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<JournalEntryDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var entry = await _db.JournalEntries.AsNoTracking()
            .Include(j => j.Lines)
            .Include(j => j.Period)
            .FirstOrDefaultAsync(j => j.OrganizationId == orgId && j.SourceType == sourceType && j.SourceId == sourceId, ct);
        if (entry is null) return Result<JournalEntryDto>.Failure(ErrorCodes.NotFound, "Journal not found.");
        return Result<JournalEntryDto>.Success(await MapAsync(entry, ct));
    }

    public async Task<Result<JournalEntryDto>> PostManualAsync(ManualJournalRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingPost);
        if (!auth.IsSuccess) return Result<JournalEntryDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        return await _posting.PostAsync(new PostJournalRequest(
            _user.OrganizationId!.Value,
            request.BranchId ?? _user.BranchId,
            request.EntryDate,
            AccountingSourceTypes.ManualJournal,
            Guid.NewGuid(),
            request.Memo,
            request.Lines), ct);
    }

    public async Task<Result<JournalEntryDto>> PostOpeningBalancesAsync(OpeningBalanceRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingPost);
        if (!auth.IsSuccess) return Result<JournalEntryDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        return await _posting.PostAsync(new PostJournalRequest(
            _user.OrganizationId!.Value,
            request.BranchId ?? _user.BranchId,
            request.EntryDate,
            AccountingSourceTypes.OpeningBalance,
            Guid.NewGuid(),
            request.Memo ?? "Opening balances",
            request.Lines), ct);
    }

    private async Task<JournalEntryDto> MapAsync(JournalEntry entry, CancellationToken ct)
    {
        var accountIds = entry.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _db.Accounts.AsNoTracking().Where(a => accountIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        return new JournalEntryDto(
            entry.Id, entry.EntryNumber, entry.PeriodId, entry.Period?.Name ?? "",
            entry.EntryDate, entry.PostedAt, entry.PostedByUserId, entry.Memo, entry.Status,
            entry.SourceType, entry.SourceId, entry.ReversesJournalEntryId,
            entry.Lines.Select(l =>
            {
                accounts.TryGetValue(l.AccountId, out var a);
                return new JournalLineDto(l.Id, l.AccountId, a?.Code ?? "", a?.Name ?? "", l.Debit, l.Credit, l.Description);
            }).ToList());
    }
}
