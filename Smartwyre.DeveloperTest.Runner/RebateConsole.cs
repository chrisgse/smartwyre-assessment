using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Smartwyre.DeveloperTest.Calculators;
using Smartwyre.DeveloperTest.Data;
using Smartwyre.DeveloperTest.Services;
using Smartwyre.DeveloperTest.Types;

namespace Smartwyre.DeveloperTest.Runner;

public static class RebateConsole
{
    public static int Run(string[] args, IEnumerable<IRebateCalculator> calculators,
        TextReader input, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(calculators);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        // Registrations drive both the available choices and each choice's input rules.
        var registered = calculators.ToDictionary(calculator => calculator.Incentive);
        if (registered.Count == 0)
            throw new ArgumentException("Register at least one incentive calculator.", nameof(calculators));
        var choices = string.Join(", ", registered.Keys);
        if (args.Length != 0 && args.Length != 4)
        {
            error.WriteLine($"Usage: runner <incentive> <amount-or-rate> <price> <volume>. Available incentives: {choices}");
            return 2;
        }

        string Read(int index, string prompt)
        {
            if (args.Length == 4) return args[index].Trim();
            output.Write($"[{index + 1}/4] {prompt}: ");
            var text = input.ReadLine();
            if (text == null)
                error.WriteLine("Input ended. No calculation was made.");
            return text?.Trim();
        }

        IncentiveType incentive;
        IRebateCalculator selectedCalculator;
        while (true)
        {
            var incentiveText = Read(0, $"Incentive type ({choices})");
            if (incentiveText == null) return 2;
            if (Enum.TryParse(incentiveText, true, out incentive) &&
                registered.TryGetValue(incentive, out selectedCalculator))
                break;

            error.WriteLine($"Invalid input. Enter one of: {choices}.");
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

                    error.WriteLine($"Invalid input. {prompt} must be nonzero for {incentive}.");
                }
                else
                {
                    error.WriteLine("Invalid input. Enter a decimal number using '.' as the decimal separator.");
                }
                if (args.Length == 4) return false;
            }
        }

        var requirements = selectedCalculator.InputRequirements;
        var valuePrompt = requirements.Percentage
            ? "Rebate rate (e.g. 0.1 for 10%)"
            : "Rebate amount";

        if (!TryReadNumber(1, valuePrompt, requirements.Amount || requirements.Percentage, out var value))
            return 2;
        if (!TryReadNumber(2, "Product price", requirements.Price, out var price))
            return 2;
        if (!TryReadNumber(3, "Volume", requirements.Volume, out var volume))
            return 2;

        var rebate = new Rebate { Identifier = "demo-rebate", Incentive = incentive, Amount = value, Percentage = value };
        var product = new Product
        {
            Identifier = "demo-product", Price = price,
            // Demo data supports all declared flags, including flags added for future incentives.
            SupportedIncentives = Enum.GetValues<SupportedIncentiveType>()
                .Aggregate((SupportedIncentiveType)0, (flags, next) => flags | next)
        };
        var store = new DemoRebateStore(rebate);
        var service = new RebateService(store, new DemoProductStore(product), registered.Values);
        try
        {
            var result = service.Calculate(new CalculateRebateRequest
            {
                RebateIdentifier = rebate.Identifier, ProductIdentifier = product.Identifier, Volume = volume
            });
            output.WriteLine(result.Success
                ? $"Rebate calculated: {store.Amount.ToString(CultureInfo.InvariantCulture)}"
                : "Rebate could not be calculated: inputs are ineligible.");
            return result.Success ? 0 : 1;
        }
        catch (OverflowException)
        {
            error.WriteLine("The calculation exceeds the supported decimal range.");
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
