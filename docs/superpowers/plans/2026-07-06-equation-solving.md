# Equation-Solving Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the ability for the AdvancedCalculaterBot Telegram bot to solve equations (linear, quadratic, cubic, quartic) entered in natural text, alongside its existing numeric-evaluation feature.

**Architecture:** A new `Services/Equations/` namespace contains focused, single-responsibility components: a `PolynomialParser` that turns text into coefficient arrays, a `LinearReducer` that handles structured degree-1 equations via sampling with NCalc, four solvers (`LinearSolver`, `QuadraticSolver`, `CubicSolver`, `QuarticSolver`) that each return `System.Numerics.Complex[]`, a `ComplexFormatter` that formats roots like the reference `EquationSolvers` project, and an `EquationSolverService` that orchestrates parsing → dispatch → formatting. The bot's message handler (`Program.cs`) auto-routes any message containing `=` and `x` to the equation path; everything else stays on the existing `CalculatorService` path. All new code is covered by a new xUnit test project.

**Tech Stack:** C# / .NET 10, `System.Numerics.Complex` (BCL), existing `NCalc` 6.4.0 for linear-equation sampling, xUnit for tests.

**Spec:** `docs/superpowers/specs/2026-07-06-equation-solving-design.md`

---

## File Structure

**New files (production):**
- `Services/Equations/ComplexFormatter.cs` — `Complex` → display string + `FormatRoots(Complex[])` → multi-line reply. Extracted from `EquationSolvers`.
- `Services/Equations/PolynomialParser.cs` — text polynomial → `double[]` coeffs (low→high). Includes `CanParse` syntactic check.
- `Services/Equations/LinearReducer.cs` — structured degree-1 lhs/rhs → `double[]` via NCalc sampling.
- `Services/Equations/LinearSolver.cs` — `[b, a]` → `Complex[]`.
- `Services/Equations/QuadraticSolver.cs` — `[c, b, a]` → `Complex[]`.
- `Services/Equations/CubicSolver.cs` — `[d, c, b, a]` → `Complex[]` (Cardano, adapted from `EquationSolvers/CubicEquation`).
- `Services/Equations/QuarticSolver.cs` — `[e, d, c, b, a]` → `Complex[]` (Ferrari, uses `CubicSolver` + `QuadraticSolver`).
- `Services/Equations/EquationSolverService.cs` — public `Solve(string)` orchestrator.

**New files (test):**
- `AdvancedCalculaterBot.Tests/AdvancedCalculaterBot.Tests.csproj`
- `AdvancedCalculaterBot.Tests/ComplexFormatterTests.cs`
- `AdvancedCalculaterBot.Tests/PolynomialParserTests.cs`
- `AdvancedCalculaterBot.Tests/LinearReducerTests.cs`
- `AdvancedCalculaterBot.Tests/SolverTests.cs`
- `AdvancedCalculaterBot.Tests/EquationSolverServiceTests.cs`

**Modified:**
- `Program.cs` — add equation routing in the message handler.
- `AdvancedCalculaterBot.sln` — create (does not exist), include both projects.

**Conventions established (used across all files):**
- Namespace: `AdvancedCalculaterBot.Services.Equations`
- Coefficient arrays are **lowest-degree first**: `coeffs[0]` is the constant, `coeffs[i]` is the coefficient of `x^i`.
- All solvers implement: `public static Complex[] Solve(double[] coeffs)`.
- Deduplication tolerance: `1e-6`.
- All `git commit` commands assume cwd is the project root `X:/Main/Development/Dotnet/Console/AdvancedCalculaterBot`.

---

### Task 1: Create the test project and solution

**Files:**
- Create: `AdvancedCalculaterBot.Tests/AdvancedCalculaterBot.Tests.csproj`
- Create: `AdvancedCalculaterBot.sln`

- [ ] **Step 1: Create the test project file**

Create `AdvancedCalculaterBot.Tests/AdvancedCalculaterBot.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\AdvancedCalculaterBot.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create the solution and add both projects**

Run:

```bash
dotnet new sln -n AdvancedCalculaterBot
dotnet sln add AdvancedCalculaterBot.csproj
dotnet sln add AdvancedCalculaterBot.Tests/AdvancedCalculaterBot.Tests.csproj
```

Expected: solution file created, two projects listed.

- [ ] **Step 3: Verify the test project builds**

Run: `dotnet build AdvancedCalculaterBot.Tests/AdvancedCalculaterBot.Tests.csproj`
Expected: BUILD SUCCEEDED (no test classes yet, but it must compile).

- [ ] **Step 4: Commit**

```bash
git add AdvancedCalculaterBot.sln AdvancedCalculaterBot.Tests/AdvancedCalculaterBot.Tests.csproj
git commit -m "test: scaffold AdvancedCalculaterBot.Tests xUnit project"
```

---

### Task 2: Implement ComplexFormatter

This is built first because every solver test depends on being able to read root values, and `EquationSolverService` depends on it for output. Extracted from `EquationSolvers/CubicEquation/Program.cs:119-134` and `EquationSolvers/QuadraticEquation/Program.cs:86-100`.

**Files:**
- Create: `Services/Equations/ComplexFormatter.cs`
- Test: `AdvancedCalculaterBot.Tests/ComplexFormatterTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `AdvancedCalculaterBot.Tests/ComplexFormatterTests.cs`:

```csharp
using System.Numerics;
using AdvancedCalculaterBot.Services.Equations;
using Xunit;

namespace AdvancedCalculaterBot.Tests;

public class ComplexFormatterTests
{
    [Theory]
    [InlineData(2.0, 0.0, "2")]
    [InlineData(-3.5, 0.0, "-3.5")]
    [InlineData(0.0, 3.0, "3i")]
    [InlineData(0.0, -3.0, "-3i")]
    [InlineData(2.0, 3.0, "2 + 3i")]
    [InlineData(2.0, -3.0, "2 - 3i")]
    public void Format_Single_Complex(double real, double imag, string expected)
    {
        var z = new Complex(real, imag);
        Assert.Equal(expected, ComplexFormatter.Format(z));
    }

    [Fact]
    public void Format_Snaps_Near_Zero_Parts_To_Zero()
    {
        var z = new Complex(1e-8, 1e-8);
        Assert.Equal("0", ComplexFormatter.Format(z));
    }

    [Fact]
    public void Format_Rounds_To_Integers_When_Close()
    {
        var z = new Complex(2.0000001, 0);
        Assert.Equal("2", ComplexFormatter.Format(z));
    }

    [Fact]
    public void FormatRoots_Single_Root_Uses_Bare_Label()
    {
        var roots = new[] { new Complex(5, 0) };
        var output = ComplexFormatter.FormatRoots(roots);
        Assert.Equal("x = 5", output);
    }

    [Fact]
    public void FormatRoots_Multiple_Roots_Use_Subscripts()
    {
        var roots = new[] { new Complex(2, 0), new Complex(3, 0) };
        var output = ComplexFormatter.FormatRoots(roots);
        Assert.Equal("x₁ = 2\nx₂ = 3", output);
    }

    [Fact]
    public void FormatRoots_Deduplicates_Near_Equal_Roots()
    {
        var roots = new[] { new Complex(2.0, 0), new Complex(2.0 + 1e-9, 0) };
        var output = ComplexFormatter.FormatRoots(roots);
        Assert.Equal("x = 2", output);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~ComplexFormatterTests"`
Expected: FAIL with compile error (type `ComplexFormatter` not found).

- [ ] **Step 3: Implement ComplexFormatter**

Create `Services/Equations/ComplexFormatter.cs`:

```csharp
using System.Numerics;

namespace AdvancedCalculaterBot.Services.Equations;

/// <summary>
/// Formats <see cref="Complex"/> roots for display, mirroring the style of the
/// reference EquationSolvers projects (e.g. "2 + 3i", "-4", "5i").
/// </summary>
public static class ComplexFormatter
{
    private const double ZeroTolerance = 1e-6;
    private const double RoundTolerance = 1e-5;

    /// <summary>Formats a single complex number.</summary>
    public static string Format(Complex z)
    {
        double r = z.Real;
        double i = z.Imaginary;

        if (Math.Abs(r) < ZeroTolerance) r = 0;
        if (Math.Abs(i) < ZeroTolerance) i = 0;

        if (Math.Abs(r - Math.Round(r)) < RoundTolerance) r = Math.Round(r);
        if (Math.Abs(i - Math.Round(i)) < RoundTolerance) i = Math.Round(i);

        if (i == 0) return r.ToString();
        if (r == 0) return $"{i}i";

        return i > 0 ? $"{r} + {i}i" : $"{r} - {Math.Abs(i)}i";
    }

    /// <summary>
    /// Formats an array of roots into a multi-line reply, deduplicating roots
    /// that are within <see cref="ZeroTolerance"/> of each other. Uses a bare
    /// "x = ..." label for a single root and "x₁ = ...", "x₂ = ..." otherwise.
    /// </summary>
    public static string FormatRoots(Complex[] roots)
    {
        var unique = Deduplicate(roots);

        if (unique.Count == 1)
            return $"x = {Format(unique[0])}";

        var labels = new[] { "x₁", "x₂", "x₃", "x₄" };
        var lines = unique.Select((r, idx) => $"{labels[idx]} = {Format(r)}");
        return string.Join("\n", lines);
    }

    private static List<Complex> Deduplicate(Complex[] roots)
    {
        var unique = new List<Complex>();
        foreach (var r in roots)
        {
            bool dup = unique.Any(ur => Complex.Abs(r - ur) < ZeroTolerance);
            if (!dup) unique.Add(r);
        }
        return unique;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~ComplexFormatterTests"`
Expected: PASS — all 9 tests green.

- [ ] **Step 5: Commit**

```bash
git add Services/Equations/ComplexFormatter.cs AdvancedCalculaterBot.Tests/ComplexFormatterTests.cs
git commit -m "feat(equations): add ComplexFormatter for root display"
```

---

### Task 3: Implement PolynomialParser

Turns text polynomials in `x` into `double[]` coefficient arrays (low→high). Includes a cheap `CanParse` syntactic check used by `EquationSolverService` to decide between direct parsing and `LinearReducer`.

**Files:**
- Create: `Services/Equations/PolynomialParser.cs`
- Test: `AdvancedCalculaterBot.Tests/PolynomialParserTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `AdvancedCalculaterBot.Tests/PolynomialParserTests.cs`:

```csharp
using AdvancedCalculaterBot.Services.Equations;
using Xunit;

namespace AdvancedCalculaterBot.Tests;

public class PolynomialParserTests
{
    [Theory]
    [InlineData("x^2 - 5x + 6", new[] { 6.0, -5.0, 1.0 })]
    [InlineData("-x^4", new[] { 0.0, 0.0, 0.0, 0.0, -1.0 })]
    [InlineData("x", new[] { 0.0, 1.0 })]
    [InlineData("3x^3 - x + 5", new[] { 5.0, -1.0, 0.0, 3.0 })]
    [InlineData("7", new[] { 7.0 })]
    [InlineData("2x", new[] { 0.0, 2.0 })]
    [InlineData("x^2 + x + 1", new[] { 1.0, 1.0, 1.0 })]
    [InlineData("-3", new[] { -3.0 })]
    [InlineData("x^4 - 10x^2 + 9", new[] { 9.0, 0.0, -10.0, 0.0, 1.0 })]
    public void Parse_Returns_Coefficients_Low_To_High(string input, double[] expected)
    {
        var coeffs = PolynomialParser.Parse(input);
        Assert.Equal(expected, coeffs);
    }

    [Theory]
    [InlineData("x^2 - 5x + 6", true)]
    [InlineData("-x^4", true)]
    [InlineData("7", true)]
    [InlineData("(x + 3) / 12", false)]   // parentheses and division -> cannot parse
    [InlineData("x / 9", false)]           // division -> cannot parse
    [InlineData("sin(x)", false)]          // not a polynomial
    public void CanParse_Detects_Polynomial_Form(string input, bool expected)
    {
        Assert.Equal(expected, PolynomialParser.CanParse(input));
    }

    [Fact]
    public void Parse_Throws_On_Invalid_Term()
    {
        Assert.Throws<FormatException>(() => PolynomialParser.Parse("x^2 + foo"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~PolynomialParserTests"`
Expected: FAIL (type not found).

- [ ] **Step 3: Implement PolynomialParser**

Create `Services/Equations/PolynomialParser.cs`:

```csharp
using System.Globalization;
using System.Text.RegularExpressions;

namespace AdvancedCalculaterBot.Services.Equations;

/// <summary>
/// Parses a single-variable (x) polynomial from text into a coefficient array,
/// lowest-degree first (index i = coefficient of x^i). Does not handle
/// parentheses or division — those equations should use <see cref="LinearReducer"/>.
/// </summary>
public static class PolynomialParser
{
    // Matches one term: optional sign, optional numeric coefficient, optional x with optional ^exponent.
    // Examples that match: "+6", "-5x", "x", "x^2", "-x^4", "3x^3", "2x", "-3"
    private static readonly Regex TermRegex = new(
        @"([+-]?)\s*([^x+\-\s]+)?\s*(x(?:\^(\d+))?)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// True if <paramref name="input"/> is a bare polynomial (no parentheses,
    /// no division, only digits/signs/x/^). Cheap check — does not validate term grammar.
    /// </summary>
    public static bool CanParse(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return true;
        foreach (char c in input)
        {
            bool ok = char.IsDigit(c) || char.IsLetter(c) ||
                      c == 'x' || c == 'X' || c == '^' ||
                      c == '+' || c == '-' || c == '.' || c == ',' ||
                      char.IsWhiteSpace(c);
            if (!ok) return false;
        }
        return true;
    }

    /// <summary>Parses a polynomial into coefficients (low→high).</summary>
    /// <exception cref="FormatException">A term cannot be parsed.</exception>
    public static double[] Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new double[] { 0 };

        // Normalize: ensure a leading sign for uniform splitting, remove spaces.
        string s = input.Replace(" ", "").Replace("\t", "");
        if (s.Length == 0) return new double[] { 0 };
        if (s[0] != '+' && s[0] != '-') s = "+" + s;

        // Split into terms while keeping the leading sign on each.
        var terms = new List<string>();
        int start = 0;
        for (int i = 1; i < s.Length; i++)
        {
            if (s[i] == '+' || s[i] == '-')
            {
                terms.Add(s.Substring(start, i - start));
                start = i;
            }
        }
        terms.Add(s.Substring(start));

        var coeffs = new List<double>();
        foreach (var term in terms)
        {
            if (string.IsNullOrEmpty(term)) continue;
            var (coef, power) = ParseTerm(term);
            while (coeffs.Count <= power) coeffs.Add(0);
            coeffs[power] += coef;
        }

        return coeffs.Count == 0 ? new double[] { 0 } : coeffs.ToArray();
    }

    private static (double coef, int power) ParseTerm(string term)
    {
        // term looks like: "+6", "-5x", "+x", "-x^4", "3x^3", "+2x"
        bool negative = term[0] == '-';
        string body = term.Substring(1); // drop the sign char

        int xIndex = body.IndexOf('x');
        if (xIndex < 0)
        {
            // pure constant
            if (!double.TryParse(body, NumberStyles.Any, CultureInfo.InvariantCulture, out double c))
                throw new FormatException($"Cannot parse term '{term}'.");
            return (negative ? -c : c, 0);
        }

        // coefficient part (before x)
        double coef;
        string coefPart = body.Substring(0, xIndex);
        if (string.IsNullOrEmpty(coefPart)) coef = 1;
        else if (!double.TryParse(coefPart, NumberStyles.Any, CultureInfo.InvariantCulture, out coef))
            throw new FormatException($"Cannot parse coefficient in term '{term}'.");

        coef = negative ? -coef : coef;

        // exponent part (after x, optionally after ^)
        int power = 1;
        int caret = body.IndexOf('^', xIndex);
        if (caret >= 0)
        {
            string expPart = body.Substring(caret + 1);
            if (!int.TryParse(expPart, out power) || power < 0)
                throw new FormatException($"Cannot parse exponent in term '{term}'.");
        }
        else if (body.Length > xIndex + 1)
        {
            // characters after x that aren't '^' -> invalid
            throw new FormatException($"Cannot parse term '{term}'.");
        }

        return (coef, power);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~PolynomialParserTests"`
Expected: PASS — all tests green. If any `[InlineData]` case fails, fix the parser before continuing.

- [ ] **Step 5: Commit**

```bash
git add Services/Equations/PolynomialParser.cs AdvancedCalculaterBot.Tests/PolynomialParserTests.cs
git commit -m "feat(equations): add PolynomialParser for text-to-coefficient conversion"
```

---

### Task 4: Implement LinearReducer

For degree-1 equations with structure the parser cannot handle (parentheses, division), e.g. `(x + 3) / 12 = x / 9`. Strategy: sample `f(x) = LHS(x) - RHS(x)` via NCalc at three points, derive `ax + b`, verify linearity.

**Files:**
- Create: `Services/Equations/LinearReducer.cs`
- Test: `AdvancedCalculaterBot.Tests/LinearReducerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `AdvancedCalculaterBot.Tests/LinearReducerTests.cs`:

```csharp
using AdvancedCalculaterBot.Services.Equations;
using Xunit;

namespace AdvancedCalculaterBot.Tests;

public class LinearReducerTests
{
    [Fact]
    public void Reduce_Linear_Fractional_Equation_Returns_Correct_Root()
    {
        // (x + 3) / 12 = x / 9  ->  x = 9
        // So f(x) = (x+3)/12 - x/9 has root 9: coeffs should be [b, a] with -b/a = 9.
        var coeffs = LinearReducer.Reduce("(x + 3) / 12", "x / 9");
        // coeffs = [b, a]; root = -b/a
        double root = -coeffs[0] / coeffs[1];
        Assert.Equal(9.0, root, precision: 6);
    }

    [Fact]
    public void Reduce_Simple_Linear_Parts_Works_Too()
    {
        // 2x + 1 = 3x - 4  ->  x = 5
        var coeffs = LinearReducer.Reduce("2x + 1", "3x - 4");
        double root = -coeffs[0] / coeffs[1];
        Assert.Equal(5.0, root, precision: 6);
    }

    [Fact]
    public void Reduce_Throws_When_Not_Linear()
    {
        // x^2 = 4 is not linear in x; samples will not be collinear.
        Assert.Throws<InvalidOperationException>(
            () => LinearReducer.Reduce("x^2", "4"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~LinearReducerTests"`
Expected: FAIL (type not found).

- [ ] **Step 3: Implement LinearReducer**

Create `Services/Equations/LinearReducer.cs`:

```csharp
using System.Globalization;
using NCalc;

namespace AdvancedCalculaterBot.Services.Equations;

/// <summary>
/// Reduces a structured linear equation (one that may contain parentheses,
/// division, or other operations valid in NCalc but not in <see cref="PolynomialParser"/>)
/// into coefficients [b, a] of ax + b = 0, by sampling f(x) = LHS(x) - RHS(x).
/// </summary>
public static class LinearReducer
{
    private const double Tolerance = 1e-9;

    /// <summary>
    /// Returns coefficients [b, a] such that LHS - RHS is equivalent to a*x + b.
    /// Throws <see cref="InvalidOperationException"/> if the expression is not linear.
    /// </summary>
    public static double[] Reduce(string lhs, string rhs)
    {
        double f0 = Evaluate(lhs, 0) - Evaluate(rhs, 0);
        double f1 = Evaluate(lhs, 1) - Evaluate(rhs, 1);
        double f2 = Evaluate(lhs, 2) - Evaluate(rhs, 2);

        double b = f0;
        double a = f1 - f0;

        // Verify linearity: f(2) must equal 2*a + b = 2*f1 - f0.
        double expected2 = 2 * f1 - f0;
        if (Math.Abs(f2 - expected2) > Tolerance)
        {
            throw new InvalidOperationException(
                "The equation is not linear in x.");
        }

        return new[] { b, a };
    }

    private static double Evaluate(string expression, double xValue)
    {
        var expr = new Expression(expression);
        expr.Parameters["x"] = xValue;
        var result = expr.Evaluate();
        return Convert.ToDouble(result, CultureInfo.InvariantCulture);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~LinearReducerTests"`
Expected: PASS — all 3 tests green.

- [ ] **Step 5: Commit**

```bash
git add Services/Equations/LinearReducer.cs AdvancedCalculaterBot.Tests/LinearReducerTests.cs
git commit -m "feat(equations): add LinearReducer for structured degree-1 equations"
```

---

### Task 5: Implement LinearSolver and QuadraticSolver

**Files:**
- Create: `Services/Equations/LinearSolver.cs`
- Create: `Services/Equations/QuadraticSolver.cs`
- Test: `AdvancedCalculaterBot.Tests/SolverTests.cs` (will be extended in later tasks)

- [ ] **Step 1: Write the failing tests**

Create `AdvancedCalculaterBot.Tests/SolverTests.cs`:

```csharp
using System.Numerics;
using AdvancedCalculaterBot.Services.Equations;
using Xunit;

namespace AdvancedCalculaterBot.Tests;

public class SolverTests
{
    private static bool Near(Complex a, Complex b, double tol = 1e-6) =>
        Complex.Abs(a - b) < tol;

    // ---- LinearSolver ----

    [Fact]
    public void LinearSolver_Returns_Single_Root()
    {
        // 2x + 6 = 0  ->  x = -3
        var roots = LinearSolver.Solve(new[] { 6.0, 2.0 });
        Assert.Single(roots);
        Assert.True(Near(roots[0], new Complex(-3, 0)));
    }

    // ---- QuadraticSolver ----

    [Fact]
    public void QuadraticSolver_Two_Real_Distinct_Roots()
    {
        // x^2 - 5x + 6 = 0  ->  x in {2, 3}
        var roots = QuadraticSolver.Solve(new[] { 6.0, -5.0, 1.0 });
        Assert.Equal(2, roots.Length);
        Assert.Contains(roots, r => Near(r, new Complex(2, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(3, 0)));
    }

    [Fact]
    public void QuadraticSolver_Complex_Roots()
    {
        // x^2 + 1 = 0  ->  x in {i, -i}
        var roots = QuadraticSolver.Solve(new[] { 1.0, 0.0, 1.0 });
        Assert.Equal(2, roots.Length);
        Assert.Contains(roots, r => Near(r, new Complex(0, 1)));
        Assert.Contains(roots, r => Near(r, new Complex(0, -1)));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~SolverTests"`
Expected: FAIL (types not found).

- [ ] **Step 3: Implement LinearSolver**

Create `Services/Equations/LinearSolver.cs`:

```csharp
using System.Numerics;

namespace AdvancedCalculaterBot.Services.Equations;

/// <summary>Solves ax + b = 0 given coefficients [b, a]. Returns a single root.</summary>
public static class LinearSolver
{
    public static Complex[] Solve(double[] coeffs)
    {
        // coeffs = [b, a]; the orchestrator guarantees degree 1, so a != 0 here.
        double b = coeffs[0];
        double a = coeffs[1];
        return new[] { new Complex(-b / a, 0) };
    }
}
```

- [ ] **Step 4: Implement QuadraticSolver**

Create `Services/Equations/QuadraticSolver.cs`:

```csharp
using System.Numerics;

namespace AdvancedCalculaterBot.Services.Equations;

/// <summary>
/// Solves ax^2 + bx + c = 0 given coefficients [c, b, a] (low→high).
/// Handles real and complex roots via Complex.Sqrt. Adapted from
/// EquationSolvers/QuadraticEquation/Program.cs.
/// </summary>
public static class QuadraticSolver
{
    public static Complex[] Solve(double[] coeffs)
    {
        double c = coeffs[0];
        double b = coeffs[1];
        double a = coeffs[2];

        Complex delta = b * b - 4 * a * c;
        Complex sqrtDelta = Complex.Sqrt(delta);

        Complex x1 = (-b + sqrtDelta) / (2 * a);
        Complex x2 = (-b - sqrtDelta) / (2 * a);

        return new[] { x1, x2 };
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~SolverTests"`
Expected: PASS — all 3 tests green.

- [ ] **Step 6: Commit**

```bash
git add Services/Equations/LinearSolver.cs Services/Equations/QuadraticSolver.cs AdvancedCalculaterBot.Tests/SolverTests.cs
git commit -m "feat(equations): add LinearSolver and QuadraticSolver"
```

---

### Task 6: Implement CubicSolver

Adapted from `EquationSolvers/CubicEquation/Program.cs:44-78` (Cardano's method). Takes `[d, c, b, a]` (low→high), returns up to 3 unique roots.

**Files:**
- Create: `Services/Equations/CubicSolver.cs`
- Modify: `AdvancedCalculaterBot.Tests/SolverTests.cs` (append cubic tests)

- [ ] **Step 1: Append the failing tests**

Add the following tests to the `SolverTests` class in `AdvancedCalculaterBot.Tests/SolverTests.cs` (after the existing quadratic tests, before the closing brace of the class):

```csharp
    // ---- CubicSolver ----

    [Fact]
    public void CubicSolver_Three_Real_Distinct_Roots()
    {
        // x^3 - 6x^2 + 11x - 6 = 0  ->  x in {1, 2, 3}
        var roots = CubicSolver.Solve(new[] { -6.0, 11.0, -6.0, 1.0 });
        Assert.Equal(3, roots.Length);
        Assert.Contains(roots, r => Near(r, new Complex(1, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(2, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(3, 0)));
    }

    [Fact]
    public void CubicSolver_One_Real_Root_Two_Complex()
    {
        // x^3 + 1 = 0  ->  x in {-1, (1 ± i*sqrt(3))/2 }
        var roots = CubicSolver.Solve(new[] { 1.0, 0.0, 0.0, 1.0 });
        Assert.Equal(3, roots.Length);
        Assert.Contains(roots, r => Near(r, new Complex(-1, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(0.5, Math.Sqrt(3) / 2)));
        Assert.Contains(roots, r => Near(r, new Complex(0.5, -Math.Sqrt(3) / 2)));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~SolverTests.CubicSolver"`
Expected: FAIL (type not found).

- [ ] **Step 3: Implement CubicSolver**

Create `Services/Equations/CubicSolver.cs`:

```csharp
using System.Numerics;

namespace AdvancedCalculaterBot.Services.Equations;

/// <summary>
/// Solves ax^3 + bx^2 + cx + d = 0 given coefficients [d, c, b, a] (low→high)
/// using Cardano's method. Adapted from EquationSolvers/CubicEquation/Program.cs.
/// Returns deduplicated roots.
/// </summary>
public static class CubicSolver
{
    private const double DedupTolerance = 1e-6;

    public static Complex[] Solve(double[] coeffs)
    {
        double d = coeffs[0];
        double c = coeffs[1];
        double b = coeffs[2];
        double a = coeffs[3];

        // Normalize to monic: x^3 + A x^2 + B x + C
        double A = b / a;
        double B = c / a;
        double C = d / a;

        // Depress via x = t - A/3:  t^3 + p t + q = 0
        double p = B - A * A / 3.0;
        double q = (2 * A * A * A) / 27.0 - (A * B) / 3.0 + C;

        Complex delta = Complex.Pow(q / 2.0, 2) + Complex.Pow(p / 3.0, 3);

        Complex u = Complex.Pow(-q / 2.0 + Complex.Sqrt(delta), 1.0 / 3.0);
        Complex v = Complex.Pow(-q / 2.0 - Complex.Sqrt(delta), 1.0 / 3.0);

        Complex omega = new Complex(-0.5, Math.Sqrt(3) / 2.0);

        Complex t1 = u + v;
        Complex t2 = u * omega + v * Complex.Conjugate(omega);
        Complex t3 = u * Complex.Conjugate(omega) + v * omega;

        // Back-substitute x = t - A/3
        var roots = new[]
        {
            t1 - A / 3.0,
            t2 - A / 3.0,
            t3 - A / 3.0
        };

        return Deduplicate(roots);
    }

    private static Complex[] Deduplicate(Complex[] roots)
    {
        var unique = new List<Complex>();
        foreach (var r in roots)
        {
            bool dup = unique.Any(ur => Complex.Abs(r - ur) < DedupTolerance);
            if (!dup) unique.Add(r);
        }
        return unique.ToArray();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~SolverTests.CubicSolver"`
Expected: PASS — both cubic tests green.

- [ ] **Step 5: Commit**

```bash
git add Services/Equations/CubicSolver.cs AdvancedCalculaterBot.Tests/SolverTests.cs
git commit -m "feat(equations): add CubicSolver (Cardano)"
```

---

### Task 7: Implement QuarticSolver (Ferrari's method)

Uses `CubicSolver` (resolvent) and `QuadraticSolver` (factor pair).

**Files:**
- Create: `Services/Equations/QuarticSolver.cs`
- Modify: `AdvancedCalculaterBot.Tests/SolverTests.cs` (append quartic tests)

- [ ] **Step 1: Append the failing tests**

Add the following tests to the `SolverTests` class in `AdvancedCalculaterBot.Tests/SolverTests.cs`:

```csharp
    // ---- QuarticSolver ----

    [Fact]
    public void QuarticSolver_Four_Real_Roots()
    {
        // x^4 - 10x^2 + 9 = 0  ->  x in {-3, -1, 1, 3}
        var roots = QuarticSolver.Solve(new[] { 9.0, 0.0, -10.0, 0.0, 1.0 });
        Assert.Equal(4, roots.Length);
        Assert.Contains(roots, r => Near(r, new Complex(-3, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(-1, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(1, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(3, 0)));
    }

    [Fact]
    public void QuarticSolver_Complex_Roots()
    {
        // x^4 + 1 = 0  ->  x in {(±1 ± i)/sqrt(2)} -> 4 distinct complex roots
        var roots = QuarticSolver.Solve(new[] { 1.0, 0.0, 0.0, 0.0, 1.0 });
        Assert.Equal(4, roots.Length);
        double m = 1.0 / Math.Sqrt(2);
        Assert.Contains(roots, r => Near(r, new Complex(m, m)));
        Assert.Contains(roots, r => Near(r, new Complex(-m, m)));
        Assert.Contains(roots, r => Near(r, new Complex(-m, -m)));
        Assert.Contains(roots, r => Near(r, new Complex(m, -m)));
    }

    [Fact]
    public void QuarticSolver_Repeated_Roots()
    {
        // (x-2)^2 (x-3)^2 = (x^2 - 5x + 6)^2 = x^4 - 10x^3 + 37x^2 - 60x + 36
        var roots = QuarticSolver.Solve(new[] { 36.0, -60.0, 37.0, -10.0, 1.0 });
        // After dedup: exactly {2, 3}
        Assert.Equal(2, roots.Length);
        Assert.Contains(roots, r => Near(r, new Complex(2, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(3, 0)));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~SolverTests.QuarticSolver"`
Expected: FAIL (type not found).

- [ ] **Step 3: Implement QuarticSolver**

Create `Services/Equations/QuarticSolver.cs`:

```csharp
using System.Numerics;

namespace AdvancedCalculaterBot.Services.Equations;

/// <summary>
/// Solves ax^4 + bx^3 + cx^2 + dx + e = 0 given coefficients [e, d, c, b, a]
/// (low→high) using Ferrari's method: depress, solve the resolvent cubic,
/// factor into two quadratics (with complex coefficients where necessary),
/// back-substitute.
/// </summary>
public static class QuarticSolver
{
    private const double BiquadraticTolerance = 1e-9;
    private const double RealRootTolerance = 1e-6;
    private const double DedupTolerance = 1e-6;

    public static Complex[] Solve(double[] coeffs)
    {
        double e = coeffs[0];
        double d = coeffs[1];
        double c = coeffs[2];
        double b = coeffs[3];
        double a = coeffs[4];

        // Normalize to monic: x^4 + B x^3 + C x^2 + D x + E
        double B = b / a;
        double C = c / a;
        double D = d / a;
        double E = e / a;

        // Depress via x = y - B/4:  y^4 + p y^2 + q y + r = 0
        double p = C - 3.0 * B * B / 8.0;
        double q = D - B * C / 2.0 + B * B * B / 8.0;
        double r = E - B * D / 4.0 + B * B * C / 16.0 - 3.0 * B * B * B * B / 256.0;

        List<Complex> yRoots;

        if (Math.Abs(q) < BiquadraticTolerance)
        {
            // Biquadratic: y^4 + p y^2 + r = 0 -> z^2 + p z + r = 0 for z = y^2.
            var zRoots = QuadraticSolver.Solve(new[] { r, p, 1.0 });
            yRoots = new List<Complex>();
            foreach (var z in zRoots)
            {
                yRoots.Add(Complex.Sqrt(z));
                yRoots.Add(-Complex.Sqrt(z));
            }
        }
        else
        {
            // Ferrari resolvent cubic (in m):  8 m^3 + 8 p m^2 + (2 p^2 - 8 r) m - q^2 = 0
            double rc_a = 8.0;
            double rc_b = 8.0 * p;
            double rc_c = 2.0 * p * p - 8.0 * r;
            double rc_d = -q * q;

            var resolventRoots = CubicSolver.Solve(new[] { rc_d, rc_c, rc_b, rc_a });

            // Pick any resolvent root whose imaginary part is (near) zero. The
            // resolvent of a real-coefficient quartic always has a real root.
            Complex m = resolventRoots.First(rr => Math.Abs(rr.Imaginary) < RealRootTolerance);

            yRoots = SolveFerrari(p, q, m);
        }

        // Back-substitute x = y - B/4.
        var xRoots = yRoots.Select(y => y - B / 4.0).ToList();
        return Deduplicate(xRoots);
    }

    // Given depressed y^4 + p y^2 + q y + r = 0 and a chosen resolvent root m,
    // factor into two quadratics and solve them. The factorization is
    //   (y^2 + α y + β)(y^2 − α y + γ)
    // where α² = 2m + p,  β + γ = m,  γ − β = q / α.
    // All four y-roots come from the two quadratics with these (possibly complex) coefficients.
    private static List<Complex> SolveFerrari(double p, double q, Complex m)
    {
        Complex alphaSquared = 2.0 * m + p;
        Complex alpha = Complex.Sqrt(alphaSquared);

        Complex beta, gamma;
        if (Complex.Abs(alpha) < 1e-9)
        {
            // α == 0 implies q == 0 in exact arithmetic; symmetric fallback.
            beta = m / 2.0;
            gamma = m / 2.0;
        }
        else
        {
            beta = (m - q / alpha) / 2.0;
            gamma = (m + q / alpha) / 2.0;
        }

        var roots = new List<Complex>(4);
        roots.AddRange(SolveQuadraticComplex(1.0, alpha, beta));
        roots.AddRange(SolveQuadraticComplex(1.0, -alpha, gamma));
        return roots;
    }

    // Quadratic formula with arbitrary complex coefficients.
    private static Complex[] SolveQuadraticComplex(Complex a, Complex b, Complex c)
    {
        Complex disc = Complex.Sqrt(b * b - 4 * a * c);
        return new[]
        {
            (-b + disc) / (2 * a),
            (-b - disc) / (2 * a)
        };
    }

    private static Complex[] Deduplicate(List<Complex> roots)
    {
        var unique = new List<Complex>();
        foreach (var r in roots)
        {
            bool dup = unique.Any(ur => Complex.Abs(r - ur) < DedupTolerance);
            if (!dup) unique.Add(r);
        }
        return unique.ToArray();
    }
}
```

> **Note for the implementer:** The biquadratic branch (used by `QuarticSolver_Four_Real_Roots`, since `x^4 - 10x^2 + 9` has `q = 0`) is simple and should pass first. The two non-biquadratic tests (`QuarticSolver_Complex_Roots` on `x^4 + 1`, and `QuarticSolver_Repeated_Roots`) exercise the `SolveFerrari` path. If the biquadratic test passes but the Ferrari-path tests fail, the issue is in `SolveFerrari` or `SolveQuadraticComplex` — do not change the biquadratic branch or `Deduplicate`. The key correctness points: (1) `alpha = Complex.Sqrt(2m + p)` must use complex sqrt (not real), since `2m + p` can be negative; (2) the two quadratics use `+alpha` and `-alpha` as their linear coefficients; (3) `SolveQuadraticComplex` takes `(a, b, c)` in **standard high-to-low** order, unlike `QuadraticSolver.Solve` which takes low-to-high `double[]`.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~SolverTests.QuarticSolver"`
Expected: PASS — all 3 quartic tests green. If the complex-roots or repeated-roots test fails, follow the note in Step 3.

- [ ] **Step 5: Commit**

```bash
git add Services/Equations/QuarticSolver.cs AdvancedCalculaterBot.Tests/SolverTests.cs
git commit -m "feat(equations): add QuarticSolver (Ferrari)"
```

---

### Task 8: Implement EquationSolverService (orchestrator)

Public entry point called by the bot. Splits on `=`, parses/reduces, dispatches by degree, returns a formatted multi-line string. Owns all error messages.

**Files:**
- Create: `Services/Equations/EquationSolverService.cs`
- Test: `AdvancedCalculaterBot.Tests/EquationSolverServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `AdvancedCalculaterBot.Tests/EquationSolverServiceTests.cs`:

```csharp
using AdvancedCalculaterBot.Services.Equations;
using Xunit;

namespace AdvancedCalculaterBot.Tests;

public class EquationSolverServiceTests
{
    [Fact]
    public void Solve_Linear_Fractional_Returns_Correct_Root()
    {
        var output = EquationSolverService.Solve("(x + 3) / 12 = x / 9");
        Assert.Contains("x = 9", output);
    }

    [Fact]
    public void Solve_Simple_Linear()
    {
        var output = EquationSolverService.Solve("2x + 6 = 0");
        Assert.Contains("x = -3", output);
    }

    [Fact]
    public void Solve_Quadratic_Two_Roots()
    {
        var output = EquationSolverService.Solve("x^2 - 5x + 6 = 0");
        Assert.Contains("x₁ = 2", output);
        Assert.Contains("x₂ = 3", output);
    }

    [Fact]
    public void Solve_Cubic_Three_Roots()
    {
        var output = EquationSolverService.Solve("x^3 - 6x^2 + 11x - 6 = 0");
        Assert.Contains("1", output);
        Assert.Contains("2", output);
        Assert.Contains("3", output);
    }

    [Fact]
    public void Solve_Quartic_Four_Roots()
    {
        var output = EquationSolverService.Solve("x^4 - 10x^2 + 9 = 0");
        Assert.Contains("-3", output);
        Assert.Contains("-1", output);
        Assert.Contains("1", output);
        Assert.Contains("3", output);
    }

    [Fact]
    public void Solve_No_Solution_Contradiction()
    {
        var output = EquationSolverService.Solve("2 = 3");
        Assert.Equal("No solution.", output);
    }

    [Fact]
    public void Solve_Infinite_Solutions_Identity()
    {
        var output = EquationSolverService.Solve("2x = 2x");
        Assert.Equal("Infinite solutions.", output);
    }

    [Fact]
    public void Solve_Degree_Too_High()
    {
        var output = EquationSolverService.Solve("x^5 + 1 = 0");
        Assert.Contains("up to degree 4", output);
    }

    [Fact]
    public void Solve_Missing_Variable_Returns_Error()
    {
        var output = EquationSolverService.Solve("2 + 2 = 4");
        Assert.Contains("only", output.ToLower());
    }

    [Fact]
    public void Solve_Empty_Side_Returns_Error()
    {
        var output = EquationSolverService.Solve("= 5");
        Assert.Contains("left and right", output.ToLower());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~EquationSolverServiceTests"`
Expected: FAIL (type not found).

- [ ] **Step 3: Implement EquationSolverService**

Create `Services/Equations/EquationSolverService.cs`:

```csharp
using System.Numerics;

namespace AdvancedCalculaterBot.Services.Equations;

/// <summary>
/// Orchestrates equation solving: split on '=', parse or reduce to a single
/// polynomial equal to zero, dispatch to the right solver by degree, and
/// format the result. This is the public entry point called by the bot.
/// </summary>
public static class EquationSolverService
{
    private const double ZeroTolerance = 1e-9;

    /// <summary>
    /// Solves an equation string and returns a human-readable reply.
    /// Never throws for expected failures — returns an error message instead.
    /// </summary>
    public static string Solve(string equation)
    {
        int eq = equation.IndexOf('=');
        if (eq < 0)
            return "That doesn't look like an equation (no '=' found).";

        // Reject chains like a = b = c.
        if (equation.IndexOf('=', eq + 1) >= 0)
            return "Please provide an equation with a single '=' sign.";

        string lhs = equation.Substring(0, eq).Trim();
        string rhs = equation.Substring(eq + 1).Trim();

        if (string.IsNullOrWhiteSpace(lhs) || string.IsNullOrWhiteSpace(rhs))
            return "Equation must have a left and right side.";

        if (!ContainsVariable(equation))
            return "Only equations in the variable 'x' are supported.";

        try
        {
            double[] coeffs = GetDifferenceCoeffs(lhs, rhs);
            return SolveFromCoefficients(coeffs);
        }
        catch (FormatException ex)
        {
            return $"Sorry, I couldn't parse \"{equation}\" as an equation. ({ex.Message})";
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    private static bool ContainsVariable(string equation)
    {
        foreach (char c in equation)
            if (c == 'x' || c == 'X') return true;
        return false;
    }

    private static double[] GetDifferenceCoeffs(string lhs, string rhs)
    {
        double[] left, right;
        if (PolynomialParser.CanParse(lhs) && PolynomialParser.CanParse(rhs))
        {
            left = PolynomialParser.Parse(lhs);
            right = PolynomialParser.Parse(rhs);
        }
        else
        {
            // LinearReducer returns [b, a]; non-linear inputs throw InvalidOperationException.
            return LinearReducer.Reduce(lhs, rhs);
        }

        int len = Math.Max(left.Length, right.Length);
        var diff = new double[len];
        for (int i = 0; i < len; i++)
        {
            double l = i < left.Length ? left[i] : 0;
            double r = i < right.Length ? right[i] : 0;
            diff[i] = l - r;
        }
        return diff;
    }

    private static string SolveFromCoefficients(double[] coeffs)
    {
        int degree = Degree(coeffs);

        if (degree == 0)
        {
            // All x coefficients are zero. Either an identity or a contradiction.
            return Math.Abs(coeffs[0]) < ZeroTolerance
                ? "Infinite solutions."
                : "No solution.";
        }

        Complex[] roots = degree switch
        {
            1 => LinearSolver.Solve(coeffs),
            2 => QuadraticSolver.Solve(coeffs),
            3 => CubicSolver.Solve(coeffs),
            4 => QuarticSolver.Solve(coeffs),
            _ => Array.Empty<Complex>()
        };

        if (degree > 4)
            return "I can only solve equations up to degree 4 (quartic).";

        return ComplexFormatter.FormatRoots(roots);
    }

    /// <summary>
    /// Returns the highest index whose coefficient is non-tiny, or 0 if all are
    /// (near) zero.
    /// </summary>
    private static int Degree(double[] coeffs)
    {
        for (int i = coeffs.Length - 1; i > 0; i--)
            if (Math.Abs(coeffs[i]) > ZeroTolerance)
                return i;
        return 0;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test AdvancedCalculaterBot.Tests --filter "FullyQualifiedName~EquationSolverServiceTests"`
Expected: PASS — all 10 tests green. If `Solve_Quartic_Four_Roots` fails, the issue is in `QuarticSolver`, not here — go back to Task 7.

- [ ] **Step 5: Commit**

```bash
git add Services/Equations/EquationSolverService.cs AdvancedCalculaterBot.Tests/EquationSolverServiceTests.cs
git commit -m "feat(equations): add EquationSolverService orchestrator"
```

---

### Task 9: Wire equation routing into Program.cs

Modify the bot message handler so any message containing `=` and `x` routes to `EquationSolverService.Solve`; everything else continues to use `CalculatorService`.

**Files:**
- Modify: `Program.cs` (the inner `try { ... }` block around lines 95-106)

- [ ] **Step 1: Add the using directive and routing branch**

In `Program.cs`:

1. Add this using directive at the top, alongside the existing `using AdvancedCalculaterBot.Services;` (line 7):

```csharp
using AdvancedCalculaterBot.Services.Equations;
```

2. Replace the existing inner block (Program.cs lines 95-106, the `try { tracker.Add... } catch {...}` block) with:

```csharp
                tracker.Add(expression);

                bool isEquation = expression.Contains('=')
                                  && expression.Contains('x', StringComparison.OrdinalIgnoreCase);

                if (isEquation)
                {
                    response = EquationSolverService.Solve(expression);
                }
                else
                {
                    try
                    {
                        var calculatorService = new CalculatorService(expression);
                        var result = calculatorService.Evaluate();
                        response = $"{expression} = {result}";
                    }
                    catch
                    {
                        response = $"Sorry, I couldn't understand \"{expression}\" as a mathematical expression.";
                    }
                }
```

Note: the outer `try`/`catch (Exception ex)` at Program.cs:61/108 stays as-is, so any unexpected exception from `EquationSolverService` is still caught and logged. The `EquationSolverService.Solve` itself returns error strings rather than throwing for expected failures.

- [ ] **Step 2: Build the whole solution**

Run: `dotnet build`
Expected: BUILD SUCCEEDED with no errors or warnings.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test`
Expected: ALL tests pass.

- [ ] **Step 4: Commit**

```bash
git add Program.cs
git commit -m "feat(bot): route equations containing '=' and 'x' to EquationSolverService"
```

---

### Task 10: Final verification

No new code — confirm everything works end to end.

- [ ] **Step 1: Build and test the whole solution one more time**

Run: `dotnet build && dotnet test`
Expected: BUILD SUCCEEDED, all tests PASS.

- [ ] **Step 2: Sanity-check the routing logic by reading Program.cs**

Open `Program.cs` and confirm:
- The new `using AdvancedCalculaterBot.Services.Equations;` is present.
- `isEquation` is true only when the message contains `=` **and** `x`/`X`.
- The calculation path is unchanged for messages without those characters.

- [ ] **Step 3: Manual scenario checklist (review only — do not need to run the bot)**

Confirm by reading code that each of these inputs produces the documented output:

| Input | Expected reply |
|---|---|
| `(x + 3) / 12 = x / 9` | `x = 9` |
| `2x + 6 = 0` | `x = -3` |
| `x^2 - 5x + 6 = 0` | `x₁ = 2\nx₂ = 3` |
| `x^3 - 6x^2 + 11x - 6 = 0` | `x₁ = 1\nx₂ = 2\nx₃ = 3` |
| `x^4 - 10x^2 + 9 = 0` | `x₁ = -3\nx₂ = -1\nx₃ = 1\nx₄ = 3` |
| `x^2 + 1 = 0` | `x₁ = i\nx₂ = -i` |
| `2 = 3` | `No solution.` |
| `2x = 2x` | `Infinite solutions.` |
| `x^5 + 1 = 0` | `I can only solve equations up to degree 4 (quartic).` |
| `2 + 2` (no `=`) | routed to CalculatorService → `2 + 2 = 4` |

- [ ] **Step 4: Final commit if anything was tweaked during verification**

Only commit if a fix was needed in Step 1-2. Otherwise this task is a no-op.

```bash
git status   # should be clean
```

---

## Done

The bot now solves linear, quadratic, cubic, and quartic equations entered as natural text, alongside its existing numeric calculation feature. All equation code lives under `Services/Equations/` and is covered by unit tests.
