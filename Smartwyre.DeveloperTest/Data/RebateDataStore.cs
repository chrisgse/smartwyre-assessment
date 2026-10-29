using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Data;

public class RebateDataStore : IRebateDataStore
{
    public Rebate GetRebate(string rebateIdentifier)
    {
        // Assessment placeholder: database lookup is omitted in the supplied starter.
        return new Rebate();
    }

    public void StoreCalculationResult(Rebate rebate, decimal rebateAmount)
    {
        // Assessment placeholder: a database implementation would persist the rebate amount.
    }
}
