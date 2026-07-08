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
        // Normalize e^(...) notation before solving
        equation = NormalizeENotation(equation);

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

        // Check for transcendental equations first
        if (LegacyTranscendentalSolver.IsTranscendental(equation))
        {
            try
            {
                double[] doubleRoots = LegacyTranscendentalSolver.Solve(equation);
                Complex[] roots = Array.ConvertAll(doubleRoots, d => new Complex(d, 0));
                if (roots.Length == 0)
                    return "No solution found (diverged or no root near x₀ = 1)";
                return ComplexFormatter.FormatRoots(roots);
            }
            catch
            {
                return "No solution found (diverged or no root near x₀ = 1)";
            }
        }

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
            return Math.Abs(coeffs[0]) < ZeroTolerance
                ? "Infinite solutions."
                : "No solution.";
        }

        // Use Durand-Kerner for quintic equations
        if (degree == 5)
        {
            Complex[] quinticRoots = QuinticSolver.Solve(coeffs);
            return ComplexFormatter.FormatRoots(quinticRoots);
        }

        if (degree > 5)
            return "Only up to degree 5 supported";

        Complex[] roots = degree switch
        {
            1 => LinearSolver.Solve(coeffs),
            2 => QuadraticSolver.Solve(coeffs),
            3 => CubicSolver.Solve(coeffs),
            4 => QuarticSolver.Solve(coeffs),
            _ => Array.Empty<Complex>()
        };

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

    /// <summary>
    /// Converts e^(expr) notation to exp(expr) for proper handling.
    /// </summary>
    private static string NormalizeENotation(string expression)
    {
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < expression.Length)
        {
            if (i + 1 < expression.Length && expression[i] == 'e' && expression[i + 1] == '^')
            {
                i += 2; // skip e^
                if (i < expression.Length && expression[i] == '(')
                {
                    // e^(...) - find matching close paren
                    int depth = 1;
                    i++; // skip opening (
                    int innerStart = i;
                    while (i < expression.Length && depth > 0)
                    {
                        if (expression[i] == '(') depth++;
                        else if (expression[i] == ')') depth--;
                        if (depth > 0) i++;
                    }
                    string inner = expression.Substring(innerStart, i - innerStart);
                    sb.Append($"exp({inner})");
                    i++; // skip closing )
                }
                else if (i < expression.Length)
                {
                    // e^x or e^2x (no parens) - take until operator or end
                    int innerStart = i;
                    while (i < expression.Length && expression[i] != '+' && expression[i] != '-' && expression[i] != '*' && expression[i] != '/' && expression[i] != '=' && expression[i] != '^')
                        i++;
                    string inner = expression.Substring(innerStart, i - innerStart);
                    sb.Append($"exp({inner})");
                }
                else
                {
                    sb.Append("exp(1)");
                }
            }
            else
            {
                sb.Append(expression[i]);
                i++;
            }
        }
        return sb.ToString();
    }
}
