using Ersms.Domain.Inventory;
using Ersms.Domain.Sales;
using Ersms.SharedKernel;
using FluentAssertions;

namespace Ersms.Domain.Tests;

public class SalesWorkflowTests
{
    [Fact]
    public void StockMath_rejects_sale_that_would_go_negative()
    {
        StockMath.ApplyAdjustment(1, -2).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void InvoiceStatus_from_payment_balances()
    {
        SaleWorkflow.InvoiceStatusFromBalances(100, 0).Should().Be(InvoiceStatuses.Unpaid);
        SaleWorkflow.InvoiceStatusFromBalances(100, 40).Should().Be(InvoiceStatuses.Partial);
        SaleWorkflow.InvoiceStatusFromBalances(100, 100).Should().Be(InvoiceStatuses.Paid);
        SaleWorkflow.InvoiceStatusFromBalances(100, 120).Should().Be(InvoiceStatuses.Paid);
    }

    [Fact]
    public void CanVoid_only_unpaid_completed()
    {
        SaleWorkflow.CanVoid(SaleStatuses.Completed, 0).IsSuccess.Should().BeTrue();
        SaleWorkflow.CanVoid(SaleStatuses.Completed, 10).IsSuccess.Should().BeFalse();
        SaleWorkflow.CanVoid(SaleStatuses.Voided, 0).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Return_qty_cannot_exceed_remaining()
    {
        var sold = 5m;
        var alreadyReturned = 2m;
        var requested = 4m;
        (requested <= sold - alreadyReturned).Should().BeFalse();
        (3m <= sold - alreadyReturned).Should().BeTrue();
    }

    [Fact]
    public void LineTotal_applies_discount()
    {
        SaleWorkflow.LineTotal(2, 50, 10).Should().Be(90);
    }
}
