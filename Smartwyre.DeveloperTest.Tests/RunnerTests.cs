using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Smartwyre.DeveloperTest.Calculators;
using Smartwyre.DeveloperTest.Runner;
using Smartwyre.DeveloperTest.Types;
using Xunit;

namespace Smartwyre.DeveloperTest.Tests;

public class RunnerTests
{
    [Fact]
    public async Task Interactive_session_calculates_multiple_rebates_before_quitting()
    {
        var result = await Run("FixedRateRebate\n0.1\n20\n3\nFixedCashAmount\n10\n0\n0\nq\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Equal(new[] { 6m, 10m }, Amounts(result.Output));
    }

    [Theory]
    [InlineData("q\n")]
    [InlineData(" Q \n")]
    public async Task Quit_at_incentive_prompt_exits_without_calculating(string input)
    {
        var result = await Run(input);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Empty(Amounts(result.Output));
    }

    [Fact]
    public async Task Cancelling_the_next_calculation_keeps_the_previous_result()
    {
        var result = await Run("FixedCashAmount\n10\n0\n0\nFixedRateRebate\n0.1\n");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Input ended", result.Error);
        Assert.Equal(new[] { 10m }, Amounts(result.Output));
    }

    [Theory]
    [InlineData("FixedRateRebate", "2", "79228162514264337593543950335", "3")]
    [InlineData("AmountPerUom", "79228162514264337593543950335", "0", "2")]
    public async Task Overflow_allows_another_calculation_in_the_same_session(
        string incentive, string value, string price, string volume)
    {
        var result = await Run($"{incentive}\n{value}\n{price}\n{volume}\nFixedCashAmount\n10\n0\n0\nq\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("exceeds the supported decimal range", result.Error);
        Assert.Equal(new[] { 10m }, Amounts(result.Output));
    }

    [Theory]
    [InlineData("79228162514264337593543950336")]
    [InlineData("-79228162514264337593543950336")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e3")]
    [InlineData("1,000")]
    public async Task Unsupported_or_out_of_range_numbers_can_be_corrected(string invalidValue)
    {
        var result = await Run($"FixedCashAmount\n{invalidValue}\n10\n0\n0\nq\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Enter a decimal number", result.Error);
        AssertAmount("10", result.Output);
    }

    [Fact]
    public void New_incentive_supplies_console_choices_and_input_rules_through_registration()
    {
        // This incentive needs only price: zero amount and volume are valid.
        using var input = new StringReader("99\n0\n0\n20\n0\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = RebateConsole.Run(Array.Empty<string>(),
            new IRebateCalculator[] { new PriceOnlyCalculator() }, input, output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Incentive type (99)", output.ToString());
        Assert.Contains("Product price must be nonzero", error.ToString());
        Assert.DoesNotContain("FixedCashAmount", output.ToString());
        AssertAmount("40", output.ToString());
    }

    [Fact]
    public async Task Invalid_incentive_can_be_corrected_without_restarting()
    {
        var result = await Run("unknown\nFixedRateRebate\n0.1\n20\n3\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Invalid input", result.Error);
        AssertAmount("6", result.Output);
    }

    [Fact]
    public async Task Multiple_incentive_names_are_rejected_before_accepting_a_single_choice()
    {
        var result = await Run("FixedRateRebate, FixedCashAmount\nFixedRateRebate\n0.1\n20\n3\nq\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Invalid input", result.Error);
        AssertAmount("6", result.Output);
    }

    [Fact]
    public void Unregistered_incentive_is_rejected_even_if_it_is_a_known_enum_value()
    {
        using var input = new StringReader("FixedRateRebate\n99\n0\n20\n0\n");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = RebateConsole.Run(Array.Empty<string>(),
            new IRebateCalculator[] { new PriceOnlyCalculator() }, input, output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Invalid input. Enter one of: 99", error.ToString());
        AssertAmount("40", output.ToString());
    }

    [Fact]
    public void New_incentive_also_works_through_command_line_arguments()
    {
        using var input = new StringReader("");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = RebateConsole.Run(new[] { "99", "0", "20", "0" },
            new IRebateCalculator[] { new PriceOnlyCalculator() }, input, output, error);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
        Assert.DoesNotContain("[1/4]", output.ToString());
        AssertAmount("40", output.ToString());
    }

    [Theory]
    [InlineData("FixedRateRebate\nwrong\n0.1\n20\n3\n")]
    [InlineData("FixedRateRebate\n\n0.1\n20\n3\n")]
    [InlineData("FixedRateRebate\n0.1\nwrong\n20\n3\n")]
    [InlineData("FixedRateRebate\n0.1\n\n20\n3\n")]
    [InlineData("FixedRateRebate\n0.1\n20\nwrong\n3\n")]
    [InlineData("FixedRateRebate\n0.1\n20\n\n3\n")]
    public async Task Invalid_number_can_be_corrected_without_reentering_other_fields(string input)
    {
        var result = await Run(input);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Invalid input", result.Error);
        AssertAmount("6", result.Output);
    }

    [Theory]
    [InlineData("FixedRateRebate\n0\n0.1\n20\n3\n", "6")]
    [InlineData("FixedRateRebate\n0.1\n0\n20\n3\n", "6")]
    [InlineData("FixedRateRebate\n0.1\n20\n0\n3\n", "6")]
    [InlineData("FixedCashAmount\n0\n10\n0\n0\n", "10")]
    [InlineData("AmountPerUom\n0\n2.5\n0\n4\n", "10")]
    [InlineData("AmountPerUom\n2.5\n0\n0\n4\n", "10")]
    public async Task Required_nonzero_value_can_be_corrected(string input, string expected)
    {
        var result = await Run(input);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("nonzero", result.Error);
        AssertAmount(expected, result.Output);
    }

    [Theory]
    [InlineData("FixedRateRebate\n0.1\n20\n3\n", "6")]
    [InlineData("FixedCashAmount\n10\n20\n3\n", "10")]
    [InlineData("AmountPerUom\n2.5\n20\n4\n", "10")]
    [InlineData("FixedCashAmount\n10\n0\n0\n", "10")]
    [InlineData("AmountPerUom\n2.5\n0\n4\n", "10")]
    [InlineData("FixedCashAmount\n-10\n0\n0\n", "-10")]
    [InlineData("FixedRateRebate\n0.1\n20\n-3\n", "-6")]
    [InlineData("AmountPerUom\n2.5\n0\n-4\n", "-10")]
    [InlineData(" fixedraterebate \n 0.1 \n 20 \n 3 \n", "6")]
    public async Task Valid_input_preserves_calculation_rules(string input, string expected)
    {
        var result = await Run(input);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        AssertAmount(expected, result.Output);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown\n")]
    [InlineData("FixedRateRebate\n")]
    [InlineData("FixedRateRebate\n0.1\n")]
    [InlineData("FixedRateRebate\n0.1\n20\n")]
    public async Task End_of_input_cancels_without_calculating(string input)
    {
        var result = await Run(input);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Input ended", result.Error);
        Assert.DoesNotContain("Rebate calculated:", result.Output);
    }

    [Theory]
    [InlineData("unknown", "0.1", "20", "3", 2)]
    [InlineData("FixedRateRebate, FixedCashAmount", "10", "20", "3", 2)]
    [InlineData("FixedRateRebate", "wrong", "20", "3", 2)]
    [InlineData("FixedRateRebate", "0", "20", "3", 1)]
    [InlineData("FixedRateRebate", "0.1", "20", "3", 0)]
    [InlineData("q", "0.1", "20", "3", 2)]
    public async Task Command_line_arguments_do_not_start_interactive_prompts(
        string incentive, string value, string price, string volume, int expectedExitCode)
    {
        var result = await Run("", incentive, value, price, volume);

        Assert.Equal(expectedExitCode, result.ExitCode);
        Assert.DoesNotContain("[1/4]", result.Output);
    }

    private sealed class PriceOnlyCalculator : IRebateCalculator
    {
        public IncentiveType Incentive => (IncentiveType)99;
        public RebateInputRequirements InputRequirements { get; } = new(Price: true);
        public decimal? Calculate(Rebate rebate, Product product, decimal volume) =>
            InputRequirements.IsSatisfiedBy(rebate, product, volume) ? product.Price * 2m : null;
    }

    private static void AssertAmount(string expected, string output)
    {
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture),
            Assert.Single(Amounts(output)));
    }

    private static decimal[] Amounts(string output)
    {
        const string prefix = "Rebate calculated: ";
        return output.Split('\n')
            .Where(line => line.Contains(prefix))
            .Select(line => decimal.Parse(
                line[(line.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length)..].Trim(),
                CultureInfo.InvariantCulture))
            .ToArray();
    }

    // Exercise the real console process, so parsing, prompts, and calculations run together.
    private static async Task<(int ExitCode, string Output, string Error)> Run(
        string input, params string[] args)
    {
        var start = new ProcessStartInfo(
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Smartwyre.DeveloperTest.Runner.dll"));
        foreach (var arg in args)
            start.ArgumentList.Add(arg);

        using var process = Process.Start(start);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();

        // A broken retry loop must fail the test rather than leave a process waiting forever.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return (process.ExitCode, await output, await error);
    }
}
