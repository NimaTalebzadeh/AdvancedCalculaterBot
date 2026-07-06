# Equation-Solving Feature Design

**Date:** 2026-07-06
**Status:** Approved (pending user review of written spec)
**Project:** AdvancedCalculaterBot

## Goal

Extend the Telegram bot so that, in addition to evaluating numeric expressions like `2 + 2 * (3 + 4)`, it can **solve equations** containing the variable `x` and an `=` sign. Supported equation families:

- **Linear / fractional** with structure, e.g. `(x + 3) / 12 = x / 9` → `x = 9` (check: `9·(x+3) = 12x` → `9x + 27 = 12x` → `x = 9`)
- **Polynomial** up to degree 4:
  - Quadratic: `x^2 - 5x + 6 = 0` → `x₁ = 2, x₂ = 3`
  - Cubic: `2x^3 + 3x^2 - 11x - 6 = 0` (Cardano, adapted from the existing `EquationSolvers` project)
  - Quartic: `x^4 - 10x^2 + 9 = 0` → `x ∈ {-3, -1, 1, 3}` (Ferrari's method)

Real **and** complex roots are reported, matching the existing `EquationSolvers` console projects' style.

## Non-Goals

- No support for variables other than `x`.
- No support for polynomial degree > 4.
- No symbolic algebra engine (no factoring, simplification, indefinite solving of arbitrary transcendental equations).
- No change to the existing numeric `CalculatorService`.
- No change to `ComplexityTracker` / `ComplexityCalculator` (equations are tracked just like calculations).
- No new NuGet packages — `System.Numerics.Complex` (BCL) and existing `NCalc` are sufficient.

## Routing

Each incoming message is auto-routed by content (no new command required):

| Condition | Treatment |
|---|---|
| Contains `=` **and** contains `x` | Equation → `EquationSolverService.Solve(text)` |
| Otherwise | Numeric calculation → existing `CalculatorService.Evaluate()` |

`/calc` is still accepted as a prefix strip. An explicit `/solve <equation>` is **not** added (auto-detect only, per the user's choice).

## Architecture

```
Program.cs (bot message handler)
    ├─ auto-detects '=' + 'x'  → EquationSolverService.Solve(text)  → formatted multi-line reply
    └─ otherwise               → CalculatorService.Evaluate()       → "{expr} = {result}"  (unchanged)

Services/Equations/
    EquationSolverService      orchestrator: split on '=', parse, subtract, dispatch by degree, format
    PolynomialParser           text  →  coefficient array (lowest-degree first)
    LinearReducer              structured degree-1 eq  →  ax + b = 0  via sampling
    LinearSolver               ax + b = 0           →  Complex[]   (single root, or none/infinite)
    QuadraticSolver            ax^2 + bx + c = 0    →  Complex[]   (discriminant)
    CubicSolver                ax^3 + ... = 0       →  Complex[]   (Cardano, from EquationSolvers)
    QuarticSolver              ax^4 + ... = 0       →  Complex[]   (Ferrari)
    ComplexFormatter           Complex  →  string   (extracted from EquationSolvers' FormatComplex)
```

All solvers return `Complex[]` and delegate formatting to `ComplexFormatter`, keeping a single presentation policy.

## Component Details

### PolynomialParser

Parses a polynomial-in-`x` string into a `double[]` of coefficients, **lowest-degree first** (index `i` = coefficient of `x^i`).

Accepted term grammar (case-insensitive `x`; exponent marker `^`):

```
term   := [sign] [number] [ 'x' ['^' exponent] ]
sign   := '+' | '-'
```

Examples:

| Input | Output (coeffs, low→high) |
|---|---|
| `x^2 - 5x + 6` | `[6, -5, 1]` |
| `-x^4` | `[0, 0, 0, 0, -1]` |
| `x` | `[0, 1]` |
| `3x^3 - x + 5` | `[5, -1, 0, 3]` |
| `7` | `[7]` |

Rules:
- Implicit coefficient `1` / `-1` for `x`, `x^2`, etc.
- Whitespace tolerant.
- Rejects any character that is not a digit, sign, `x`, `^`, `. , or whitespace → throws `FormatException` with a descriptive message.
- Empty input → `[0]`.

**Does not** support parentheses or `/` — those are handled by `LinearReducer` for the degree-1 case only.

### LinearReducer

For degree-1 equations whose text contains structure the `PolynomialParser` cannot handle (parentheses, division, nested expressions), e.g. `(x + 3) / 12 = x / 9`.

Strategy (sampling — exact for genuine linear equations, avoids building a full symbolic engine):

1. Take `f(x) = LHS(x) - RHS(x)`, where each side is evaluated by `NCalc` with `x` substituted as a parameter.
2. Sample at two points: `f0 = f(0)`, `f1 = f(1)`.
3. For a linear function `f(x) = a·x + b`: `b = f0`, `a = f1 - f0`.
4. Verify linearity with a third sample: `f(2)` must equal `2a + b = 2·f1 - f0` within tolerance `1e-9`. If it doesn't, the equation is **not** actually linear → throw (caller reports "I can only solve linear equations of this form").
5. Return coefficients `[b, a]` (i.e. `ax + b = 0`).

### EquationSolverService (orchestrator)

```
Solve(text):
    split on first '=' into lhs, rhs
    if either side empty → error "Equation must have a left and right side."
    if 'x' not in text → error "Only equations in the variable 'x' are supported."

    if PolynomialParser.CanParse(lhs) and PolynomialParser.CanParse(rhs):
        coeffs = PolynomialParser.Parse(lhs) - PolynomialParser.Parse(rhs)  (term-wise)
    else:
        coeffs = LinearReducer.Reduce(lhs, rhs)        # throws if not linear

    trim trailing zeros (highest-degree) → degree = highest nonzero index
    if all zero:
        if constants equal (always true here) → "Infinite solutions."
    dispatch by degree:
        0  →  "No solution." or "Infinite solutions." (constant equality)
        1  →  LinearSolver
        2  →  QuadraticSolver
        3  →  CubicSolver
        4  →  QuarticSolver
        >4 →  "I can only solve equations up to degree 4 (quartic)."
    roots = solver.Solve(coeffs)
    return ComplexFormatter.FormatRoots(roots)
```

`PolynomialParser.CanParse` performs a cheap syntactic check (no `(`, `)`, `/`, and matches the term grammar) to decide whether to use direct parsing or fall back to `LinearReducer`.

### Solvers

All take `double[] coeffs` (low→high), return `Complex[]`.

**LinearSolver** (`[b, a]`): if `a == 0` → handled by orchestrator (no/infinite solutions); else single root `-b/a`.

**QuadraticSolver** (`[c, b, a]`): discriminant `Δ = b² - 4ac`, `x = (-b ± √Δ) / (2a)`. Uses `Complex.Sqrt` so negative Δ yields complex roots. Returns both roots (deduplicated when real and equal).

**CubicSolver** (`[d, c, b, a]`): adapted verbatim (algorithmically) from `EquationSolvers/CubicEquation/Program.cs` — depress to `p, q`, compute discriminant `δ`, Cardano roots `u + v`, apply cube roots of unity, deduplicate within `1e-6`.

**QuarticSolver** (`[e, d, c, b, a]`): Ferrari's method:
1. Divide by `a` to monic.
2. Depress: substitute `x = y - b/4` to remove the `y³` term → `y⁴ + py² + qy + r = 0`.
3. Solve the **resolvent cubic** (reuse `CubicSolver`) for `m`.
4. Factor into two quadratics in `y`, solve each with `QuadraticSolver`.
5. Back-substitute `x = y - b/4`.
6. Deduplicate within `1e-6`.

### ComplexFormatter

Extracted from `FormatComplex` in `EquationSolvers`:
- Snap near-zero real/imaginary parts to `0` (tolerance `1e-6`).
- Round to a sensible precision (6 dp).
- `i == 0` → `"r"`; `r == 0` → `"±i"`; else `"r ± i"`.
- `FormatRoots(Complex[])`: dedupes near-equal roots, labels `x = ...` for one root, `x₁ = ...`, `x₂ = ...` otherwise, joins with newlines.

## Error Handling

| Situation | Bot reply |
|---|---|
| No `=` in message | (Routed to `CalculatorService`, not an equation.) |
| `=` present but no `x` | "Only equations in the variable 'x' are supported." |
| Empty side (`"= 5"`, `"5 ="`) | "Equation must have a left and right side." |
| Both sides simplify to identical zero | "Infinite solutions." |
| Degree 0, constants unequal (`3 = 5`) | "No solution." |
| Degree > 4 | "I can only solve equations up to degree 4 (quartic)." |
| Equation looks linear but isn't (sampling mismatch) | "This equation is not linear; I can only solve polynomials up to degree 4." |
| Parse failure (bad term) | "Sorry, I couldn't parse \"{text}\" as an equation." |
| Any unexpected exception | "Sorry, there was an error solving that equation." (logged via Serilog) |

No solver path throws uncaught — the orchestrator wraps dispatch in try/catch.

## Testing

A new `AdvancedCalculaterBot.Tests` xUnit project targets `net10.0` and references the main project. Coverage:

**PolynomialParserTests**
- `x^2 - 5x + 6` → `[6, -5, 1]`
- `-x^4` → `[0,0,0,0,-1]`
- `x` → `[0, 1]`
- `3x^3 - x + 5` → `[5, -1, 0, 3]`
- `7` → `[7]`
- `(x + 1)` → throws / `CanParse` returns false

**LinearReducerTests**
- `(x + 3) / 12` vs `x / 9` → reduces so the single root is `x = 9`
- A genuinely nonlinear input (e.g. `x^2 = 4` with structure) → throws

**SolverTests**
- `QuadraticSolver([6, -5, 1])` → `{2, 3}`
- `QuadraticSolver([1, 0, 1])` (x² + 1) → `{i, -i}`
- `CubicSolver` on `x³ - 6x² + 11x - 6` → `{1, 2, 3}`
- `QuarticSolver` on `x⁴ - 10x² + 9` → `{-3, -1, 1, 3}` (order-insensitive)
- `QuarticSolver` on `x⁴ + 1` → four complex roots

**EquationSolverServiceTests** (end-to-end via the public `Solve` string API)
- `(x + 3) / 12 = x / 9` → reply contains `x = 9`
- `x^2 - 5x + 6 = 0` → reply contains `2` and `3`
- `2 = 3` → "No solution."
- `2x = 2x` → "Infinite solutions."
- `x^5 + 1 = 0` → degree-too-high message

## Files

**New:**
- `Services/Equations/PolynomialParser.cs`
- `Services/Equations/LinearReducer.cs`
- `Services/Equations/LinearSolver.cs`
- `Services/Equations/QuadraticSolver.cs`
- `Services/Equations/CubicSolver.cs`
- `Services/Equations/QuarticSolver.cs`
- `Services/Equations/ComplexFormatter.cs`
- `Services/Equations/EquationSolverService.cs`
- `AdvancedCalculaterBot.Tests/EquationsTests.cs` (and per-component files as appropriate)
- `AdvancedCalculaterBot.Tests/AdvancedCalculaterBot.Tests.csproj`

**Modified:**
- `Program.cs` — message routing: if text contains `=` and `x`, build `EquationSolverService`, call `Solve`, reply; otherwise existing path. Wrap in try/catch matching existing style.
- `AdvancedCalculaterBot.sln` — created if absent, with both projects.

**Unchanged:** `CalculatorService`, `ComplexityCalculator`, `ComplexityTracker`, `appsettings.json`, `Dockerfile`, `docker-compose.yml`.

## Open Questions (to confirm during implementation)

None — all four design decisions were resolved during brainstorming:
1. Routing: auto-detect by content. ✅
2. Polynomial input: parse terms from text. ✅
3. Linear equations: symbolic reduction (sampling). ✅
4. Roots: real + complex. ✅
