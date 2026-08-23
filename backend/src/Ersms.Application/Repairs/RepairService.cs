using Ersms.Application.Common;
using Ersms.Domain.Repairs;
using Ersms.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Repairs;

public sealed record RepairListItemDto(
    Guid Id,
    string RepairNumber,
    Guid CustomerId,
    string CustomerName,
    Guid DeviceId,
    string DeviceLabel,
    string StatusCode,
    string StatusName,
    Guid? TechnicianUserId,
    decimal TotalAmount,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? DueAt);

public sealed record RepairDetailDto(
    Guid Id,
    string RepairNumber,
    Guid BranchId,
    Guid CustomerId,
    Guid DeviceId,
    Guid StatusId,
    string StatusCode,
    string StatusName,
    string ReportedProblem,
    string? Condition,
    string? Accessories,
    string? Diagnosis,
    Guid? TechnicianUserId,
    decimal? EstimateAmount,
    string ApprovalStatus,
    int? WarrantyDays,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TotalAmount,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<RepairServiceLineDto> ServiceLines,
    IReadOnlyList<RepairStatusHistoryDto> StatusHistory,
    IReadOnlyList<RepairNoteDto> Notes);

public sealed record RepairServiceLineDto(Guid Id, Guid? ServiceId, string ServiceName, decimal Quantity, decimal UnitPrice, decimal Discount, decimal LineTotal);
public sealed record RepairStatusHistoryDto(Guid Id, Guid? PreviousStatusId, Guid NewStatusId, Guid ActorUserId, DateTimeOffset ChangedAt, string? Reason);
public sealed record RepairNoteDto(Guid Id, Guid AuthorUserId, string Body, DateTimeOffset CreatedAt);
public sealed record RepairStatusDto(Guid Id, string Code, string Name, int SortOrder, bool IsTerminal);

public sealed record CreateRepairRequest(
    Guid CustomerId,
    Guid DeviceId,
    string ReportedProblem,
    string? Condition,
    string? Accessories,
    Guid? TechnicianUserId,
    decimal? EstimateAmount,
    DateTimeOffset? DueAt,
    IReadOnlyList<CreateRepairServiceLineRequest>? ServiceLines);

public sealed record CreateRepairServiceLineRequest(Guid? ServiceId, string? ServiceName, decimal Quantity, decimal UnitPrice, decimal Discount);

public sealed record ChangeRepairStatusRequest(string StatusCode, string? Reason);
public sealed record AddRepairNoteRequest(string Body);
public sealed record AssignTechnicianRequest(Guid? TechnicianUserId);

public sealed class CreateRepairValidator : AbstractValidator<CreateRepairRequest>
{
    public CreateRepairValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.ReportedProblem).NotEmpty().MaximumLength(2000);
    }
}

public interface IRepairService
{
    Task<Result<PagedResult<RepairListItemDto>>> ListAsync(PagedQuery query, string? statusCode, CancellationToken ct = default);
    Task<Result<RepairDetailDto>> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<RepairDetailDto>> CreateAsync(CreateRepairRequest request, CancellationToken ct = default);
    Task<Result<RepairDetailDto>> ChangeStatusAsync(Guid id, ChangeRepairStatusRequest request, CancellationToken ct = default);
    Task<Result<RepairDetailDto>> AssignTechnicianAsync(Guid id, AssignTechnicianRequest request, CancellationToken ct = default);
    Task<Result<RepairNoteDto>> AddNoteAsync(Guid id, AddRepairNoteRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RepairStatusDto>>> ListStatusesAsync(CancellationToken ct = default);
}

public sealed class RepairService : IRepairService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly IValidator<CreateRepairRequest> _validator;

    public RepairService(IApplicationDbContext db, ICurrentUser user, IAuditService audit, IValidator<CreateRepairRequest> validator)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _validator = validator;
    }

    public async Task<Result<PagedResult<RepairListItemDto>>> ListAsync(PagedQuery query, string? statusCode, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.RepairsRead);
        if (!auth.IsSuccess) return Result<PagedResult<RepairListItemDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var q =
            from r in _db.Repairs.AsNoTracking()
            join c in _db.Customers.AsNoTracking() on r.CustomerId equals c.Id
            join d in _db.Devices.AsNoTracking() on r.DeviceId equals d.Id
            join s in _db.RepairStatusDefinitions.AsNoTracking() on r.StatusId equals s.Id
            where r.OrganizationId == orgId
            select new { r, c, d, s };

        if (!string.IsNullOrWhiteSpace(statusCode))
            q = q.Where(x => x.s.Code == statusCode);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim().ToLower();
            q = q.Where(x =>
                x.r.RepairNumber.ToLower().Contains(s) ||
                x.c.Name.ToLower().Contains(s) ||
                (x.c.Phone != null && x.c.Phone.ToLower().Contains(s)) ||
                x.d.Model.ToLower().Contains(s) ||
                (x.d.SerialNumber != null && x.d.SerialNumber.ToLower().Contains(s)) ||
                (x.d.Imei != null && x.d.Imei.ToLower().Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.r.ReceivedAt)
            .Skip(query.Skip).Take(query.Take)
            .Select(x => new RepairListItemDto(
                x.r.Id, x.r.RepairNumber, x.c.Id, x.c.Name, x.d.Id,
                x.d.Brand + " " + x.d.Model, x.s.Code, x.s.Name,
                x.r.TechnicianUserId, x.r.TotalAmount, x.r.ReceivedAt, x.r.DueAt))
            .ToListAsync(ct);

        return Result<PagedResult<RepairListItemDto>>.Success(new PagedResult<RepairListItemDto>
        {
            Items = items, Page = query.Page, PageSize = query.Take, TotalCount = total
        });
    }

    public async Task<Result<RepairDetailDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.RepairsRead);
        if (!auth.IsSuccess) return Result<RepairDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var repair = await LoadDetailAsync(id, ct);
        if (repair is null) return Result<RepairDetailDto>.Failure(ErrorCodes.NotFound, "Repair not found.");
        return Result<RepairDetailDto>.Success(repair);
    }

    public async Task<Result<RepairDetailDto>> CreateAsync(CreateRepairRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.RepairsWrite);
        if (!auth.IsSuccess) return Result<RepairDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return Result<RepairDetailDto>.Failure(ErrorCodes.Validation, validation.Errors[0].ErrorMessage);

        var orgId = _user.OrganizationId!.Value;
        var branchId = _user.BranchId;
        if (branchId is null)
            return Result<RepairDetailDto>.Failure(ErrorCodes.Validation, "Branch context is required.");

        var customerOk = await _db.Customers.AnyAsync(c => c.Id == request.CustomerId && c.OrganizationId == orgId, ct);
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == request.DeviceId && d.OrganizationId == orgId, ct);
        if (!customerOk) return Result<RepairDetailDto>.Failure(ErrorCodes.NotFound, "Customer not found.");
        if (device is null) return Result<RepairDetailDto>.Failure(ErrorCodes.NotFound, "Device not found.");
        if (device.CustomerId != request.CustomerId)
            return Result<RepairDetailDto>.Failure(ErrorCodes.Validation, "Device does not belong to customer.");

        var received = await _db.RepairStatusDefinitions
            .FirstOrDefaultAsync(s => s.OrganizationId == orgId && s.Code == "RECEIVED", ct);
        if (received is null)
            return Result<RepairDetailDto>.Failure(ErrorCodes.Conflict, "Repair statuses are not configured.");

        var repairNumber = await NextRepairNumberAsync(orgId, ct);
        var repair = new Repair
        {
            OrganizationId = orgId,
            BranchId = branchId.Value,
            RepairNumber = repairNumber,
            CustomerId = request.CustomerId,
            DeviceId = request.DeviceId,
            StatusId = received.Id,
            ReportedProblem = request.ReportedProblem.Trim(),
            Condition = request.Condition,
            Accessories = request.Accessories,
            TechnicianUserId = request.TechnicianUserId,
            EstimateAmount = request.EstimateAmount,
            DueAt = request.DueAt,
            ReceivedAt = DateTimeOffset.UtcNow
        };

        if (request.ServiceLines is not null)
        {
            foreach (var line in request.ServiceLines)
            {
                var name = line.ServiceName;
                if (line.ServiceId.HasValue)
                {
                    var svc = await _db.Services.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == line.ServiceId && s.OrganizationId == orgId, ct);
                    if (svc is not null) name ??= svc.Name;
                }

                repair.ServiceLines.Add(new RepairServiceLine
                {
                    ServiceId = line.ServiceId,
                    ServiceName = string.IsNullOrWhiteSpace(name) ? "Service" : name.Trim(),
                    Quantity = line.Quantity <= 0 ? 1 : line.Quantity,
                    UnitPrice = line.UnitPrice,
                    Discount = line.Discount
                });
            }
        }

        repair.RecalculateTotals();
        repair.StatusHistory.Add(new RepairStatusHistory
        {
            PreviousStatusId = null,
            NewStatusId = received.Id,
            ActorUserId = _user.UserId!.Value,
            Reason = "Repair created"
        });

        _db.Repairs.Add(repair);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("create", "Repair", repair.Id.ToString(), null, new { repair.RepairNumber, repair.StatusId }, ct);

        var detail = await LoadDetailAsync(repair.Id, ct);
        return Result<RepairDetailDto>.Success(detail!);
    }

    public async Task<Result<RepairDetailDto>> ChangeStatusAsync(Guid id, ChangeRepairStatusRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.RepairsStatus);
        if (!auth.IsSuccess) return Result<RepairDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var repair = await _db.Repairs
            .Include(r => r.Status)
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == _user.OrganizationId, ct);
        if (repair is null) return Result<RepairDetailDto>.Failure(ErrorCodes.NotFound, "Repair not found.");

        var newStatus = await _db.RepairStatusDefinitions
            .FirstOrDefaultAsync(s => s.OrganizationId == _user.OrganizationId && s.Code == request.StatusCode && s.IsActive, ct);
        if (newStatus is null)
            return Result<RepairDetailDto>.Failure(ErrorCodes.NotFound, "Status not found.");

        var fromCode = repair.Status?.Code ?? string.Empty;
        var transition = RepairWorkflow.CanTransition(fromCode, newStatus.Code);
        if (!transition.IsSuccess)
            return Result<RepairDetailDto>.Failure(transition.ErrorCode!, transition.ErrorMessage!);

        var before = new { StatusCode = fromCode };
        var previousId = repair.StatusId;
        repair.StatusId = newStatus.Id;
        repair.UpdatedAt = DateTimeOffset.UtcNow;
        if (newStatus.Code is "APPROVED")
        {
            repair.ApprovalStatus = "Approved";
            repair.ApprovedAt = DateTimeOffset.UtcNow;
        }
        if (newStatus.Code is "COMPLETED")
            repair.CompletedAt = DateTimeOffset.UtcNow;
        if (newStatus.Code is "CANCELLED")
            repair.ApprovalStatus = "Cancelled";

        _db.RepairStatusHistories.Add(new RepairStatusHistory
        {
            RepairId = repair.Id,
            PreviousStatusId = previousId,
            NewStatusId = newStatus.Id,
            ActorUserId = _user.UserId!.Value,
            Reason = request.Reason
        });

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("status_change", "Repair", repair.Id.ToString(), before, new { StatusCode = newStatus.Code, request.Reason }, ct);

        var detail = await LoadDetailAsync(repair.Id, ct);
        return Result<RepairDetailDto>.Success(detail!);
    }

    public async Task<Result<RepairDetailDto>> AssignTechnicianAsync(Guid id, AssignTechnicianRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.RepairsWrite);
        if (!auth.IsSuccess) return Result<RepairDetailDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var repair = await _db.Repairs.FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == _user.OrganizationId, ct);
        if (repair is null) return Result<RepairDetailDto>.Failure(ErrorCodes.NotFound, "Repair not found.");

        var before = new { repair.TechnicianUserId };
        repair.TechnicianUserId = request.TechnicianUserId;
        repair.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("update", "Repair", repair.Id.ToString(), before, new { repair.TechnicianUserId }, ct);
        return Result<RepairDetailDto>.Success((await LoadDetailAsync(id, ct))!);
    }

    public async Task<Result<RepairNoteDto>> AddNoteAsync(Guid id, AddRepairNoteRequest request, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.RepairsWrite);
        if (!auth.IsSuccess) return Result<RepairNoteDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);
        if (string.IsNullOrWhiteSpace(request.Body))
            return Result<RepairNoteDto>.Failure(ErrorCodes.Validation, "Note body is required.");

        var exists = await _db.Repairs.AnyAsync(r => r.Id == id && r.OrganizationId == _user.OrganizationId, ct);
        if (!exists) return Result<RepairNoteDto>.Failure(ErrorCodes.NotFound, "Repair not found.");

        var note = new RepairNote
        {
            RepairId = id,
            AuthorUserId = _user.UserId!.Value,
            Body = request.Body.Trim()
        };
        _db.RepairNotes.Add(note);
        await _db.SaveChangesAsync(ct);
        return Result<RepairNoteDto>.Success(new RepairNoteDto(note.Id, note.AuthorUserId, note.Body, note.CreatedAt));
    }

    public async Task<Result<IReadOnlyList<RepairStatusDto>>> ListStatusesAsync(CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.RepairsRead);
        if (!auth.IsSuccess) return Result<IReadOnlyList<RepairStatusDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var items = await _db.RepairStatusDefinitions.AsNoTracking()
            .Where(s => s.OrganizationId == _user.OrganizationId && s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Select(s => new RepairStatusDto(s.Id, s.Code, s.Name, s.SortOrder, s.IsTerminal))
            .ToListAsync(ct);
        return Result<IReadOnlyList<RepairStatusDto>>.Success(items);
    }

    private async Task<string> NextRepairNumberAsync(Guid orgId, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"R{year}-";
        var last = await _db.Repairs.AsNoTracking()
            .Where(r => r.OrganizationId == orgId && r.RepairNumber.StartsWith(prefix))
            .OrderByDescending(r => r.RepairNumber)
            .Select(r => r.RepairNumber)
            .FirstOrDefaultAsync(ct);

        var next = 1;
        if (last is not null && int.TryParse(last.AsSpan(prefix.Length), out var n))
            next = n + 1;
        return $"{prefix}{next:D5}";
    }

    private async Task<RepairDetailDto?> LoadDetailAsync(Guid id, CancellationToken ct)
    {
        var repair = await _db.Repairs.AsNoTracking()
            .Include(r => r.Status)
            .Include(r => r.ServiceLines)
            .Include(r => r.StatusHistory)
            .Include(r => r.Notes)
            .FirstOrDefaultAsync(r => r.Id == id && r.OrganizationId == _user.OrganizationId, ct);
        if (repair is null) return null;

        return new RepairDetailDto(
            repair.Id,
            repair.RepairNumber,
            repair.BranchId,
            repair.CustomerId,
            repair.DeviceId,
            repair.StatusId,
            repair.Status!.Code,
            repair.Status.Name,
            repair.ReportedProblem,
            repair.Condition,
            repair.Accessories,
            repair.Diagnosis,
            repair.TechnicianUserId,
            repair.EstimateAmount,
            repair.ApprovalStatus,
            repair.WarrantyDays,
            repair.Subtotal,
            repair.DiscountTotal,
            repair.TotalAmount,
            repair.ReceivedAt,
            repair.DueAt,
            repair.CompletedAt,
            repair.ServiceLines.Select(l => new RepairServiceLineDto(l.Id, l.ServiceId, l.ServiceName, l.Quantity, l.UnitPrice, l.Discount, l.LineTotal)).ToList(),
            repair.StatusHistory.OrderBy(h => h.ChangedAt).Select(h => new RepairStatusHistoryDto(h.Id, h.PreviousStatusId, h.NewStatusId, h.ActorUserId, h.ChangedAt, h.Reason)).ToList(),
            repair.Notes.OrderByDescending(n => n.CreatedAt).Select(n => new RepairNoteDto(n.Id, n.AuthorUserId, n.Body, n.CreatedAt)).ToList());
    }
}
