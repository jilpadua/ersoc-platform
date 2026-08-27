using Ersms.Application.Common;
using Ersms.Domain.Accounting;
using Ersms.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Accounting;

public sealed record ExpenseCategoryDto(Guid Id, string Name, Guid AccountId, string AccountCode, string AccountName, bool IsActive);

public sealed record ExpenseAttachmentDto(Guid Id, string StorageKey, string FileName, string ContentType, DateTimeOffset CreatedAt);

public sealed record ExpenseDto(
    Guid Id,
    Guid BranchId,
    Guid CategoryId,
    string CategoryName,
    decimal Amount,
    DateTimeOffset ExpenseDate,
    string? Payee,
    string? MethodCode,
    bool Payable,
    string Status,
    string? Notes,
    Guid CreatedByUserId,
    Guid? ApprovedByUserId,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? PostedAt,
    IReadOnlyList<ExpenseAttachmentDto> Attachments);

public sealed record CreateExpenseRequest(
    Guid CategoryId,
    decimal Amount,
    DateTimeOffset ExpenseDate,
    string? Payee,
    string? MethodCode,
    bool Payable,
    string? Notes,
    Guid? BranchId = null);

public sealed record ApproveExpenseRequest(string? Notes = null);

public sealed record CreateExpenseCategoryRequest(string Name, Guid AccountId);

public sealed class CreateExpenseValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.ExpenseDate).NotEmpty();
    }
}

public sealed class CreateExpenseCategoryValidator : AbstractValidator<CreateExpenseCategoryRequest>
{
    public CreateExpenseCategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountId).NotEmpty();
    }
}

public interface IExpenseService
{
    Task<Result<IReadOnlyList<ExpenseCategoryDto>>> ListCategoriesAsync(bool? activeOnly, CancellationToken ct = default);
    Task<Result<ExpenseCategoryDto>> CreateCategoryAsync(CreateExpenseCategoryRequest request, CancellationToken ct = default);
    Task<Result<PagedResult<ExpenseDto>>> ListExpensesAsync(PagedQuery query, string? status, CancellationToken ct = default);
    Task<Result<ExpenseDto>> GetExpenseAsync(Guid id, CancellationToken ct = default);
    Task<Result<ExpenseDto>> CreateAsync(CreateExpenseRequest request, CancellationToken ct = default);
    Task<Result<ExpenseDto>> ApproveAsync(Guid id, ApproveExpenseRequest request, CancellationToken ct = default);
    Task<Result<ExpenseDto>> PostAsync(Guid id, CancellationToken ct = default);
    Task<Result<ExpenseDto>> VoidAsync(Guid id, CancellationToken ct = default);
    Task<Result<ExpenseAttachmentDto>> AddAttachmentAsync(
        Guid expenseId, Stream content, string fileName, string contentType, CancellationToken ct = default);
}

public sealed class ExpenseService : IExpenseService
{
    public const string ExpenseVoidedSourceType = "ExpenseVoided";

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IAccountingPostingService _posting;
    private readonly IFileStorage _files;
    private readonly IValidator<CreateExpenseRequest> _createValidator;
    private readonly IValidator<CreateExpenseCategoryRequest> _categoryValidator;

    public ExpenseService(
        IApplicationDbContext db,
        ICurrentUser user,
        IAuditService audit,
        IAccountingPostingService posting,
        IFileStorage files,
        IValidator<CreateExpenseRequest> createValidator,
        IValidator<CreateExpenseCategoryRequest> categoryValidator)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _posting = posting;
        _files = files;
        _createValidator = createValidator;
        _categoryValidator = categoryValidator;
    }

    public async Task<Result<IReadOnlyList<ExpenseCategoryDto>>> ListCategoriesAsync(bool? activeOnly, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<IReadOnlyList<ExpenseCategoryDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var q =
            from c in _db.ExpenseCategories.AsNoTracking()
            join a in _db.Accounts.AsNoTracking() on c.AccountId equals a.Id
            where c.OrganizationId == orgId
            select new { c, a };
        if (activeOnly == true) q = q.Where(x => x.c.IsActive);

        var items = await q.OrderBy(x => x.c.Name)
            .Select(x => new ExpenseCategoryDto(x.c.Id, x.c.Name, x.c.AccountId, x.a.Code, x.a.Name, x.c.IsActive))
            .ToListAsync(ct);
        return Result<IReadOnlyList<ExpenseCategoryDto>>.Success(items);
    }

    public async Task<Result<ExpenseCategoryDto>> CreateCategoryAsync(CreateExpenseCategoryRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingWrite);
        if (!auth.IsSuccess) return Result<ExpenseCategoryDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _categoryValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<ExpenseCategoryDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var orgId = _user.OrganizationId!.Value;
        var account = await _db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && a.OrganizationId == orgId && a.IsActive, ct);
        if (account is null)
            return Result<ExpenseCategoryDto>.Failure(ErrorCodes.NotFound, "Account not found.");

        var entity = new ExpenseCategory
        {
            OrganizationId = orgId,
            Name = request.Name.Trim(),
            AccountId = request.AccountId,
            IsActive = true
        };
        _db.ExpenseCategories.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("create", "ExpenseCategory", entity.Id.ToString(), null,
            new { entity.Name, entity.AccountId }, ct);

        return Result<ExpenseCategoryDto>.Success(
            new ExpenseCategoryDto(entity.Id, entity.Name, entity.AccountId, account.Code, account.Name, entity.IsActive));
    }

    public async Task<Result<PagedResult<ExpenseDto>>> ListExpensesAsync(PagedQuery query, string? status, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<PagedResult<ExpenseDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var q = _db.Expenses.AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.Attachments)
            .Where(e => e.OrganizationId == orgId);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(e => e.Status == status);

        q = q.OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.CreatedAt);
        var total = await q.CountAsync(ct);
        var items = await q.Skip(query.Skip).Take(query.Take).ToListAsync(ct);
        return Result<PagedResult<ExpenseDto>>.Success(new PagedResult<ExpenseDto>
        {
            Items = items.Select(ToDto).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = total
        });
    }

    public async Task<Result<ExpenseDto>> GetExpenseAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<ExpenseDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var entity = await _db.Expenses.AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.Attachments)
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == orgId, ct);
        if (entity is null) return Result<ExpenseDto>.Failure(ErrorCodes.NotFound, "Expense not found.");
        return Result<ExpenseDto>.Success(ToDto(entity));
    }

    public async Task<Result<ExpenseDto>> CreateAsync(CreateExpenseRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingWrite);
        if (!auth.IsSuccess) return Result<ExpenseDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<ExpenseDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        if (!request.Payable && string.IsNullOrWhiteSpace(request.MethodCode))
            return Result<ExpenseDto>.Failure(ErrorCodes.Validation, "MethodCode is required when expense is not payable.");

        var orgId = _user.OrganizationId!.Value;
        var branchId = request.BranchId ?? _user.BranchId;
        if (branchId is null)
            return Result<ExpenseDto>.Failure(ErrorCodes.Validation, "Branch is required.");

        var category = await _db.ExpenseCategories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.OrganizationId == orgId && c.IsActive, ct);
        if (category is null)
            return Result<ExpenseDto>.Failure(ErrorCodes.NotFound, "Expense category not found.");

        var entity = new Expense
        {
            OrganizationId = orgId,
            BranchId = branchId.Value,
            CategoryId = category.Id,
            Amount = Math.Round(request.Amount, 2),
            ExpenseDate = request.ExpenseDate,
            Payee = request.Payee?.Trim(),
            MethodCode = request.MethodCode?.Trim(),
            Payable = request.Payable,
            Status = ExpenseStatuses.Draft,
            Notes = request.Notes?.Trim(),
            CreatedByUserId = _user.UserId!.Value
        };
        _db.Expenses.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("create", "Expense", entity.Id.ToString(), null, ToDto(entity), ct);

        entity.Category = category;
        return Result<ExpenseDto>.Success(ToDto(entity));
    }

    public async Task<Result<ExpenseDto>> ApproveAsync(Guid id, ApproveExpenseRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.RequireAny(_user, Permissions.AccountingApproveExpense, Permissions.AccountingPost);
        if (!auth.IsSuccess) return Result<ExpenseDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var entity = await _db.Expenses
            .Include(e => e.Category)
            .Include(e => e.Attachments)
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == orgId, ct);
        if (entity is null) return Result<ExpenseDto>.Failure(ErrorCodes.NotFound, "Expense not found.");
        if (entity.Status == ExpenseStatuses.Posted)
            return Result<ExpenseDto>.Success(ToDto(entity));
        if (entity.Status != ExpenseStatuses.Draft && entity.Status != ExpenseStatuses.Approved)
            return Result<ExpenseDto>.Failure(ErrorCodes.Conflict, $"Cannot approve expense in status {entity.Status}.");

        if (!string.IsNullOrWhiteSpace(request.Notes))
            entity.Notes = string.IsNullOrWhiteSpace(entity.Notes)
                ? request.Notes.Trim()
                : $"{entity.Notes}\n{request.Notes.Trim()}";

        entity.Status = ExpenseStatuses.Approved;
        entity.ApprovedByUserId = _user.UserId;
        entity.ApprovedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // MVP: approve posts the journal in the same flow and lands on Posted.
        return await PostInternalAsync(entity, ct);
    }

    public async Task<Result<ExpenseDto>> PostAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingPost);
        if (!auth.IsSuccess) return Result<ExpenseDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var entity = await _db.Expenses
            .Include(e => e.Category)
            .Include(e => e.Attachments)
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == orgId, ct);
        if (entity is null) return Result<ExpenseDto>.Failure(ErrorCodes.NotFound, "Expense not found.");
        if (entity.Status == ExpenseStatuses.Posted)
            return Result<ExpenseDto>.Success(ToDto(entity));
        if (entity.Status is not (ExpenseStatuses.Draft or ExpenseStatuses.Approved))
            return Result<ExpenseDto>.Failure(ErrorCodes.Conflict, $"Cannot post expense in status {entity.Status}.");

        return await PostInternalAsync(entity, ct);
    }

    public async Task<Result<ExpenseDto>> VoidAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.RequireAny(_user, Permissions.AccountingPost, Permissions.AccountingWrite);
        if (!auth.IsSuccess) return Result<ExpenseDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var entity = await _db.Expenses
            .Include(e => e.Category)
            .Include(e => e.Attachments)
            .FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == orgId, ct);
        if (entity is null) return Result<ExpenseDto>.Failure(ErrorCodes.NotFound, "Expense not found.");
        if (entity.Status == ExpenseStatuses.Voided)
            return Result<ExpenseDto>.Success(ToDto(entity));
        if (entity.Status == ExpenseStatuses.Draft)
        {
            entity.Status = ExpenseStatuses.Voided;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("void", "Expense", entity.Id.ToString(), null, new { entity.Status }, ct);
            return Result<ExpenseDto>.Success(ToDto(entity));
        }

        await using var tx = await _db.BeginTransactionAsync(ct);
        try
        {
            if (entity.Status == ExpenseStatuses.Posted)
            {
                var journal = await _db.JournalEntries.AsNoTracking()
                    .FirstOrDefaultAsync(j =>
                        j.OrganizationId == orgId
                        && j.SourceType == AccountingSourceTypes.ExpensePosted
                        && j.SourceId == entity.Id, ct);
                if (journal is not null)
                {
                    var reverse = await _posting.ReverseAsync(
                        journal.Id,
                        DateTimeOffset.UtcNow,
                        ExpenseVoidedSourceType,
                        entity.Id,
                        $"Void expense {entity.Id}",
                        ct);
                    if (!reverse.IsSuccess)
                    {
                        await tx.RollbackAsync(ct);
                        return Result<ExpenseDto>.Failure(reverse.ErrorCode!, reverse.ErrorMessage!);
                    }
                }
            }

            entity.Status = ExpenseStatuses.Voided;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await _audit.WriteAsync("void", "Expense", entity.Id.ToString(), null, new { entity.Status }, ct);
            return Result<ExpenseDto>.Success(ToDto(entity));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<ExpenseAttachmentDto>> AddAttachmentAsync(
        Guid expenseId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingWrite);
        if (!auth.IsSuccess) return Result<ExpenseAttachmentDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        if (string.IsNullOrWhiteSpace(fileName))
            return Result<ExpenseAttachmentDto>.Failure(ErrorCodes.Validation, "File name is required.");

        var orgId = _user.OrganizationId!.Value;
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId && e.OrganizationId == orgId, ct);
        if (expense is null) return Result<ExpenseAttachmentDto>.Failure(ErrorCodes.NotFound, "Expense not found.");
        if (expense.Status == ExpenseStatuses.Voided)
            return Result<ExpenseAttachmentDto>.Failure(ErrorCodes.Conflict, "Cannot attach files to a voided expense.");

        var (storageKey, _) = await _files.SaveAsync(content, fileName, contentType, ct);
        var attachment = new ExpenseAttachment
        {
            ExpenseId = expense.Id,
            StorageKey = storageKey,
            FileName = fileName.Trim(),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.ExpenseAttachments.Add(attachment);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("attach", "ExpenseAttachment", attachment.Id.ToString(), null,
            new { attachment.FileName, expense.Id }, ct);

        return Result<ExpenseAttachmentDto>.Success(
            new ExpenseAttachmentDto(attachment.Id, attachment.StorageKey, attachment.FileName, attachment.ContentType, attachment.CreatedAt));
    }

    private async Task<Result<ExpenseDto>> PostInternalAsync(Expense entity, CancellationToken ct)
    {
        if (entity.Category is null)
            entity.Category = await _db.ExpenseCategories.FirstAsync(c => c.Id == entity.CategoryId, ct);

        var maps = await AccountingLineBuilders.LoadMapsAsync(_db, entity.OrganizationId, ct);
        var lines = AccountingLineBuilders.ExpensePosted(
            maps, entity.Category.AccountId, entity.Amount, entity.MethodCode, entity.Payable);
        if (!lines.IsSuccess)
            return Result<ExpenseDto>.Failure(lines.ErrorCode!, lines.ErrorMessage!);

        await using var tx = await _db.BeginTransactionAsync(ct);
        try
        {
            var journal = await _posting.PostAsync(new PostJournalRequest(
                entity.OrganizationId,
                entity.BranchId,
                entity.ExpenseDate,
                AccountingSourceTypes.ExpensePosted,
                entity.Id,
                $"Expense {entity.Id}",
                lines.Value!), ct);
            if (!journal.IsSuccess)
            {
                await tx.RollbackAsync(ct);
                return Result<ExpenseDto>.Failure(journal.ErrorCode!, journal.ErrorMessage!);
            }

            entity.Status = ExpenseStatuses.Posted;
            entity.PostedAt = DateTimeOffset.UtcNow;
            entity.ApprovedByUserId ??= _user.UserId;
            entity.ApprovedAt ??= DateTimeOffset.UtcNow;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await _audit.WriteAsync("post", "Expense", entity.Id.ToString(), null, new { entity.Status, entity.Amount }, ct);
            return Result<ExpenseDto>.Success(ToDto(entity));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static ExpenseDto ToDto(Expense e) =>
        new(
            e.Id,
            e.BranchId,
            e.CategoryId,
            e.Category?.Name ?? "",
            e.Amount,
            e.ExpenseDate,
            e.Payee,
            e.MethodCode,
            e.Payable,
            e.Status,
            e.Notes,
            e.CreatedByUserId,
            e.ApprovedByUserId,
            e.ApprovedAt,
            e.PostedAt,
            e.Attachments.Select(a => new ExpenseAttachmentDto(a.Id, a.StorageKey, a.FileName, a.ContentType, a.CreatedAt)).ToList());
}
