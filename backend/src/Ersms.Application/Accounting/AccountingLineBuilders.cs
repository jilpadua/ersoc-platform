using Ersms.Domain.Accounting;
using Ersms.Domain.Sales;

namespace Ersms.Application.Accounting;

public static class AccountingLineBuilders
{
    public static IReadOnlyList<JournalLineInput> SaleCompleted(
        IReadOnlyDictionary<string, Guid> maps,
        decimal totalAmount,
        decimal amountPaid,
        string? paymentMethodCode,
        decimal cogsAmount)
    {
        var lines = new List<JournalLineInput>();
        var revenue = maps[MappingKeys.SalesRevenue];
        var ar = maps[MappingKeys.AccountsReceivable];
        var cashKey = paymentMethodCode is null
            ? MappingKeys.Cash
            : PaymentMethodAccounts.MappingKeyFor(paymentMethodCode);
        var cashAccount = maps[cashKey];
        var balanceDue = Math.Round(totalAmount - amountPaid, 2);

        if (amountPaid > 0)
            lines.Add(new JournalLineInput(cashAccount, amountPaid, 0, "Sale payment at completion"));
        if (balanceDue > 0)
            lines.Add(new JournalLineInput(ar, balanceDue, 0, "Sale receivable"));
        lines.Add(new JournalLineInput(revenue, 0, totalAmount, "Sales revenue"));

        if (cogsAmount > 0)
        {
            lines.Add(new JournalLineInput(maps[MappingKeys.Cogs], cogsAmount, 0, "COGS"));
            lines.Add(new JournalLineInput(maps[MappingKeys.InventoryAsset], 0, cogsAmount, "Inventory relief"));
        }

        return MergeSameAccount(lines);
    }

    public static IReadOnlyList<JournalLineInput> PaymentSucceeded(
        IReadOnlyDictionary<string, Guid> maps,
        decimal amount,
        string methodCode)
    {
        var cash = maps[PaymentMethodAccounts.MappingKeyFor(methodCode)];
        return
        [
            new JournalLineInput(cash, amount, 0, "Customer payment"),
            new JournalLineInput(maps[MappingKeys.AccountsReceivable], 0, amount, "AR reduction")
        ];
    }

    public static IReadOnlyList<JournalLineInput> SaleReturn(
        IReadOnlyDictionary<string, Guid> maps,
        decimal returnTotal,
        decimal cogsAmount,
        decimal refundAmount,
        string? refundMethodCode,
        decimal creditToAr)
    {
        var lines = new List<JournalLineInput>
        {
            new(maps[MappingKeys.SalesRevenue], returnTotal, 0, "Return revenue reversal")
        };

        if (creditToAr > 0)
            lines.Add(new JournalLineInput(maps[MappingKeys.AccountsReceivable], 0, creditToAr, "AR credit from return"));
        if (refundAmount > 0)
        {
            var cash = maps[PaymentMethodAccounts.MappingKeyFor(refundMethodCode ?? "CASH")];
            lines.Add(new JournalLineInput(cash, 0, refundAmount, "Cash refund"));
        }

        if (cogsAmount > 0)
        {
            lines.Add(new JournalLineInput(maps[MappingKeys.InventoryAsset], cogsAmount, 0, "Inventory restock"));
            lines.Add(new JournalLineInput(maps[MappingKeys.Cogs], 0, cogsAmount, "COGS reversal"));
        }

        return MergeSameAccount(lines);
    }

    public static IReadOnlyList<JournalLineInput> PurchaseReceived(
        IReadOnlyDictionary<string, Guid> maps,
        decimal inventoryValue)
    {
        return
        [
            new JournalLineInput(maps[MappingKeys.InventoryAsset], inventoryValue, 0, "Purchase receive"),
            new JournalLineInput(maps[MappingKeys.AccountsPayable], 0, inventoryValue, "AP from receive")
        ];
    }

    public static IReadOnlyList<JournalLineInput> SupplierPayment(
        IReadOnlyDictionary<string, Guid> maps,
        decimal amount,
        string methodCode)
    {
        var cash = maps[PaymentMethodAccounts.MappingKeyFor(methodCode)];
        return
        [
            new JournalLineInput(maps[MappingKeys.AccountsPayable], amount, 0, "AP payment"),
            new JournalLineInput(cash, 0, amount, "Cash/bank payment to supplier")
        ];
    }

    public static IReadOnlyList<JournalLineInput> ExpensePosted(
        IReadOnlyDictionary<string, Guid> maps,
        Guid expenseAccountId,
        decimal amount,
        string? methodCode,
        bool payable)
    {
        if (payable)
        {
            return
            [
                new JournalLineInput(expenseAccountId, amount, 0, "Expense"),
                new JournalLineInput(maps[MappingKeys.AccountsPayable], 0, amount, "Expense payable")
            ];
        }

        var cash = maps[PaymentMethodAccounts.MappingKeyFor(methodCode ?? "CASH")];
        return
        [
            new JournalLineInput(expenseAccountId, amount, 0, "Expense"),
            new JournalLineInput(cash, 0, amount, "Expense payment")
        ];
    }

    public static IReadOnlyList<JournalLineInput> StockAdjusted(
        IReadOnlyDictionary<string, Guid> maps,
        decimal valueDelta)
    {
        var abs = Math.Abs(valueDelta);
        if (abs == 0) return Array.Empty<JournalLineInput>();

        if (valueDelta > 0)
        {
            return
            [
                new JournalLineInput(maps[MappingKeys.InventoryAsset], abs, 0, "Stock increase"),
                new JournalLineInput(maps[MappingKeys.InventoryAdjustment], 0, abs, "Inventory adjustment gain")
            ];
        }

        return
        [
            new JournalLineInput(maps[MappingKeys.InventoryAdjustment], abs, 0, "Inventory adjustment loss"),
            new JournalLineInput(maps[MappingKeys.InventoryAsset], 0, abs, "Stock decrease")
        ];
    }

    public static async Task<IReadOnlyDictionary<string, Guid>> LoadMapsAsync(
        Common.IApplicationDbContext db,
        Guid orgId,
        CancellationToken ct)
    {
        var maps = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToDictionaryAsync(
            db.AccountingAccountMappings.Where(m => m.OrganizationId == orgId),
            m => m.MappingKey,
            m => m.AccountId,
            ct);
        return maps;
    }

    private static IReadOnlyList<JournalLineInput> MergeSameAccount(List<JournalLineInput> lines)
    {
        return lines
            .GroupBy(l => l.AccountId)
            .Select(g =>
            {
                var debit = g.Sum(x => x.Debit);
                var credit = g.Sum(x => x.Credit);
                var netDebit = Math.Max(0, debit - credit);
                var netCredit = Math.Max(0, credit - debit);
                return new JournalLineInput(g.Key, Math.Round(netDebit, 2), Math.Round(netCredit, 2), g.First().Description);
            })
            .Where(l => l.Debit > 0 || l.Credit > 0)
            .ToList();
    }
}
