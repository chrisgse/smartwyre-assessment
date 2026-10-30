using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Calculators;

public sealed class AmountPerUomCalculator : IRebateCalculator
{
    public IncentiveType Incentive => IncentiveType.AmountPerUom;
    public RebateInputRequirements InputRequirements { get; } = new(Amount: true, Volume: true);

    public decimal? Calculate(Rebate rebate, Product product, decimal volume)
    {
        if (!InputRequirements.IsSatisfiedBy(rebate, product, volume) ||
            !product.SupportedIncentives.HasFlag(SupportedIncentiveType.AmountPerUom))
        {
            return null;
        }

        // The amount is per unit; this formula does not use price or convert units.
        return rebate.Amount * volume;
    }
}
