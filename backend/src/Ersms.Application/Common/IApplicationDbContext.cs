using Ersms.Domain.Audit;
using Ersms.Domain.Customers;
using Ersms.Domain.Devices;
using Ersms.Domain.Identity;
using Ersms.Domain.Inventory;
using Ersms.Domain.Purchasing;
using Ersms.Domain.Repairs;
using Ersms.Domain.Sales;
using Ersms.Domain.ServiceCatalog;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Common;

public interface IApplicationDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<Branch> Branches { get; }
    DbSet<AppRole> AppRoles { get; }
    DbSet<AppPermission> AppPermissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> AppUserRoles { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Device> Devices { get; }
    DbSet<DevicePhoto> DevicePhotos { get; }
    DbSet<ServiceCategory> ServiceCategories { get; }
    DbSet<ServiceItem> Services { get; }
    DbSet<RepairStatusDefinition> RepairStatusDefinitions { get; }
    DbSet<Repair> Repairs { get; }
    DbSet<RepairServiceLine> RepairServiceLines { get; }
    DbSet<RepairStatusHistory> RepairStatusHistories { get; }
    DbSet<RepairNote> RepairNotes { get; }
    DbSet<RepairPhoto> RepairPhotos { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Part> Parts { get; }
    DbSet<StockLedgerEntry> StockLedgerEntries { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }
    DbSet<PaymentMethod> PaymentMethods { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleLine> SaleLines { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<Payment> Payments { get; }
    DbSet<SaleReturn> SaleReturns { get; }
    DbSet<SaleReturnLine> SaleReturnLines { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
