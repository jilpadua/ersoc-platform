using Ersms.Application.Common;
using Ersms.Domain.Accounting;
using Ersms.Domain.Sales;
using Ersms.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Ersms.Application.Accounting;

public sealed record ReportAccountAmountDto(Guid AccountId, string Code, string Name, decimal Amount);

public sealed record GeneralLedgerLineDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    Guid JournalEntryId,
    string EntryNumber,
    DateTimeOffset EntryDate,
    string? Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance);

public sealed record TrialBalanceRowDto(
    Guid AccountId,
    string Code,
    string Name,
    string AccountType,
    decimal DebitTotal,
    decimal CreditTotal,
    decimal Balance);

public sealed record ProfitAndLossDto(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<ReportAccountAmountDto> Revenue,
    IReadOnlyList<ReportAccountAmountDto> CostOfGoodsSold,
    IReadOnlyList<ReportAccountAmountDto> Expenses,
    decimal TotalRevenue,
    decimal TotalCogs,
    decimal TotalExpenses,
    decimal NetIncome);

public sealed record BalanceSheetDto(
    DateTimeOffset AsOf,
    IReadOnlyList<ReportAccountAmountDto> Assets,
    IReadOnlyList<ReportAccountAmountDto> Liabilities,
    IReadOnlyList<ReportAccountAmountDto> Equity,
    decimal TotalAssets,
    decimal TotalLiabilities,
    decimal TotalEquity,
    decimal RetainedEarnings,
    decimal TotalLiabilitiesAndEquity);

public sealed record CashFlowLineDto(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    Guid JournalEntryId,
    string EntryNumber,
    DateTimeOffset EntryDate,
    string? Memo,
    string? Description,
    decimal Debit,
    decimal Credit,
    decimal NetChange);

public sealed record AgingRowDto(
    Guid PartyId,
    string PartyName,
    string DocumentNumber,
    Guid DocumentId,
    DateTimeOffset AnchorDate,
    int DaysPastDue,
    decimal BalanceDue,
    string Bucket);

public sealed record AgingReportDto(
    DateTimeOffset AsOf,
    IReadOnlyList<AgingRowDto> Rows,
    decimal Current,
    decimal Days1To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Days90Plus,
    decimal Total);

public sealed record CustomerStatementLineDto(
    DateTimeOffset Date,
    string Type,
    string Reference,
    string? Description,
    decimal Debit,
    decimal Credit,
    decimal Balance);

public sealed record CustomerStatementDto(
    Guid CustomerId,
    string CustomerName,
    DateTimeOffset From,
    DateTimeOffset To,
    decimal OpeningBalance,
    decimal ClosingBalance,
    IReadOnlyList<CustomerStatementLineDto> Lines);

public sealed record ReconciliationCheckDto(
    string Code,
    string Status,
    string Message,
    decimal? Expected,
    decimal? Actual);

public interface IAccountingReportService
{
    Task<Result<IReadOnlyList<GeneralLedgerLineDto>>> GeneralLedgerAsync(
        DateTimeOffset fromDate, DateTimeOffset toDate, Guid? accountId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<TrialBalanceRowDto>>> TrialBalanceAsync(DateTimeOffset asOf, CancellationToken ct = default);
    Task<Result<ProfitAndLossDto>> ProfitAndLossAsync(DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken ct = default);
    Task<Result<BalanceSheetDto>> BalanceSheetAsync(DateTimeOffset asOf, CancellationToken ct = default);
    Task<Result<IReadOnlyList<CashFlowLineDto>>> CashFlowAsync(DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken ct = default);
    Task<Result<AgingReportDto>> ArAgingAsync(DateTimeOffset asOf, CancellationToken ct = default);
    Task<Result<AgingReportDto>> ApAgingAsync(DateTimeOffset asOf, CancellationToken ct = default);
    Task<Result<CustomerStatementDto>> CustomerStatementAsync(
        Guid customerId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ReconciliationCheckDto>>> RunReconciliationAsync(DateTimeOffset asOf, CancellationToken ct = default);
}

public sealed class AccountingReportService : IAccountingReportService
{
    private const decimal Tolerance = 0.01m;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _user;

    public AccountingReportService(IApplicationDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task<Result<IReadOnlyList<GeneralLedgerLineDto>>> GeneralLedgerAsync(
        DateTimeOffset fromDate, DateTimeOffset toDate, Guid? accountId, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<IReadOnlyList<GeneralLedgerLineDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);
        if (toDate < fromDate) return Result<IReadOnlyList<GeneralLedgerLineDto>>.Failure(ErrorCodes.Validation, "Invalid date range.");

        var orgId = _user.OrganizationId!.Value;
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.OrganizationId == orgId && (accountId == null || a.Id == accountId))
            .ToDictionaryAsync(a => a.Id, ct);

        var lines = await (
            from l in _db.JournalLines.AsNoTracking()
            join j in _db.JournalEntries.AsNoTracking() on l.JournalEntryId equals j.Id
            where j.OrganizationId == orgId
                  && j.Status == JournalStatuses.Posted
                  && j.EntryDate >= fromDate
                  && j.EntryDate <= toDate
                  && (accountId == null || l.AccountId == accountId)
            orderby l.AccountId, j.EntryDate, j.EntryNumber, l.Id
            select new { l, j }).ToListAsync(ct);

        var opening = await (
            from l in _db.JournalLines.AsNoTracking()
            join j in _db.JournalEntries.AsNoTracking() on l.JournalEntryId equals j.Id
            where j.OrganizationId == orgId
                  && j.Status == JournalStatuses.Posted
                  && j.EntryDate < fromDate
                  && (accountId == null || l.AccountId == accountId)
            group l by l.AccountId into g
            select new { AccountId = g.Key, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) }
        ).ToDictionaryAsync(x => x.AccountId, ct);

        var running = new Dictionary<Guid, decimal>();
        foreach (var a in accounts.Values)
        {
            opening.TryGetValue(a.Id, out var o);
            var debit = o?.Debit ?? 0m;
            var credit = o?.Credit ?? 0m;
            running[a.Id] = SignedBalance(a.NormalBalance, debit, credit);
        }

        var result = new List<GeneralLedgerLineDto>();
        foreach (var row in lines)
        {
            if (!accounts.TryGetValue(row.l.AccountId, out var account)) continue;
            running.TryGetValue(account.Id, out var bal);
            bal += SignedMovement(account.NormalBalance, row.l.Debit, row.l.Credit);
            running[account.Id] = bal;
            result.Add(new GeneralLedgerLineDto(
                account.Id, account.Code, account.Name,
                row.j.Id, row.j.EntryNumber, row.j.EntryDate, row.l.Description,
                row.l.Debit, row.l.Credit, Math.Round(bal, 2)));
        }

        return Result<IReadOnlyList<GeneralLedgerLineDto>>.Success(result);
    }

    public async Task<Result<IReadOnlyList<TrialBalanceRowDto>>> TrialBalanceAsync(DateTimeOffset asOf, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<IReadOnlyList<TrialBalanceRowDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var totals = await PostedTotalsThroughAsync(orgId, asOf, ct);
        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.OrganizationId == orgId)
            .OrderBy(a => a.Code)
            .ToListAsync(ct);

        var rows = accounts.Select(a =>
        {
            totals.TryGetValue(a.Id, out var t);
            var debit = t.Debit;
            var credit = t.Credit;
            var balance = SignedBalance(a.NormalBalance, debit, credit);
            return new TrialBalanceRowDto(a.Id, a.Code, a.Name, a.AccountType, debit, credit, Math.Round(balance, 2));
        }).Where(r => r.DebitTotal != 0 || r.CreditTotal != 0 || r.Balance != 0).ToList();

        return Result<IReadOnlyList<TrialBalanceRowDto>>.Success(rows);
    }

    public async Task<Result<ProfitAndLossDto>> ProfitAndLossAsync(DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<ProfitAndLossDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);
        if (toDate < fromDate) return Result<ProfitAndLossDto>.Failure(ErrorCodes.Validation, "Invalid date range.");

        var orgId = _user.OrganizationId!.Value;
        var accounts = await _db.Accounts.AsNoTracking().Where(a => a.OrganizationId == orgId).ToListAsync(ct);
        var totals = await PostedTotalsInRangeAsync(orgId, fromDate, toDate, ct);

        var revenue = AmountsForTypes(accounts, totals, AccountTypes.Revenue, NormalBalances.Credit);
        var cogs = AmountsForTypes(accounts, totals, AccountTypes.CostOfGoodsSold, NormalBalances.Debit);
        var expenses = AmountsForTypes(accounts, totals, AccountTypes.Expense, NormalBalances.Debit);
        var totalRevenue = revenue.Sum(x => x.Amount);
        var totalCogs = cogs.Sum(x => x.Amount);
        var totalExpenses = expenses.Sum(x => x.Amount);

        return Result<ProfitAndLossDto>.Success(new ProfitAndLossDto(
            fromDate, toDate, revenue, cogs, expenses,
            totalRevenue, totalCogs, totalExpenses,
            Math.Round(totalRevenue - totalCogs - totalExpenses, 2)));
    }

    public async Task<Result<BalanceSheetDto>> BalanceSheetAsync(DateTimeOffset asOf, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<BalanceSheetDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var accounts = await _db.Accounts.AsNoTracking().Where(a => a.OrganizationId == orgId).ToListAsync(ct);
        var totals = await PostedTotalsThroughAsync(orgId, asOf, ct);

        var assets = AmountsForTypes(accounts, totals, AccountTypes.Asset, NormalBalances.Debit);
        var liabilities = AmountsForTypes(accounts, totals, AccountTypes.Liability, NormalBalances.Credit);
        var equity = AmountsForTypes(accounts, totals, AccountTypes.Equity, NormalBalances.Credit);

        var revenue = AmountsForTypes(accounts, totals, AccountTypes.Revenue, NormalBalances.Credit).Sum(x => x.Amount);
        var cogs = AmountsForTypes(accounts, totals, AccountTypes.CostOfGoodsSold, NormalBalances.Debit).Sum(x => x.Amount);
        var expenses = AmountsForTypes(accounts, totals, AccountTypes.Expense, NormalBalances.Debit).Sum(x => x.Amount);
        var retained = Math.Round(revenue - cogs - expenses, 2);

        var totalAssets = assets.Sum(x => x.Amount);
        var totalLiabilities = liabilities.Sum(x => x.Amount);
        var totalEquity = Math.Round(equity.Sum(x => x.Amount) + retained, 2);

        return Result<BalanceSheetDto>.Success(new BalanceSheetDto(
            asOf, assets, liabilities, equity,
            totalAssets, totalLiabilities, totalEquity, retained,
            Math.Round(totalLiabilities + totalEquity, 2)));
    }

    public async Task<Result<IReadOnlyList<CashFlowLineDto>>> CashFlowAsync(
        DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<IReadOnlyList<CashFlowLineDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);
        if (toDate < fromDate) return Result<IReadOnlyList<CashFlowLineDto>>.Failure(ErrorCodes.Validation, "Invalid date range.");

        var orgId = _user.OrganizationId!.Value;
        var cashKeys = new[] { MappingKeys.Cash, MappingKeys.Bank, MappingKeys.CardClearing };
        var cashAccountIds = await _db.AccountingAccountMappings.AsNoTracking()
            .Where(m => m.OrganizationId == orgId && cashKeys.Contains(m.MappingKey))
            .Select(m => m.AccountId)
            .Distinct()
            .ToListAsync(ct);

        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => cashAccountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var rows = await (
            from l in _db.JournalLines.AsNoTracking()
            join j in _db.JournalEntries.AsNoTracking() on l.JournalEntryId equals j.Id
            where j.OrganizationId == orgId
                  && j.Status == JournalStatuses.Posted
                  && j.EntryDate >= fromDate
                  && j.EntryDate <= toDate
                  && cashAccountIds.Contains(l.AccountId)
            orderby j.EntryDate, j.EntryNumber
            select new { l, j }).ToListAsync(ct);

        var result = rows.Select(r =>
        {
            accounts.TryGetValue(r.l.AccountId, out var a);
            var net = Math.Round(r.l.Debit - r.l.Credit, 2);
            return new CashFlowLineDto(
                r.l.AccountId, a?.Code ?? "", a?.Name ?? "",
                r.j.Id, r.j.EntryNumber, r.j.EntryDate, r.j.Memo, r.l.Description,
                r.l.Debit, r.l.Credit, net);
        }).ToList();

        return Result<IReadOnlyList<CashFlowLineDto>>.Success(result);
    }

    public async Task<Result<AgingReportDto>> ArAgingAsync(DateTimeOffset asOf, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<AgingReportDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var invoices = await (
            from i in _db.Invoices.AsNoTracking()
            join s in _db.Sales.AsNoTracking() on i.SaleId equals s.Id
            join c in _db.Customers.AsNoTracking() on s.CustomerId equals c.Id into cj
            from c in cj.DefaultIfEmpty()
            where i.OrganizationId == orgId
                  && i.BalanceDue > 0
                  && i.Status != InvoiceStatuses.Voided
                  && i.IssuedAt <= asOf
            select new
            {
                i.Id,
                i.InvoiceNumber,
                i.BalanceDue,
                Anchor = i.DueAt ?? i.IssuedAt,
                PartyId = s.CustomerId ?? Guid.Empty,
                PartyName = c != null ? c.Name : "(Walk-in)"
            }).ToListAsync(ct);

        return Result<AgingReportDto>.Success(BuildAging(asOf, invoices.Select(x =>
            (x.PartyId, x.PartyName, x.InvoiceNumber, x.Id, x.Anchor, x.BalanceDue))));
    }

    public async Task<Result<AgingReportDto>> ApAgingAsync(DateTimeOffset asOf, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<AgingReportDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var bills = await (
            from b in _db.SupplierBills.AsNoTracking()
            join s in _db.Suppliers.AsNoTracking() on b.SupplierId equals s.Id
            where b.OrganizationId == orgId
                  && b.BalanceDue > 0
                  && b.Status != SupplierBillStatuses.Voided
                  && b.IssuedAt <= asOf
            select new
            {
                b.Id,
                b.BillNumber,
                b.BalanceDue,
                Anchor = b.IssuedAt,
                PartyId = b.SupplierId,
                PartyName = s.Name
            }).ToListAsync(ct);

        return Result<AgingReportDto>.Success(BuildAging(asOf, bills.Select(x =>
            (x.PartyId, x.PartyName, x.BillNumber, x.Id, x.Anchor, x.BalanceDue))));
    }

    public async Task<Result<CustomerStatementDto>> CustomerStatementAsync(
        Guid customerId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<CustomerStatementDto>.Failure(auth.ErrorCode!, auth.ErrorMessage!);
        if (toDate < fromDate) return Result<CustomerStatementDto>.Failure(ErrorCodes.Validation, "Invalid date range.");

        var orgId = _user.OrganizationId!.Value;
        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.OrganizationId == orgId, ct);
        if (customer is null) return Result<CustomerStatementDto>.Failure(ErrorCodes.NotFound, "Customer not found.");

        var saleIds = await _db.Sales.AsNoTracking()
            .Where(s => s.OrganizationId == orgId && s.CustomerId == customerId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.OrganizationId == orgId && saleIds.Contains(i.SaleId) && i.Status != InvoiceStatuses.Voided)
            .ToListAsync(ct);
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.OrganizationId == orgId && saleIds.Contains(p.SaleId) && p.Status == PaymentStatuses.Succeeded)
            .ToListAsync(ct);

        decimal opening = 0;
        foreach (var inv in invoices.Where(i => i.IssuedAt < fromDate))
            opening += inv.TotalAmount;
        foreach (var p in payments.Where(p => p.PaidAt < fromDate))
            opening -= p.Amount;
        opening = Math.Round(opening, 2);

        var events = new List<(DateTimeOffset Date, string Type, string Ref, string? Desc, decimal Debit, decimal Credit)>();
        foreach (var inv in invoices.Where(i => i.IssuedAt >= fromDate && i.IssuedAt <= toDate))
            events.Add((inv.IssuedAt, "Invoice", inv.InvoiceNumber, "Invoice issued", inv.TotalAmount, 0));
        foreach (var p in payments.Where(p => p.PaidAt >= fromDate && p.PaidAt <= toDate))
            events.Add((p.PaidAt, "Payment", p.IdempotencyKey, $"Payment {p.MethodCode}", 0, p.Amount));

        var balance = opening;
        var lines = new List<CustomerStatementLineDto>();
        foreach (var e in events.OrderBy(x => x.Date).ThenBy(x => x.Type))
        {
            balance = Math.Round(balance + e.Debit - e.Credit, 2);
            lines.Add(new CustomerStatementLineDto(e.Date, e.Type, e.Ref, e.Desc, e.Debit, e.Credit, balance));
        }

        return Result<CustomerStatementDto>.Success(new CustomerStatementDto(
            customer.Id, customer.Name, fromDate, toDate, opening, balance, lines));
    }

    public async Task<Result<IReadOnlyList<ReconciliationCheckDto>>> RunReconciliationAsync(
        DateTimeOffset asOf, CancellationToken ct = default)
    {
        var auth = AuthorizationGuard.Require(_user, Permissions.AccountingRead);
        if (!auth.IsSuccess) return Result<IReadOnlyList<ReconciliationCheckDto>>.Failure(auth.ErrorCode!, auth.ErrorMessage!);

        var orgId = _user.OrganizationId!.Value;
        var checks = new List<ReconciliationCheckDto>();
        var maps = await AccountingLineBuilders.LoadMapsAsync(_db, orgId, ct);
        var totals = await PostedTotalsThroughAsync(orgId, asOf, ct);

        var debitSum = totals.Values.Sum(t => t.Debit);
        var creditSum = totals.Values.Sum(t => t.Credit);
        checks.Add(Check("TRIAL_BALANCE", debitSum, creditSum, "Posted journal debits should equal credits."));

        if (maps.TryGetValue(MappingKeys.SalesRevenue, out var revenueId))
        {
            totals.TryGetValue(revenueId, out var rev);
            var salesTotal = await _db.Sales.AsNoTracking()
                .Where(s => s.OrganizationId == orgId
                            && s.Status == SaleStatuses.Completed
                            && s.CompletedAt != null
                            && s.CompletedAt <= asOf)
                .SumAsync(s => (decimal?)s.TotalAmount, ct) ?? 0m;
            checks.Add(Check("SALES_VS_REVENUE", salesTotal, rev.Credit - rev.Debit,
                "Completed sale totals should approximate sales revenue credits."));
        }
        else
        {
            checks.Add(new ReconciliationCheckDto("SALES_VS_REVENUE", "RequiresAttention",
                "SalesRevenue mapping is missing.", null, null));
        }

        if (maps.TryGetValue(MappingKeys.InventoryAsset, out var invId))
        {
            totals.TryGetValue(invId, out var invGl);
            var invGlBal = invGl.Debit - invGl.Credit;
            var onHandByPart = await _db.StockLedgerEntries.AsNoTracking()
                .Where(e => e.OrganizationId == orgId && e.CreatedAt <= asOf)
                .GroupBy(e => e.PartId)
                .Select(g => new { PartId = g.Key, Qty = g.Sum(x => x.QuantityDelta) })
                .ToListAsync(ct);
            var partIds = onHandByPart.Select(x => x.PartId).ToList();
            var costs = await _db.Parts.AsNoTracking()
                .Where(p => p.OrganizationId == orgId && partIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.UnitCost, ct);
            var stockValue = onHandByPart.Sum(x =>
            {
                costs.TryGetValue(x.PartId, out var cost);
                return x.Qty * cost;
            });
            checks.Add(Check("INVENTORY_GL_VS_STOCK", Math.Round(stockValue, 2), Math.Round(invGlBal, 2),
                "Inventory GL balance should match part cost × on-hand."));
        }
        else
        {
            checks.Add(new ReconciliationCheckDto("INVENTORY_GL_VS_STOCK", "RequiresAttention",
                "InventoryAsset mapping is missing.", null, null));
        }

        if (maps.TryGetValue(MappingKeys.AccountsReceivable, out var arId))
        {
            totals.TryGetValue(arId, out var arGl);
            var arGlBal = arGl.Debit - arGl.Credit;
            var arOpen = await _db.Invoices.AsNoTracking()
                .Where(i => i.OrganizationId == orgId
                            && i.BalanceDue > 0
                            && i.Status != InvoiceStatuses.Voided
                            && i.IssuedAt <= asOf)
                .SumAsync(i => (decimal?)i.BalanceDue, ct) ?? 0m;
            checks.Add(Check("AR_GL_VS_INVOICES", arOpen, arGlBal, "AR GL should match open invoice balances."));
        }
        else
        {
            checks.Add(new ReconciliationCheckDto("AR_GL_VS_INVOICES", "RequiresAttention",
                "AccountsReceivable mapping is missing.", null, null));
        }

        if (maps.TryGetValue(MappingKeys.AccountsPayable, out var apId))
        {
            totals.TryGetValue(apId, out var apGl);
            var apGlBal = apGl.Credit - apGl.Debit;
            var apOpen = await _db.SupplierBills.AsNoTracking()
                .Where(b => b.OrganizationId == orgId
                            && b.BalanceDue > 0
                            && b.Status != SupplierBillStatuses.Voided
                            && b.IssuedAt <= asOf)
                .SumAsync(b => (decimal?)b.BalanceDue, ct) ?? 0m;
            checks.Add(Check("AP_GL_VS_BILLS", apOpen, apGlBal, "AP GL should match open supplier bill balances."));
        }
        else
        {
            checks.Add(new ReconciliationCheckDto("AP_GL_VS_BILLS", "RequiresAttention",
                "AccountsPayable mapping is missing.", null, null));
        }

        var paymentTotal = await _db.Payments.AsNoTracking()
            .Where(p => p.OrganizationId == orgId
                        && p.Status == PaymentStatuses.Succeeded
                        && p.PaidAt <= asOf)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var cashKeys = new[] { MappingKeys.Cash, MappingKeys.Bank, MappingKeys.CardClearing };
        var cashIds = maps.Where(kv => cashKeys.Contains(kv.Key)).Select(kv => kv.Value).ToHashSet();
        decimal cashIn = 0, arCredits = 0;
        if (maps.TryGetValue(MappingKeys.AccountsReceivable, out var arMapId))
        {
            foreach (var (accountId, t) in totals)
            {
                if (cashIds.Contains(accountId)) cashIn += t.Debit;
                if (accountId == arMapId) arCredits += t.Credit;
            }
        }
        checks.Add(Check("PAYMENTS_VS_CASH_AR", paymentTotal, Math.Min(cashIn, arCredits),
            "Customer payments should align with cash inflows / AR credits (sampled)."));

        return Result<IReadOnlyList<ReconciliationCheckDto>>.Success(checks);
    }

    private async Task<Dictionary<Guid, (decimal Debit, decimal Credit)>> PostedTotalsThroughAsync(
        Guid orgId, DateTimeOffset asOf, CancellationToken ct) =>
        await (
            from l in _db.JournalLines.AsNoTracking()
            join j in _db.JournalEntries.AsNoTracking() on l.JournalEntryId equals j.Id
            where j.OrganizationId == orgId && j.Status == JournalStatuses.Posted && j.EntryDate <= asOf
            group l by l.AccountId into g
            select new { AccountId = g.Key, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) }
        ).ToDictionaryAsync(x => x.AccountId, x => (x.Debit, x.Credit), ct);

    private async Task<Dictionary<Guid, (decimal Debit, decimal Credit)>> PostedTotalsInRangeAsync(
        Guid orgId, DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken ct) =>
        await (
            from l in _db.JournalLines.AsNoTracking()
            join j in _db.JournalEntries.AsNoTracking() on l.JournalEntryId equals j.Id
            where j.OrganizationId == orgId
                  && j.Status == JournalStatuses.Posted
                  && j.EntryDate >= fromDate
                  && j.EntryDate <= toDate
            group l by l.AccountId into g
            select new { AccountId = g.Key, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) }
        ).ToDictionaryAsync(x => x.AccountId, x => (x.Debit, x.Credit), ct);

    private static List<ReportAccountAmountDto> AmountsForTypes(
        List<Account> accounts,
        Dictionary<Guid, (decimal Debit, decimal Credit)> totals,
        string accountType,
        string normalBalance)
    {
        return accounts
            .Where(a => a.AccountType == accountType)
            .OrderBy(a => a.Code)
            .Select(a =>
            {
                totals.TryGetValue(a.Id, out var t);
                var amount = Math.Round(SignedBalance(normalBalance, t.Debit, t.Credit), 2);
                return new ReportAccountAmountDto(a.Id, a.Code, a.Name, amount);
            })
            .Where(x => x.Amount != 0)
            .ToList();
    }

    private static AgingReportDto BuildAging(
        DateTimeOffset asOf,
        IEnumerable<(Guid PartyId, string PartyName, string DocNumber, Guid DocId, DateTimeOffset Anchor, decimal Balance)> items)
    {
        decimal current = 0, d1 = 0, d31 = 0, d61 = 0, d90 = 0;
        var rows = new List<AgingRowDto>();
        foreach (var item in items)
        {
            var days = Math.Max(0, (int)(asOf.Date - item.Anchor.UtcDateTime.Date).TotalDays);
            var bucket = days switch
            {
                0 => "Current",
                <= 30 => "1-30",
                <= 60 => "31-60",
                <= 90 => "61-90",
                _ => "90+"
            };
            switch (bucket)
            {
                case "Current": current += item.Balance; break;
                case "1-30": d1 += item.Balance; break;
                case "31-60": d31 += item.Balance; break;
                case "61-90": d61 += item.Balance; break;
                default: d90 += item.Balance; break;
            }
            rows.Add(new AgingRowDto(item.PartyId, item.PartyName, item.DocNumber, item.DocId, item.Anchor, days, item.Balance, bucket));
        }

        return new AgingReportDto(
            asOf, rows.OrderByDescending(r => r.DaysPastDue).ToList(),
            Math.Round(current, 2), Math.Round(d1, 2), Math.Round(d31, 2), Math.Round(d61, 2), Math.Round(d90, 2),
            Math.Round(current + d1 + d31 + d61 + d90, 2));
    }

    private static ReconciliationCheckDto Check(string code, decimal expected, decimal actual, string message)
    {
        var ok = Math.Abs(Math.Round(expected - actual, 2)) <= Tolerance;
        return new ReconciliationCheckDto(
            code,
            ok ? "Ok" : "RequiresAttention",
            ok ? message : $"{message} Mismatch detected.",
            Math.Round(expected, 2),
            Math.Round(actual, 2));
    }

    private static decimal SignedBalance(string normalBalance, decimal debit, decimal credit) =>
        normalBalance == NormalBalances.Credit ? credit - debit : debit - credit;

    private static decimal SignedMovement(string normalBalance, decimal debit, decimal credit) =>
        SignedBalance(normalBalance, debit, credit);
}
