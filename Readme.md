# Smartwyre Developer Test Instructions

You have been selected to complete our candidate coding exercise. Please follow the directions in this readme.

Clone, **DO NOT FORK**, this repository to your account on the online Git resource of your choosing (GitHub, BitBucket, GitLab, etc.). Your solution should retain previous commit history and you should utilize best practices for committing your changes to the repository.

You are welcome to use whatever tools you normally would when coding — including documentation, libraries, frameworks, or AI tools (such as ChatGPT or Copilot).

However, it is important that you fully understand your solution. As part of the interview process, we will review your code with you in detail. You should be able to:

- Explain the design choices you made.
- Walk us through how your solution works.
- Make modifications or extensions to your code during the review.

Please note: if your submission appears to have been generated entirely by an AI agent or another third party, without your own understanding or contribution, it will not meet our evaluation criteria.

# The Exercise

In the 'RebateService.cs' file you will find a method for calculating a rebate. At a high level the steps for calculating a rebate are:

 1. Lookup the rebate that the request is being made against.
 2. Lookup the product that the request is being made against.
 2. Check that the rebate and request are valid to calculate the incentive type rebate.
 3. Store the rebate calculation.

What we'd like you to do is refactor the code with the following things in mind:

 - Adherence to SOLID principles
 - Testability
 - Readability
 - Currently there are 3 known incentive types. In the future the business will want to add many more incentive types. Your solution should make it easy for developers to add new incentive types in the future.

We’d also like you to 
 - Add some unit tests to the Smartwyre.DeveloperTest.Tests project to show how you would test the code that you’ve produced 
 - Run the RebateService from the Smartwyre.DeveloperTest.Runner console application accepting inputs (either via command line arguments or via prompts is fine)

The only specific "rules" are:

- The solution must build
- All tests must pass

You are free to use any frameworks/NuGet packages that you see fit. You should plan to spend around 1 hour completing the exercise.

Feel free to use code comments to describe your changes. You are also welcome to update this readme with any important details for us to consider.

Once you have completed the exercise either ensure your repository is available publicly or contact the hiring manager to set up a private share.

# Running this solution

Requires the .NET 10 SDK. From the repository directory:

```sh
dotnet build Smartwyre.DeveloperTest.sln --configuration Release
dotnet test Smartwyre.DeveloperTest.sln --configuration Release
dotnet run --project Smartwyre.DeveloperTest.Runner -- FixedRateRebate 0.1 20 3
```

The example calculates `20 × 0.1 × 3 = 6`. Other examples:

```sh
dotnet run --project Smartwyre.DeveloperTest.Runner -- FixedCashAmount 10 20 3
dotnet run --project Smartwyre.DeveloperTest.Runner -- AmountPerUom 2.5 20 4
```

These return 10 and 10 respectively. Run without arguments for four numbered prompts:
incentive type, rebate amount or rate, product price, then volume. Each answer is
validated before the next prompt appears. Invalid incentives, blank entries, and
invalid numbers show an explanation and repeat only
that prompt. Required values must be nonzero for the selected incentive; irrelevant
zeros and negative values retain the rules below. Numbers use `.` as the decimal
separator. End-of-input cancels cleanly. Command-line arguments report errors without
starting interactive prompts. Exit codes: 0 = success, 1 = ineligible calculation,
2 = invalid command-line input, cancelled input, or decimal overflow.

The runner creates one demo rebate and product from the inputs. The product supports
all three incentives. Its stores retain the calculation only during that run;
the original database stores are still placeholders, not production persistence.

# Design and interview walkthrough

Start with `Services/RebateService.cs`, then one calculator, then its tests.

1. The runner constructs the service with two store interfaces and a collection of
   calculators. This is manual dependency injection; no container is needed here.
2. The service rejects missing identifiers, looks up the rebate, and selects a
   calculator by incentive type. Unknown types and missing records return failure.
3. The selected calculator checks the product's supported-incentive flags and the
   fields needed by its formula. A nullable decimal distinguishes ineligibility
   from an actual calculated value.
4. The service saves a successful calculation through the same injected rebate
   store, exactly once, then returns success. Persistence exceptions propagate;
   callers must not be told a failed save succeeded.

Each calculator has one responsibility: the eligibility and formula for one
incentive. The service depends on small abstractions rather than constructing
concrete stores. These seams let the tests use simple recording fakes with no
database or mocking library. The service is open to new registered calculators
without changing its orchestration logic. The interfaces describe only operations
needed by their consumers.

## Business rules retained and assumptions

- Fixed cash: product supports fixed cash and rebate amount is nonzero. Price and
  volume are irrelevant; the result is the rebate amount.
- Fixed rate: product supports fixed rate, and percentage, price, and volume are
  nonzero. The result is price × percentage × volume. A 10% rate is `0.1`.
- Amount per UOM: product supports this incentive, and amount and volume are
  nonzero. The result is amount × volume. UOM conversion is outside the starter's
  behavior.
- Negative values remain valid because the starter rejected only zero. Whether
  negatives or rates above 1 should be prohibited is a business decision, not an
  incidental refactoring change.
- No currency rounding was introduced; decimal arithmetic preserves the original
  formulas. Currency precision and rounding policy need explicit requirements.
- Missing products now fail consistently, including for fixed cash. The starter
  would dereference a null product. Null requests throw `ArgumentNullException`;
  blank identifiers return an unsuccessful result without querying stores.
- Duplicate calculator registrations fail at construction as a configuration
  error. Store failures and calculation overflow propagate from the service.

## Adding an incentive during the interview

1. Add its domain member to `IncentiveType` and a distinct power-of-two flag to
   `SupportedIncentiveType` if product eligibility needs one.
2. Implement `IRebateCalculator`, including the new eligibility rule and formula.
3. Register the calculator at the composition root (the runner's `Program`).
4. Add formula and eligibility tests. Add runner input support if the new rule
   requires different fields. `RebateService` stays unchanged.

The extension test registers a test-only incentive to demonstrate that the service
has no hard-coded knowledge of the three current strategies.

## Tradeoffs to discuss

A switch is reasonable for three permanently fixed rules. Separate calculators
are justified here by the explicit requirement to add many incentive types.
Avoid presenting the pattern as universally necessary. A dependency injection
framework, generic repository, and reflection-based discovery would add machinery
without helping this small exercise.

The result retains the starter's boolean contract. A production API could expose
structured failure reasons and the calculated amount if consumers need them.
Real persistence would also need transaction and retry/idempotency requirements;
this exercise does not invent those requirements.

Before submitting, explain the fixed-rate example aloud, change a rule yourself,
and run the tests. Be ready to explain why the cash calculation ignores volume,
why negative inputs are preserved, and why a save failure cannot report success.
