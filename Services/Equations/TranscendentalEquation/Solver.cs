using System.Globalization;

namespace AdvancedCalculaterBot.Services.Equations.TranscendentalSolving;

/// <summary>
/// Main entry point for solving transcendental equations.
/// Parses the equation, moves all terms to left side, finds intervals with sign changes,
/// refines roots with bisection+Newton, then deduplicates.
/// </summary>
public sealed class TranscendentalEquationSolver
{
    private readonly ExpressionParser _parser = new();

    /// <summary>
    /// Solve a transcendental equation. Returns all distinct real roots.
    /// </summary>
    /// <param name="equation">Equation string, e.g. "sin(x) = 0.5" or "x^2 + sin(x) - 5 = 0".</param>
    /// <param name="variable">Variable to solve for (default: "x").</param>
    /// <param name="lo">Lower bound of search interval (default: -100).</param>
    /// <param name="hi">Upper bound of search interval (default: 100).</param>
    /// <param name="step">Step size for interval scanning (default: 0.25).</param>
    /// <param name="tolerance">Root accuracy tolerance (default: 1e-10).</param>
    /// <returns>List of distinct real roots.</returns>
    public List<double> Solve(string equation, string variable = "x",
        double lo = -100, double hi = 100, double step = 0.25,
        double tolerance = 1e-10)
    {
        try
        {
            // Step 1: Move all terms to left side → f(x) = 0
            ExprNode f = BuildLeftSide(equation, variable);

            // Build evaluation function
            Func<double, double> eval = x =>
            {
                var vars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    [variable] = x
                };
                return f.Eval(vars);
            };

            // Step 2: Scan for sign-change intervals
            var intervals = IntervalScanner.FindRootIntervals(eval, lo, hi, step);

            // Step 3: Refine each interval
            var roots = new List<double>();
            foreach (var (a, b) in intervals)
            {
                // Check if function is exactly zero at endpoints
                double faVal = SafeEval(eval, a);
                double fbVal = SafeEval(eval, b);
                double root;

                if (!double.IsNaN(faVal) && Math.Abs(faVal) < 0.01)
                {
                    root = a;
                }
                else if (!double.IsNaN(fbVal) && Math.Abs(fbVal) < 0.01)
                {
                    root = b;
                }
                else if (faVal * fbVal < 0)
                {
                    // Sign change — use bisection
                    root = BrentSolver.Solve(eval, a, b, tolerance);
                }
                else
                {
                    // No sign change — try midpoint
                    double mid = (a + b) / 2.0;
                    double fMid = SafeEval(eval, mid);
                    if (!double.IsNaN(fMid) && Math.Abs(fMid) < 0.01)
                        root = mid;
                    else
                        continue;
                }

                if (double.IsNaN(root) || double.IsInfinity(root))
                    continue;

                // Verify the root is actually a root
                double fVal = SafeEval(eval, root);
                if (double.IsNaN(fVal) || Math.Abs(fVal) > 0.01)
                    continue;

                // Add if not duplicate
                if (!roots.Any(r => Math.Abs(r - root) < 0.01))
                    roots.Add(Math.Round(root, 6));
            }

            return roots.OrderBy(r => r).ToList();
        }
        catch
        {
            return new List<double>();
        }
    }

    /// <summary>
    /// Parse "lhs = rhs" and return lhs - rhs as an expression tree.
    /// </summary>
    private ExprNode BuildLeftSide(string equation, string variable)
    {
        int eq = equation.IndexOf('=');
        if (eq < 0)
            return _parser.Parse(equation);

        string lhs = equation.Substring(0, eq).Trim();
        string rhs = equation.Substring(eq + 1).Trim();

        ExprNode leftTree = _parser.Parse(lhs);
        ExprNode rightTree = _parser.Parse(rhs);

        return new BinOpNode('-', leftTree, rightTree);
    }

    private static double SafeEval(Func<double, double> f, double x)
    {
        try { return f(x); }
        catch { return double.NaN; }
    }
}
