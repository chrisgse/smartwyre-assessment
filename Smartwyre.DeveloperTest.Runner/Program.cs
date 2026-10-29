using System;
using System.Globalization;
using Smartwyre.DeveloperTest.Calculators;
using Smartwyre.DeveloperTest.Data;
using Smartwyre.DeveloperTest.Services;
using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Runner;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 0 && args.Length != 4)
        {
            Console.Error.WriteLine("Usage: runner <FixedCashAmount|FixedRateRebate|AmountPerUom> <amount-or-rate> <price> <volume>");
            return 2;
        }

        string Read(int index, string prompt)
        {
            if (args.Length == 4) return args[index];
            Console.Write(prompt + ": ");
            return Console.ReadLine() ?? "";
        }

        var incentiveText = Read(0, "Incentive (FixedCashAmount, FixedRateRebate, AmountPerUom)");
        var valueText = Read(1, "Amount or fractional rate (e.g. 0.1 for 10%)");
        var priceText = Read(2, "Product price");
        var volumeText = Read(3, "Volume");
        // Keep numeric input consistent across machine locales: '.' is the decimal separator.
        if (!Enum.TryParse<IncentiveType>(incentiveText, true, out var incentive) ||
            !Enum.IsDefined(incentive) ||
            !decimal.TryParse(valueText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ||
            !decimal.TryParse(priceText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price) ||
            !decimal.TryParse(volumeText, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var volume))
        {
            Console.Error.WriteLine("Invalid input. Use a supported incentive and decimal numbers with a '.' separator.");
            return 2;
        }

        var rebate = new Rebate { Identifier = "demo-rebate", Incentive = incentive, Amount = value, Percentage = value };
        var product = new Product
        {
            Identifier = "demo-product", Price = price,
            SupportedIncentives = SupportedIncentiveType.FixedCashAmount |
                SupportedIncentiveType.FixedRateRebate | SupportedIncentiveType.AmountPerUom
        };
        var store = new DemoRebateStore(rebate);
        // Register new calculators here; the service selects them by incentive type.
        var service = new RebateService(store, new DemoProductStore(product), new IRebateCalculator[]
        {
            new FixedCashAmountCalculator(), new FixedRateRebateCalculator(), new AmountPerUomCalculator()
        });
        try
        {
            var result = service.Calculate(new CalculateRebateRequest
            {
                RebateIdentifier = rebate.Identifier, ProductIdentifier = product.Identifier, Volume = volume
            });
            Console.WriteLine(result.Success
                ? $"Rebate calculated: {store.Amount.ToString(CultureInfo.InvariantCulture)}"
                : "Rebate could not be calculated: inputs are ineligible.");
            return result.Success ? 0 : 1;
        }
        catch (OverflowException)
        {
            Console.Error.WriteLine("The calculation exceeds the supported decimal range.");
            return 2;
        }
    }

    // The supplied database stores are placeholders. Demo data lives only for this run.
    private sealed class DemoRebateStore(Rebate rebate) : IRebateDataStore
    {
        public decimal Amount { get; private set; }
        public Rebate GetRebate(string identifier) => identifier == rebate.Identifier ? rebate : null;
        public void StoreCalculationResult(Rebate rebate, decimal amount) => Amount = amount;
    }

    private sealed class DemoProductStore(Product product) : IProductDataStore
    {
        public Product GetProduct(string identifier) => identifier == product.Identifier ? product : null;
    }
}
