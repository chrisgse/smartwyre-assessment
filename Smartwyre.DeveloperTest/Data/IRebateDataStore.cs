using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Data;

public interface IRebateDataStore
{
    Rebate GetRebate(string rebateIdentifier);
    void StoreCalculationResult(Rebate rebate, decimal rebateAmount);
}
