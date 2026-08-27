using Ersms.Application.Common;
using Ersms.Domain.Accounting;
using Ersms.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Accounting;

public sealed class AccountingPostingService : IAccountingPostingService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;

    public AccountingPostingService(IApplicationDbContext db, ICurrentUser user, IAuditService audit)
    {
        _db = db;
        _user = user;
        _audit = audit;
    }

    public async Task<Result<JournalEntryDto>> PostAsync(PostJournalRequest request, CancellationToken ct = default)
    {
        if (request.OrganizationId == Guid.Empty)
            return Result<JournalEntryDto>.Failure(ErrorCodes.Validation, "Organization is required.");
        if (string.IsNullOrWhiteSpace(request.SourceType))
            return Result<JournalEntryDto>.Failure(ErrorCodes.Validation, "SourceType is required.");
        if (request.SourceId == Guid.Empty)
            return Result<JournalEntryDto>.Failure(ErrorCodes.Validation, "SourceId is required.");

        var existing = await _db.JournalEntries.AsNoTracking()
            .Include(j => j.Lines)
            .Include(j => j.Period)
            .FirstOrDefaultAsync(j =>
                j.OrganizationId == request.OrganizationId
                && j.SourceType == request.SourceType
                && j.SourceId == request.SourceId, ct);
        if (existing is not null)
            return Result<JournalEntryDto>.Success(await ToDtoAsync(existing, ct));

        var balance = JournalMath.ValidateBalanced(request.Lines.Select(l => (l.Debit, l.Credit)));
        if (!balance.IsSuccess)
            return Result<JournalEntryDto>.Failure(balance.ErrorCode!, balance.ErrorMessage!);

        var entryDate = request.EntryDate;
        var dateOnly = DateOnly.FromDateTime(entryDate.UtcDateTime);
        var period = await _db.AccountingPeriods
            .FirstOrDefaultAsync(p =>
                p.OrganizationId == request.OrganizationId
                && p.StartDate <= dateOnly
                && p.EndDate >= dateOnly, ct);
        if (period is null)
            return Result<JournalEntryDto>.Failure(ErrorCodes.Conflict, $"No accounting period covers {dateOnly:yyyy-MM-dd}.");
        if (period.Status != PeriodStatuses.Open)
            return Result<JournalEntryDto>.Failure(ErrorCodes.Conflict, $"Accounting period {period.Name} is closed.");

        var accountIds = request.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _db.Accounts
            .Where(a => a.OrganizationId == request.OrganizationId && accountIds.Contains(a.Id))
            .ToListAsync(ct);
        if (accounts.Count != accountIds.Count)
            return Result<JournalEntryDto>.Failure(ErrorCodes.Validation, "One or more accounts are invalid.");
        if (accounts.Any(a => !a.IsActive))
            return Result<JournalEntryDto>.Failure(ErrorCodes.Validation, "Cannot post to inactive accounts.");

        var postedBy = _user.UserId ?? Guid.Empty;
        var entry = new JournalEntry
        {
            OrganizationId = request.OrganizationId,
            BranchId = request.BranchId,
            PeriodId = period.Id,
            EntryNumber = await NextEntryNumberAsync(request.OrganizationId, ct),
            EntryDate = entryDate,
            PostedAt = DateTimeOffset.UtcNow,
            PostedByUserId = postedBy,
            Memo = request.Memo?.Trim(),
            Status = JournalStatuses.Posted,
            SourceType = request.SourceType,
            SourceId = request.SourceId,
            ReversesJournalEntryId = request.ReversesJournalEntryId
        };

        foreach (var line in request.Lines)
        {
            entry.Lines.Add(new JournalLine
            {
                AccountId = line.AccountId,
                Debit = Math.Round(line.Debit, 2),
                Credit = Math.Round(line.Credit, 2),
                Description = line.Description
            });
        }

        _db.JournalEntries.Add(entry);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var raced = await _db.JournalEntries.AsNoTracking()
                .Include(j => j.Lines)
                .Include(j => j.Period)
                .FirstOrDefaultAsync(j =>
                    j.OrganizationId == request.OrganizationId
                    && j.SourceType == request.SourceType
                    && j.SourceId == request.SourceId, ct);
            if (raced is not null)
                return Result<JournalEntryDto>.Success(await ToDtoAsync(raced, ct));
            throw;
        }

        await _audit.WriteAsync("post", "JournalEntry", entry.Id.ToString(), null,
            new { entry.EntryNumber, entry.SourceType, entry.SourceId }, ct);

        entry.Period = period;
        return Result<JournalEntryDto>.Success(await ToDtoAsync(entry, ct));
    }

    public async Task<Result<JournalEntryDto>> ReverseAsync(
        Guid journalEntryId,
        DateTimeOffset entryDate,
        string sourceType,
        Guid sourceId,
        string? memo,
        CancellationToken ct = default)
    {
        var original = await _db.JournalEntries
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Id == journalEntryId, ct);
        if (original is null)
            return Result<JournalEntryDto>.Failure(ErrorCodes.NotFound, "Journal entry not found.");

        var reversed = JournalMath.ReverseLines(original.Lines.Select(l =>
            (l.AccountId, l.Debit, l.Credit, l.Description)));

        return await PostAsync(new PostJournalRequest(
            original.OrganizationId,
            original.BranchId,
            entryDate,
            sourceType,
            sourceId,
            memo ?? $"Reversal of {original.EntryNumber}",
            reversed.Select(l => new JournalLineInput(l.AccountId, l.Debit, l.Credit, l.Description)).ToList(),
            original.Id), ct);
    }

    private async Task<string> NextEntryNumberAsync(Guid orgId, CancellationToken ct)
    {
        const string prefix = "JE-";
        var last = await _db.JournalEntries.AsNoTracking()
            .Where(j => j.OrganizationId == orgId && j.EntryNumber.StartsWith(prefix))
            .OrderByDescending(j => j.EntryNumber)
            .Select(j => j.EntryNumber)
            .FirstOrDefaultAsync(ct);
        var next = 1;
        if (last is not null && int.TryParse(last.AsSpan(prefix.Length), out var n))
            next = n + 1;
        return $"{prefix}{next:D6}";
    }

    private async Task<JournalEntryDto> ToDtoAsync(JournalEntry entry, CancellationToken ct)
    {
        var accountIds = entry.Lines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);
        var periodName = entry.Period?.Name
            ?? await _db.AccountingPeriods.AsNoTracking()
                .Where(p => p.Id == entry.PeriodId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(ct)
            ?? "";

        return new JournalEntryDto(
            entry.Id,
            entry.EntryNumber,
            entry.PeriodId,
            periodName,
            entry.EntryDate,
            entry.PostedAt,
            entry.PostedByUserId,
            entry.Memo,
            entry.Status,
            entry.SourceType,
            entry.SourceId,
            entry.ReversesJournalEntryId,
            entry.Lines.Select(l =>
            {
                accounts.TryGetValue(l.AccountId, out var a);
                return new JournalLineDto(l.Id, l.AccountId, a?.Code ?? "", a?.Name ?? "", l.Debit, l.Credit, l.Description);
            }).ToList());
    }
}
