using Ersms.Application.Common;
using Ersms.Domain.Purchasing;
using Ersms.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Purchasing;

public sealed record SupplierDto(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? ContactName,
    string? Notes,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateSupplierRequest(
    string Name,
    string? Email,
    string? Phone,
    string? ContactName,
    string? Notes,
    bool? IsActive = null);

public sealed class CreateSupplierValidator : AbstractValidator<CreateSupplierRequest>
{
    public CreateSupplierValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
    }
}

public interface ISupplierService
{
    Task<Result<PagedResult<SupplierDto>>> ListAsync(PagedQuery query, bool includeInactive = false, CancellationToken ct = default);
    Task<Result<SupplierDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<SupplierDto>> CreateAsync(CreateSupplierRequest request, CancellationToken ct = default);
    Task<Result<SupplierDto>> UpdateAsync(Guid id, CreateSupplierRequest request, CancellationToken ct = default);
    Task<Result<SupplierDto>> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
}

public sealed class SupplierService : ISupplierService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IValidator<CreateSupplierRequest> _validator;

    public SupplierService(IApplicationDbContext db, ICurrentUser user, IAuditService audit, IValidator<CreateSupplierRequest> validator)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _validator = validator;
    }

    public async Task<Result<PagedResult<SupplierDto>>> ListAsync(PagedQuery query, bool includeInactive = false, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.PurchasingRead);
        if (!auth.IsSuccess) return Result<PagedResult<SupplierDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var q = _db.Suppliers.AsNoTracking().Where(s => s.OrganizationId == orgId);
        if (!includeInactive) q = q.Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(x =>
                x.Name.ToLower().Contains(s) ||
                (x.Phone != null && x.Phone.ToLower().Contains(s)) ||
                (x.Email != null && x.Email.ToLower().Contains(s)));
        }

        q = query.SortDesc ? q.OrderByDescending(x => x.Name) : q.OrderBy(x => x.Name);
        var total = await q.CountAsync(ct);
        var items = await q.Skip(query.Skip).Take(query.Take).Select(Map).ToListAsync(ct);
        return Result<PagedResult<SupplierDto>>.Success(new PagedResult<SupplierDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.Take,
            TotalCount = total
        });
    }

    public async Task<Result<SupplierDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.PurchasingRead);
        if (!auth.IsSuccess) return Result<SupplierDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var entity = await _db.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == _user.OrganizationId, ct);
        if (entity is null) return Result<SupplierDto>.Failure(ErrorCodes.NotFound, "Supplier not found.");
        return Result<SupplierDto>.Success(ToDto(entity));
    }

    public async Task<Result<SupplierDto>> CreateAsync(CreateSupplierRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.PurchasingWrite);
        if (!auth.IsSuccess) return Result<SupplierDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<SupplierDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var entity = new Supplier
        {
            OrganizationId = _user.OrganizationId!.Value,
            Name = request.Name.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            ContactName = request.ContactName?.Trim(),
            Notes = request.Notes,
            IsActive = request.IsActive ?? true
        };
        _db.Suppliers.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("create", "Supplier", entity.Id.ToString(), null, ToDto(entity), ct);
        return Result<SupplierDto>.Success(ToDto(entity));
    }

    public async Task<Result<SupplierDto>> UpdateAsync(Guid id, CreateSupplierRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.PurchasingWrite);
        if (!auth.IsSuccess) return Result<SupplierDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<SupplierDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var entity = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == _user.OrganizationId, ct);
        if (entity is null) return Result<SupplierDto>.Failure(ErrorCodes.NotFound, "Supplier not found.");

        var before = ToDto(entity);
        entity.Name = request.Name.Trim();
        entity.Email = request.Email?.Trim();
        entity.Phone = request.Phone?.Trim();
        entity.ContactName = request.ContactName?.Trim();
        entity.Notes = request.Notes;
        if (request.IsActive.HasValue) entity.IsActive = request.IsActive.Value;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("update", "Supplier", entity.Id.ToString(), before, ToDto(entity), ct);
        return Result<SupplierDto>.Success(ToDto(entity));
    }

    public async Task<Result<SupplierDto>> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.PurchasingWrite);
        if (!auth.IsSuccess) return Result<SupplierDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var entity = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == _user.OrganizationId, ct);
        if (entity is null) return Result<SupplierDto>.Failure(ErrorCodes.NotFound, "Supplier not found.");

        var before = ToDto(entity);
        entity.IsActive = isActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(isActive ? "activate" : "deactivate", "Supplier", entity.Id.ToString(), before, ToDto(entity), ct);
        return Result<SupplierDto>.Success(ToDto(entity));
    }

    private static System.Linq.Expressions.Expression<Func<Supplier, SupplierDto>> Map =>
        s => new SupplierDto(s.Id, s.Name, s.Email, s.Phone, s.ContactName, s.Notes, s.IsActive, s.CreatedAt);

    private static SupplierDto ToDto(Supplier s) =>
        new(s.Id, s.Name, s.Email, s.Phone, s.ContactName, s.Notes, s.IsActive, s.CreatedAt);
}
