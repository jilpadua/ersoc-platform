using Ersms.SharedKernel;

namespace Ersms.Domain.Sales;

public static class SaleStatuses
{
    public const string Completed = "COMPLETED";
    public const string Voided = "VOIDED";
}

public static class InvoiceStatuses
{
    public const string Unpaid = "UNPAID";
    public const string Partial = "PARTIAL";
    public const string Paid = "PAID";
    public const string Voided = "VOIDED";
}

public static class PaymentStatuses
{
    public const string Succeeded = "Succeeded";
    public const string Refunded = "Refunded";
}

public class PaymentMethod : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class Sale : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid? CustomerId { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public string Status { get; set; } = SaleStatuses.Completed;
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public Guid CreatedByUserId { get; set; }

    public ICollection<SaleLine> Lines { get; set; } = new List<SaleLine>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public Invoice? Invoice { get; set; }
    public ICollection<SaleReturn> Returns { get; set; } = new List<SaleReturn>();
}

public class SaleLine : Entity
{
    public Guid SaleId { get; set; }
    public Guid PartId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal LineTotal { get; set; }

    public Sale? Sale { get; set; }
}

public class Invoice : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid SaleId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string Status { get; set; } = InvoiceStatuses.Unpaid;
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }

    public Sale? Sale { get; set; }
}

public class Payment : Entity
{
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid SaleId { get; set; }
    public decimal Amount { get; set; }
    public string MethodCode { get; set; } = string.Empty;
    public DateTimeOffset PaidAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ReceivedByUserId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Status { get; set; } = PaymentStatuses.Succeeded;

    public Sale? Sale { get; set; }
}

public class SaleReturn : AuditableEntity
{
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid SaleId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public decimal RefundAmount { get; set; }
    public Guid CreatedByUserId { get; set; }

    public Sale? Sale { get; set; }
    public ICollection<SaleReturnLine> Lines { get; set; } = new List<SaleReturnLine>();
}

public class SaleReturnLine : Entity
{
    public Guid SaleReturnId { get; set; }
    public Guid SaleLineId { get; set; }
    public Guid PartId { get; set; }
    public decimal Quantity { get; set; }

    public SaleReturn? SaleReturn { get; set; }
}

public static class SaleWorkflow
{
    public static string InvoiceStatusFromBalances(decimal total, decimal amountPaid)
    {
        if (amountPaid <= 0) return InvoiceStatuses.Unpaid;
        if (amountPaid >= total) return InvoiceStatuses.Paid;
        return InvoiceStatuses.Partial;
    }

    public static Result CanVoid(string status, decimal amountPaid) =>
        status == SaleStatuses.Completed && amountPaid <= 0
            ? Result.Success()
            : Result.Failure(ErrorCodes.InvalidTransition, "Only unpaid completed sales can be voided.");

    public static Result CanPay(string status) =>
        status == SaleStatuses.Completed
            ? Result.Success()
            : Result.Failure(ErrorCodes.InvalidTransition, "Payments can only be recorded on completed sales.");

    public static Result CanReturn(string status) =>
        status == SaleStatuses.Completed
            ? Result.Success()
            : Result.Failure(ErrorCodes.InvalidTransition, "Returns can only be created for completed sales.");

    public static decimal LineTotal(decimal quantity, decimal unitPrice, decimal discount) =>
        quantity * unitPrice - discount;
}
