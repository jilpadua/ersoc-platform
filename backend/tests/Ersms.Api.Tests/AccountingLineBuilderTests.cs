using Ersms.Application.Accounting;
using Ersms.Domain.Accounting;
using FluentAssertions;

namespace Ersms.Api.Tests;

public class AccountingLineBuilderTests
{
    private static Dictionary<string, Guid> FullMaps() => new()
    {
        [MappingKeys.Cash] = Guid.NewGuid(),
        [MappingKeys.Bank] = Guid.NewGuid(),
        [MappingKeys.CardClearing] = Guid.NewGuid(),
        [MappingKeys.AccountsReceivable] = Guid.NewGuid(),
        [MappingKeys.InventoryAsset] = Guid.NewGuid(),
        [MappingKeys.AccountsPayable] = Guid.NewGuid(),
        [MappingKeys.SalesRevenue] = Guid.NewGuid(),
        [MappingKeys.Cogs] = Guid.NewGuid(),
        [MappingKeys.InventoryAdjustment] = Guid.NewGuid(),
        [MappingKeys.OperatingExpense] = Guid.NewGuid(),
        [MappingKeys.OpeningEquity] = Guid.NewGuid(),
    };

    [Fact]
    public void PaymentSucceeded_fails_when_mapping_missing()
    {
        var maps = new Dictionary<string, Guid> { [MappingKeys.Cash] = Guid.NewGuid() };
        var result = AccountingLineBuilders.PaymentSucceeded(maps, 10m, "CASH");
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Missing account mapping");
        result.ErrorMessage.Should().Contain(MappingKeys.AccountsReceivable);
    }

    [Fact]
    public void PaymentSucceeded_builds_balanced_lines()
    {
        var result = AccountingLineBuilders.PaymentSucceeded(FullMaps(), 25m, "CASH");
        result.IsSuccess.Should().BeTrue();
        var lines = result.Value!;
        lines.Sum(l => l.Debit).Should().Be(lines.Sum(l => l.Credit));
    }

    [Fact]
    public void SaleCompleted_fails_when_revenue_mapping_missing()
    {
        var maps = FullMaps();
        maps.Remove(MappingKeys.SalesRevenue);
        var result = AccountingLineBuilders.SaleCompleted(maps, 100m, 100m, "CASH", 0m);
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain(MappingKeys.SalesRevenue);
    }
}
