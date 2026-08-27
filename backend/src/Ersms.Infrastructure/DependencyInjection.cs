using System.Security.Claims;
using Ersms.Application.Common;
using Ersms.Domain.Identity;
using Ersms.Domain.Repairs;
using Ersms.Domain.ServiceCatalog;
using Ersms.Infrastructure.Auth;
using Ersms.Infrastructure.Audit;
using Ersms.Infrastructure.Persistence;
using Ersms.Infrastructure.Storage;
using Ersms.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ersms.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=ersms;Username=ersms;Password=ersms_dev_password";

        services.AddDbContext<ErsmsDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ErsmsDbContext>());

        services
            .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = false;
            })
            .AddEntityFrameworkStores<ErsmsDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "ersms_auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
            options.SlidingExpiration = true;
            options.ExpireTimeSpan = TimeSpan.FromHours(12);
            options.Events.OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        return services;
    }

    public static async Task SeedDataAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ErsmsDbContext>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
        var config = sp.GetRequiredService<IConfiguration>();

        if (db.Database.IsRelational())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();

        await EnsurePermissionsSyncedAsync(db, logger);
        await EnsurePaymentMethodsSyncedAsync(db, logger);
        await EnsureOrganizationTimeZonesSyncedAsync(db, logger);
        await AccountingSeed.EnsureForAllOrganizationsAsync(db, logger);

        if (await db.Organizations.AnyAsync())
            return;

        logger.LogInformation("Seeding initial organization, roles, and owner user...");

        var org = new Organization
        {
            Name = config["Seed:OrganizationName"] ?? "Demo Repair Shop",
            TimeZoneId = config["Seed:TimeZoneId"] ?? "Asia/Manila"
        };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            OrganizationId = org.Id,
            Name = config["Seed:BranchName"] ?? "Main Branch",
            Address = "Local"
        };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var permissions = await db.AppPermissions.ToListAsync();
        var roles = new Dictionary<string, string[]>
        {
            [RoleCodes.Owner] = Permissions.All.ToArray(),
            [RoleCodes.AdminManager] =
            [
                Permissions.CustomersRead, Permissions.CustomersWrite,
                Permissions.DevicesRead, Permissions.DevicesWrite,
                Permissions.ServicesRead, Permissions.ServicesWrite,
                Permissions.RepairsRead, Permissions.RepairsWrite, Permissions.RepairsStatus,
                Permissions.DashboardRead, Permissions.AuditRead,
                Permissions.InventoryRead, Permissions.InventoryWrite,
                Permissions.PurchasingRead, Permissions.PurchasingWrite,
                Permissions.SalesRead, Permissions.SalesWrite, Permissions.SalesRefund,
                Permissions.AccountingRead, Permissions.AccountingWrite, Permissions.AccountingPost,
                Permissions.AccountingPeriods, Permissions.AccountingApproveExpense, Permissions.AccountingAp
            ],
            [RoleCodes.Cashier] =
            [
                Permissions.CustomersRead, Permissions.CustomersWrite,
                Permissions.DevicesRead, Permissions.RepairsRead, Permissions.DashboardRead,
                Permissions.SalesRead, Permissions.SalesWrite, Permissions.SalesRefund,
                Permissions.AccountingRead
            ],
            [RoleCodes.Technician] =
            [
                Permissions.CustomersRead, Permissions.DevicesRead, Permissions.DevicesWrite,
                Permissions.ServicesRead, Permissions.RepairsRead, Permissions.RepairsWrite, Permissions.RepairsStatus,
                Permissions.DashboardRead
            ],
            [RoleCodes.InventoryStaff] =
            [
                Permissions.CustomersRead, Permissions.DevicesRead, Permissions.ServicesRead, Permissions.DashboardRead,
                Permissions.InventoryRead, Permissions.InventoryWrite,
                Permissions.PurchasingRead, Permissions.PurchasingWrite,
                Permissions.SalesRead
            ]
        };

        var roleEntities = new Dictionary<string, AppRole>();
        foreach (var (code, perms) in roles)
        {
            var role = new AppRole
            {
                OrganizationId = org.Id,
                Code = code,
                Name = code.Replace('_', ' ')
            };
            db.AppRoles.Add(role);
            roleEntities[code] = role;
            await db.SaveChangesAsync();

            foreach (var p in perms)
            {
                var perm = permissions.First(x => x.Code == p);
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
            }
        }

        foreach (var (code, name, order, terminal, pending) in DefaultRepairStatuses.All)
        {
            db.RepairStatusDefinitions.Add(new RepairStatusDefinition
            {
                OrganizationId = org.Id,
                Code = code,
                Name = name,
                SortOrder = order,
                IsTerminal = terminal,
                CountsAsPending = pending
            });
        }

        db.ServiceCategories.Add(new ServiceCategory { OrganizationId = org.Id, Name = "General" });

        foreach (var (code, name) in new[] { ("CASH", "Cash"), ("CARD", "Card"), ("TRANSFER", "Bank transfer") })
        {
            db.PaymentMethods.Add(new Domain.Sales.PaymentMethod
            {
                OrganizationId = org.Id,
                Code = code,
                Name = name,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();

        await AccountingSeed.EnsureForOrganizationAsync(db, org.Id);
        await db.SaveChangesAsync();

        var email = config["Seed:OwnerEmail"] ?? "owner@ersms.local";
        var password = config["Seed:OwnerPassword"] ?? "Owner123!";
        var owner = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            OrganizationId = org.Id,
            BranchId = branch.Id,
            DisplayName = "Owner"
        };
        var create = await userManager.CreateAsync(owner, password);
        if (!create.Succeeded)
            throw new InvalidOperationException(string.Join("; ", create.Errors.Select(e => e.Description)));

        db.AppUserRoles.Add(new UserRole { UserId = owner.Id, RoleId = roleEntities[RoleCodes.Owner].Id });
        await db.SaveChangesAsync();

        logger.LogInformation("Seed complete. Owner login: {Email}", email);
    }

    private static async Task EnsurePermissionsSyncedAsync(ErsmsDbContext db, ILogger logger)
    {
        var existing = await db.AppPermissions.Select(p => p.Code).ToListAsync();
        var missing = Permissions.All.Except(existing).ToList();
        if (missing.Count > 0)
        {
            foreach (var code in missing)
                db.AppPermissions.Add(new AppPermission { Code = code, Name = code });
            await db.SaveChangesAsync();
            logger.LogInformation("Added {Count} new permissions.", missing.Count);
        }

        var permissions = await db.AppPermissions.ToListAsync();
        var byCode = permissions.ToDictionary(p => p.Code);

        async Task EnsureRolePerms(string roleCode, IEnumerable<string> codes)
        {
            var roles = await db.AppRoles.Where(r => r.Code == roleCode).ToListAsync();
            foreach (var role in roles)
            {
                var have = await db.RolePermissions.Where(rp => rp.RoleId == role.Id).Select(rp => rp.PermissionId).ToListAsync();
                foreach (var code in codes)
                {
                    if (!byCode.TryGetValue(code, out var perm)) continue;
                    if (have.Contains(perm.Id)) continue;
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
                }
            }
        }

        await EnsureRolePerms(RoleCodes.Owner, Permissions.All);
        await EnsureRolePerms(RoleCodes.AdminManager,
        [
            Permissions.InventoryRead, Permissions.InventoryWrite,
            Permissions.PurchasingRead, Permissions.PurchasingWrite,
            Permissions.SalesRead, Permissions.SalesWrite, Permissions.SalesRefund,
            Permissions.AccountingRead, Permissions.AccountingWrite, Permissions.AccountingPost,
            Permissions.AccountingPeriods, Permissions.AccountingApproveExpense, Permissions.AccountingAp
        ]);
        await EnsureRolePerms(RoleCodes.Cashier,
        [
            Permissions.SalesRead, Permissions.SalesWrite, Permissions.SalesRefund,
            Permissions.AccountingRead
        ]);
        await EnsureRolePerms(RoleCodes.InventoryStaff,
        [
            Permissions.InventoryRead, Permissions.InventoryWrite,
            Permissions.PurchasingRead, Permissions.PurchasingWrite,
            Permissions.SalesRead
        ]);
        await db.SaveChangesAsync();
    }

    private static async Task EnsurePaymentMethodsSyncedAsync(ErsmsDbContext db, ILogger logger)
    {
        var orgs = await db.Organizations.Select(o => o.Id).ToListAsync();
        var seeded = new (string Code, string Name)[]
        {
            ("CASH", "Cash"),
            ("CARD", "Card"),
            ("TRANSFER", "Bank transfer")
        };
        var added = 0;
        foreach (var orgId in orgs)
        {
            var existing = await db.PaymentMethods.Where(m => m.OrganizationId == orgId).Select(m => m.Code).ToListAsync();
            foreach (var (code, name) in seeded)
            {
                if (existing.Contains(code)) continue;
                db.PaymentMethods.Add(new Domain.Sales.PaymentMethod
                {
                    OrganizationId = orgId,
                    Code = code,
                    Name = name,
                    IsActive = true
                });
                added++;
            }
        }
        if (added > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} payment methods.", added);
        }
    }

    private static async Task EnsureOrganizationTimeZonesSyncedAsync(ErsmsDbContext db, ILogger logger)
    {
        var missing = await db.Organizations
            .Where(o => string.IsNullOrWhiteSpace(o.TimeZoneId))
            .ToListAsync();
        if (missing.Count == 0) return;
        foreach (var org in missing)
            org.TimeZoneId = "Asia/Manila";
        await db.SaveChangesAsync();
        logger.LogInformation("Backfilled TimeZoneId on {Count} organizations.", missing.Count);
    }

    public static async Task<IList<Claim>> BuildUserClaimsAsync(this ErsmsDbContext db, ApplicationUser user)
    {
        var roleIds = await db.AppUserRoles.Where(ur => ur.UserId == user.Id).Select(ur => ur.RoleId).ToListAsync();
        var permissionCodes = await db.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Join(db.AppPermissions, rp => rp.PermissionId, p => p.Id, (_, p) => p.Code)
            .Distinct()
            .ToListAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.DisplayName),
            new("org_id", user.OrganizationId.ToString())
        };
        if (user.BranchId.HasValue)
            claims.Add(new Claim("branch_id", user.BranchId.Value.ToString()));

        foreach (var p in permissionCodes)
            claims.Add(new Claim("permission", p));

        var roleCodes = await db.AppRoles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Code).ToListAsync();
        foreach (var r in roleCodes)
            claims.Add(new Claim(ClaimTypes.Role, r));

        return claims;
    }
}
