using System;
using System.Collections.Generic;
using System.Linq;
using Smartwyre.DeveloperTest.Calculators;
using Smartwyre.DeveloperTest.Data;
using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Services;

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
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.RebateIdentifier) ||
            string.IsNullOrWhiteSpace(request.ProductIdentifier))
            return new CalculateRebateResult();

        var rebate = rebates.GetRebate(request.RebateIdentifier);
        if (rebate == null || !calculators.TryGetValue(rebate.Incentive, out var calculator))
            return new CalculateRebateResult();

        var product = products.GetProduct(request.ProductIdentifier);
        if (product == null)
            return new CalculateRebateResult();

        var amount = calculator.Calculate(rebate, product, request.Volume);
        if (!amount.HasValue)
            return new CalculateRebateResult();

        rebates.StoreCalculationResult(rebate, amount.Value);
        return new CalculateRebateResult { Success = true };
    }
}
