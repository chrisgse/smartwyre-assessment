using System;
using System.Collections.Generic;
using Smartwyre.DeveloperTest.Calculators;
using Smartwyre.DeveloperTest.Data;
using Smartwyre.DeveloperTest.Services;
using Smartwyre.DeveloperTest.Types;
using Xunit;

namespace Smartwyre.DeveloperTest.Tests;

public class RebateServiceTests
{
    private static IRebateCalculator[] Calculators() => new IRebateCalculator[]
    {
        new FixedCashAmountCalculator(), new FixedRateRebateCalculator(), new AmountPerUomCalculator()
    };

    [Theory]
    [InlineData(IncentiveType.FixedCashAmount, 10)]
    [InlineData(IncentiveType.FixedRateRebate, 6)]
    [InlineData(IncentiveType.AmountPerUom, 30)]
    public void Eligible_rebate_persists_exact_amount_once(IncentiveType incentive, decimal expected)
    {
        var store = new RebateStore { Rebate = new Rebate { Incentive = incentive, Amount = 10, Percentage = 0.1m } };
        var products = new ProductStore();
        var service = new RebateService(store, products, Calculators());

        Assert.True(service.Calculate(Request()).Success);
        Assert.Equal(expected, store.Amount);
        Assert.Equal(1, store.Writes);
        Assert.Same(store.Rebate, store.SavedRebate);
        Assert.Equal("rebate", store.Lookup);
        Assert.Equal("product", products.Lookup);
    }

    [Theory]
    [InlineData(IncentiveType.FixedRateRebate)]
    [InlineData(IncentiveType.AmountPerUom)]
    public void Tiny_nonzero_inputs_can_produce_a_valid_zero_result(IncentiveType incentive)
    {
        const decimal smallestPositiveDecimal = 0.0000000000000000000000000001m;
        var store = new RebateStore
        {
            Rebate = new Rebate { Incentive = incentive, Amount = smallestPositiveDecimal, Percentage = .1m }
        };
        var products = new ProductStore();
        products.Product.Price = smallestPositiveDecimal;
        var request = Request();
        request.Volume = .1m;

        // Decimal cannot represent these products: the result becomes zero, while the inputs stay eligible.
        Assert.True(new RebateService(store, products, Calculators()).Calculate(request).Success);
        Assert.Equal(0m, store.Amount);
        Assert.Equal(1, store.Writes);
    }

    public static IEnumerable<object[]> ValidEdgeCases()
    {
        foreach (var limit in new[] { decimal.MinValue, decimal.MaxValue })
        {
            // Extreme irrelevant inputs must not affect the selected formula.
            yield return new object[] { IncentiveType.FixedCashAmount, limit, decimal.MaxValue, decimal.MaxValue, decimal.MaxValue, limit };
            yield return new object[] { IncentiveType.FixedRateRebate, decimal.MaxValue, 1m, limit, 1m, limit };
            yield return new object[] { IncentiveType.AmountPerUom, limit, decimal.MaxValue, decimal.MaxValue, 1m, limit };
        }

        // The starter permits rates above 100% and signed inputs; no new restrictions are introduced.
        yield return new object[] { IncentiveType.FixedRateRebate, 0m, 1.25m, 20m, 2m, 50m };
        yield return new object[] { IncentiveType.FixedRateRebate, 0m, -.1m, 20m, 3m, -6m };
        yield return new object[] { IncentiveType.FixedRateRebate, 0m, .1m, -20m, 3m, -6m };
        yield return new object[] { IncentiveType.FixedRateRebate, 0m, -.1m, -20m, 3m, 6m };
        yield return new object[] { IncentiveType.AmountPerUom, -2.5m, 0m, 0m, -4m, 10m };
    }

    [Theory]
    [MemberData(nameof(ValidEdgeCases))]
    public void Eligible_edge_values_persist_the_exact_result(IncentiveType incentive,
        decimal amount, decimal percentage, decimal price, decimal volume, decimal expected)
    {
        var store = new RebateStore
        {
            Rebate = new Rebate { Incentive = incentive, Amount = amount, Percentage = percentage }
        };
        var products = new ProductStore();
        products.Product.Price = price;
        var request = Request();
        request.Volume = volume;

        Assert.True(new RebateService(store, products, Calculators()).Calculate(request).Success);
        Assert.Equal(expected, store.Amount);
        Assert.Equal(1, store.Writes);
    }

    public static IEnumerable<object[]> OverflowCases()
    {
        foreach (var limit in new[] { decimal.MinValue, decimal.MaxValue })
        {
            yield return new object[] { IncentiveType.FixedRateRebate, 0m, 2m, limit, 1m };
            // Preserve the starter's multiplication order: an intermediate product can overflow.
            yield return new object[] { IncentiveType.FixedRateRebate, 0m, 2m, limit, .1m };
            yield return new object[] { IncentiveType.AmountPerUom, limit, 0m, 0m, 2m };
        }
    }

    [Theory]
    [MemberData(nameof(OverflowCases))]
    public void Overflow_does_not_save_and_the_service_can_calculate_again(IncentiveType incentive,
        decimal amount, decimal percentage, decimal price, decimal volume)
    {
        var store = new RebateStore
        {
            Rebate = new Rebate { Incentive = IncentiveType.FixedCashAmount, Amount = 10m }
        };
        var products = new ProductStore();
        var service = new RebateService(store, products, Calculators());
        Assert.True(service.Calculate(Request()).Success);

        store.Rebate = new Rebate { Incentive = incentive, Amount = amount, Percentage = percentage };
        products.Product.Price = price;
        var request = Request();
        request.Volume = volume;

        Assert.Throws<OverflowException>(() => service.Calculate(request));
        Assert.Equal(1, store.Writes);
        Assert.Equal(10m, store.Amount);

        store.Rebate = new Rebate { Incentive = IncentiveType.FixedCashAmount, Amount = 20m };
        Assert.True(service.Calculate(Request()).Success);
        Assert.Equal(2, store.Writes);
        Assert.Equal(20m, store.Amount);
    }

    [Theory]
    [InlineData(IncentiveType.FixedCashAmount)]
    [InlineData(IncentiveType.FixedRateRebate)]
    [InlineData(IncentiveType.AmountPerUom)]
    public void Required_zero_inputs_are_rejected_even_when_other_values_are_extreme(IncentiveType incentive)
    {
        var store = new RebateStore
        {
            Rebate = new Rebate
            {
                Incentive = incentive,
                Amount = incentive == IncentiveType.FixedCashAmount ? 0m : decimal.MaxValue,
                Percentage = decimal.MaxValue
            }
        };
        var products = new ProductStore();
        products.Product.Price = decimal.MaxValue;
        var request = Request();
        request.Volume = 0m;

        Assert.False(new RebateService(store, products, Calculators()).Calculate(request).Success);
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Missing_record_fails_without_persisting(bool missingRebate)
    {
        var store = new RebateStore { Rebate = missingRebate ? null : new Rebate() };
        var products = new ProductStore { Product = missingRebate ? new Product() : null };
        Assert.False(new RebateService(store, products, Calculators()).Calculate(Request()).Success);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void Unknown_incentive_fails_without_persisting()
    {
        var store = new RebateStore { Rebate = new Rebate { Incentive = (IncentiveType)99 } };
        Assert.False(new RebateService(store, new ProductStore(), Calculators()).Calculate(Request()).Success);
        Assert.Equal(0, store.Writes);
    }

    [Theory]
    [InlineData(null, "product")]
    [InlineData("", "product")]
    [InlineData(" ", "product")]
    [InlineData("rebate", null)]
    [InlineData("rebate", "")]
    public void Missing_identifier_does_not_query_stores(string rebate, string product)
    {
        var store = new RebateStore();
        var products = new ProductStore();
        var result = new RebateService(store, products, Calculators()).Calculate(new CalculateRebateRequest
        { RebateIdentifier = rebate, ProductIdentifier = product });
        Assert.False(result.Success);
        Assert.Null(store.Lookup);
        Assert.Null(products.Lookup);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void Null_request_is_programming_error()
    {
        Assert.Throws<ArgumentNullException>(() => new RebateService(new RebateStore(), new ProductStore(), Calculators()).Calculate(null));
    }

    [Fact]
    public void Duplicate_registration_is_configuration_error()
    {
        Assert.Throws<ArgumentException>(() => new RebateService(new RebateStore(), new ProductStore(),
            new IRebateCalculator[] { new FixedCashAmountCalculator(), new FixedCashAmountCalculator() }));
    }

    [Fact]
    public void New_calculator_can_be_registered_without_changing_service()
    {
        var store = new RebateStore { Rebate = new Rebate { Incentive = (IncentiveType)99 } };
        var service = new RebateService(store, new ProductStore(), new[] { new NewCalculator() });
        Assert.True(service.Calculate(Request()).Success);
        Assert.Equal(42m, store.Amount);
    }

    [Theory]
    [InlineData(IncentiveType.FixedCashAmount)]
    [InlineData(IncentiveType.FixedRateRebate)]
    [InlineData(IncentiveType.AmountPerUom)]
    public void Ineligible_calculation_is_never_persisted(IncentiveType incentive)
    {
        var store = new RebateStore { Rebate = new Rebate { Incentive = incentive, Amount = 10, Percentage = .1m } };
        var products = new ProductStore { Product = new Product { SupportedIncentives = 0, Price = 20 } };
        Assert.False(new RebateService(store, products, Calculators()).Calculate(Request()).Success);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void Persistence_failure_propagates_instead_of_reporting_success()
    {
        var store = new RebateStore { Rebate = new Rebate { Incentive = IncentiveType.FixedCashAmount, Amount = 10 }, FailWrite = true };
        Assert.Throws<InvalidOperationException>(() => new RebateService(store, new ProductStore(), Calculators()).Calculate(Request()));
    }

    private static CalculateRebateRequest Request() => new()
    { RebateIdentifier = "rebate", ProductIdentifier = "product", Volume = 3 };

    private sealed class NewCalculator : IRebateCalculator
    {
        public IncentiveType Incentive => (IncentiveType)99;
        public RebateInputRequirements InputRequirements { get; } = new();
        public decimal? Calculate(Rebate rebate, Product product, decimal volume) => 42m;
    }

    private sealed class RebateStore : IRebateDataStore
    {
        public Rebate Rebate { get; set; }
        public string Lookup { get; private set; }
        public int Writes { get; private set; }
        public decimal Amount { get; private set; }
        public Rebate SavedRebate { get; private set; }
        public bool FailWrite { get; set; }
        public Rebate GetRebate(string identifier) { Lookup = identifier; return Rebate; }
        public void StoreCalculationResult(Rebate rebate, decimal amount)
        {
            if (FailWrite) throw new InvalidOperationException("Database unavailable");
            Writes++; Amount = amount; SavedRebate = rebate;
        }
    }

    private sealed class ProductStore : IProductDataStore
    {
        public Product Product { get; set; } = new Product
        { Price = 20, SupportedIncentives = SupportedIncentiveType.FixedCashAmount | SupportedIncentiveType.FixedRateRebate | SupportedIncentiveType.AmountPerUom };
        public string Lookup { get; private set; }
        public Product GetProduct(string identifier) { Lookup = identifier; return Product; }
    }
}
