using Ersms.Domain.Accounting;
using FluentAssertions;

namespace Ersms.Domain.Tests;

public class AccountingJournalMathTests
{
    [Fact]
    public void ValidateBalanced_accepts_equal_debits_credits()
    {
        var result = JournalMath.ValidateBalanced(
        [
            (100m, 0m),
            (0m, 100m)
        ]);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateBalanced_rejects_unbalanced()
    {
        var result = JournalMath.ValidateBalanced(
        [
            (100m, 0m),
            (0m, 90m)
        ]);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void ReverseLines_swaps_debit_and_credit()
    {
        var reversed = JournalMath.ReverseLines(
        [
            (Guid.NewGuid(), 50m, 0m, "a"),
            (Guid.NewGuid(), 0m, 50m, "b")
        ]);
        reversed[0].Debit.Should().Be(0m);
        reversed[0].Credit.Should().Be(50m);
        reversed[1].Debit.Should().Be(50m);
        reversed[1].Credit.Should().Be(0m);
    }
}
