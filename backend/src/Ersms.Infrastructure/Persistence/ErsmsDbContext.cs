using Ersms.Application.Common;
using Ersms.Domain.Customers;
using Ersms.Domain.Devices;
using Ersms.Domain.Repairs;
using Ersms.Domain.ServiceCatalog;
using Ersms.Domain.Identity;
using Ersms.Domain.Audit;
using Ersms.Domain.Inventory;
using Ersms.Domain.Purchasing;
using Ersms.Domain.Sales;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Infrastructure.Persistence;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class ErsmsDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    public ErsmsDbContext(DbContextOptions<ErsmsDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<AppRole> AppRoles => Set<AppRole>();
    public DbSet<AppPermission> AppPermissions => Set<AppPermission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> AppUserRoles => Set<UserRole>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DevicePhoto> DevicePhotos => Set<DevicePhoto>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<ServiceItem> Services => Set<ServiceItem>();
    public DbSet<RepairStatusDefinition> RepairStatusDefinitions => Set<RepairStatusDefinition>();
    public DbSet<Repair> Repairs => Set<Repair>();
    public DbSet<RepairServiceLine> RepairServiceLines => Set<RepairServiceLine>();
    public DbSet<RepairStatusHistory> RepairStatusHistories => Set<RepairStatusHistory>();
    public DbSet<RepairNote> RepairNotes => Set<RepairNote>();
    public DbSet<RepairPhoto> RepairPhotos => Set<RepairPhoto>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<StockLedgerEntry> StockLedgerEntries => Set<StockLedgerEntry>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<PurchaseReceive> PurchaseReceives => Set<PurchaseReceive>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleLine> SaleLines => Set<SaleLine>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnLine> SaleReturnLines => Set<SaleReturnLine>();

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational())
            return Task.FromResult<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(new NoopDbContextTransaction());
        return Database.BeginTransactionAsync(cancellationToken);
    }

    private sealed class NoopDbContextTransaction : Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public void Commit() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Rollback() { }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        });

        builder.Entity<Branch>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
            e.HasOne(x => x.Organization).WithMany(x => x.Branches).HasForeignKey(x => x.OrganizationId);
        });

        builder.Entity<AppRole>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
        });

        builder.Entity<AppPermission>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(128).IsRequired();
        });

        builder.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
            e.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId);
        });

        builder.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        });

        builder.Entity<Customer>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.Phone });
            e.HasIndex(x => new { x.OrganizationId, x.Name });
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(50);
            e.Property(x => x.Email).HasMaxLength(256);
        });

        builder.Entity<Device>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.SerialNumber });
            e.HasIndex(x => new { x.OrganizationId, x.Imei });
            e.HasIndex(x => x.CustomerId);
            e.Property(x => x.Brand).HasMaxLength(100).IsRequired();
            e.Property(x => x.Model).HasMaxLength(100).IsRequired();
            e.Property(x => x.SerialNumber).HasMaxLength(100);
            e.Property(x => x.Imei).HasMaxLength(32);
        });

        builder.Entity<DevicePhoto>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Device).WithMany(x => x.Photos).HasForeignKey(x => x.DeviceId);
        });

        builder.Entity<ServiceCategory>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
        });

        builder.Entity<ServiceItem>(e =>
        {
            e.ToTable("Services");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.Name });
            e.Property(x => x.DefaultPrice).HasPrecision(18, 2);
            e.HasOne(x => x.Category).WithMany(x => x.Services).HasForeignKey(x => x.CategoryId);
        });

        builder.Entity<RepairStatusDefinition>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64).IsRequired();
        });

        builder.Entity<Repair>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.RepairNumber }).IsUnique();
            e.HasIndex(x => new { x.OrganizationId, x.CustomerId });
            e.HasIndex(x => new { x.OrganizationId, x.DeviceId });
            e.Property(x => x.RepairNumber).HasMaxLength(32).IsRequired();
            e.Property(x => x.EstimateAmount).HasPrecision(18, 2);
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.DiscountTotal).HasPrecision(18, 2);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.StatusId);
        });

        builder.Entity<RepairServiceLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantity).HasPrecision(18, 2);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.Discount).HasPrecision(18, 2);
            e.HasOne(x => x.Repair).WithMany(x => x.ServiceLines).HasForeignKey(x => x.RepairId);
        });

        builder.Entity<RepairStatusHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Repair).WithMany(x => x.StatusHistory).HasForeignKey(x => x.RepairId);
        });

        builder.Entity<RepairNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Repair).WithMany(x => x.Notes).HasForeignKey(x => x.RepairId);
        });

        builder.Entity<RepairPhoto>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Repair).WithMany(x => x.Photos).HasForeignKey(x => x.RepairId);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.Timestamp });
            e.HasIndex(x => new { x.OrganizationId, x.EntityType, x.EntityId });
            e.Property(x => x.Action).HasMaxLength(64).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(128).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(64).IsRequired();
        });

        builder.Entity<Part>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.Sku }).IsUnique();
            e.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.UnitCost).HasPrecision(18, 2);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.ReorderLevel).HasPrecision(18, 2);
        });

        builder.Entity<StockLedgerEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.BranchId, x.PartId });
            e.Property(x => x.QuantityDelta).HasPrecision(18, 2);
            e.Property(x => x.EntryType).HasMaxLength(32).IsRequired();
            e.Property(x => x.ReferenceType).HasMaxLength(64);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasOne(x => x.Part).WithMany().HasForeignKey(x => x.PartId);
        });

        builder.Entity<Supplier>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.Name });
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(50);
        });

        builder.Entity<PurchaseOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.PoNumber }).IsUnique();
            e.Property(x => x.PoNumber).HasMaxLength(32).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId);
        });

        builder.Entity<PurchaseOrderLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.QuantityOrdered).HasPrecision(18, 2);
            e.Property(x => x.QuantityReceived).HasPrecision(18, 2);
            e.Property(x => x.UnitCost).HasPrecision(18, 2);
            e.HasOne(x => x.PurchaseOrder).WithMany(x => x.Lines).HasForeignKey(x => x.PurchaseOrderId);
        });

        builder.Entity<PurchaseReceive>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.PurchaseOrderId });
            e.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId);
        });

        builder.Entity<PaymentMethod>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(32).IsRequired();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        builder.Entity<Sale>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.SaleNumber }).IsUnique();
            e.Property(x => x.SaleNumber).HasMaxLength(32).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.DiscountTotal).HasPrecision(18, 2);
            e.Property(x => x.TaxTotal).HasPrecision(18, 2);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.AmountPaid).HasPrecision(18, 2);
            e.Property(x => x.BalanceDue).HasPrecision(18, 2);
        });

        builder.Entity<SaleLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Property(x => x.Quantity).HasPrecision(18, 2);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.UnitCost).HasPrecision(18, 2);
            e.Property(x => x.Discount).HasPrecision(18, 2);
            e.Property(x => x.LineTotal).HasPrecision(18, 2);
            e.HasOne(x => x.Sale).WithMany(x => x.Lines).HasForeignKey(x => x.SaleId);
        });

        builder.Entity<Invoice>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.InvoiceNumber }).IsUnique();
            e.HasIndex(x => x.SaleId).IsUnique();
            e.Property(x => x.InvoiceNumber).HasMaxLength(32).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.AmountPaid).HasPrecision(18, 2);
            e.Property(x => x.BalanceDue).HasPrecision(18, 2);
            e.HasOne(x => x.Sale).WithOne(x => x.Invoice).HasForeignKey<Invoice>(x => x.SaleId);
        });

        builder.Entity<Payment>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.IdempotencyKey }).IsUnique();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.MethodCode).HasMaxLength(32).IsRequired();
            e.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
            e.Property(x => x.Status).HasMaxLength(32).IsRequired();
            e.HasOne(x => x.Sale).WithMany(x => x.Payments).HasForeignKey(x => x.SaleId);
        });

        builder.Entity<SaleReturn>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OrganizationId, x.ReturnNumber }).IsUnique();
            e.Property(x => x.ReturnNumber).HasMaxLength(32).IsRequired();
            e.Property(x => x.RefundAmount).HasPrecision(18, 2);
            e.HasOne(x => x.Sale).WithMany(x => x.Returns).HasForeignKey(x => x.SaleId);
        });

        builder.Entity<SaleReturnLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Quantity).HasPrecision(18, 2);
            e.HasOne(x => x.SaleReturn).WithMany(x => x.Lines).HasForeignKey(x => x.SaleReturnId);
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.NormalizedEmail }).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(200);
        });
    }
}
