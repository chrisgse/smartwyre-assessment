using System.Collections.Generic;
using Smartwyre.DeveloperTest.Calculators;
using Smartwyre.DeveloperTest.Types;
using Xunit;

namespace Smartwyre.DeveloperTest.Tests;

public class RebateCalculatorTests
{
    public static IEnumerable<object[]> EligibilityCases()
    {
        foreach (var type in new[] { IncentiveType.FixedCashAmount, IncentiveType.FixedRateRebate, IncentiveType.AmountPerUom })
        {
            yield return new object[] { type, 0m, .1m, 20m, 3m, type == IncentiveType.FixedRateRebate };
            yield return new object[] { type, 10m, 0m, 20m, 3m, type != IncentiveType.FixedRateRebate };
            yield return new object[] { type, 10m, .1m, 0m, 3m, type != IncentiveType.FixedRateRebate };
            yield return new object[] { type, 10m, .1m, 20m, 0m, type == IncentiveType.FixedCashAmount };
        }
    }

    [Theory]
    [MemberData(nameof(EligibilityCases))]
    public void Only_inputs_required_by_the_incentive_must_be_nonzero(IncentiveType type,
        decimal amount, decimal rate, decimal price, decimal volume, bool expectedEligible)
    {
        var result = Calculator(type).Calculate(new Rebate { Amount = amount, Percentage = rate }, Product(price), volume);
        Assert.Equal(expectedEligible, result.HasValue);
    }

    [Theory]
    [InlineData(IncentiveType.FixedCashAmount, SupportedIncentiveType.FixedRateRebate)]
    [InlineData(IncentiveType.FixedRateRebate, SupportedIncentiveType.AmountPerUom)]
    [InlineData(IncentiveType.AmountPerUom, SupportedIncentiveType.FixedCashAmount)]
    public void Different_supported_incentive_does_not_qualify(IncentiveType type, SupportedIncentiveType supported)
    {
        Assert.Null(Calculator(type).Calculate(new Rebate { Amount = 10, Percentage = .1m }, new Product { Price = 20, SupportedIncentives = supported }, 3));
    }

    [Theory]
    [InlineData(IncentiveType.FixedCashAmount)]
    [InlineData(IncentiveType.FixedRateRebate)]
    [InlineData(IncentiveType.AmountPerUom)]
    public void Null_records_are_ineligible(IncentiveType type)
    {
        Assert.Null(Calculator(type).Calculate(null, Product(20), 3));
        Assert.Null(Calculator(type).Calculate(new Rebate(), null, 3));
    }

    [Fact]
    public void Fractional_values_retain_decimal_precision()
    {
        Assert.Equal(.9375m, new FixedRateRebateCalculator().Calculate(new Rebate { Percentage = .125m }, Product(2.5m), 3));
        Assert.Equal(3.125m, new AmountPerUomCalculator().Calculate(new Rebate { Amount = 1.25m }, Product(0), 2.5m));
    }

    [Fact]
    public void Existing_negative_value_behavior_is_preserved()
    {
        Assert.Equal(-10m, new FixedCashAmountCalculator().Calculate(new Rebate { Amount = -10 }, Product(20), 3));
        Assert.Equal(-6m, new FixedRateRebateCalculator().Calculate(new Rebate { Percentage = .1m }, Product(20), -3));
        Assert.Equal(-30m, new AmountPerUomCalculator().Calculate(new Rebate { Amount = 10 }, Product(20), -3));
    }

    private static Product Product(decimal price) => new()
    { Price = price, SupportedIncentives = SupportedIncentiveType.FixedCashAmount | SupportedIncentiveType.FixedRateRebate | SupportedIncentiveType.AmountPerUom };

    private static IRebateCalculator Calculator(IncentiveType type) => type switch
    {
        IncentiveType.FixedCashAmount => new FixedCashAmountCalculator(),
        IncentiveType.FixedRateRebate => new FixedRateRebateCalculator(),
        _ => new AmountPerUomCalculator()
    };
}
