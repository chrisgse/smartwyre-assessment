using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Calculators;

public interface IRebateCalculator
{
    IncentiveType Incentive { get; }
    // Null means the inputs are ineligible; zero is not used as a failure sentinel.
    decimal? Calculate(Rebate rebate, Product product, decimal volume);
}
