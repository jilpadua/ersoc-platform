using Ersms.SharedKernel;

namespace Ersms.Domain.Accounting;

public static class AccountTypes
{
    public const string Asset = "Asset";
    public const string Liability = "Liability";
    public const string Equity = "Equity";
    public const string Revenue = "Revenue";
    public const string CostOfGoodsSold = "CostOfGoodsSold";
    public const string Expense = "Expense";
}

public static class NormalBalances
{
    public const string Debit = "Debit";
    public const string Credit = "Credit";
}

public static class PeriodStatuses
{
    public const string Open = "Open";
    public const string Closed = "Closed";
}

public static class JournalStatuses
{
    public const string Posted = "Posted";
}

public static class AccountingSourceTypes
{
    public const string SaleCompleted = "SaleCompleted";
    public const string PaymentSucceeded = "PaymentSucceeded";
    public const string SaleReturnCompleted = "SaleReturnCompleted";
    public const string SaleVoided = "SaleVoided";
    public const string PurchaseReceived = "PurchaseReceived";
    public const string StockAdjusted = "StockAdjusted";
    public const string SupplierPayment = "SupplierPayment";
    public const string ExpensePosted = "ExpensePosted";
    public const string ExpenseVoided = "ExpenseVoided";
    public const string ManualJournal = "ManualJournal";
    public const string OpeningBalance = "OpeningBalance";
}

public static class MappingKeys
{
    public const string Cash = "Cash";
    public const string Bank = "Bank";
    public const string CardClearing = "CardClearing";
    public const string AccountsReceivable = "AccountsReceivable";
    public const string InventoryAsset = "InventoryAsset";
    public const string AccountsPayable = "AccountsPayable";
    public const string OpeningEquity = "OpeningEquity";
    public const string SalesRevenue = "SalesRevenue";
    public const string Cogs = "Cogs";
    public const string InventoryAdjustment = "InventoryAdjustment";
    public const string OperatingExpense = "OperatingExpense";
}

public static class SupplierBillStatuses
{
    public const string Open = "Open";
    public const string Partial = "Partial";
    public const string Paid = "Paid";
    public const string Voided = "Voided";
}

public static class ExpenseStatuses
{
    public const string Draft = "Draft";
    public const string Approved = "Approved";
    public const string Posted = "Posted";
    public const string Voided = "Voided";
}

public class Account : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = AccountTypes.Asset;
    public string NormalBalance { get; set; } = NormalBalances.Debit;
    public Guid? ParentAccountId { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AccountingPeriod : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = PeriodStatuses.Open;
}

public class JournalEntry : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid PeriodId { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public DateTimeOffset EntryDate { get; set; }
    public DateTimeOffset PostedAt { get; set; }
    public Guid PostedByUserId { get; set; }
    public string? Memo { get; set; }
    public string Status { get; set; } = JournalStatuses.Posted;
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public Guid? ReversesJournalEntryId { get; set; }

    public AccountingPeriod? Period { get; set; }
    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();
}

public class JournalLine : Entity
{
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }

    public JournalEntry? JournalEntry { get; set; }
    public Account? Account { get; set; }
}

public class AccountingAccountMapping : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public string MappingKey { get; set; } = string.Empty;
    public Guid AccountId { get; set; }

    public Account? Account { get; set; }
}

public class SupplierBill : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid? BranchId { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public Guid? SourceReceiveId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string Status { get; set; } = SupplierBillStatuses.Open;
    public DateTimeOffset IssuedAt { get; set; }
    public string? Notes { get; set; }
}

public class SupplierPayment : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid SupplierId { get; set; }
    public decimal Amount { get; set; }
    public string MethodCode { get; set; } = string.Empty;
    public DateTimeOffset PaidAt { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public string? Notes { get; set; }

    public ICollection<SupplierPaymentAllocation> Allocations { get; set; } = new List<SupplierPaymentAllocation>();
}

public class SupplierPaymentAllocation : Entity
{
    public Guid SupplierPaymentId { get; set; }
    public Guid SupplierBillId { get; set; }
    public decimal Amount { get; set; }

    public SupplierPayment? SupplierPayment { get; set; }
    public SupplierBill? SupplierBill { get; set; }
}

public class ExpenseCategory : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public bool IsActive { get; set; } = true;

    public Account? Account { get; set; }
}

public class Expense : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset ExpenseDate { get; set; }
    public string? Payee { get; set; }
    public string? MethodCode { get; set; }
    public bool Payable { get; set; }
    public string Status { get; set; } = ExpenseStatuses.Draft;
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? PostedAt { get; set; }

    public ExpenseCategory? Category { get; set; }
    public ICollection<ExpenseAttachment> Attachments { get; set; } = new List<ExpenseAttachment>();
}

public class ExpenseAttachment : Entity
{
    public Guid ExpenseId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Expense? Expense { get; set; }
}

/// <summary>Default CoA template and mapping keys used when seeding an organization.</summary>
public static class DefaultChartOfAccounts
{
    public static readonly (string Code, string Name, string Type, string Normal, string? MappingKey)[] Accounts =
    [
        ("1000", "Cash", AccountTypes.Asset, NormalBalances.Debit, MappingKeys.Cash),
        ("1010", "Bank", AccountTypes.Asset, NormalBalances.Debit, MappingKeys.Bank),
        ("1020", "Card Clearing", AccountTypes.Asset, NormalBalances.Debit, MappingKeys.CardClearing),
        ("1100", "Accounts Receivable", AccountTypes.Asset, NormalBalances.Debit, MappingKeys.AccountsReceivable),
        ("1200", "Inventory", AccountTypes.Asset, NormalBalances.Debit, MappingKeys.InventoryAsset),
        ("2000", "Accounts Payable", AccountTypes.Liability, NormalBalances.Credit, MappingKeys.AccountsPayable),
        ("3000", "Opening Equity", AccountTypes.Equity, NormalBalances.Credit, MappingKeys.OpeningEquity),
        ("4000", "Sales Revenue", AccountTypes.Revenue, NormalBalances.Credit, MappingKeys.SalesRevenue),
        ("5000", "Cost of Goods Sold", AccountTypes.CostOfGoodsSold, NormalBalances.Debit, MappingKeys.Cogs),
        ("5100", "Inventory Adjustment", AccountTypes.Expense, NormalBalances.Debit, MappingKeys.InventoryAdjustment),
        ("6000", "Operating Expense", AccountTypes.Expense, NormalBalances.Debit, MappingKeys.OperatingExpense),
    ];
}

public static class JournalMath
{
    public static Result ValidateBalanced(IEnumerable<(decimal Debit, decimal Credit)> lines)
    {
        var list = lines.ToList();
        if (list.Count < 2)
            return Result.Failure(ErrorCodes.Validation, "Journal requires at least two lines.");

        decimal debits = 0, credits = 0;
        foreach (var (debit, credit) in list)
        {
            if (debit < 0 || credit < 0)
                return Result.Failure(ErrorCodes.Validation, "Debit and credit must be non-negative.");
            if (debit > 0 && credit > 0)
                return Result.Failure(ErrorCodes.Validation, "A line cannot have both debit and credit.");
            if (debit == 0 && credit == 0)
                return Result.Failure(ErrorCodes.Validation, "A line must have a debit or credit amount.");
            debits += debit;
            credits += credit;
        }

        if (Math.Round(debits, 2) != Math.Round(credits, 2))
            return Result.Failure(ErrorCodes.Validation, $"Journal is unbalanced: debits {debits:F2} != credits {credits:F2}.");

        return Result.Success();
    }

    public static IReadOnlyList<(Guid AccountId, decimal Debit, decimal Credit, string? Description)> ReverseLines(
        IEnumerable<(Guid AccountId, decimal Debit, decimal Credit, string? Description)> lines)
        => lines.Select(l => (l.AccountId, l.Credit, l.Debit, l.Description)).ToList();
}
