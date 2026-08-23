using Ersms.Application.Common;
using Ersms.Domain.Devices;
using Ersms.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Devices;

public sealed record DeviceDto(
    Guid Id,
    Guid CustomerId,
    string DeviceType,
    string Brand,
    string Model,
    string? SerialNumber,
    string? Imei,
    string? Color,
    string? Condition,
    string? Accessories,
    string? IdentifyingDetails,
    DateTimeOffset CreatedAt);

public sealed record CreateDeviceRequest(
    Guid CustomerId,
    string DeviceType,
    string Brand,
    string Model,
    string? SerialNumber,
    string? Imei,
    string? Color,
    string? Condition,
    string? Accessories,
    string? IdentifyingDetails);

public sealed class CreateDeviceValidator : AbstractValidator<CreateDeviceRequest>
{
    public CreateDeviceValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DeviceType).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(100);
    }
}

public interface IDeviceService
{
    Task<Result<PagedResult<DeviceDto>>> ListAsync(PagedQuery query, Guid? customerId, CancellationToken ct = default);
    Task<Result<DeviceDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<DeviceDto>> CreateAsync(CreateDeviceRequest request, CancellationToken ct = default);
    Task<Result<DeviceDto>> UpdateAsync(Guid id, CreateDeviceRequest request, CancellationToken ct = default);
}

public sealed class DeviceService : IDeviceService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IValidator<CreateDeviceRequest> _validator;

    public DeviceService(IApplicationDbContext db, ICurrentUser user, IAuditService audit, IValidator<CreateDeviceRequest> validator)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _validator = validator;
    }

    public async Task<Result<PagedResult<DeviceDto>>> ListAsync(PagedQuery query, Guid? customerId, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.DevicesRead);
        if (!auth.IsSuccess) return Result<PagedResult<DeviceDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var q = _db.Devices.AsNoTracking().Where(d => d.OrganizationId == orgId);
        if (customerId.HasValue) q = q.Where(d => d.CustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(d =>
                d.Brand.ToLower().Contains(s) ||
                d.Model.ToLower().Contains(s) ||
                (d.SerialNumber != null && d.SerialNumber.ToLower().Contains(s)) ||
                (d.Imei != null && d.Imei.ToLower().Contains(s)));
        }

        q = q.OrderByDescending(d => d.CreatedAt);
        var total = await q.CountAsync(ct);
        var items = await q.Skip(query.Skip).Take(query.Take)
            .Select(d => new DeviceDto(d.Id, d.CustomerId, d.DeviceType, d.Brand, d.Model, d.SerialNumber, d.Imei, d.Color, d.Condition, d.Accessories, d.IdentifyingDetails, d.CreatedAt))
            .ToListAsync(ct);

        return Result<PagedResult<DeviceDto>>.Success(new PagedResult<DeviceDto>
        {
            Items = items, Page = query.Page, PageSize = query.Take, TotalCount = total
        });
    }

    public async Task<Result<DeviceDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.DevicesRead);
        if (!auth.IsSuccess) return Result<DeviceDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var d = await _db.Devices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == _user.OrganizationId, ct);
        if (d is null) return Result<DeviceDto>.Failure(ErrorCodes.NotFound, "Device not found.");
        return Result<DeviceDto>.Success(ToDto(d));
    }

    public async Task<Result<DeviceDto>> CreateAsync(CreateDeviceRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.DevicesWrite);
        if (!auth.IsSuccess) return Result<DeviceDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<DeviceDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var customerExists = await _db.Customers.AnyAsync(
            c => c.Id == request.CustomerId && c.OrganizationId == _user.OrganizationId, ct);
        if (!customerExists)
            return Result<DeviceDto>.Failure(ErrorCodes.NotFound, "Customer not found.");

        var entity = new Device
        {
            OrganizationId = _user.OrganizationId!.Value,
            CustomerId = request.CustomerId,
            DeviceType = request.DeviceType.Trim(),
            Brand = request.Brand.Trim(),
            Model = request.Model.Trim(),
            SerialNumber = request.SerialNumber?.Trim(),
            Imei = request.Imei?.Trim(),
            Color = request.Color,
            Condition = request.Condition,
            Accessories = request.Accessories,
            IdentifyingDetails = request.IdentifyingDetails
        };
        _db.Devices.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("create", "Device", entity.Id.ToString(), null, ToDto(entity), ct);
        return Result<DeviceDto>.Success(ToDto(entity));
    }

    public async Task<Result<DeviceDto>> UpdateAsync(Guid id, CreateDeviceRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.DevicesWrite);
        if (!auth.IsSuccess) return Result<DeviceDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var entity = await _db.Devices.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == _user.OrganizationId, ct);
        if (entity is null) return Result<DeviceDto>.Failure(ErrorCodes.NotFound, "Device not found.");

        var before = ToDto(entity);
        entity.DeviceType = request.DeviceType.Trim();
        entity.Brand = request.Brand.Trim();
        entity.Model = request.Model.Trim();
        entity.SerialNumber = request.SerialNumber?.Trim();
        entity.Imei = request.Imei?.Trim();
        entity.Color = request.Color;
        entity.Condition = request.Condition;
        entity.Accessories = request.Accessories;
        entity.IdentifyingDetails = request.IdentifyingDetails;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("update", "Device", entity.Id.ToString(), before, ToDto(entity), ct);
        return Result<DeviceDto>.Success(ToDto(entity));
    }

    private static DeviceDto ToDto(Device d) =>
        new(d.Id, d.CustomerId, d.DeviceType, d.Brand, d.Model, d.SerialNumber, d.Imei, d.Color, d.Condition, d.Accessories, d.IdentifyingDetails, d.CreatedAt);
}
