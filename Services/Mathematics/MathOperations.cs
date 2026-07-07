using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using AdvancedCalculaterBot.Services.Equations;

namespace AdvancedCalculaterBot.Services.Mathematics;

/// <summary>
/// Handles all mathematical operations: solve, derivative, integral, limit, simplify, expand, factor.
/// </summary>
public static class MathOperations
{
    private const double ZeroTolerance = 1e-9;

    /// <summary>
    /// Solves an algebraic equation.
    /// </summary>
    public static MathResult Solve(string expression, string variable = "x")
    {
        try
        {
            expression = expression.Trim();

            if (!expression.Contains('='))
                return MathResult.ErrorResult("Equation must contain '=' to solve.");

            int eqIndex = expression.IndexOf('=');
            string lhs = expression.Substring(0, eqIndex).Trim();
            string rhs = expression.Substring(eqIndex + 1).Trim();

            if (string.IsNullOrWhiteSpace(lhs) || string.IsNullOrWhiteSpace(rhs))
                return MathResult.ErrorResult("Equation must have both left and right sides.");

            if (!ContainsVariable(expression))
                return MathResult.ErrorResult($"Equation must contain variable '{variable}'.");

            // First, try to solve as a polynomial equation
            try
            {
                double[] coeffs = GetDifferenceCoeffs(lhs, rhs);
                Complex[] roots = SolveFromCoefficients(coeffs);

                // If we found solutions, return them
                if (roots.Length > 0)
                    return MathResult.SuccessResult(expression, roots);
            }
            catch
            {
                // Not a polynomial equation, fall through to general solver
            }

            // Fall back to the general equation solver
            // which can handle transcendental equations like sin(x) = 1
            string solution = EquationSolverService.Solve(expression);
            
            // Check if the solver returned an error message
            if (solution.StartsWith("Sorry, I couldn't parse") || 
                solution.StartsWith("That doesn't look like an equation") ||
                solution.StartsWith("Please provide an equation") ||
                solution.StartsWith("Equation must have") ||
                solution.StartsWith("Only equations in the variable") ||
                solution.StartsWith("No solution found") ||
                solution.StartsWith("Infinite solutions.") ||
                solution.StartsWith("No solution."))
            {
                return MathResult.ErrorResult(solution);
            }
            
            return MathResult.SuccessResult(expression, solution);
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to solve equation: {ex.Message}", ex.ToString());
        }
    }

    /// <summary>
    /// Computes the derivative with respect to the default variable.
    /// </>
    public static MathResult Derivative(string expression, string variable = "x")
    {
        try
        {
            expression = expression.Trim();

            if (string.IsNullOrWhiteSpace(expression))
                return MathResult.ErrorResult("Expression cannot be empty.");

            string derivative = ComputeDerivative(expression, variable);
            return MathResult.SuccessResult($"d({expression}, {variable})", derivative);
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to compute derivative: {ex.Message}", ex.ToString());
        }
    }

    /// <summary>
    /// Computes the derivative with respect to a specified variable.
    /// </summary>
    public static MathResult Derivative(string expression, string variable, string parameter)
    {
        return Derivative(expression, variable);
    }

    /// <summary>
    /// Computes the indefinite integral.
    /// </summary>
    public static MathResult Integral(string expression, string variable = "x")
    {
        try
        {
            expression = expression.Trim();

            if (string.IsNullOrWhiteSpace(expression))
                return MathResult.ErrorResult("Expression cannot be empty.");

            if (!ContainsVariable(expression))
                return MathResult.ErrorResult($"Expression must contain variable '{variable}'.");

            string integral = ComputeIndefiniteIntegral(expression, variable);
            return MathResult.SuccessResult($"∫ {expression} d{variable}", integral);
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to compute integral: {ex.Message}", ex.ToString());
        }
    }

    /// <summary>
    /// Computes the definite integral.
    /// </summary>
    public static MathResult Integral(string expression, string variable, double lower, double upper)
    {
        try
        {
            expression = expression.Trim();

            if (string.IsNullOrWhiteSpace(expression))
                return MathResult.ErrorResult("Expression cannot be empty.");

            if (!ContainsVariable(expression))
                return MathResult.ErrorResult($"Expression must contain variable '{variable}'.");

            if (lower >= upper)
                return MathResult.ErrorResult("Lower limit must be less than upper limit.");

            double result = ComputeDefiniteIntegral(expression, variable, lower, upper);
            return MathResult.SuccessResult($"∫_{lower}^{upper} {expression} d{variable}", result.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to compute integral: {ex.Message}", ex.ToString());
        }
    }

    /// <summary>
    /// Computes the limit.
    /// </summary>
    public static MathResult Limit(string expression, string variable, double point)
    {
        try
        {
            expression = expression.Trim();

            if (string.IsNullOrWhiteSpace(expression))
                return MathResult.ErrorResult("Expression cannot be empty.");

            if (!ContainsVariable(expression))
                return MathResult.ErrorResult($"Expression must contain variable '{variable}'.");

            double result = ComputeLimit(expression, variable, point);
            return MathResult.SuccessResult($"lim_{{x→{point}}} {expression}", result.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to compute limit: {ex.Message}", ex.ToString());
        }
    }

    /// <summary>
    /// Simplifies an algebraic expression.
    /// </summary>
    public static MathResult Simplify(string expression)
    {
        try
        {
            expression = expression.Trim();

            if (string.IsNullOrWhiteSpace(expression))
                return MathResult.ErrorResult("Expression cannot be empty.");

            string simplified = SimplifyExpression(expression);
            return MathResult.SuccessResult($"simplify({expression})", simplified);
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to simplify expression: {ex.Message}", ex.ToString());
        }
    }

    /// <summary>
    /// Expands an algebraic expression.
    /// </summary>
    public static MathResult Expand(string expression)
    {
        try
        {
            expression = expression.Trim();

            if (string.IsNullOrWhiteSpace(expression))
                return MathResult.ErrorResult("Expression cannot be empty.");

            string expanded = ExpandExpression(expression);
            return MathResult.SuccessResult($"expand({expression})", expanded);
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to expand expression: {ex.Message}", ex.ToString());
        }
    }

    /// <summary>
    /// Factors an algebraic expression.
    /// </summary>
    public static MathResult Factor(string expression)
    {
        try
        {
            expression = expression.Trim();

            if (string.IsNullOrWhiteSpace(expression))
                return MathResult.ErrorResult("Expression cannot be empty.");

            string factored = FactorExpression(expression);
            return MathResult.SuccessResult($"factor({expression})", factored);
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to factor expression: {ex.Message}", ex.ToString());
        }
    }

    private static bool ContainsVariable(string expression)
    {
        foreach (char c in expression)
            if (c == 'x' || c == 'y' || c == 'z' || char.IsLetter(c))
                return true;
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

    private static Complex[] SolveFromCoefficients(double[] coeffs)
    {
        int degree = Degree(coeffs);

        if (degree == 0)
            return Array.Empty<Complex>();

        if (degree > 5)
            return Array.Empty<Complex>();

        Complex[] roots = degree switch
        {
            1 => LinearSolver.Solve(coeffs),
            2 => QuadraticSolver.Solve(coeffs),
            3 => CubicSolver.Solve(coeffs),
            4 => QuarticSolver.Solve(coeffs),
            _ => Array.Empty<Complex>()
        };

        return roots;
    }

    private static int Degree(double[] coeffs)
    {
        for (int i = coeffs.Length - 1; i > 0; i--)
            if (Math.Abs(coeffs[i]) > ZeroTolerance)
                return i;
        return 0;
    }

    private static string ComputeDerivative(string expression, string variable)
    {
        expression = NormalizeExpression(expression);

        if (!expression.Contains(variable))
            return "0";

        string derivative = PolynomialDerivative(expression, variable);
        if (!string.IsNullOrEmpty(derivative))
            return derivative;

        return "Derivative not supported for this expression type.";
    }

    private static string PolynomialDerivative(string expression, string variable)
    {
        var terms = ParsePolynomialTerms(expression);
        if (terms.Count == 0)
            return "0";

        var resultTerms = new List<string>();
        foreach (var term in terms)
        {
            if (term.Power == 0)
                continue;

            double newCoefficient = term.Coefficient * term.Power;
            string newTerm = newCoefficient.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);

            if (term.Power == 1)
            {
                resultTerms.Add(newTerm);
            }
            else
            {
                resultTerms.Add($"{newTerm}{variable}^{term.Power - 1}");
            }
        }

        return string.Join(" + ", resultTerms);
    }

    private static string ComputeIndefiniteIntegral(string expression, string variable)
    {
        expression = NormalizeExpression(expression);
        var terms = ParsePolynomialTerms(expression);
        if (terms.Count == 0)
            return "∫ 0 dx";

        var resultTerms = new List<string>();
        foreach (var term in terms)
        {
            double newCoefficient = term.Coefficient / (term.Power + 1);
            string newTerm = newCoefficient.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);

            if (term.Power == 0)
            {
                resultTerms.Add($"{newTerm}");
            }
            else
            {
                resultTerms.Add($"{newTerm}{variable}^{term.Power + 1} / ({term.Power + 1})");
            }
        }

        return $"∫ ({string.Join(" + ", resultTerms)}) d{variable}";
    }

    private static double ComputeDefiniteIntegral(string expression, string variable, double lower, double upper)
    {
        var terms = ParsePolynomialTerms(expression);
        if (terms.Count == 0)
            return 0;

        double result = 0;
        foreach (var term in terms)
        {
            double lowerTerm = term.Coefficient * Math.Pow(lower, term.Power + 1) / (term.Power + 1);
            double upperTerm = term.Coefficient * Math.Pow(upper, term.Power + 1) / (term.Power + 1);
            result += upperTerm - lowerTerm;
        }

        return result;
    }

    private static double ComputeLimit(string expression, string variable, double point)
    {
        expression = NormalizeExpression(expression);
        var terms = ParsePolynomialTerms(expression);
        if (terms.Count == 0)
            return 0;

        double result = 0;
        foreach (var term in terms)
        {
            if (term.Power == 0)
            {
                result += term.Coefficient;
            }
            else if (term.Power > 0)
            {
                result += term.Coefficient * Math.Pow(point, term.Power);
            }
        }

        return result;
    }

    private static string SimplifyExpression(string expression)
    {
        expression = NormalizeExpression(expression);
        var terms = ParsePolynomialTerms(expression);
        if (terms.Count == 0)
            return expression;

        var simplifiedTerms = terms
            .Where(t => Math.Abs(t.Coefficient) > ZeroTolerance)
            .OrderByDescending(t => t.Power)
            .ToList();

        var resultTerms = new List<string>();
        foreach (var term in simplifiedTerms)
        {
            string termStr = term.Coefficient.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            if (term.Power == 0)
            {
                resultTerms.Add(termStr);
            }
            else if (term.Power == 1)
            {
                resultTerms.Add($"{termStr}*x");
            }
            else
            {
                resultTerms.Add($"{termStr}*x^{term.Power}");
            }
        }

        return string.Join(" + ", resultTerms);
    }

    private static string ExpandExpression(string expression)
    {
        expression = NormalizeExpression(expression);
        var terms = ParsePolynomialTerms(expression);
        if (terms.Count == 0)
            return expression;

        var resultTerms = new List<string>();
        foreach (var term in terms)
        {
            string termStr = term.Coefficient.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            if (term.Power == 0)
            {
                resultTerms.Add(termStr);
            }
            else if (term.Power == 1)
            {
                resultTerms.Add($"{termStr}*x");
            }
            else
            {
                resultTerms.Add($"{termStr}*x^{term.Power}");
            }
        }

        return string.Join(" + ", resultTerms);
    }

    private static string FactorExpression(string expression)
    {
        expression = NormalizeExpression(expression);
        var terms = ParsePolynomialTerms(expression);
        if (terms.Count <= 1)
            return expression;

        var nonZeroCoeffs = terms.Where(t => Math.Abs(t.Coefficient) > ZeroTolerance).Select(t => t.Coefficient).ToList();
        if (nonZeroCoeffs.Count == 0)
            return expression;

        double absGcd = Math.Abs(nonZeroCoeffs[0]);
        for (int i = 1; i < nonZeroCoeffs.Count; i++)
            absGcd = GCD(absGcd, Math.Abs(nonZeroCoeffs[i]));

        if (absGcd < ZeroTolerance)
            return expression;

        double sign = nonZeroCoeffs[0] < 0 ? -1 : 1;
        double commonFactor = sign * absGcd;

        if (Math.Abs(commonFactor - 1) < ZeroTolerance || Math.Abs(commonFactor + 1) < ZeroTolerance)
            return expression;

        var factoredTerms = terms.Select(t => (Coefficient: t.Coefficient / commonFactor, Power: t.Power)).ToList();

        var termStrings = factoredTerms.Select(t =>
        {
            double c = t.Coefficient;
            if (Math.Abs(c) < ZeroTolerance) return null;
            if (t.Power == 0) return c.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            if (Math.Abs(c - 1) < ZeroTolerance) return t.Power == 1 ? "x" : $"x^{t.Power}";
            if (Math.Abs(c + 1) < ZeroTolerance) return t.Power == 1 ? "-x" : $"-x^{t.Power}";
            return t.Power == 1 ? $"{c}*x" : $"{c}*x^{t.Power}";
        }).Where(s => s != null).ToList();

        if (termStrings.Count == 0)
            return expression;

        return $"{commonFactor}({string.Join(" + ", termStrings)})";
    }

    private static double FindGreatestCommonDivisor(IEnumerable<double> numbers)
    {
        var gcd = 1.0;
        foreach (var num in numbers)
        {
            gcd = GCD(gcd, num);
            if (Math.Abs(gcd) < ZeroTolerance)
                break;
        }
        return gcd;
    }

    private static double GCD(double a, double b)
    {
        while (Math.Abs(b) > ZeroTolerance)
        {
            double temp = b;
            b = a % b;
            a = temp;
        }
        return Math.Abs(a);
    }

    private static string NormalizeExpression(string expression)
    {
        return expression.Replace(" ", "").Replace("\t", "");
    }

    private static System.Collections.Generic.List<(double Coefficient, int Power)> ParsePolynomialTerms(string expression)
    {
        var terms = new System.Collections.Generic.List<(double Coefficient, int Power)>();

        string remaining = expression;

        var xMatches = Regex.Matches(remaining, @"([+-]?\s*\d*\.?\d*)\s*x\^?(\d*)");

        var matchedRanges = new List<(int Start, int Length)>();
        foreach (System.Text.RegularExpressions.Match match in xMatches)
        {
            string coeffStr = match.Groups[1].Value;
            string powerStr = match.Groups[2].Value;

            double coefficient = string.IsNullOrEmpty(coeffStr) || coeffStr.Trim() == "+" || coeffStr.Trim() == "-"
                ? (coeffStr.Trim() == "-" ? -1 : 1)
                : double.Parse(coeffStr.Replace(" ", ""), System.Globalization.CultureInfo.InvariantCulture);

            int power = string.IsNullOrEmpty(powerStr) ? 1 : int.Parse(powerStr, System.Globalization.CultureInfo.InvariantCulture);

            terms.Add((coefficient, power));
            matchedRanges.Add((match.Index, match.Length));
        }

        for (int i = matchedRanges.Count - 1; i >= 0; i--)
        {
            remaining = remaining.Substring(0, matchedRanges[i].Start) + remaining.Substring(matchedRanges[i].Start + matchedRanges[i].Length);
        }

        remaining = remaining.Trim();

        if (remaining.Length > 0)
        {
            string cleaned = remaining.Replace(" ", "").TrimStart('+');
            if (cleaned.Length > 0 && double.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double constVal))
            {
                terms.Add((constVal, 0));
            }
        }

        var grouped = new System.Collections.Generic.Dictionary<int, double>();
        foreach (var term in terms)
        {
            if (grouped.ContainsKey(term.Power))
                grouped[term.Power] += term.Coefficient;
            else
                grouped[term.Power] = term.Coefficient;
        }

        var result = grouped
            .Select(kv => (Coefficient: kv.Value, Power: kv.Key))
            .OrderByDescending(t => t.Power)
            .ToList();

        return result;
    }
}