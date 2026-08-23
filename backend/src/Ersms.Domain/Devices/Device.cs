using Ersms.SharedKernel;

namespace Ersms.Domain.Devices;

public class Device : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid CustomerId { get; set; }
    public string DeviceType { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string? Imei { get; set; }
    public string? Color { get; set; }
    public string? Condition { get; set; }
    public string? Accessories { get; set; }
    public string? IdentifyingDetails { get; set; }
    public ICollection<DevicePhoto> Photos { get; set; } = new List<DevicePhoto>();
}

public class DevicePhoto : AuditableEntity
{
    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public Guid UploadedByUserId { get; set; }
}
