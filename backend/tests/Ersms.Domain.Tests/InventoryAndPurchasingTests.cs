using Ersms.Domain.Inventory;
using Ersms.Domain.Purchasing;
using Ersms.SharedKernel;
using FluentAssertions;

namespace Ersms.Domain.Tests;

public class InventoryAndPurchasingTests
{
    [Fact]
    public void StockMath_rejects_negative_on_hand()
    {
        var result = StockMath.ApplyAdjustment(2, -3);
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Conflict);
    }

    [Fact]
    public void StockMath_allows_valid_adjustment()
    {
        StockMath.ApplyAdjustment(2, -1).Value.Should().Be(1);
        StockMath.ApplyAdjustment(0, 5).Value.Should().Be(5);
    }

    [Fact]
    public void PurchaseOrderWorkflow_submit_only_from_draft()
    {
        PurchaseOrderWorkflow.CanSubmit(PurchaseOrderStatuses.Draft).IsSuccess.Should().BeTrue();
        PurchaseOrderWorkflow.CanSubmit(PurchaseOrderStatuses.Ordered).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void PurchaseOrderWorkflow_cancel_rules()
    {
        PurchaseOrderWorkflow.CanCancel(PurchaseOrderStatuses.Draft).IsSuccess.Should().BeTrue();
        PurchaseOrderWorkflow.CanCancel(PurchaseOrderStatuses.Ordered).IsSuccess.Should().BeTrue();
        PurchaseOrderWorkflow.CanCancel(PurchaseOrderStatuses.Received).IsSuccess.Should().BeFalse();
        PurchaseOrderWorkflow.CanCancel(PurchaseOrderStatuses.PartiallyReceived).IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void PurchaseOrderWorkflow_status_after_receive()
    {
        var lines = new List<PurchaseOrderLine>
        {
            new() { QuantityOrdered = 10, QuantityReceived = 4 },
            new() { QuantityOrdered = 5, QuantityReceived = 0 }
        };
        PurchaseOrderWorkflow.StatusAfterReceive(lines).Should().Be(PurchaseOrderStatuses.PartiallyReceived);

        lines[0].QuantityReceived = 10;
        lines[1].QuantityReceived = 5;
        PurchaseOrderWorkflow.StatusAfterReceive(lines).Should().Be(PurchaseOrderStatuses.Received);
    }
}
