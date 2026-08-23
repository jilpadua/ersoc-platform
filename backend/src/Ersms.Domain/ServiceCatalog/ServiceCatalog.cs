using Ersms.SharedKernel;

namespace Ersms.Domain.ServiceCatalog;

public class ServiceCategory : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<ServiceItem> Services { get; set; } = new List<ServiceItem>();
}

public class ServiceItem : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? CategoryId { get; set; }
    public ServiceCategory? Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DefaultPrice { get; set; }
    public int WarrantyDays { get; set; }
    public bool IsActive { get; set; } = true;
}
