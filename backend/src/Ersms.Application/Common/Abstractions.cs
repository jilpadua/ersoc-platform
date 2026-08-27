using Ersms.Domain.Audit;
using Ersms.SharedKernel;

namespace Ersms.Application.Common;

public interface IAuditService
{
    Task WriteAsync(
        string action,
        string entityType,
        string entityId,
        object? before,
        object? after,
        CancellationToken cancellationToken = default);
}

public interface IFileStorage
{
    Task<(string StorageKey, long SizeBytes)> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
}

public static class AuthorizationGuard
{
    public static Result Require(ICurrentUser user, string permission)
    {
        if (!user.IsAuthenticated || user.OrganizationId is null)
            return Result.Failure(ErrorCodes.Unauthorized, "Authentication required.");
        if (!user.HasPermission(permission))
            return Result.Failure(ErrorCodes.Forbidden, $"Missing permission: {permission}");
        return Result.Success();
    }

    public static Result RequireAny(ICurrentUser user, params string[] permissions)
    {
        if (!user.IsAuthenticated || user.OrganizationId is null)
            return Result.Failure(ErrorCodes.Unauthorized, "Authentication required.");
        if (permissions.Any(user.HasPermission))
            return Result.Success();
        return Result.Failure(ErrorCodes.Forbidden, "Missing required permission.");
    }
}
