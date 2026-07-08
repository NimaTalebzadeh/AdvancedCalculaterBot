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

            result = SimplifyCalculusResult(result);

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
            string simplified = SimplifyCalculusResult(integral);
            return MathResult.SuccessResult($"∫ {expression} d{variable}", simplified + " + C");
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

        // Check for functions or division (quotient rule)
        if (expression.Contains("sin") || expression.Contains("cos") || expression.Contains("tan") ||
            expression.Contains("asin") || expression.Contains("acos") || expression.Contains("atan") ||
            expression.Contains("ln") || expression.Contains("log") || expression.Contains("exp") ||
            expression.Contains("sqrt") || expression.Contains("/"))
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
        // Strip balanced outer parentheses so (exp(x)) → exp(x) for pattern matching
        expression = StripOuterParentheses(expression);

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
            string duDisplay = WrapIfNeeded(du);
            string vDisplay = WrapIfNeeded(v);
            string uDisplay = WrapIfNeeded(u);
            string dvDisplay = WrapIfNeeded(dv);
            string term1 = du == "0" ? "0" : dv == "1" ? $"{duDisplay}*{vDisplay}" : du == "1" ? vDisplay : $"{duDisplay}*{vDisplay}";
            string term2 = dv == "0" ? "0" : du == "1" ? $"{uDisplay}*{dvDisplay}" : dv == "1" ? uDisplay : $"{uDisplay}*{dvDisplay}";
            if (term1 == "0") return term2;
            if (term2 == "0") return term1;
            return $"{term1} + {term2}";
        }

        // Handle leading + or - sign: strip it, derive, reapply
        if (expression.Length > 1 && (expression[0] == '+' || expression[0] == '-'))
        {
            char sign = expression[0];
            string rest = expression.Substring(1);
            string innerDeriv = TrigDerivative(rest, variable);
            if (sign == '-')
            {
                if (innerDeriv.StartsWith("-"))
                    return innerDeriv.Substring(1); // -(-result) = result
                return $"-{innerDeriv}";
            }
            return innerDeriv; // + just passes through
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

        if (expression.StartsWith("tan(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(4, expression.Length - 5);
            string dInner = TrigDerivative(inner, variable);
            if (dInner == "1") return $"1+tan({inner})^2";
            return $"(1+tan({inner})^2)*({dInner})";
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

        if (expression.StartsWith("log(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(4, expression.Length - 5);
            string dInner = TrigDerivative(inner, variable);
            if (dInner == "1") return $"1/({inner}*ln(10))";
            return $"({dInner})/({inner}*ln(10))";
        }

        if (expression.StartsWith("sqrt(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(5, expression.Length - 6);
            string dInner = TrigDerivative(inner, variable);
            // d/dx sqrt(u) = 1/(2*sqrt(u)) * du/dx
            if (dInner == "1") return $"1/(2*sqrt({inner}))";
            return $"({dInner})/(2*sqrt({inner}))";
        }

        // Handle quotient rule: (u/v)' = (u'v - uv') / v^2
        var divParts = SplitDivisionParts(expression);
        if (divParts != null)
        {
            string u = divParts.Value.Item1;
            string v = divParts.Value.Item2;
            string du = TrigDerivative(u, variable);
            string dv = TrigDerivative(v, variable);
            string uD = WrapIfNeeded(u);
            string vD = WrapIfNeeded(v);
            string duD = WrapIfNeeded(du);
            string dvD = WrapIfNeeded(dv);
            string num = $"({duD}*{vD} - {uD}*{dvD})";
            string den = $"({vD})^2";
            return $"{num}/{den}";
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

    private static string WrapIfNeeded(string expr)
    {
        // Only wrap in parens if expr has multiple additive terms (contains + or non-leading -)
        int depth = 0;
        for (int i = 0; i < expr.Length; i++)
        {
            char c = expr[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (depth == 0 && c == '+') return $"({expr})";
            else if (depth == 0 && c == '-' && i > 0) return $"({expr})";
        }
        return expr;
    }

    private static string ComputeIndefiniteIntegral(string expression, string variable)
    {
        expression = NormalizeExpression(expression);

        // Handle leading + or - sign
        if (expression.Length > 1 && expression[0] == '-')
        {
            string inner = ComputeIndefiniteIntegral(expression.Substring(1), variable);
            if (inner.StartsWith("-")) return inner.Substring(1);
            return $"-{inner}";
        }
        if (expression.Length > 1 && expression[0] == '+')
            return ComputeIndefiniteIntegral(expression.Substring(1), variable);

        // Handle additive terms: split by + and - at depth 0
        var addTerms = SplitAdditiveTerms(expression);
        if (addTerms.Count > 1)
        {
            var results = addTerms.Select(t => ComputeIndefiniteIntegral(t, variable)).ToList();
            return JoinTerms(results);
        }

        // Handle division: numerator/denominator
        // Special case: f'(x)/f(x) → ln|f(x)|
        var divParts = SplitDivisionParts(expression);
        if (divParts != null)
        {
            string numerator = divParts.Value.Item1;
            string denominator = divParts.Value.Item2;

            // Check if numerator is the derivative of denominator
            string denomDeriv = ComputeDerivativeForIntegralCheck(denominator, variable);
            if (!string.IsNullOrEmpty(denomDeriv) && ExpressionsMatch(numerator, denomDeriv))
            {
                return $"ln({denominator})";
            }
            // Check for arctan pattern: 1/(x^2+1) → arctan(x), 1/(a^2x^2+1) → arctan(ax)/a
            string cleanDenom = StripOuterParentheses(denominator);
            if (IsOne(numerator))
            {
                // Pattern: 1/(x^2+1) → arctan(x)
                string arctanResult = TryArctanPattern(cleanDenom, variable);
                if (arctanResult != null) return arctanResult;
            }
            // Check with coefficient: k/(x^2+1) → k*arctan(x)
            var coeffMatch = Regex.Match(numerator, @"^(\d+\.?\d*)\*?(.+)$");
            if (coeffMatch.Success)
            {
                string coeffStr = coeffMatch.Groups[1].Value;
                string rest = coeffMatch.Groups[2].Value;
                if (double.TryParse(coeffStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double coeff))
                {
                    string restDeriv = ComputeDerivativeForIntegralCheck(denominator, variable);
                    if (!string.IsNullOrEmpty(restDeriv) && ExpressionsMatch(rest, restDeriv))
                    {
                        if (Math.Abs(coeff - 1) < ZeroTolerance) return $"ln({denominator})";
                        return $"{coeffStr}*ln({denominator})";
                    }
                }
            }
        }

        // Handle coefficient * function: e.g., "2sin(x)", "3cos(x)", "2*sin(x)"
        var mulMatch = Regex.Match(expression, @"^(\d+\.?\d*)\*?(sin|cos|tan|exp|ln|log)\(");
        if (mulMatch.Success)
        {
            string coeffStr = mulMatch.Groups[1].Value;
            string func = expression.Substring(coeffStr.Length).TrimStart('*');
            if (double.TryParse(coeffStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double coeff))
            {
                string innerInt = ComputeIndefiniteIntegral(func, variable);
                if (Math.Abs(coeff - 1) < ZeroTolerance) return innerInt;
                if (Math.Abs(coeff + 1) < ZeroTolerance) return $"-{innerInt}";
                // Clean up: -coeff*cos(x) → -2*cos(x) instead of 2*-cos(x)
                if (innerInt.StartsWith("-"))
                    return $"-{coeffStr}*{innerInt.Substring(1)}";
                return $"{coeffStr}*{innerInt}";
            }
        }

        // Handle bare function calls
        if (expression.StartsWith("sin(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(4, expression.Length - 5);
            if (inner == variable) return $"-cos({variable})";
            return $"-cos({inner}) / ({TrigDerivative(inner, variable)})";
        }
        if (expression.StartsWith("cos(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(4, expression.Length - 5);
            if (inner == variable) return $"sin({variable})";
            return $"sin({inner}) / ({TrigDerivative(inner, variable)})";
        }
        if (expression.StartsWith("tan(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(4, expression.Length - 5);
            if (inner == variable) return $"-ln(cos({variable}))";
            return $"-ln(cos({inner})) / ({TrigDerivative(inner, variable)})";
        }
        if (expression.StartsWith("exp(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(4, expression.Length - 5);
            if (inner == variable) return $"exp({variable})";
            return $"exp({inner}) / ({TrigDerivative(inner, variable)})";
        }
        if (expression.StartsWith("ln(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(3, expression.Length - 4);
            if (inner == variable) return $"{variable}*ln({variable}) - {variable}";
            return $"({variable}*ln({inner}) - ∫{variable}/({inner}) d{variable})";
        }
        if (expression.StartsWith("log(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(4, expression.Length - 5);
            if (inner == variable) return $"{variable}*log({variable}) - {variable}/ln(10)";
            return $"({variable}*log({inner}) - ∫{variable}/({inner}*ln(10)) d{variable})";
        }

        // Handle product: polynomial * trig/exp/ln → integration by parts
        var productParts = SplitProductParts(expression);
        if (productParts.Count == 2)
        {
            string left = productParts[0];
            string right = productParts[1];

            // Identify which is polynomial and which is transcendental
            bool leftIsPoly = IsPolynomial(left, variable);
            bool rightIsPoly = IsPolynomial(right, variable);

            if (leftIsPoly && !rightIsPoly)
                return IntegrationByParts(left, right, variable);
            if (!leftIsPoly && rightIsPoly)
                return IntegrationByParts(right, left, variable);
        }

        // Handle polynomial terms
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

    private static bool IsPolynomial(string expression, string variable)
    {
        // Returns true if expression is purely polynomial (no sin/cos/tan/exp/ln/log)
        string lower = expression.ToLower();
        return !lower.Contains("sin(") && !lower.Contains("cos(") && !lower.Contains("tan(")
            && !lower.Contains("exp(") && !lower.Contains("ln(") && !lower.Contains("log(");
    }

    /// <summary>
    /// Splits an expression by the top-level / operator.
    /// Returns null if there is no division.
    /// </summary>
    private static (string, string)? SplitDivisionParts(string expression)
    {
        int depth = 0;
        for (int i = expression.Length - 1; i >= 0; i--)
        {
            if (expression[i] == ')') depth++;
            else if (expression[i] == '(') depth--;
            else if (depth == 0 && expression[i] == '/')
            {
                string left = expression.Substring(0, i);
                string right = expression.Substring(i + 1);
                if (left.Length > 0 && right.Length > 0)
                    return (left, right);
            }
        }
        return null;
    }

    /// <summary>
    /// Computes a simple derivative for integral checking purposes.
    /// Uses the same logic as ComputeDerivative but returns empty string on failure.
    /// </summary>
    private static string ComputeDerivativeForIntegralCheck(string expression, string variable)
    {
        try
        {
            string norm = NormalizeExpression(expression);

            // Strip outer parentheses pairs: ((x^2)+1) → (x^2)+1 → x^2+1
            while (norm.StartsWith("(") && norm.EndsWith(")"))
            {
                string inner = norm.Substring(1, norm.Length - 2);
                // Check balanced parens - only strip if outer parens are matched
                int depth = 0;
                bool balanced = true;
                for (int i = 0; i < inner.Length; i++)
                {
                    if (inner[i] == '(') depth++;
                    else if (inner[i] == ')') depth--;
                    if (depth < 0) { balanced = false; break; }
                }
                if (balanced && depth == 0) norm = inner;
                else break;
            }

            if (norm == variable) return "1";
            if (!norm.Contains(variable)) return "0";

            // Try polynomial derivative
            var terms = ParsePolynomialTerms(norm);
            if (terms.Count > 0 && terms.Any(t => t.Power > 0))
            {
                var resultTerms = new List<string>();
                foreach (var term in terms)
                {
                    if (term.Power == 0) continue;
                    double newCoefficient = term.Coefficient * term.Power;
                    int newPower = term.Power - 1;
                    resultTerms.Add(FormatTerm(newCoefficient, newPower));
                }
                if (resultTerms.Count > 0) return JoinTerms(resultTerms);
            }
        }
        catch { }
        return "";
    }

    /// <summary>
    /// Checks if an expression equals "1" (handles "1", "(1)", etc.)
    /// </summary>
    private static bool IsOne(string expr)
    {
        string clean = StripOuterParentheses(expr.Trim());
        return clean == "1";
    }

    /// <summary>
    /// Tries to match a denominator against arctan patterns: x^2+1, a^2x^2+1, etc.
    /// Returns "arctan(x)" or "arctan(ax)/a" or null if no match.
    /// </summary>
    private static string? TryArctanPattern(string denominator, string variable)
    {
        // Normalize: strip spaces
        string d = denominator.Replace(" ", "");

        // Pattern: x^2+1 or 1+x^2
        if (d == $"{variable}^2+1" || d == $"1+{variable}^2")
            return $"arctan({variable})";

        // Pattern: a^2*x^2+1 → arctan(ax)/a
        var match = Regex.Match(d, $@"^(\d+\.?\d*)\*?{Regex.Escape(variable)}\^2\+1$");
        if (!match.Success)
            match = Regex.Match(d, $@"^1\+(\d+\.?\d*)\*?{Regex.Escape(variable)}\^2$");
        if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double a))
        {
            if (Math.Abs(a - 1) < ZeroTolerance) return $"arctan({variable})";
            double sqrtA = Math.Sqrt(a);
            return $"arctan({sqrtA}*{variable})/{sqrtA}";
        }

        // Pattern: (x^2+1) with outer parens
        if (d.StartsWith("(") && d.EndsWith(")"))
        {
            string inner = StripOuterParentheses(d);
            if (inner != d) return TryArctanPattern(inner, variable);
        }

        return null;
    }

    /// <summary>
    /// Checks if two expressions are algebraically equivalent (simple string comparison after normalization).
    /// </summary>
    private static bool ExpressionsMatch(string a, string b)
    {
        string na = NormalizeExpression(a);
        string nb = NormalizeExpression(b);
        if (na == nb) return true;

        // Try swapping order of addition terms
        var termsA = SplitAdditiveTerms(na).OrderBy(t => t).ToList();
        var termsB = SplitAdditiveTerms(nb).OrderBy(t => t).ToList();
        if (termsA.Count == termsB.Count && termsA.SequenceEqual(termsB)) return true;

        // Try with explicit multiplication signs added
        string na2 = na.Replace("*", "");
        string nb2 = nb.Replace("*", "");
        if (na2 == nb2) return true;

        return false;
    }

    private static string IntegrationByParts(string poly, string func, string variable)
    {
        // ∫ poly * func dx using integration by parts
        // For x^n * f(x): u = x^n, dv = f(x)dx → du = n*x^(n-1), v = ∫f(x)dx
        // Result: x^n * V - ∫ n*x^(n-1) * V dx, where V = ∫f(x)dx

        string v = ComputeIndefiniteIntegral(func, variable); // ∫f(x)dx

        // Get polynomial power and coefficient
        var terms = ParsePolynomialTerms(poly);
        if (terms.Count == 0) return "0";

        // For simplicity, handle single-term polynomials: a*x^n
        var mainTerm = terms[0];
        double coeff = mainTerm.Coefficient;
        int power = mainTerm.Power;

        if (power == 0)
        {
            // Constant * func: just pull out constant
            if (Math.Abs(coeff - 1) < ZeroTolerance) return v;
            if (Math.Abs(coeff + 1) < ZeroTolerance) return $"-{v}";
            string cStr = coeff.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            return $"{cStr}*{v}";
        }

        // u = x^n, du = n*x^(n-1)
        string uStr = power == 1 ? variable : $"{variable}^{power}";
        string duCoeff = (coeff * power).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
        string duPoly = power == 2 ? $"{duCoeff}*{variable}" : $"{duCoeff}*{variable}^{power - 1}";

        // ∫ u*dv = u*v - ∫ v*du
        // But v is an antiderivative expression, not a simple function — we can't integrate v*du symbolically in general.
        // So we apply parts iteratively for specific known patterns.

        // Known patterns:
        // ∫x*sin(x)dx = -x*cos(x) + sin(x)
        // ∫x*cos(x)dx = x*sin(x) + cos(x)
        // ∫x*exp(x)dx = x*exp(x) - exp(x)
        // ∫x*ln(x)dx = x²/2*ln(x) - x²/4

        if (power == 1)
        {
            if (func.StartsWith("sin(") && func.EndsWith(")"))
            {
                string inner = func.Substring(4, func.Length - 5);
                if (inner == variable)
                    return $"-{variable}*cos({variable}) + sin({variable})";
            }
            if (func.StartsWith("cos(") && func.EndsWith(")"))
            {
                string inner = func.Substring(4, func.Length - 5);
                if (inner == variable)
                    return $"{variable}*sin({variable}) + cos({variable})";
            }
            if (func.StartsWith("exp(") && func.EndsWith(")"))
            {
                string inner = func.Substring(4, func.Length - 5);
                if (inner == variable)
                    return $"{variable}*exp({variable}) - exp({variable})";
            }
            if (func.StartsWith("ln(") && func.EndsWith(")"))
            {
                string inner = func.Substring(3, func.Length - 4);
                if (inner == variable)
                    return $"{variable}^2/2*ln({variable}) - {variable}^2/4";
            }
        }

        if (power == 2)
        {
            if (func.StartsWith("sin(") && func.EndsWith(")"))
            {
                string inner = func.Substring(4, func.Length - 5);
                if (inner == variable)
                    return $"-{variable}^2*cos({variable}) + 2*{variable}*sin({variable}) + 2*cos({variable})";
            }
            if (func.StartsWith("cos(") && func.EndsWith(")"))
            {
                string inner = func.Substring(4, func.Length - 5);
                if (inner == variable)
                    return $"{variable}^2*sin({variable}) - 2*{variable}*cos({variable}) + 2*sin({variable})";
            }
            if (func.StartsWith("exp(") && func.EndsWith(")"))
            {
                string inner = func.Substring(4, func.Length - 5);
                if (inner == variable)
                    return $"{variable}^2*exp({variable}) - 2*{variable}*exp({variable}) + 2*exp({variable})";
            }
        }

        // Generic fallback: u*v - ∫v*du (show symbolic)
        string uPower = power == 1 ? variable : $"{variable}^{power}";
        return $"{uPower}*{v} - ∫{duPoly}*{v} d{variable}";
    }

    private static double ComputeDefiniteIntegral(string expression, string variable, double lower, double upper)
    {
        string clean = NormalizeExpression(expression);

        // Check for known function integrals (radians mode for calculus)
        string cleanTrimmed = StripOuterParentheses(clean);

        if (cleanTrimmed == "sin(" + variable + ")" || cleanTrimmed == "sin" + variable)
        {
            // ∫sin(x)dx = -cos(x), evaluated from lower to upper
            double result = -Math.Cos(upper) - (-Math.Cos(lower));
            return result;
        }
        if (cleanTrimmed == "cos(" + variable + ")" || cleanTrimmed == "cos" + variable)
        {
            // ∫cos(x)dx = sin(x)
            double result = Math.Sin(upper) - Math.Sin(lower);
            return result;
        }
        if (cleanTrimmed == "exp(" + variable + ")" || cleanTrimmed == "exp" + variable)
        {
            return Math.Exp(upper) - Math.Exp(lower);
        }

        // Handle additive terms
        var addTerms = SplitAdditiveTerms(clean);
        if (addTerms.Count > 1)
        {
            return addTerms.Sum(t => ComputeDefiniteIntegral(t, variable, lower, upper));
        }

        // Try polynomial
        var terms = ParsePolynomialTerms(clean);
        if (terms.Count == 0)
            return 0;

        double result2 = 0;
        foreach (var term in terms)
        {
            double lowerTerm = term.Coefficient * Math.Pow(lower, term.Power + 1) / (term.Power + 1);
            double upperTerm = term.Coefficient * Math.Pow(upper, term.Power + 1) / (term.Power + 1);
            result2 += upperTerm - lowerTerm;
        }

        return result2;
    }

    private static double ComputeLimit(string expression, string variable, double point)
    {
        expression = NormalizeExpression(expression);

        // Handle infinity: approach from one side with large values
        if (double.IsPositiveInfinity(point) || double.IsNegativeInfinity(point))
        {
            return ComputeLimitAtInfinity(expression, variable, double.IsPositiveInfinity(point));
        }

        // Check for division: numerator/denominator
        var divParts = SplitDivisionParts(expression);
        if (divParts != null)
        {
            string num = divParts.Value.Item1;
            string den = divParts.Value.Item2;

            // Evaluate numerator and denominator at the point (radians for trig)
            double numVal = EvaluateExpressionNumerically(num, variable, point, useRadians: true);
            double denVal = EvaluateExpressionNumerically(den, variable, point, useRadians: true);

            // If not indeterminate, just compute
            if (Math.Abs(denVal) > 1e-15)
                return numVal / denVal;

            // 0/0 or ∞/∞ - try L'Hôpital's rule symbolically first
            string lhNum = num;
            string lhDen = den;
            for (int i = 0; i < 5; i++)
            {
                string dNum = ComputeDerivative(lhNum, variable);
                string dDen = ComputeDerivative(lhDen, variable);
                if (string.IsNullOrEmpty(dNum) || string.IsNullOrEmpty(dDen))
                    break;

                double dNumVal = EvaluateExpressionNumerically(dNum, variable, point, useRadians: true);
                double dDenVal = EvaluateExpressionNumerically(dDen, variable, point, useRadians: true);

                if (Math.Abs(dDenVal) > 1e-15)
                    return dNumVal / dDenVal;

                lhNum = dNum;
                lhDen = dDen;
            }

            // Fall back to numerical approach
            return NumericalLimit(num, den, variable, point);
        }

        // No division - evaluate numerically (radians for trig)
        return EvaluateExpressionNumerically(expression, variable, point, useRadians: true);
    }

    /// <summary>
    /// Computes a limit as x approaches +∞ or -∞.
    /// </summary>
    private static double ComputeLimitAtInfinity(string expression, string variable, bool positive)
    {
        double bestResult = double.NaN;
        double[] testPoints = positive
            ? new[] { 100, 1000, 10000, 100000, 1e6, 1e8 }
            : new[] { -100, -1000, -10000, -100000, -1e6, -1e8 };

        double prev = double.NaN;
        foreach (double x in testPoints)
        {
            double val = EvaluateExpressionNumerically(expression, variable, x, useRadians: true);
            if (double.IsNaN(val) || double.IsInfinity(val))
            {
                bestResult = val;
                break;
            }
            if (!double.IsNaN(prev) && Math.Abs(val - prev) < 1e-6)
            {
                bestResult = val;
                break;
            }
            prev = val;
            bestResult = val;
        }
        return bestResult;
    }

    /// <summary>
    /// Numerically evaluates a limit by approaching from both sides.
    /// </summary>
    private static double NumericalLimit(string numerator, string denominator, string variable, double point)
    {
        // Try approaching from both sides with decreasing step sizes
        double bestResult = double.NaN;
        for (int exp = 2; exp <= 10; exp++)
        {
            double h = Math.Pow(10, -exp);
            double numP = EvaluateExpressionNumerically(numerator, variable, point + h, useRadians: true);
            double denP = EvaluateExpressionNumerically(denominator, variable, point + h, useRadians: true);
            double numM = EvaluateExpressionNumerically(numerator, variable, point - h, useRadians: true);
            double denM = EvaluateExpressionNumerically(denominator, variable, point - h, useRadians: true);

            if (Math.Abs(denP) > 1e-30 && Math.Abs(denM) > 1e-30)
            {
                double fromRight = numP / denP;
                double fromLeft = numM / denM;
                if (Math.Abs(fromRight - fromLeft) < 0.01)
                {
                    bestResult = (fromRight + fromLeft) / 2.0;
                    break;
                }
            }
        }
        return double.IsNaN(bestResult) ? 0 : bestResult;
    }

    /// <summary>
    /// Numerically evaluates an expression at a given point using simple token replacement.
    /// </summary>
    private static double EvaluateExpressionNumerically(string expression, string variable, double value, bool useRadians = false)
    {
        string expr = expression;
        string valStr = value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
        // Only replace standalone variable, not inside function names like exp, sin, etc.
        expr = System.Text.RegularExpressions.Regex.Replace(expr,
            @"(?<![a-zA-Z])" + System.Text.RegularExpressions.Regex.Escape(variable) + @"(?![a-zA-Z])",
            $"({valStr})");

        // Handle sin/cos/tan/exp/ln by evaluating innermost parens first
        // Use a loop to evaluate from inside out
        for (int iter = 0; iter < 20; iter++)
        {
            // Find innermost function call (no nested parens inside)
            string before = expr;

            // Handle exp(...)
            expr = System.Text.RegularExpressions.Regex.Replace(expr, @"exp\(([^()]+)\)", match =>
            {
                double innerVal = EvalSimpleNumeric(match.Groups[1].Value);
                return Math.Exp(innerVal).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            });

            // Handle sin(...)
            expr = System.Text.RegularExpressions.Regex.Replace(expr, @"sin\(([^()]+)\)", match =>
            {
                double innerVal = EvalSimpleNumeric(match.Groups[1].Value);
                double angle = useRadians ? innerVal : innerVal * Math.PI / 180.0;
                return Math.Sin(angle).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            });

            // Handle cos(...)
            expr = System.Text.RegularExpressions.Regex.Replace(expr, @"cos\(([^()]+)\)", match =>
            {
                double innerVal = EvalSimpleNumeric(match.Groups[1].Value);
                double angle = useRadians ? innerVal : innerVal * Math.PI / 180.0;
                return Math.Cos(angle).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            });

            // Handle tan(...)
            expr = System.Text.RegularExpressions.Regex.Replace(expr, @"tan\(([^()]+)\)", match =>
            {
                double innerVal = EvalSimpleNumeric(match.Groups[1].Value);
                double angle = useRadians ? innerVal : innerVal * Math.PI / 180.0;
                return Math.Tan(angle).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            });

            // Handle ln(...)
            expr = System.Text.RegularExpressions.Regex.Replace(expr, @"ln\(([^()]+)\)", match =>
            {
                double innerVal = EvalSimpleNumeric(match.Groups[1].Value);
                return Math.Log(innerVal).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            });

            // Handle sqrt(...)
            expr = System.Text.RegularExpressions.Regex.Replace(expr, @"sqrt\(([^()]+)\)", match =>
            {
                double innerVal = EvalSimpleNumeric(match.Groups[1].Value);
                return Math.Sqrt(innerVal).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            });

            // Evaluate bare parenthesized expressions like (3.14)
            expr = System.Text.RegularExpressions.Regex.Replace(expr, @"\(([^()]+)\)", match =>
            {
                string inner = match.Groups[1].Value;
                bool isNumeric = inner.All(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+' || c == 'e' || c == 'E');
                if (!isNumeric)
                    return match.Value; // leave non-numeric parens alone

                bool isNegative = inner.TrimStart().StartsWith("-");
                int idx = match.Index;
                bool precededByToken = idx > 0 && (char.IsDigit(expr[idx - 1]) || char.IsLetter(expr[idx - 1]));

                if (precededByToken)
                {
                    // 2(-3) → 2*-3, sin(-3) → sin*-3 — strip parens, sin/cos regex will handle
                    return $"*{inner}";
                }
                else if (idx > 0 && expr[idx - 1] == '(')
                {
                    // Function call context: sin((-0.01)) → strip parens so sin regex can match
                    return inner;
                }
                else
                {
                    // Standalone (-3) → keep parens to preserve sign: (-3)^2
                    return isNegative ? $"({inner})" : inner;
                }
            });

            if (expr == before) break;
        }

        return EvalSimpleNumeric(expr);
    }

    private static double EvalSimpleNumeric(string expr)
    {
        try
        {
            // NCalc uses ** for power, not ^
            string ncalcExpr = expr.Replace("^", "**");
            var ncalc = new NCalc.Expression(ncalcExpr);
            ncalc.Parameters["pi"] = Math.PI;
            ncalc.Parameters["e"] = Math.E;
            return Convert.ToDouble(ncalc.Evaluate(), System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Strips balanced outer parentheses from an expression.
    /// (exp(x)) → exp(x), (x^2) → x^2, ((x)) → x
    /// Does NOT strip if inner parens are unbalanced.
    /// </summary>
    private static string StripOuterParentheses(string expression)
    {
        while (expression.StartsWith("(") && expression.EndsWith(")"))
        {
            string inner = expression.Substring(1, expression.Length - 2);
            int depth = 0;
            bool balanced = true;
            for (int i = 0; i < inner.Length; i++)
            {
                if (inner[i] == '(') depth++;
                else if (inner[i] == ')') depth--;
                if (depth < 0) { balanced = false; break; }
            }
            if (balanced && depth == 0)
                expression = inner;
            else
                break;
        }
        return expression;
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

    /// <summary>
    /// Post-processes calculus results: strips redundant parens, combines like terms,
    /// moves constants to front, cleans formatting.
    /// </summary>
    private static string SimplifyCalculusResult(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return expression;

        // Handle "expr + C" for integrals
        string cSuffix = "";
        string expr = expression.Trim();
        if (expr.EndsWith("+ C"))
        {
            cSuffix = " + C";
            expr = expr.Substring(0, expr.Length - 3).TrimEnd();
        }

        // Step 1: Strip redundant outer parens
        expr = StripRedundantOuterParens(expr);

        // Step 2: Clean +(expr) and -(expr) (only at term boundaries, not inside nested parens)
        // Only strip when closing ) is NOT followed by * or / (to preserve multiplication semantics)
        expr = Regex.Replace(expr, @"(?<!\()\+\s*\(([^()]+)\)(?![a-zA-Z0-9\(\*\/])", "+$1");
        expr = Regex.Replace(expr, @"(?<!\()-\s*\(([^()]+)\)(?![a-zA-Z0-9\(\*\/])", "-$1");
        // Clean nested: (+(x^3)) → x^3
        expr = Regex.Replace(expr, @"\(\+\(([^()]+)\)\)", "$1");
        // Clean +(+(expr)) → +expr
        expr = Regex.Replace(expr, @"\+\(\+([^()]+)\)\)", "+$1");

        // Step 3: Clean (exp(x)) → exp(x), (sin(x)) → sin(x), etc.
        expr = Regex.Replace(expr, @"\((exp\([^()]+\))\)", "$1");
        expr = Regex.Replace(expr, @"\((sin\([^()]+\))\)", "$1");
        expr = Regex.Replace(expr, @"\((cos\([^()]+\))\)", "$1");
        expr = Regex.Replace(expr, @"\((ln\([^()]+\))\)", "$1");

        // Step 4: Move trailing constant to front: exp(3x)*(3) → 3*exp(3*x)
        expr = Regex.Replace(expr, @"(exp\([^()]+\))\*\((\d+\.?\d*)\)", "$2*$1");
        expr = Regex.Replace(expr, @"(sin\([^()]+\))\*\((\d+\.?\d*)\)", "$2*$1");
        expr = Regex.Replace(expr, @"(cos\([^()]+\))\*\((\d+\.?\d*)\)", "$2*$1");

        // Step 5: Combine additive terms with same structure
        expr = CombineLikeTerms(expr);

        return expr + cSuffix;
    }

    /// <summary>
    /// Strips outer parentheses when they are redundant (balanced and nothing follows).
    /// </summary>
    private static string StripRedundantOuterParens(string expr)
    {
        expr = expr.Trim();
        while (expr.StartsWith("(") && expr.EndsWith(")"))
        {
            int depth = 0;
            bool balanced = true;
            for (int i = 0; i < expr.Length; i++)
            {
                if (expr[i] == '(') depth++;
                if (expr[i] == ')') depth--;
                if (depth == 0 && i < expr.Length - 1)
                {
                    balanced = false;
                    break;
                }
            }
            if (balanced && depth == 0)
                expr = expr.Substring(1, expr.Length - 2).Trim();
            else
                break;
        }
        return expr;
    }

    /// <summary>
    /// Splits an expression into additive terms, extracts coefficient + structure,
    /// groups by structure, and sums coefficients.
    /// </summary>
    private static string CombineLikeTerms(string expression)
    {
        // Skip combining if expression has division (fractions like "x^3 / 3")
        // — our term parser can't handle those correctly
        if (expression.Contains("/"))
            return expression;

        var terms = SplitAdditiveTerms(expression);
        if (terms.Count <= 1) return expression;

        // Parse each term into (coefficient, structure)
        var parsed = new List<(double coeff, string structure)>();
        foreach (var term in terms)
        {
            var (coeff, structure) = ExtractCoefficientAndStructure(term);
            parsed.Add((coeff, structure));
        }

        // Group by structure, sum coefficients
        var grouped = new Dictionary<string, double>();
        var order = new List<string>();
        foreach (var (coeff, structure) in parsed)
        {
            if (!grouped.ContainsKey(structure))
            {
                grouped[structure] = 0;
                order.Add(structure);
            }
            grouped[structure] += coeff;
        }

        // Build result terms
        var resultTerms = new List<(double coeff, string structure)>();
        foreach (var key in order)
        {
            double c = grouped[key];
            if (Math.Abs(c) > 1e-10)
                resultTerms.Add((c, key));
        }

        // Sort: polynomial-like terms by power desc, then mixed terms, then constants last
        resultTerms = resultTerms
            .OrderByDescending(t => {
                if (string.IsNullOrEmpty(t.structure)) return -1; // constants last
                if (Regex.IsMatch(t.structure, @"^x\^(\d+)$"))
                    return int.Parse(Regex.Match(t.structure, @"x\^(\d+)").Groups[1].Value);
                if (t.structure == "x") return 1;
                return 0; // mixed terms after polynomial, before constants
            })
            .ToList();

        var sb = new System.Text.StringBuilder();
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        foreach (var (coeff, structure) in resultTerms)
        {
            if (sb.Length > 0)
            {
                if (coeff >= 0)
                    sb.Append(" + ");
                else
                    sb.Append(" - ");
            }
            else if (coeff < 0)
            {
                sb.Append("-");
            }

            double absCoeff = Math.Abs(coeff);
            if (string.IsNullOrEmpty(structure))
            {
                // Pure constant
                sb.Append(absCoeff.ToString("G10", culture));
            }
            else if (Regex.IsMatch(structure, @"^x\^(\d+)$"))
            {
                // Polynomial term like x^3: use FormatTerm
                int power = int.Parse(Regex.Match(structure, @"x\^(\d+)").Groups[1].Value);
                sb.Append(FormatTerm(absCoeff, power));
            }
            else if (structure == "x")
            {
                // Linear term: use FormatTerm
                sb.Append(FormatTerm(absCoeff, 1));
            }
            else if (Math.Abs(absCoeff - 1) < 1e-10)
            {
                sb.Append(structure);
            }
            else
            {
                // Mixed term like x^2*exp(x)
                sb.Append(absCoeff.ToString("G10", culture));
                sb.Append("*");
                sb.Append(structure);
            }
        }

        return sb.Length > 0 ? sb.ToString() : expression;
    }

    /// <summary>
    /// Extracts a numeric coefficient and "structure" (variable part) from a term.
    /// e.g. "3x^2*exp(x)" → (3, "x^2*exp(x)"), "(x^2)*cos(x)" → (1, "x^2*cos(x)")
    /// </summary>
    private static (double coeff, string structure) ExtractCoefficientAndStructure(string term)
    {
        string t = term.Trim();
        if (string.IsNullOrEmpty(t)) return (0, "");

        // Handle leading sign
        double sign = 1;
        if (t.StartsWith("-"))
        {
            sign = -1;
            t = t.Substring(1).Trim();
        }
        else if (t.StartsWith("+"))
        {
            t = t.Substring(1).Trim();
        }

        // Clean outer parens from the whole term: (3x^2)*cos(x) → 3x^2*cos(x)
        t = StripRedundantOuterParens(t);

        // Try to extract leading number followed by * or end
        var match = Regex.Match(t, @"^(\d+\.?\d*)\*?(.*)$");
        if (match.Success)
        {
            double coeff = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            string rest = match.Groups[2].Value.Trim();
            if (rest.StartsWith("*")) rest = rest.Substring(1).Trim();
            return (sign * coeff, rest);
        }

        // No leading number: coefficient is 1
        return (sign * 1, t);
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
        string result = expression.Replace(" ", "").Replace("\t", "");

        // Convert e^(expr) to exp(expr) for proper handling
        // Match e^(...) patterns - handles nested parens
        result = ConvertENotation(result);

        // Add implicit multiplication: )x → )*x, )e → )*e, )2 → )*2, etc.
        result = AddImplicitMultiplication(result);

        return result;
    }

    private static string ConvertENotation(string expression)
    {
        // Convert e^(expr) to exp(expr) - handles nested parens
        // Also convert a^(expr) to exp(expr*ln(a)) for any numeric base
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < expression.Length)
        {
            if (i + 1 < expression.Length && expression[i] == 'e' && expression[i + 1] == '^')
            {
                // Found e^ - convert to exp(...)
                int start = i;
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
                    // e^x (no parens) - take single variable/number
                    int innerStart = i;
                    while (i < expression.Length && char.IsLetterOrDigit(expression[i]))
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

            // After appending a char, check for number^pattern (e.g., 2^x, 10^(x+1))
            // This handles non-e bases like 2^x → exp(x*ln(2))
            if (sb.Length >= 1 && i < expression.Length && expression[i] == '^'
                && i + 1 < expression.Length && char.IsDigit(sb[sb.Length - 1])
                && sb[sb.Length - 1] != 'e') // not already part of scientific notation
            {
                // Collect the base number
                int baseEnd = sb.Length - 1;
                int baseStart = baseEnd;
                while (baseStart > 0 && (char.IsDigit(sb[baseStart - 1]) || sb[baseStart - 1] == '.'))
                    baseStart--;
                string baseNum = sb.ToString(baseStart, baseEnd - baseStart + 1);

                i++; // skip ^
                string inner;
                if (i < expression.Length && expression[i] == '(')
                {
                    // a^(...) - find matching close paren
                    int depth = 1;
                    int innerStart = i + 1;
                    i++;
                    while (i < expression.Length && depth > 0)
                    {
                        if (expression[i] == '(') depth++;
                        else if (expression[i] == ')') depth--;
                        if (depth > 0) i++;
                    }
                    inner = expression.Substring(innerStart, i - innerStart);
                    i++; // skip closing )
                }
                else
                {
                    // a^x - take single token
                    int innerStart = i;
                    while (i < expression.Length && char.IsLetterOrDigit(expression[i]))
                        i++;
                    inner = expression.Substring(innerStart, i - innerStart);
                }

                // Replace baseNum^inner with exp(inner*ln(baseNum))
                sb.Length = baseStart;
                sb.Append($"exp({inner}*ln({baseNum}))");
            }
        }
        return sb.ToString();
    }

    private static string AddImplicitMultiplication(string expression)
    {
        // Insert * between: )letter, )digit, )/, digit letter, letter( (but not function names)
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < expression.Length; i++)
        {
            result.Append(expression[i]);
            if (i + 1 < expression.Length)
            {
                char cur = expression[i];
                char next = expression[i + 1];
                bool curIsCloseParen = cur == ')';
                bool nextIsLetter = char.IsLetter(next);
                bool nextIsOpenParen = next == '(';
                bool nextIsDigit = char.IsDigit(next);
                bool curIsDigit = char.IsDigit(cur);

                // ) followed by letter or digit or (
                if (curIsCloseParen && (nextIsLetter || nextIsDigit || nextIsOpenParen))
                    result.Append('*');
                // digit followed by letter or ( — only add * for function names and (, NOT for variable x
                else if (curIsDigit && (nextIsLetter || nextIsOpenParen))
                {
                    if (nextIsOpenParen)
                        result.Append('*');
                    else if (nextIsLetter && next != 'x' && next != 'X')
                        result.Append('*');
                }
                // letter followed by ( — but NOT if it's a function name
                // e.g., "sin(" should stay as is, but "x(" should become "x*("
                else if (char.IsLetter(cur) && nextIsOpenParen && cur != '^')
                {
                    // Check if this is a function name: look back to see if this is the end of a function
                    // If the previous char(s) form a function name like sin, cos, etc., don't add *
                    string[] funcNames = { "sin", "cos", "tan", "asin", "acos", "atan", "sinh", "cosh", "tanh", "exp", "ln", "log", "sqrt", "abs", "floor", "ceil", "round" };
                    bool isFuncName = false;
                    foreach (var fn in funcNames)
                    {
                        int fnStart = i - fn.Length + 1;
                        if (fnStart >= 0 && expression.Substring(fnStart, fn.Length).Equals(fn, StringComparison.OrdinalIgnoreCase))
                        {
                            isFuncName = true;
                            break;
                        }
                    }
                    if (!isFuncName)
                        result.Append('*');
                }
            }
        }
        return result.ToString();
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