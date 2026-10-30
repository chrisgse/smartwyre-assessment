using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Smartwyre.DeveloperTest.Tests;

public class RunnerTests
{
    [Fact]
    public async Task Invalid_incentive_can_be_corrected_without_restarting()
    {
        var result = await Run("unknown\nFixedRateRebate\n0.1\n20\n3\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Invalid input", result.Error);
        AssertAmount("6", result.Output);
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
    [InlineData("FixedRateRebate", "wrong", "20", "3", 2)]
    [InlineData("FixedRateRebate", "0", "20", "3", 1)]
    [InlineData("FixedRateRebate", "0.1", "20", "3", 0)]
    public async Task Command_line_arguments_do_not_start_interactive_prompts(
        string incentive, string value, string price, string volume, int expectedExitCode)
    {
        var result = await Run("", incentive, value, price, volume);

        Assert.Equal(expectedExitCode, result.ExitCode);
        Assert.DoesNotContain("Incentive (", result.Output);
    }

    private static void AssertAmount(string expected, string output)
    {
        const string prefix = "Rebate calculated: ";
        Assert.Contains(prefix, output);
        var amount = output[(output.LastIndexOf(prefix, StringComparison.Ordinal) + prefix.Length)..].Trim();
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture),
            decimal.Parse(amount, CultureInfo.InvariantCulture));
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
