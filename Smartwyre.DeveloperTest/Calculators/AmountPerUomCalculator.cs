using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Calculators;

public sealed class AmountPerUomCalculator : IRebateCalculator
{
    public IncentiveType Incentive => IncentiveType.AmountPerUom;

    public decimal? Calculate(Rebate rebate, Product product, decimal volume)
    {
        if (rebate == null || product == null ||
            !product.SupportedIncentives.HasFlag(SupportedIncentiveType.AmountPerUom) ||
            rebate.Amount == 0 || volume == 0)
        {
            return null;
        }

        return rebate.Amount * volume;
    }
}
