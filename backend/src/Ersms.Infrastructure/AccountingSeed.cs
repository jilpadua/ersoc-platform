using Ersms.Domain.Accounting;
using Ersms.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ersms.Infrastructure;

public static class AccountingSeed
{
    public static async Task EnsureForAllOrganizationsAsync(ErsmsDbContext db, ILogger logger)
    {
        var orgIds = await db.Organizations.Select(o => o.Id).ToListAsync();
        var totals = (Accounts: 0, Periods: 0, Mappings: 0, Categories: 0);
        foreach (var orgId in orgIds)
        {
            var r = await EnsureForOrganizationAsync(db, orgId);
            totals.Accounts += r.Accounts;
            totals.Periods += r.Periods;
            totals.Mappings += r.Mappings;
            totals.Categories += r.Categories;
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();

        if (totals.Accounts + totals.Periods + totals.Mappings + totals.Categories > 0)
        {
            logger.LogInformation(
                "Accounting seed: {Accounts} accounts, {Periods} periods, {Mappings} mappings, {Categories} categories.",
                totals.Accounts, totals.Periods, totals.Mappings, totals.Categories);
        }
    }

    public static async Task<(int Accounts, int Periods, int Mappings, int Categories)> EnsureForOrganizationAsync(
        ErsmsDbContext db, Guid orgId)
    {
        var existingCodes = await db.Accounts.Where(a => a.OrganizationId == orgId).Select(a => a.Code).ToListAsync();
        var accountsAdded = 0;

        foreach (var (code, name, type, normal, _) in DefaultChartOfAccounts.Accounts)
        {
            if (existingCodes.Contains(code)) continue;
            db.Accounts.Add(new Account
            {
                OrganizationId = orgId,
                Code = code,
                Name = name,
                AccountType = type,
                NormalBalance = normal,
                IsSystem = true,
                IsActive = true
            });
            accountsAdded++;
        }

        if (accountsAdded > 0)
            await db.SaveChangesAsync();

        var accountByCode = await db.Accounts.Where(a => a.OrganizationId == orgId).ToDictionaryAsync(a => a.Code);
        var existingMaps = await db.AccountingAccountMappings
            .Where(m => m.OrganizationId == orgId)
            .ToListAsync();
        var mapsByKey = existingMaps.ToDictionary(m => m.MappingKey, StringComparer.OrdinalIgnoreCase);

        var mappingsChanged = 0;
        foreach (var (code, _, _, _, mappingKey) in DefaultChartOfAccounts.Accounts)
        {
            if (mappingKey is null) continue;
            if (!accountByCode.TryGetValue(code, out var account)) continue;

            if (!mapsByKey.TryGetValue(mappingKey, out var mapping))
            {
                db.AccountingAccountMappings.Add(new AccountingAccountMapping
                {
                    OrganizationId = orgId,
                    MappingKey = mappingKey,
                    AccountId = account.Id
                });
                mappingsChanged++;
                continue;
            }

            // Keep seeded system mappings aligned with the default CoA codes.
            if (mapping.AccountId != account.Id)
            {
                mapping.AccountId = account.Id;
                mapping.UpdatedAt = DateTimeOffset.UtcNow;
                mappingsChanged++;
            }
        }

        var categoriesAdded = 0;
        var expenseAccount = accountByCode.GetValueOrDefault("6000");
        if (expenseAccount is not null)
        {
            var hasCat = await db.ExpenseCategories.AnyAsync(c => c.OrganizationId == orgId && c.Name == "General");
            if (!hasCat)
            {
                db.ExpenseCategories.Add(new ExpenseCategory
                {
                    OrganizationId = orgId,
                    Name = "General",
                    AccountId = expenseAccount.Id,
                    IsActive = true
                });
                categoriesAdded++;
            }
        }

        var year = DateTime.UtcNow.Year;
        var periodsAdded = 0;
        for (var month = 1; month <= 12; month++)
        {
            var start = new DateOnly(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);
            var exists = await db.AccountingPeriods.AnyAsync(p =>
                p.OrganizationId == orgId && p.StartDate == start && p.EndDate == end);
            if (exists) continue;
            db.AccountingPeriods.Add(new AccountingPeriod
            {
                OrganizationId = orgId,
                Name = $"{year}-{month:D2}",
                StartDate = start,
                EndDate = end,
                Status = PeriodStatuses.Open
            });
            periodsAdded++;
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync();

        return (accountsAdded, periodsAdded, mappingsChanged, categoriesAdded);
    }
}
