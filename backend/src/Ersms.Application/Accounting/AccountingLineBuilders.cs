using Ersms.Domain.Accounting;
using Ersms.SharedKernel;

namespace Ersms.Application.Accounting;

public static class AccountingLineBuilders
{
    public static Result RequireMaps(IReadOnlyDictionary<string, Guid> maps, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!maps.ContainsKey(key))
                return Result.Failure(ErrorCodes.Conflict, $"Missing account mapping: {key}");
        }
        return Result.Success();
    }

    public static Result<IReadOnlyList<JournalLineInput>> SaleCompleted(
        IReadOnlyDictionary<string, Guid> maps,
        decimal totalAmount,
        decimal amountPaid,
        string? paymentMethodCode,
        decimal cogsAmount)
    {
        var cashKey = paymentMethodCode is null
            ? MappingKeys.Cash
            : PaymentMethodAccounts.MappingKeyFor(paymentMethodCode);
        var required = new List<string> { MappingKeys.SalesRevenue, MappingKeys.AccountsReceivable, cashKey };
        if (cogsAmount > 0)
        {
            required.Add(MappingKeys.Cogs);
            required.Add(MappingKeys.InventoryAsset);
        }
        var check = RequireMaps(maps, required.ToArray());
        if (!check.IsSuccess)
            return Result<IReadOnlyList<JournalLineInput>>.Failure(check.ErrorCode!, check.ErrorMessage!);

        var lines = new List<JournalLineInput>();
        var revenue = maps[MappingKeys.SalesRevenue];
        var ar = maps[MappingKeys.AccountsReceivable];
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

        return Result<IReadOnlyList<JournalLineInput>>.Success(MergeSameAccount(lines));
    }

    public static Result<IReadOnlyList<JournalLineInput>> PaymentSucceeded(
        IReadOnlyDictionary<string, Guid> maps,
        decimal amount,
        string methodCode)
    {
        var cashKey = PaymentMethodAccounts.MappingKeyFor(methodCode);
        var check = RequireMaps(maps, cashKey, MappingKeys.AccountsReceivable);
        if (!check.IsSuccess)
            return Result<IReadOnlyList<JournalLineInput>>.Failure(check.ErrorCode!, check.ErrorMessage!);

        return Result<IReadOnlyList<JournalLineInput>>.Success(
        [
            new JournalLineInput(maps[cashKey], amount, 0, "Customer payment"),
            new JournalLineInput(maps[MappingKeys.AccountsReceivable], 0, amount, "AR reduction")
        ]);
    }

    public static Result<IReadOnlyList<JournalLineInput>> SaleReturn(
        IReadOnlyDictionary<string, Guid> maps,
        decimal returnTotal,
        decimal cogsAmount,
        decimal refundAmount,
        string? refundMethodCode,
        decimal creditToAr)
    {
        var required = new List<string> { MappingKeys.SalesRevenue };
        if (creditToAr > 0) required.Add(MappingKeys.AccountsReceivable);
        if (refundAmount > 0) required.Add(PaymentMethodAccounts.MappingKeyFor(refundMethodCode ?? "CASH"));
        if (cogsAmount > 0)
        {
            required.Add(MappingKeys.InventoryAsset);
            required.Add(MappingKeys.Cogs);
        }
        var check = RequireMaps(maps, required.ToArray());
        if (!check.IsSuccess)
            return Result<IReadOnlyList<JournalLineInput>>.Failure(check.ErrorCode!, check.ErrorMessage!);

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

        return Result<IReadOnlyList<JournalLineInput>>.Success(MergeSameAccount(lines));
    }

    public static Result<IReadOnlyList<JournalLineInput>> PurchaseReceived(
        IReadOnlyDictionary<string, Guid> maps,
        decimal inventoryValue)
    {
        var check = RequireMaps(maps, MappingKeys.InventoryAsset, MappingKeys.AccountsPayable);
        if (!check.IsSuccess)
            return Result<IReadOnlyList<JournalLineInput>>.Failure(check.ErrorCode!, check.ErrorMessage!);

        return Result<IReadOnlyList<JournalLineInput>>.Success(
        [
            new JournalLineInput(maps[MappingKeys.InventoryAsset], inventoryValue, 0, "Purchase receive"),
            new JournalLineInput(maps[MappingKeys.AccountsPayable], 0, inventoryValue, "AP from receive")
        ]);
    }

    public static Result<IReadOnlyList<JournalLineInput>> SupplierPayment(
        IReadOnlyDictionary<string, Guid> maps,
        decimal amount,
        string methodCode)
    {
        var cashKey = PaymentMethodAccounts.MappingKeyFor(methodCode);
        var check = RequireMaps(maps, MappingKeys.AccountsPayable, cashKey);
        if (!check.IsSuccess)
            return Result<IReadOnlyList<JournalLineInput>>.Failure(check.ErrorCode!, check.ErrorMessage!);

        return Result<IReadOnlyList<JournalLineInput>>.Success(
        [
            new JournalLineInput(maps[MappingKeys.AccountsPayable], amount, 0, "AP payment"),
            new JournalLineInput(maps[cashKey], 0, amount, "Cash/bank payment to supplier")
        ]);
    }

    public static Result<IReadOnlyList<JournalLineInput>> ExpensePosted(
        IReadOnlyDictionary<string, Guid> maps,
        Guid expenseAccountId,
        decimal amount,
        string? methodCode,
        bool payable)
    {
        if (payable)
        {
            var check = RequireMaps(maps, MappingKeys.AccountsPayable);
            if (!check.IsSuccess)
                return Result<IReadOnlyList<JournalLineInput>>.Failure(check.ErrorCode!, check.ErrorMessage!);

            return Result<IReadOnlyList<JournalLineInput>>.Success(
            [
                new JournalLineInput(expenseAccountId, amount, 0, "Expense"),
                new JournalLineInput(maps[MappingKeys.AccountsPayable], 0, amount, "Expense payable")
            ]);
        }

        var cashKey = PaymentMethodAccounts.MappingKeyFor(methodCode ?? "CASH");
        var cashCheck = RequireMaps(maps, cashKey);
        if (!cashCheck.IsSuccess)
            return Result<IReadOnlyList<JournalLineInput>>.Failure(cashCheck.ErrorCode!, cashCheck.ErrorMessage!);

        return Result<IReadOnlyList<JournalLineInput>>.Success(
        [
            new JournalLineInput(expenseAccountId, amount, 0, "Expense"),
            new JournalLineInput(maps[cashKey], 0, amount, "Expense payment")
        ]);
    }

    public static Result<IReadOnlyList<JournalLineInput>> StockAdjusted(
        IReadOnlyDictionary<string, Guid> maps,
        decimal valueDelta)
    {
        var abs = Math.Abs(valueDelta);
        if (abs == 0)
            return Result<IReadOnlyList<JournalLineInput>>.Success(Array.Empty<JournalLineInput>());

        var check = RequireMaps(maps, MappingKeys.InventoryAsset, MappingKeys.InventoryAdjustment);
        if (!check.IsSuccess)
            return Result<IReadOnlyList<JournalLineInput>>.Failure(check.ErrorCode!, check.ErrorMessage!);

        if (valueDelta > 0)
        {
            return Result<IReadOnlyList<JournalLineInput>>.Success(
            [
                new JournalLineInput(maps[MappingKeys.InventoryAsset], abs, 0, "Stock increase"),
                new JournalLineInput(maps[MappingKeys.InventoryAdjustment], 0, abs, "Inventory adjustment gain")
            ]);
        }

        return Result<IReadOnlyList<JournalLineInput>>.Success(
        [
            new JournalLineInput(maps[MappingKeys.InventoryAdjustment], abs, 0, "Inventory adjustment loss"),
            new JournalLineInput(maps[MappingKeys.InventoryAsset], 0, abs, "Stock decrease")
        ]);
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
