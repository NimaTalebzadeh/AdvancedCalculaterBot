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

            double[] coeffs = GetDifferenceCoeffs(lhs, rhs);
            Complex[] roots = SolveFromCoefficients(coeffs);

            if (roots.Length == 0)
                return MathResult.ErrorResult("No solution found.");

            return MathResult.SuccessResult(expression, roots);
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to solve equation: {ex.Message}", ex.ToString());
        }
    }

    /// <summary>
    /// Computes the derivative with respect to the default variable.
    /// </summary>
    public static MathResult Derivative(string expression, string variable = "x")
    {
        try
        {
            expression = expression.Trim();

            if (string.IsNullOrWhiteSpace(expression))
                return MathResult.ErrorResult("Expression cannot be empty.");

            if (!ContainsVariable(expression))
                return MathResult.ErrorResult($"Expression must contain variable '{variable}'.");

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

        if (expression.Contains(variable))
        {
            string derivative = PolynomialDerivative(expression, variable);
            if (!string.IsNullOrEmpty(derivative))
                return derivative;
        }

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
            double newCoefficient = term.Coefficient / (term.Power + 1);
            double lowerTerm = newCoefficient * (Math.Pow(lower, term.Power + 1) / (term.Power + 1));
            double upperTerm = newCoefficient * (Math.Pow(upper, term.Power + 1) / (term.Power + 1));
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

        double firstTermCoeff = terms[0].Coefficient;
        double commonFactor = FindGreatestCommonDivisor(terms.Select(t => t.Coefficient));

        if (Math.Abs(commonFactor) > ZeroTolerance)
        {
            var factoredTerms = terms.Select(t => new
            {
                Coefficient = t.Coefficient / commonFactor,
                Power = t.Power
            }).ToList();

            string factorStr = Math.Abs(commonFactor - 1) < ZeroTolerance ? "" : $"{commonFactor}*";

            var factorizedTerms = factoredTerms.Select(t =>
            {
                if (t.Power == 0)
                    return t.Coefficient.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
                if (t.Power == 1)
                    return $"{t.Coefficient}*x";
                return $"{t.Coefficient}*x^{t.Power}";
            }).ToList();

            return $"{factorStr}({string.Join(" + ", factorizedTerms)})";
        }

        return expression;
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
        var terms = new System.Collections.Generic.List<(double, int)>();
        var matches = Regex.Matches(expression, @"([+-]?\s*\d*\.?\d*)\s*x\^?(\d*)");

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            string coeffStr = match.Groups[1].Value;
            string powerStr = match.Groups[2].Value;

            double coefficient = string.IsNullOrEmpty(coeffStr) || coeffStr == "+" || coeffStr == "-"
                ? (coeffStr == "-" ? -1 : 1)
                : double.Parse(coeffStr, System.Globalization.CultureInfo.InvariantCulture);

            int power = string.IsNullOrEmpty(powerStr) ? 1 : int.Parse(powerStr, System.Globalization.CultureInfo.InvariantCulture);

            terms.Add((coefficient, power));
        }

        return terms;
    }
}