using Ersms.SharedKernel;

namespace Ersms.Domain.Identity;

public class Organization : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? BusinessInfo { get; set; }
    public string Status { get; set; } = "Active";
    /// <summary>IANA timezone id used for displaying business timestamps (e.g. Asia/Manila).</summary>
    public string TimeZoneId { get; set; } = "Asia/Manila";
    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}

public class Branch : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string Status { get; set; } = "Active";
}

public class AppRole : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public class AppPermission : Entity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class RolePermission
{
    public Guid RoleId { get; set; }
    public AppRole? Role { get; set; }
    public Guid PermissionId { get; set; }
    public AppPermission? Permission { get; set; }
}

public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public AppRole? Role { get; set; }
}

public static class RoleCodes
{
    public const string Owner = "OWNER";
    public const string AdminManager = "ADMIN_MANAGER";
    public const string Cashier = "CASHIER";
    public const string Technician = "TECHNICIAN";
    public const string InventoryStaff = "INVENTORY_STAFF";
}
