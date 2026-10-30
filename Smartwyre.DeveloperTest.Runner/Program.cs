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
            if (args.Length == 4) return args[index].Trim();
            Console.Write(prompt + ": ");
            var input = Console.ReadLine();
            if (input == null)
                Console.Error.WriteLine("Input ended. No calculation was made.");
            return input?.Trim();
        }

        IncentiveType incentive;
        while (true)
        {
            var incentiveText = Read(0, "Incentive (FixedCashAmount, FixedRateRebate, AmountPerUom)");
            if (incentiveText == null) return 2;
            if (Enum.TryParse(incentiveText, true, out incentive) && Enum.IsDefined(incentive))
                break;

            Console.Error.WriteLine("Invalid input. Enter FixedCashAmount, FixedRateRebate, or AmountPerUom.");
            if (args.Length == 4) return 2;
        }

        bool TryReadNumber(int index, string prompt, bool requiresNonzero, out decimal number)
        {
            number = 0m;
            while (true)
            {
                var text = Read(index, prompt);
                if (text == null) return false;

                // Keep numeric input consistent across machine locales.
                if (decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out number))
                {
                    // Prompt feedback mirrors eligibility; command-line inputs are checked by the service.
                    if (args.Length == 4 || !requiresNonzero || number != 0m)
                        return true;

                    Console.Error.WriteLine($"Invalid input. {prompt} must be nonzero for {incentive}.");
                }
                else
                {
                    Console.Error.WriteLine("Invalid input. Enter a decimal number using '.' as the decimal separator.");
                }
                if (args.Length == 4) return false;
            }
        }

        if (!TryReadNumber(1, "Amount or fractional rate (e.g. 0.1 for 10%)", true, out var value) ||
            !TryReadNumber(2, "Product price", incentive == IncentiveType.FixedRateRebate, out var price) ||
            !TryReadNumber(3, "Volume", incentive != IncentiveType.FixedCashAmount, out var volume))
            return 2;

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
