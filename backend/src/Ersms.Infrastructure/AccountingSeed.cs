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
        var addedAccounts = 0;
        var addedPeriods = 0;
        foreach (var orgId in orgIds)
        {
            var r = await EnsureForOrganizationAsync(db, orgId);
            addedAccounts += r.Accounts;
            addedPeriods += r.Periods;
        }
        if (addedAccounts + addedPeriods > 0)
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Accounting seed: {Accounts} accounts, {Periods} periods.", addedAccounts, addedPeriods);
        }
    }

    public static async Task<(int Accounts, int Periods)> EnsureForOrganizationAsync(ErsmsDbContext db, Guid orgId)
    {
        var existingCodes = await db.Accounts.Where(a => a.OrganizationId == orgId).Select(a => a.Code).ToListAsync();
        var accountByCode = await db.Accounts.Where(a => a.OrganizationId == orgId).ToDictionaryAsync(a => a.Code);
        var accountsAdded = 0;

        foreach (var (code, name, type, normal, _) in DefaultChartOfAccounts.Accounts)
        {
            if (existingCodes.Contains(code)) continue;
            var account = new Account
            {
                OrganizationId = orgId,
                Code = code,
                Name = name,
                AccountType = type,
                NormalBalance = normal,
                IsSystem = true,
                IsActive = true
            };
            db.Accounts.Add(account);
            accountByCode[code] = account;
            accountsAdded++;
        }

        if (accountsAdded > 0)
            await db.SaveChangesAsync();

        accountByCode = await db.Accounts.Where(a => a.OrganizationId == orgId).ToDictionaryAsync(a => a.Code);

        var existingKeys = await db.AccountingAccountMappings
            .Where(m => m.OrganizationId == orgId)
            .Select(m => m.MappingKey)
            .ToListAsync();

        foreach (var (code, _, _, _, mappingKey) in DefaultChartOfAccounts.Accounts)
        {
            if (mappingKey is null) continue;
            if (existingKeys.Contains(mappingKey)) continue;
            if (!accountByCode.TryGetValue(code, out var account)) continue;
            db.AccountingAccountMappings.Add(new AccountingAccountMapping
            {
                OrganizationId = orgId,
                MappingKey = mappingKey,
                AccountId = account.Id
            });
        }

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

        return (accountsAdded, periodsAdded);
    }
}
