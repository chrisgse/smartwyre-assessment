using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Calculators;

// Identifies the fields that must be nonzero; signed values remain supported.
public sealed record RebateInputRequirements(
    bool Amount = false, bool Percentage = false, bool Price = false, bool Volume = false)
{
    public bool IsSatisfiedBy(Rebate rebate, Product product, decimal volume) =>
        rebate != null && product != null &&
        (!Amount || rebate.Amount != 0m) &&
        (!Percentage || rebate.Percentage != 0m) &&
        (!Price || product.Price != 0m) &&
        (!Volume || volume != 0m);
}
