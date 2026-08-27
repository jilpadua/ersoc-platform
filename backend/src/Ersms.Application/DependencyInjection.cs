using System.Reflection;
using Ersms.Application.Audit;
using Ersms.Application.Customers;
using Ersms.Application.Dashboard;
using Ersms.Application.Devices;
using Ersms.Application.Inventory;
using Ersms.Application.Purchasing;
using Ersms.Application.Repairs;
using Ersms.Application.Sales;
using Ersms.Application.Search;
using Ersms.Application.ServiceCatalog;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Ersms.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddScoped<IRepairService, RepairService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IPartService, PartService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<ISaleService, SaleService>();

        return services;
    }
}
