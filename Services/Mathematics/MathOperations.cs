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
            string solution;
            try
            {
                solution = EquationSolverService.Solve(expression);
            }
            catch
            {
                return MathResult.ErrorResult("No solution found for this equation.");
            }
            
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
    /// </summary>
    public static MathResult Derivative(string expression, string variable = "x")
    {
        return Derivative(expression, variable, 1);
    }

    /// <summary>
    /// Computes the n-th order derivative with respect to a specified variable.
    /// </summary>
    public static MathResult Derivative(string expression, string variable, int order)
    {
        try
        {
            expression = expression.Trim();

            if (string.IsNullOrWhiteSpace(expression))
                return MathResult.ErrorResult("Expression cannot be empty.");

            if (order < 1)
                return MathResult.ErrorResult("Derivative order must be at least 1.");

            string result = expression;
            for (int i = 0; i < order; i++)
            {
                result = ComputeDerivative(result, variable);
                if (result.Contains("not supported"))
                    return MathResult.ErrorResult($"Derivative not supported after {i + 1} differentiation(s).");
            }

            string label = order == 1
                ? $"d({expression}, {variable})"
                : $"d^{order}({expression})/d{variable}^{order}";
            return MathResult.SuccessResult(label, result);
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to compute derivative: {ex.Message}", ex.ToString());
        }
    }

    /// <summary>
    /// Computes the derivative with respect to a specified variable (string parameter overload).
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
            return MathResult.SuccessResult($"∫ {expression} d{variable}", integral + " + C");
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
    /// Computes a multiple (indefinite) integral with respect to multiple variables.
    /// e.g., int(x*y, x, y) computes ∫∫ xy dx dy = x^2*y^2/4 + C
    /// Works for polynomial expressions with multiple variables.
    /// </summary>
    public static MathResult MultipleIntegral(string expression, string[] variables)
    {
        try
        {
            expression = expression.Trim();

            if (string.IsNullOrWhiteSpace(expression))
                return MathResult.ErrorResult("Expression cannot be empty.");

            if (variables == null || variables.Length == 0)
                return MathResult.ErrorResult("At least one variable is required.");

            string result = expression;
            string varList = string.Join(", ", variables);

            foreach (string v in variables)
            {
                result = IntegrateWithRespectTo(result, v);
            }

            return MathResult.SuccessResult($"∫∫ {expression} d{varList}", result + " + C");
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to compute multiple integral: {ex.Message}", ex.ToString());
        }
    }

    /// <summary>
    /// Integrates a polynomial expression with respect to one variable, treating all
    /// other letters as constant coefficients. Handles terms like "3*x^2*y", "-x*z", "5*y".
    /// </summary>
    private static string IntegrateWithRespectTo(string expression, string variable)
    {
        expression = NormalizeExpression(expression);

        // Split into additive terms
        var terms = new List<string>();
        int start = 0;
        for (int i = 1; i < expression.Length; i++)
        {
            if ((expression[i] == '+' || expression[i] == '-') && i > start)
            {
                terms.Add(expression.Substring(start, i - start));
                start = i;
            }
        }
        terms.Add(expression.Substring(start));

        var resultTerms = new List<string>();
        foreach (var term in terms)
        {
            if (string.IsNullOrEmpty(term)) continue;
            string integrated = IntegrateSingleTerm(term, variable);
            if (!string.IsNullOrEmpty(integrated))
                resultTerms.Add(integrated);
        }

        if (resultTerms.Count == 0) return "0";
        return JoinTerms(resultTerms);
    }

    private static string IntegrateSingleTerm(string term, string variable)
    {
        // Parse a term like "3*x^2*y" or "-x" or "5" or "x*y*z"
        // Strategy: find the variable's power, extract the constant coefficient (everything else)

        bool negative = term[0] == '-';
        string body = (term[0] == '+' || term[0] == '-') ? term.Substring(1) : term;

        // Find the variable part: e.g. "x^2" or "x"
        int varIdx = body.IndexOf(variable);
        if (varIdx < 0)
        {
            // Variable not in this term — it's a constant, integral is term*x
            return $"{term}{variable}";
        }

        // Extract coefficient (everything before the variable)
        string before = body.Substring(0, varIdx);
        // Clean trailing * or nothing
        string coeffStr = before.TrimEnd('*');

        // Extract power
        int afterIdx = varIdx + 1;
        int power = 1;
        if (afterIdx < body.Length && body[afterIdx] == '^')
        {
            afterIdx++;
            string powStr = "";
            while (afterIdx < body.Length && (char.IsDigit(body[afterIdx])))
            {
                powStr += body[afterIdx];
                afterIdx++;
            }
            power = int.Parse(powStr);
        }

        // Extract suffix (everything after the variable part)
        string after = body.Substring(afterIdx);
        string suffix = after.TrimStart('*');

        // Build coefficient: before * suffix (with proper formatting)
        string fullCoeff = "";
        if (!string.IsNullOrEmpty(coeffStr) && !string.IsNullOrEmpty(suffix))
            fullCoeff = $"{coeffStr}{suffix}";
        else if (!string.IsNullOrEmpty(coeffStr))
            fullCoeff = coeffStr;
        else if (!string.IsNullOrEmpty(suffix))
            fullCoeff = suffix;

        // Integrate: ∫ coeff * var^n dx = coeff * var^(n+1) / (n+1)
        int newPower = power + 1;
        string coeff = string.IsNullOrEmpty(fullCoeff) ? "1" : fullCoeff;
        string sign = negative ? "-" : "";

        // Format nicely — wrap multi-char coefficients in parens to avoid ambiguity
        string coeffDisplay = coeff;
        if (coeff.Length > 1 && !coeff.StartsWith("("))
            coeffDisplay = $"({coeff})";

        if (newPower == 1)
            return $"{sign}{coeffDisplay}{variable}";
        else
            return $"{sign}{coeffDisplay}{variable}^{newPower} / {newPower}";
    }

    /// <summary>
    /// Computes a double definite integral: ∫∫ f(x,y) dx dy over given bounds.
    /// </summary>
    public static MathResult DoubleDefiniteIntegral(string expression,
        string var1, double lo1, double hi1,
        string var2, double lo2, double hi2)
    {
        try
        {
            expression = expression.Trim();

            if (string.IsNullOrWhiteSpace(expression))
                return MathResult.ErrorResult("Expression cannot be empty.");

            if (lo1 >= hi1)
                return MathResult.ErrorResult($"Lower limit for {var1} must be less than upper limit.");
            if (lo2 >= hi2)
                return MathResult.ErrorResult($"Lower limit for {var2} must be less than upper limit.");

            // First integrate with respect to var1 (inner integral)
            string innerResult = ComputeIndefiniteIntegral(expression, var1);

            // Evaluate at bounds for var1
            string evaluated = EvaluateAtBounds(innerResult, var1, hi1) + " - " + EvaluateAtBounds(innerResult, var1, lo1);

            // Now integrate the result with respect to var2
            // We need to evaluate the difference numerically
            double outerResult = ComputeDefiniteIntegral(expression, var1, lo1, hi1);
            // The above only integrates var1. For a true double integral, we need to
            // substitute and integrate var2. Let's use numerical approach:
            // Integrate the inner definite integral result over var2.
            double result = ComputeDoubleDefiniteIntegralNumerical(expression, var1, lo1, hi1, var2, lo2, hi2);

            return MathResult.SuccessResult(
                $"∫∫ {expression} d{var1} d{var2} [{lo1}→{hi1}, {lo2}→{hi2}]",
                result.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to compute double integral: {ex.Message}", ex.ToString());
        }
    }

    private static string EvaluateAtBounds(string expression, string variable, double value)
    {
        return expression.Replace($"{variable}", $"({value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)})");
    }

    private static double ComputeDoubleDefiniteIntegralNumerical(string expression,
        string var1, double lo1, double hi1,
        string var2, double lo2, double hi2)
    {
        // Use Simpson's rule for the outer integral
        int n = 100; // must be even
        double h2 = (hi2 - lo2) / n;
        double sum = 0;

        for (int i = 0; i <= n; i++)
        {
            double y = lo2 + i * h2;
            string exprWithY = expression.Replace($"{var2}", $"({y.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)})");
            double innerVal = ComputeDefiniteIntegral(exprWithY, var1, lo1, hi1);

            double weight = (i == 0 || i == n) ? 1 : (i % 2 == 1) ? 4 : 2;
            sum += weight * innerVal;
        }

        return sum * h2 / 3.0;
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

        if (expression.Contains("sin") || expression.Contains("cos") || expression.Contains("tan") ||
            expression.Contains("asin") || expression.Contains("acos") || expression.Contains("atan") ||
            expression.Contains("ln") || expression.Contains("log") || expression.Contains("exp"))
        {
            return TrigDerivative(expression, variable);
        }

        string derivative = PolynomialDerivative(expression, variable);
        if (!string.IsNullOrEmpty(derivative))
            return derivative;

        return "Derivative not supported for this expression type.";
    }

    private static string TrigDerivative(string expression, string variable)
    {
        var productTerms = SplitAdditiveTerms(expression);
        if (productTerms.Count > 1)
        {
            var derivTerms = productTerms.Select(t => TrigDerivative(t, variable)).ToList();
            return JoinTerms(derivTerms);
        }

        var productParts = SplitProductParts(expression);
        if (productParts.Count == 2)
        {
            string u = productParts[0];
            string v = productParts[1];
            string du = TrigDerivative(u, variable);
            string dv = TrigDerivative(v, variable);
            return $"({du})*({v}) + ({u})*({dv})";
        }

        if (expression.StartsWith("sin(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(4, expression.Length - 5);
            string dInner = TrigDerivative(inner, variable);
            if (dInner == "1") return $"cos({inner})";
            return $"cos({inner})*({dInner})";
        }

        if (expression.StartsWith("cos(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(4, expression.Length - 5);
            string dInner = TrigDerivative(inner, variable);
            if (dInner == "1") return $"-sin({inner})";
            return $"-sin({inner})*({dInner})";
        }

        if (expression.StartsWith("exp(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(4, expression.Length - 5);
            string dInner = TrigDerivative(inner, variable);
            if (dInner == "1") return $"exp({inner})";
            return $"exp({inner})*({dInner})";
        }

        if (expression.StartsWith("ln(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(3, expression.Length - 4);
            string dInner = TrigDerivative(inner, variable);
            if (dInner == "1") return $"1/({inner})";
            return $"({dInner})/({inner})";
        }

        return PolynomialDerivative(expression, variable);
    }

    private static List<string> SplitAdditiveTerms(string expression)
    {
        var terms = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < expression.Length; i++)
        {
            if (expression[i] == '(') depth++;
            else if (expression[i] == ')') depth--;
            else if (depth == 0 && i > 0 && (expression[i] == '+' || expression[i] == '-'))
            {
                string term = expression.Substring(start, i - start).Trim();
                if (term.Length > 0) terms.Add(term);
                start = i;
            }
        }
        string last = expression.Substring(start).Trim();
        if (last.Length > 0) terms.Add(last);
        return terms;
    }

    private static List<string> SplitProductParts(string expression)
    {
        int depth = 0;
        int lastStar = -1;
        for (int i = 0; i < expression.Length; i++)
        {
            if (expression[i] == '(') depth++;
            else if (expression[i] == ')') depth--;
            else if (depth == 0 && expression[i] == '*')
                lastStar = i;
        }

        if (lastStar > 0)
        {
            string left = expression.Substring(0, lastStar).Trim();
            string right = expression.Substring(lastStar + 1).Trim();
            if (left.Length > 0 && right.Length > 0)
                return new List<string> { left, right };
        }

        return new List<string> { expression };
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
            int newPower = term.Power - 1;

            resultTerms.Add(FormatTerm(newCoefficient, newPower));
        }

        if (resultTerms.Count == 0)
            return "0";

        return JoinTerms(resultTerms);
    }

    private static string JoinTerms(IEnumerable<string> terms)
    {
        var list = terms.ToList();
        if (list.Count == 0) return "0";
        var result = list[0];
        for (int i = 1; i < list.Count; i++)
        {
            if (list[i].StartsWith("-"))
                result += $" - {list[i].Substring(1)}";
            else
                result += $" + {list[i]}";
        }
        return result;
    }

    private static string ComputeIndefiniteIntegral(string expression, string variable)
    {
        expression = NormalizeExpression(expression);
        var terms = ParsePolynomialTerms(expression);
        if (terms.Count == 0)
            return "0";

        var resultTerms = new List<string>();
        foreach (var term in terms)
        {
            double newPower = term.Power + 1;
            string newPowerStr = newPower.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);

            if (Math.Abs(term.Coefficient - 1) < ZeroTolerance)
                resultTerms.Add($"{variable}^{newPowerStr} / {newPowerStr}");
            else if (Math.Abs(term.Coefficient + 1) < ZeroTolerance)
                resultTerms.Add($"-{variable}^{newPowerStr} / {newPowerStr}");
            else
            {
                string coeffStr = term.Coefficient.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
                resultTerms.Add($"{coeffStr}{variable}^{newPowerStr} / {newPowerStr}");
            }
        }

        return JoinTerms(resultTerms);
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
            resultTerms.Add(FormatTerm(term.Coefficient, term.Power));
        }

        if (resultTerms.Count == 0) return "0";

        // Join with proper sign handling: first term is as-is, subsequent terms
        // use " - " for negative coefficients instead of " + -"
        var result = resultTerms[0];
        for (int i = 1; i < resultTerms.Count; i++)
        {
            if (resultTerms[i].StartsWith("-"))
                result += $" - {resultTerms[i].Substring(1)}";
            else
                result += $" + {resultTerms[i]}";
        }
        return result;
    }

    private static string ExpandExpression(string expression)
    {
        string result = expression;

        var binomialPattern = Regex.Match(result, @"\(([^()]+)\)\^(\d+)");
        while (binomialPattern.Success)
        {
            string inner = binomialPattern.Groups[1].Value;
            int power = int.Parse(binomialPattern.Groups[2].Value);
            string expanded = ExpandBinomial(inner, power);
            result = result.Substring(0, binomialPattern.Index) + expanded + result.Substring(binomialPattern.Index + binomialPattern.Length);
            binomialPattern = Regex.Match(result, @"\(([^()]+)\)\^(\d+)");
        }

        result = NormalizeExpression(result);
        var terms = ParsePolynomialTerms(result);
        if (terms.Count == 0)
            return expression;

        var resultTerms = new List<string>();
        foreach (var term in terms)
        {
            string termStr = FormatTerm(term.Coefficient, term.Power);
            if (termStr != null)
                resultTerms.Add(termStr);
        }

        if (resultTerms.Count == 0)
            return "0";

        return JoinTerms(resultTerms);
    }

    private static string ExpandBinomial(string inner, int power)
    {
        double a = 1, b = 1;
        string[] parts = Regex.Split(inner, @"(?<!\d)([+-])");
        if (inner.Contains('x'))
        {
            var xMatch = Regex.Match(inner, @"([+-]?\s*\d*\.?\d*)\s*x");
            if (xMatch.Success)
            {
                string coeffStr = xMatch.Groups[1].Value;
                a = string.IsNullOrEmpty(coeffStr) || coeffStr.Trim() == "+" || coeffStr.Trim() == "-"
                    ? (coeffStr.Trim() == "-" ? -1 : 1)
                    : double.Parse(coeffStr.Replace(" ", ""), System.Globalization.CultureInfo.InvariantCulture);
            }
            string remaining = Regex.Replace(inner, @"[+-]?\s*\d*\.?\d*\s*x\^?\d*", "").Trim();
            if (remaining.Length > 0)
                b = double.Parse(remaining.Replace("+", "").Trim(), System.Globalization.CultureInfo.InvariantCulture);
            else
                b = 0;
        }
        else
        {
            b = double.Parse(inner.Replace("+", "").Trim(), System.Globalization.CultureInfo.InvariantCulture);
            a = 0;
        }

        var result = new List<(double Coeff, int Pow)>();
        for (int i = 0; i <= power; i++)
        {
            double binomCoeff = 1.0;
            for (int k = 1; k <= i; k++)
                binomCoeff = binomCoeff * (power - k + 1) / k;

            double termCoeff = binomCoeff * Math.Pow(a, power - i) * Math.Pow(b, i);
            int termPower = power - i;

            if (Math.Abs(termCoeff) > ZeroTolerance)
                result.Add((termCoeff, termPower));
        }

        var termStrs = result
            .OrderByDescending(t => t.Pow)
            .Select(t => FormatTerm(t.Coeff, t.Pow))
            .Where(s => s != null)
            .ToList();

        if (termStrs.Count == 0) return "0";
        return JoinTerms(termStrs);
    }

    private static string FormatTerm(double coeff, int power)
    {
        if (Math.Abs(coeff) < ZeroTolerance) return null;

        string coeffStr;
        if (Math.Abs(coeff - 1) < ZeroTolerance && power > 0)
            coeffStr = "";
        else if (Math.Abs(coeff + 1) < ZeroTolerance && power > 0)
            coeffStr = "-";
        else
            coeffStr = coeff.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);

        if (power == 0)
            return coeff.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
        if (power == 1)
            return $"{coeffStr}x";
        return $"{coeffStr}x^{power}";
    }

    private static string JoinTerms(List<string> terms)
    {
        if (terms.Count == 0) return "";
        var result = terms[0];
        for (int i = 1; i < terms.Count; i++)
        {
            if (terms[i].StartsWith("-"))
                result += $" - {terms[i].Substring(1)}";
            else
                result += $" + {terms[i]}";
        }
        return result;
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

        double commonFactor = 1.0;
        if (absGcd > ZeroTolerance)
        {
            double sign = nonZeroCoeffs[0] < 0 ? -1 : 1;
            commonFactor = sign * absGcd;
        }

        var reduced = terms.Select(t => (Coefficient: t.Coefficient / commonFactor, Power: t.Power)).ToList();
        int degree = reduced.Max(t => t.Power);

        if (degree == 2)
        {
            double a = reduced.FirstOrDefault(t => t.Power == 2).Coefficient;
            double b = reduced.FirstOrDefault(t => t.Power == 1).Coefficient;
            double c = reduced.FirstOrDefault(t => t.Power == 0).Coefficient;

            if (Math.Abs(a) > ZeroTolerance)
            {
                double disc = b * b - 4 * a * c;
                if (disc >= -ZeroTolerance)
                {
                    disc = Math.Max(0, disc);
                    double r1 = (-b + Math.Sqrt(disc)) / (2 * a);
                    double r2 = (-b - Math.Sqrt(disc)) / (2 * a);

                    if (Math.Abs(disc) < ZeroTolerance)
                    {
                        string coeffStr = Math.Abs(commonFactor) > ZeroTolerance && Math.Abs(commonFactor - 1) > ZeroTolerance
                            ? FormatCoeff(commonFactor) : "";
                        string inner = FormatLinearFactor(1, -r1);
                        return $"{coeffStr}({inner})^2";
                    }
                    else
                    {
                        string coeffStr = Math.Abs(commonFactor) > ZeroTolerance && Math.Abs(commonFactor - 1) > ZeroTolerance
                            ? FormatCoeff(commonFactor) : "";
                        string f1 = FormatLinearFactor(1, -r1);
                        string f2 = FormatLinearFactor(1, -r2);
                        return $"{coeffStr}({f1})({f2})";
                    }
                }
            }
        }

        if (degree == 3)
        {
            double a = reduced.FirstOrDefault(t => t.Power == 3).Coefficient;
            double b = reduced.FirstOrDefault(t => t.Power == 2).Coefficient;
            double c = reduced.FirstOrDefault(t => t.Power == 1).Coefficient;
            double d = reduced.FirstOrDefault(t => t.Power == 0).Coefficient;

            if (Math.Abs(a) > ZeroTolerance)
            {
                double[] roots = FindCubicRoots(a, b, c, d);
                if (roots != null && roots.Length == 3)
                {
                    string coeffStr = Math.Abs(commonFactor) > ZeroTolerance && Math.Abs(commonFactor - 1) > ZeroTolerance
                        ? FormatCoeff(commonFactor) : "";
                    var factors = roots.Select(r => FormatLinearFactor(1, -r)).ToList();
                    return $"{coeffStr}({factors[0]})({factors[1]})({factors[2]})";
                }
            }
        }

        var factoredTerms = terms.Select(t => (Coefficient: t.Coefficient / commonFactor, Power: t.Power)).ToList();
        var termStrings = factoredTerms.Select(t =>
        {
            return FormatTerm(t.Coefficient, t.Power);
        }).Where(s => s != null).ToList();

        if (termStrings.Count == 0)
            return expression;

        if (Math.Abs(commonFactor) > ZeroTolerance && Math.Abs(commonFactor - 1) > ZeroTolerance)
            return $"{FormatCoeff(commonFactor)}({JoinTerms(termStrings)})";

        return expression;
    }

    private static string FormatCoeff(double c)
    {
        if (Math.Abs(c - 1) < ZeroTolerance) return "";
        if (Math.Abs(c + 1) < ZeroTolerance) return "-";
        return c.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatLinearFactor(double a, double b)
    {
        if (Math.Abs(a - 1) < ZeroTolerance)
        {
            if (Math.Abs(b) < ZeroTolerance) return "x";
            if (b > 0) return $"x + {FormatNum(b)}";
            return $"x - {FormatNum(-b)}";
        }
        if (Math.Abs(a + 1) < ZeroTolerance)
        {
            if (Math.Abs(b) < ZeroTolerance) return "-x";
            if (b > 0) return $"-x + {FormatNum(b)}";
            return $"-x - {FormatNum(-b)}";
        }
        string aStr = FormatNum(a);
        if (Math.Abs(b) < ZeroTolerance) return $"{aStr}x";
        if (b > 0) return $"{aStr}x + {FormatNum(b)}";
        return $"{aStr}x - {FormatNum(-b)}";
    }

    private static string FormatNum(double n)
    {
        if (Math.Abs(n - Math.Round(n)) < ZeroTolerance)
            return Math.Round(n).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
        return n.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static double[] FindCubicRoots(double a, double b, double c, double d)
    {
        if (Math.Abs(a) < ZeroTolerance) return null;
        b /= a; c /= a; d /= a;
        double q = (3 * c - b * b) / 9;
        double r = (9 * b * c - 27 * d - 2 * b * b * b) / 54;
        double q3 = q * q * q;
        double det = q3 + r * r;
        if (det < 0)
        {
            double theta = Math.Acos(r / Math.Sqrt(-q3));
            double sq = Math.Sqrt(-q);
            return new[] {
                2 * sq * Math.Cos(theta / 3) - b / 3,
                2 * sq * Math.Cos((theta + 2 * Math.PI) / 3) - b / 3,
                2 * sq * Math.Cos((theta + 4 * Math.PI) / 3) - b / 3
            };
        }
        if (det >= 0)
        {
            double s = Math.Sign(r + Math.Sqrt(det)) * Math.Pow(Math.Abs(r + Math.Sqrt(det)), 1.0 / 3);
            double t = Math.Sign(r - Math.Sqrt(det)) * Math.Pow(Math.Abs(r - Math.Sqrt(det)), 1.0 / 3);
            double root1 = s + t - b / 3;
            double realPart = -(s + t) / 2 - b / 3;
            double imagPart = Math.Sqrt(3) / 2 * (s - t);
            if (Math.Abs(imagPart) < ZeroTolerance)
                return new[] { root1, realPart, realPart };
            return null;
        }
        return null;
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