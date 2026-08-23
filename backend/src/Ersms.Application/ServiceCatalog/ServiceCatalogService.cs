using Ersms.Application.Common;
using Ersms.Domain.ServiceCatalog;
using Ersms.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.ServiceCatalog;

public sealed record ServiceDto(
    Guid Id,
    Guid? CategoryId,
    string? CategoryName,
    string Name,
    string? Description,
    decimal DefaultPrice,
    int WarrantyDays,
    bool IsActive);

public sealed record CreateServiceRequest(
    Guid? CategoryId,
    string Name,
    string? Description,
    decimal DefaultPrice,
    int WarrantyDays,
    bool IsActive = true);

public sealed class CreateServiceValidator : AbstractValidator<CreateServiceRequest>
{
    public CreateServiceValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DefaultPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.WarrantyDays).GreaterThanOrEqualTo(0);
    }
}

public interface IServiceCatalogService
{
    Task<Result<PagedResult<ServiceDto>>> ListAsync(PagedQuery query, CancellationToken ct = default);
    Task<Result<ServiceDto>> CreateAsync(CreateServiceRequest request, CancellationToken ct = default);
    Task<Result<ServiceDto>> UpdateAsync(Guid id, CreateServiceRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<(Guid Id, string Name)>>> ListCategoriesAsync(CancellationToken ct = default);
    Task<Result<(Guid Id, string Name)>> CreateCategoryAsync(string name, CancellationToken ct = default);
}

public sealed class ServiceCatalogService : IServiceCatalogService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IValidator<CreateServiceRequest> _validator;

    public ServiceCatalogService(IApplicationDbContext db, ICurrentUser user, IAuditService audit, IValidator<CreateServiceRequest> validator)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _validator = validator;
    }

    public async Task<Result<PagedResult<ServiceDto>>> ListAsync(PagedQuery query, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.ServicesRead);
        if (!auth.IsSuccess) return Result<PagedResult<ServiceDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var q = _db.Services.AsNoTracking().Include(s => s.Category).Where(s => s.OrganizationId == orgId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(x => x.Name.ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(x => x.Name).Skip(query.Skip).Take(query.Take)
            .Select(x => new ServiceDto(x.Id, x.CategoryId, x.Category != null ? x.Category.Name : null, x.Name, x.Description, x.DefaultPrice, x.WarrantyDays, x.IsActive))
            .ToListAsync(ct);

        return Result<PagedResult<ServiceDto>>.Success(new PagedResult<ServiceDto>
        {
            Items = items, Page = query.Page, PageSize = query.Take, TotalCount = total
        });
    }

    public async Task<Result<ServiceDto>> CreateAsync(CreateServiceRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.ServicesWrite);
        if (!auth.IsSuccess) return Result<ServiceDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<ServiceDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var entity = new ServiceItem
        {
            OrganizationId = _user.OrganizationId!.Value,
            CategoryId = request.CategoryId,
            Name = request.Name.Trim(),
            Description = request.Description,
            DefaultPrice = request.DefaultPrice,
            WarrantyDays = request.WarrantyDays,
            IsActive = request.IsActive
        };
        _db.Services.Add(entity);
        await _db.SaveChangesAsync(ct);
        var dto = new ServiceDto(entity.Id, entity.CategoryId, null, entity.Name, entity.Description, entity.DefaultPrice, entity.WarrantyDays, entity.IsActive);
        await _audit.WriteAsync("create", "Service", entity.Id.ToString(), null, dto, ct);
        return Result<ServiceDto>.Success(dto);
    }

    public async Task<Result<ServiceDto>> UpdateAsync(Guid id, CreateServiceRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.ServicesWrite);
        if (!auth.IsSuccess) return Result<ServiceDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var entity = await _db.Services.FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == _user.OrganizationId, ct);
        if (entity is null) return Result<ServiceDto>.Failure(ErrorCodes.NotFound, "Service not found.");

        var before = new ServiceDto(entity.Id, entity.CategoryId, null, entity.Name, entity.Description, entity.DefaultPrice, entity.WarrantyDays, entity.IsActive);
        entity.CategoryId = request.CategoryId;
        entity.Name = request.Name.Trim();
        entity.Description = request.Description;
        entity.DefaultPrice = request.DefaultPrice;
        entity.WarrantyDays = request.WarrantyDays;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        var after = new ServiceDto(entity.Id, entity.CategoryId, null, entity.Name, entity.Description, entity.DefaultPrice, entity.WarrantyDays, entity.IsActive);
        await _audit.WriteAsync("update", "Service", entity.Id.ToString(), before, after, ct);
        return Result<ServiceDto>.Success(after);
    }

    public async Task<Result<IReadOnlyList<(Guid Id, string Name)>>> ListCategoriesAsync(CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.ServicesRead);
        if (!auth.IsSuccess) return Result<IReadOnlyList<(Guid, string)>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var items = await _db.ServiceCategories.AsNoTracking()
            .Where(c => c.OrganizationId == _user.OrganizationId && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new ValueTuple<Guid, string>(c.Id, c.Name))
            .ToListAsync(ct);
        return Result<IReadOnlyList<(Guid, string)>>.Success(items);
    }

    public async Task<Result<(Guid Id, string Name)>> CreateCategoryAsync(string name, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.ServicesWrite);
        if (!auth.IsSuccess) return Result<(Guid, string)>.Failure(auth.ErrorCode!, auth.ErrorMessage!);
        if (string.IsNullOrWhiteSpace(name))
            return Result<(Guid, string)>.Failure(ErrorCodes.Validation, "Category name is required.");

        var entity = new ServiceCategory
        {
            OrganizationId = _user.OrganizationId!.Value,
            Name = name.Trim()
        };
        _db.ServiceCategories.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Result<(Guid, string)>.Success((entity.Id, entity.Name));
    }
}
