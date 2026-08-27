namespace Ersms.SharedKernel;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

public abstract class DomainEventBase : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; } = DateTimeOffset.UtcNow;
}

public sealed class Result
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string code, string message) => new(false, code, message);
}

public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, T? value, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public static Result<T> Failure(string code, string message) => new(false, default, code, message);
}

public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class PagedQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }

    public int Skip => Math.Max(Page - 1, 0) * Math.Clamp(PageSize, 1, 100);
    public int Take => Math.Clamp(PageSize, 1, 100);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? OrganizationId { get; }
    Guid? BranchId { get; }
    string? Email { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool IsAuthenticated { get; }
    bool HasPermission(string permission);
}

public static class Permissions
{
    public const string CustomersRead = "customers.read";
    public const string CustomersWrite = "customers.write";
    public const string DevicesRead = "devices.read";
    public const string DevicesWrite = "devices.write";
    public const string ServicesRead = "services.read";
    public const string ServicesWrite = "services.write";
    public const string RepairsRead = "repairs.read";
    public const string RepairsWrite = "repairs.write";
    public const string RepairsStatus = "repairs.status";
    public const string AuditRead = "audit.read";
    public const string DashboardRead = "dashboard.read";
    public const string SettingsManage = "settings.manage";
    public const string UsersManage = "users.manage";
    public const string InventoryRead = "inventory.read";
    public const string InventoryWrite = "inventory.write";
    public const string PurchasingRead = "purchasing.read";
    public const string PurchasingWrite = "purchasing.write";
    public const string SalesRead = "sales.read";
    public const string SalesWrite = "sales.write";
    public const string SalesRefund = "sales.refund";

    public static IReadOnlyList<string> All { get; } =
    [
        CustomersRead, CustomersWrite,
        DevicesRead, DevicesWrite,
        ServicesRead, ServicesWrite,
        RepairsRead, RepairsWrite, RepairsStatus,
        AuditRead, DashboardRead, SettingsManage, UsersManage,
        InventoryRead, InventoryWrite, PurchasingRead, PurchasingWrite,
        SalesRead, SalesWrite, SalesRefund
    ];
}

public static class ErrorCodes
{
    public const string Validation = "validation_error";
    public const string NotFound = "not_found";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";
    public const string Conflict = "conflict";
    public const string InvalidTransition = "invalid_transition";
    public const string InvalidCredentials = "invalid_credentials";
}
