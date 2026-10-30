using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Calculators;

// Keeps eligibility and arithmetic together, with no data-store access.
public interface IRebateCalculator
{
    IncentiveType Incentive { get; }
    RebateInputRequirements InputRequirements { get; }
    // Null means the inputs are ineligible; zero is not used as a failure sentinel.
    decimal? Calculate(Rebate rebate, Product product, decimal volume);
}
