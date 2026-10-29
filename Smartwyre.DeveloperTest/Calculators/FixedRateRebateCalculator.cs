using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Calculators;

public sealed class FixedRateRebateCalculator : IRebateCalculator
{
    public IncentiveType Incentive => IncentiveType.FixedRateRebate;

    public decimal? Calculate(Rebate rebate, Product product, decimal volume)
    {
        // Preserve the starter's zero checks; negative values remain eligible.
        if (rebate == null || product == null ||
            !product.SupportedIncentives.HasFlag(SupportedIncentiveType.FixedRateRebate) ||
            rebate.Percentage == 0 || product.Price == 0 || volume == 0)
        {
            return null;
        }

        // Percentage is fractional (0.10 means 10%); no currency rounding is introduced.
        return product.Price * rebate.Percentage * volume;
    }
}
