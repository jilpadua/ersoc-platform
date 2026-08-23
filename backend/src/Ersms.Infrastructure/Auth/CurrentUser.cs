using System.Security.Claims;
using Ersms.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace Ersms.Infrastructure.Auth;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public Guid? OrganizationId => Guid.TryParse(Principal?.FindFirstValue("org_id"), out var id) ? id : null;

    public Guid? BranchId => Guid.TryParse(Principal?.FindFirstValue("branch_id"), out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public IReadOnlyCollection<string> Permissions =>
        Principal?.FindAll("permission").Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}
