using System;
using System.Collections.Generic;
using System.Linq;
using Smartwyre.DeveloperTest.Calculators;
using Smartwyre.DeveloperTest.Data;
using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Services;

// Coordinates the workflow; calculators own incentive-specific eligibility and arithmetic.
public class RebateService : IRebateService
{
    private readonly IRebateDataStore rebates;
    private readonly IProductDataStore products;
    private readonly IReadOnlyDictionary<IncentiveType, IRebateCalculator> calculators;

    public RebateService(IRebateDataStore rebates, IProductDataStore products,
        IEnumerable<IRebateCalculator> calculators)
    {
        this.rebates = rebates ?? throw new ArgumentNullException(nameof(rebates));
        this.products = products ?? throw new ArgumentNullException(nameof(products));
        ArgumentNullException.ThrowIfNull(calculators);
        // Duplicate registrations fail at startup instead of selecting a strategy arbitrarily.
        this.calculators = calculators.ToDictionary(calculator => calculator.Incentive);
    }

    public CalculateRebateResult Calculate(CalculateRebateRequest request)
    {
        // A null request is a caller error; missing or ineligible data returns failure.
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RebateIdentifier) ||
            string.IsNullOrWhiteSpace(request.ProductIdentifier))
            return new CalculateRebateResult();

        var rebate = rebates.GetRebate(request.RebateIdentifier);
        // Registration lets new incentives work without adding branches to this method.
        if (rebate == null || !calculators.TryGetValue(rebate.Incentive, out var calculator))
            return new CalculateRebateResult();

        var product = products.GetProduct(request.ProductIdentifier);
        if (product == null)
            return new CalculateRebateResult();

        var amount = calculator.Calculate(rebate, product, request.Volume);
        // Null means ineligible; an actual calculated zero remains a valid result to save.
        if (!amount.HasValue)
            return new CalculateRebateResult();

        // Report success only after saving; let persistence failures reach the caller.
        rebates.StoreCalculationResult(rebate, amount.Value);
        return new CalculateRebateResult { Success = true };
    }
}
