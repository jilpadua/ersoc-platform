using Ersms.Application.Common;
using Ersms.Domain.Customers;
using Ersms.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Customers;

public sealed record CustomerDto(
    Guid Id,
    string Name,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Province,
    string? PostalCode,
    string? Notes,
    DateTimeOffset CreatedAt);

public sealed record CreateCustomerRequest(
    string Name,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Province,
    string? PostalCode,
    string? Notes);

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
    }
}

public interface ICustomerService
{
    Task<Result<PagedResult<CustomerDto>>> ListAsync(PagedQuery query, CancellationToken ct = default);
    Task<Result<CustomerDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<CustomerDto>> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);
    Task<Result<CustomerDto>> UpdateAsync(Guid id, CreateCustomerRequest request, CancellationToken ct = default);
}

public sealed class CustomerService : ICustomerService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IValidator<CreateCustomerRequest> _validator;

    public CustomerService(
        IApplicationDbContext db,
        ICurrentUser user,
        IAuditService audit,
        IValidator<CreateCustomerRequest> validator)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _validator = validator;
    }

    public async Task<Result<PagedResult<CustomerDto>>> ListAsync(PagedQuery query, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.CustomersRead);
        if (!auth.IsSuccess) return Result<PagedResult<CustomerDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var q = _db.Customers.AsNoTracking().Where(c => c.OrganizationId == orgId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(c =>
                c.Name.ToLower().Contains(s) ||
                (c.Phone != null && c.Phone.ToLower().Contains(s)) ||
                (c.Email != null && c.Email.ToLower().Contains(s)));
        }

        q = (query.SortBy?.ToLower()) switch
        {
            "phone" => query.SortDesc ? q.OrderByDescending(c => c.Phone) : q.OrderBy(c => c.Phone),
            "createdat" => query.SortDesc ? q.OrderByDescending(c => c.CreatedAt) : q.OrderBy(c => c.CreatedAt),
            _ => query.SortDesc ? q.OrderByDescending(c => c.Name) : q.OrderBy(c => c.Name)
        };

        var total = await q.CountAsync(ct);
        var items = await q.Skip(query.Skip).Take(query.Take).Select(Map).ToListAsync(ct);
        return Result<PagedResult<CustomerDto>>.Success(new PagedResult<CustomerDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.Take,
            TotalCount = total
        });
    }

    public async Task<Result<CustomerDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.CustomersRead);
        if (!auth.IsSuccess) return Result<CustomerDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == _user.OrganizationId, ct);
        if (customer is null) return Result<CustomerDto>.Failure(ErrorCodes.NotFound, "Customer not found.");
        return Result<CustomerDto>.Success(ToDto(customer));
    }

    public async Task<Result<CustomerDto>> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.CustomersWrite);
        if (!auth.IsSuccess) return Result<CustomerDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<CustomerDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var entity = new Customer
        {
            OrganizationId = _user.OrganizationId!.Value,
            Name = request.Name.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            Province = request.Province,
            PostalCode = request.PostalCode,
            Notes = request.Notes
        };
        _db.Customers.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("create", "Customer", entity.Id.ToString(), null, ToDto(entity), ct);
        return Result<CustomerDto>.Success(ToDto(entity));
    }

    public async Task<Result<CustomerDto>> UpdateAsync(Guid id, CreateCustomerRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.CustomersWrite);
        if (!auth.IsSuccess) return Result<CustomerDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<CustomerDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var entity = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id && c.OrganizationId == _user.OrganizationId, ct);
        if (entity is null) return Result<CustomerDto>.Failure(ErrorCodes.NotFound, "Customer not found.");

        var before = ToDto(entity);
        entity.Name = request.Name.Trim();
        entity.Email = request.Email?.Trim();
        entity.Phone = request.Phone?.Trim();
        entity.AddressLine1 = request.AddressLine1;
        entity.AddressLine2 = request.AddressLine2;
        entity.City = request.City;
        entity.Province = request.Province;
        entity.PostalCode = request.PostalCode;
        entity.Notes = request.Notes;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("update", "Customer", entity.Id.ToString(), before, ToDto(entity), ct);
        return Result<CustomerDto>.Success(ToDto(entity));
    }

    private static System.Linq.Expressions.Expression<Func<Customer, CustomerDto>> Map =>
        c => new CustomerDto(c.Id, c.Name, c.Email, c.Phone, c.AddressLine1, c.AddressLine2, c.City, c.Province, c.PostalCode, c.Notes, c.CreatedAt);

    private static CustomerDto ToDto(Customer c) =>
        new(c.Id, c.Name, c.Email, c.Phone, c.AddressLine1, c.AddressLine2, c.City, c.Province, c.PostalCode, c.Notes, c.CreatedAt);
}
