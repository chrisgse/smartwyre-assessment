using System;
using Smartwyre.DeveloperTest.Calculators;

namespace Smartwyre.DeveloperTest.Runner;

internal static class Program
{
    private static int Main(string[] args)
    {
        // Add a calculator here; console choices and validation come from its registration.
        return RebateConsole.Run(args, new IRebateCalculator[]
        {
            new FixedCashAmountCalculator(), new FixedRateRebateCalculator(), new AmountPerUomCalculator()
        }, Console.In, Console.Out, Console.Error);
    }
}
