using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Calculators;

public sealed class FixedCashAmountCalculator : IRebateCalculator
{
    public IncentiveType Incentive => IncentiveType.FixedCashAmount;

    public decimal? Calculate(Rebate rebate, Product product, decimal volume)
    {
        if (rebate == null || product == null ||
            !product.SupportedIncentives.HasFlag(SupportedIncentiveType.FixedCashAmount) ||
            rebate.Amount == 0)
        {
            return null;
        }

        return rebate.Amount;
    }
}
