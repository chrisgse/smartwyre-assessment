using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Calculators;

public sealed class FixedRateRebateCalculator : IRebateCalculator
{
    public IncentiveType Incentive => IncentiveType.FixedRateRebate;
    public RebateInputRequirements InputRequirements { get; } = new(Percentage: true, Price: true, Volume: true);

    public decimal? Calculate(Rebate rebate, Product product, decimal volume)
    {
        // Preserve the starter's zero checks; negative values remain eligible.
        if (!InputRequirements.IsSatisfiedBy(rebate, product, volume) ||
            !product.SupportedIncentives.HasFlag(SupportedIncentiveType.FixedRateRebate))
        {
            return null;
        }

        // Percentage is fractional (0.10 means 10%); no currency rounding is introduced.
        return product.Price * rebate.Percentage * volume;
    }
}
