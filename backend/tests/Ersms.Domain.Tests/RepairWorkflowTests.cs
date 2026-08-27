using Ersms.Domain.Repairs;
using Ersms.SharedKernel;
using FluentAssertions;

namespace Ersms.Domain.Tests;

public class RepairWorkflowTests
{
    [Theory]
    [InlineData("RECEIVED", "DIAGNOSIS")]
    [InlineData("DIAGNOSIS", "WAITING_FOR_APPROVAL")]
    [InlineData("WAITING_FOR_APPROVAL", "APPROVED")]
    [InlineData("APPROVED", "REPAIRING")]
    [InlineData("REPAIRING", "TESTING")]
    [InlineData("TESTING", "READY_FOR_PICKUP")]
    [InlineData("READY_FOR_PICKUP", "COMPLETED")]
    [InlineData("RECEIVED", "CANCELLED")]
    public void Allows_valid_transitions(string from, string to)
    {
        RepairWorkflow.CanTransition(from, to).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("RECEIVED", "COMPLETED")]
    [InlineData("COMPLETED", "REPAIRING")]
    [InlineData("CANCELLED", "DIAGNOSIS")]
    [InlineData("READY_FOR_PICKUP", "RECEIVED")]
    public void Rejects_invalid_transitions(string from, string to)
    {
        var result = RepairWorkflow.CanTransition(from, to);
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.InvalidTransition);
    }

    [Fact]
    public void Recalculates_totals_from_service_lines()
    {
        var repair = new Repair();
        repair.ServiceLines.Add(new RepairServiceLine { Quantity = 2, UnitPrice = 100, Discount = 20 });
        repair.ServiceLines.Add(new RepairServiceLine { Quantity = 1, UnitPrice = 50, Discount = 0 });
        repair.DiscountTotal = 10;
        repair.RecalculateTotals();
        repair.Subtotal.Should().Be(230);
        repair.TotalAmount.Should().Be(220);
    }

    [Theory]
    [InlineData("RECEIVED", "DIAGNOSIS")]
    [InlineData("RECEIVED", "CANCELLED")]
    public void GetAllowedNext_includes_expected_codes(string from, string expected)
    {
        RepairWorkflow.GetAllowedNext(from).Should().Contain(expected);
    }

    [Fact]
    public void GetAllowedNext_completed_is_empty()
    {
        RepairWorkflow.GetAllowedNext("COMPLETED").Should().BeEmpty();
    }

    [Fact]
    public void GetAllowedNext_repairing_prefers_testing_before_waiting_for_parts()
    {
        var next = RepairWorkflow.GetAllowedNext("REPAIRING");
        next.Should().ContainInOrder("TESTING", "WAITING_FOR_PARTS", "CANCELLED");
        next[0].Should().Be("TESTING");
    }

    [Fact]
    public void Allows_bidirectional_waiting_for_parts_and_repairing()
    {
        RepairWorkflow.CanTransition("WAITING_FOR_PARTS", "REPAIRING").IsSuccess.Should().BeTrue();
        RepairWorkflow.CanTransition("REPAIRING", "WAITING_FOR_PARTS").IsSuccess.Should().BeTrue();
    }
}
