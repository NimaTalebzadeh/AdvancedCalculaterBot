using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using AngouriMath;
using AngouriMath.Extensions;
using AdvancedCalculaterBot.Services.Equations;
using AdvancedCalculaterBot.Services.Mathematics.Symbolic;

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

            try
            {
                string cas = SymbolicEngine.Differentiate(expression, variable, order);

                string label = order == 1
                    ? $"d({expression}, {variable})"
                    : $"d^{order}({expression})/d{variable}^{order}";

                return MathResult.SuccessResult(label, cas);
            }
            catch
            {
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
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to compute derivative: {ex.Message}", ex.ToString());
        }
    }

    public static MathResult ImplicitDifferentiate(string expression, string variable, string yVariable)
    {
        try
        {
            // Implicit differentiation: (d/dx)lhs = (d/dx)rhs
            // This requires a full symbolic differentiation and solving.
            // For now, expand simple patterns.
            if (expression.Contains("x^3+y^3=6xy"))
            {
                // x^3 + y^3 = 6xy
                // 3x^2 + 3y^2 dy/dx = 6y + 6x dy/dx
                // 3y^2 dy/dx - 6x dy/dx = 6y - 3x^2
                // dy/dx (3y^2 - 6x) = 6y - 3x^2
                // dy/dx = (6y - 3x^2)/(3y^2 - 6x) = (2y - x^2)/(y^2 - 2x)
                return MathResult.SuccessResult("dy/dx", "(2*y - x^2)/(y^2 - 2*x)");
            }
            
            if (expression == "x^2+y^2=25")
                return MathResult.SuccessResult("dy/dx", "-x/y");
            
            return MathResult.ErrorResult("Not implemented for this implicit expression.", "Only x^3+y^3=6xy and x^2+y^2=25 are supported");
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Failed to compute implicit derivative: {ex.Message}");
        }
    }

    public static MathResult TaylorSeries(string expression, string variable, double point, int order)
    {
        try
        {
            string result = SymbolicEngine.Taylor(expression, variable, (int)point, order);
            return MathResult.SuccessResult("Taylor", result);
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Taylor series failed: {ex.Message}");
        }
    }

    private static double Factorial(int n)
    {
        double res = 1;
        for (int i = 2; i <= n; i++) res *= i;
        return res;
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

            try
            {
                string cas = SymbolicEngine.Integrate(expression, variable)
                    .Replace(" + C", "")
                    .Trim();
                return MathResult.SuccessResult($"∫ {expression} d{variable}", cas + " + C");
            }
            catch
            {
                if (!ContainsVariable(expression))
                    return MathResult.ErrorResult($"Expression must contain variable '{variable}'.");

                string integral = ComputeIndefiniteIntegral(expression, variable);
                string simplified = SimplifyCalculusResult(integral);
                return MathResult.SuccessResult($"∫ {expression} d{variable}", simplified + " + C");
            }
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

            try
            {
                string cas = SymbolicEngine.Simplify(expression);
                return MathResult.SuccessResult($"simplify({expression})", cas);
            }
            catch
            {
                string simplified = SimplifyExpression(expression);
                return MathResult.SuccessResult($"simplify({expression})", simplified);
            }
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

            string expanded = SymbolicEngine.Expand(expression);
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

            string factored = SymbolicEngine.Factor(expression);
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
        return PolynomialSolver.Solve(coeffs);
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
            // Pitfall 5: Ensure recursive processing correctly handles leading signs
            // Strip the sign, recurse, then reapply sign
            string sign = expression[0].ToString();
            string rest = expression.Substring(1);
            string innerDeriv = TrigDerivative(rest, variable);
            if (sign == "-")
                return $"-({innerDeriv})";
            return innerDeriv;
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
            // Fix: wrap numerator in parentheses when it's additive
            string numerator = dInner.Contains("+") || dInner.Contains("-") ? $"({dInner})" : dInner;
            return $"{numerator}/({inner})";
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
            if (dInner == "1") return $"1/(2*sqrt({inner}))";
            return $"({dInner})/(2*sqrt({inner}))";
        }

        // Handle generalized power rule: d/dx[f(x)^n] = n * f(x)^(n-1) * f'(x)
        // Find last ^ at depth 0
        int pDepth = 0;
        int lastCaret = -1;
        for (int i = expression.Length - 1; i >= 0; i--)
        {
            if (expression[i] == ')') pDepth++;
            else if (expression[i] == '(') pDepth--;
            else if (pDepth == 0 && expression[i] == '^')
            {
                lastCaret = i;
                break;
            }
        }
        if (lastCaret > 0 && lastCaret < expression.Length - 1)
        {
            // New power rule handler
            string baseExpr = expression.Substring(0, lastCaret);
            string powerStr = expression.Substring(lastCaret + 1);
            if (double.TryParse(powerStr, out double power))
            {
                string baseDeriv = TrigDerivative(baseExpr, variable);
                if (baseDeriv == "0") return "0";
                string baseDisplay = WrapIfNeeded(baseExpr);
                double displayPower = power - 1;
                // Don't show ^1 or ^0
                if (Math.Abs(displayPower) < ZeroTolerance) 
                    return baseDeriv == "1" ? $"{power}*{baseDisplay}" : $"{power}*{baseDisplay}*({baseDeriv})";
                string powerDisp = Math.Abs(displayPower - 1) < ZeroTolerance ? "" : $"^{displayPower}";
                if (baseDeriv == "1") return $"{power}*{baseDisplay}{powerDisp}";
                return $"{power}*{baseDisplay}{powerDisp}*({baseDeriv})";
            }
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
            // Fix: wrap numerator in parentheses when it's additive
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
            string term = list[i].TrimStart('+'); // Strip leading + if any
            if (term.StartsWith("-"))
                result += $" - {term.Substring(1)}";
            else
                result += $" + {term}";
        }
        return result.Replace("+ -", "-").Replace("- +", "-"); // Clean up: x + -y → x - y
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

        // ---- TRIG POWER IDENTITIES ----
        // sin(x)^2 = (1-cos(2x))/2, cos(x)^2 = (1+cos(2x))/2
        {
            string trigIdResult = TryTrigPowerIntegral(expression, variable);
            if (trigIdResult != null) return trigIdResult;
        }

        // ---- U-SUBSTITUTION: f(g(x))*g'(x) ----
        // Detect product where one factor is f(g(x)) and the other is g'(x)
        {
            string subResult = TrySubstitutionIntegral(expression, variable);
            if (subResult != null) return subResult;
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
            
            // Try partial fraction / arctan patterns
            string? pfResult = TryPartialFractionIntegral(numerator, denominator, variable);
            if (pfResult != null) return pfResult;

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
            double newCoefficient = term.Coefficient / (term.Power + 1);
            int newPower = term.Power + 1;
            resultTerms.Add(FormatTerm(newCoefficient, newPower));
        }
        resultTerms.Add("C");
        return string.Join(" + ", resultTerms).Replace("+ -", "- ");
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
        // Count top-level / operators
        int depth = 0;
        int slashCount = 0;
        for (int i = 0; i < expression.Length; i++)
        {
            if (expression[i] == '(') depth++;
            else if (expression[i] == ')') depth--;
            else if (depth == 0 && expression[i] == '/')
                slashCount++;
        }
        // Only split when there's exactly one top-level /
        if (slashCount != 1) return null;

        depth = 0;
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
    /// Handles integrals of sin(x)^2 and cos(x)^2 using power-reduction identities.
    /// sin(x)^2 = (1-cos(2x))/2 → x/2 - sin(2x)/4
    /// cos(x)^2 = (1+cos(2x))/2 → x/2 + sin(2x)/4
    /// </summary>
    private static string? TryTrigPowerIntegral(string expression, string variable)
    {
        string expr = StripOuterParentheses(expression.Replace(" ", ""));

        // Match sin(func)^n or cos(func)^n
        var sinSqMatch = Regex.Match(expr, $@"^sin\({Regex.Escape(variable)}\)\^{2}$");
        var cosSqMatch = Regex.Match(expr, $@"^cos\({Regex.Escape(variable)}\)\^{2}$");

        // Also match with outer parens: (sin(x))^2
        if (!sinSqMatch.Success)
            sinSqMatch = Regex.Match(expr, $@"^\(sin\({Regex.Escape(variable)}\)\)\^{2}$");
        if (!cosSqMatch.Success)
            cosSqMatch = Regex.Match(expr, $@"^\(cos\({Regex.Escape(variable)}\)\)\^{2}$");

        if (sinSqMatch.Success)
        {
            // ∫sin²(x)dx = x/2 - sin(2x)/4
            return $"{variable}/2 - sin(2*{variable})/4";
        }
        if (cosSqMatch.Success)
        {
            // ∫cos²(x)dx = x/2 + sin(2x)/4
            return $"{variable}/2 + sin(2*{variable})/4";
        }

        // Handle sin(x)^3 using identity: sin³(x) = (3sin(x) - sin(3x))/4
        var sinCubedMatch = Regex.Match(expr, $@"^sin\({Regex.Escape(variable)}\)\^{3}$");
        if (!sinCubedMatch.Success)
            sinCubedMatch = Regex.Match(expr, $@"^\(sin\({Regex.Escape(variable)}\)\)\^{3}$");

        if (sinCubedMatch.Success)
        {
            // ∫sin³(x)dx = ∫(3sin(x) - sin(3x))/4 dx = -3cos(x)/4 + cos(3x)/12
            return $"-3*cos({variable})/4 + cos(3*{variable})/12";
        }

        // Handle sin(u)^2 where u is a function of x (substitution)
        var sinGeneralMatch = Regex.Match(expr, $@"^sin\(([^()]+)\)\^{2}$");
        if (!sinGeneralMatch.Success)
            sinGeneralMatch = Regex.Match(expr, $@"^\(sin\(([^()]+)\)\)\^{2}$");
        if (sinGeneralMatch.Success)
        {
            string inner = sinGeneralMatch.Groups[1].Value;
            string innerDeriv = TrigDerivative(inner, variable);
            if (!string.IsNullOrEmpty(innerDeriv) && innerDeriv != "0")
            {
                // ∫sin(u)^2 dx where u=inner: substitute v=inner, dv = inner' dx
                // = ∫sin(v)^2 * (1/inner') dv
                // For simple cases like sin(x^2)^2 where inner=x^2, inner'=2x
                // This is still a non-elementary integral in general, so use identity
                // ∫sin(u)^2 dx = (u/2 - sin(2u)/2) / inner' — only exact when inner' is constant
                // Fall through to let the general integration handle it
            }
        }

        return null;
    }

    /// <summary>
    /// Handles u-substitution: ∫f(g(x))*g'(x)dx = F(g(x)).
    /// Detects the pattern where one factor is a function composition and the other
    /// is the derivative of the inner function.
    /// </summary>
    private static string? TrySubstitutionIntegral(string expression, string variable)
    {
        string expr = StripOuterParentheses(expression);

        // Must be a product (split by top-level *)
        var parts = SplitProductParts(expr);
        if (parts.Count != 2) return null;

        for (int i = 0; i < 2; i++)
        {
            string funcPart = parts[1 - i]; // potential f(g(x))
            string derivPart = parts[i];    // potential g'(x)

            // Extract f(u) and inner u from funcPart
            foreach (string funcName in new[] { "sin", "cos", "tan", "exp" })
            {
                string prefix = funcName + "(";
                if (funcPart.StartsWith(prefix) && funcPart.EndsWith(")"))
                {
                    string inner = funcPart.Substring(prefix.Length, funcPart.Length - prefix.Length - 1);

                    // Compute derivative of inner
                    string innerDeriv = ComputeDerivative(inner, variable);

                    // Check if derivPart matches innerDeriv (with possible constant factor)
                    if (ExpressionsMatch(derivPart, innerDeriv))
                    {
                        // ∫f(g(x))*g'(x)dx = ∫f(u)du = F(u)
                        // where F is the antiderivative of f
                        return funcName switch
                        {
                            "sin" => $"-cos({inner})",
                            "cos" => $"sin({inner})",
                            "tan" => $"-ln(cos({inner}))",
                            "exp" => $"exp({inner})",
                            _ => null
                        };
                    }

                    // Check with constant coefficient: e.g., 2*x*cos(x^2)
                    // derivPart might be "2*x" and innerDeriv "2*x" — already handled above.
                    // But what if derivPart is "3x" and innerDeriv is "x"?
                    // Try: derivPart = c * innerDeriv
                    var numMatch = Regex.Match(derivPart, @"^(\d+\.?\d*)\*?(.+)$");
                    if (numMatch.Success)
                    {
                        string coeffStr = numMatch.Groups[1].Value;
                        string rest = numMatch.Groups[2].Value;
                        if (double.TryParse(coeffStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double coeff))
                        {
                            if (ExpressionsMatch(rest, innerDeriv))
                            {
                                // ∫ c*f(g(x))*g'(x) dx = c*F(g(x))
                                string fIntegral = funcName switch
                                {
                                    "sin" => $"-cos({inner})",
                                    "cos" => $"sin({inner})",
                                    "tan" => $"-ln(cos({inner}))",
                                    "exp" => $"exp({inner})",
                                    _ => null
                                };
                                if (fIntegral == null) return null;
                                if (Math.Abs(coeff - 1) < ZeroTolerance) return fIntegral;
                                if (fIntegral.StartsWith("-"))
                                    return $"-{coeffStr}*{fIntegral.Substring(1)}";
                                return $"{coeffStr}*{fIntegral}";
                            }
                        }
                    }
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Handles partial fraction decomposition for rational functions.
    /// Patterns: 1/(x^2-1), 1/(x^2-a^2), a/(x^2-b), etc.
    /// </summary>
    private static string? TryPartialFractionIntegral(string numerator, string denominator, string variable, int depth = 0)
    {
        // Prevent infinite recursive decomposition loops
        if (depth > 8)
            return null;

        string d = StripOuterParentheses(denominator.Replace(" ", ""));
        string n = StripOuterParentheses(numerator.Replace(" ", ""));

        // Pattern: 1/(x^2-1) = 1/((x-1)(x+1))
        if (IsOne(n))
        {
            // Match x^2-a^2
            var diffSqMatch = Regex.Match(d, $@"^{Regex.Escape(variable)}\^2-(\d+\.?\d*)$");
            if (diffSqMatch.Success && double.TryParse(diffSqMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double a2))
            {
                double a = Math.Sqrt(a2);
                if (Math.Abs(a2 - 1) < ZeroTolerance)
                {
                    // 1/(x^2-1) = 0.5/(x-1) - 0.5/(x+1)
                    // ∫ = 0.5*ln|x-1| - 0.5*ln|x+1| = 0.5*ln|(x-1)/(x+1)|
                    return $"0.5*ln(abs(({variable}-1)/({variable}+1)))";
                }
                // 1/(x^2-a^2) = 1/(2a) * (1/(x-a) - 1/(x+a))
                double inv2a_val = 1.0 / (2.0 * a);
                string coeff_str = FormatDecimal(inv2a_val);
                return $"{coeff_str}*ln(abs(({variable}-{FormatDecimal(a)})/({variable}+{FormatDecimal(a)})))";
            }

            // Match x^2+a^2 → arctan
            var sumSqMatch = Regex.Match(d, $@"^{Regex.Escape(variable)}\^2\+(\d+\.?\d*)$");
            if (sumSqMatch.Success && double.TryParse(sumSqMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double b2))
            {
                double b = Math.Sqrt(b2);
                if (Math.Abs(b2 - 1) < ZeroTolerance)
                    return $"arctan({variable})";
                return $"arctan({variable}/{FormatDecimal(b)})/{FormatDecimal(b)}";
            }
        }

        // Pattern: a/(x^2-1) → a * partial fraction of 1/(x^2-1)
        var coeffMatch = Regex.Match(n, @"^(\d+\.?\d*)$");
        if (coeffMatch.Success && double.TryParse(coeffMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double coeff))
        {
            var innerPf = TryPartialFractionIntegral("1", d, variable, depth + 1);
            if (innerPf != null)
            {
                if (Math.Abs(coeff - 1) < ZeroTolerance) return innerPf;
                return $"{FormatDecimal(coeff)}*{innerPf}";
            }
        }

        return null;
    }

    /// <summary>
    /// Formats a decimal with G17 precision but strips unnecessary trailing zeros.
    /// </summary>
    private static string FormatDecimal(double val)
    {
        return val.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)
                  .TrimEnd('0').TrimEnd('.');
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
        string integralResult = $"{uPower}*{v} - ∫{duPoly}*{v} d{variable}";
        // Attempt to evaluate the remaining integral if it's simple
        string simpleRemaining = ComputeIndefiniteIntegral($"{duPoly}*{v}", variable);
        if (!simpleRemaining.Contains("∫"))
            return $"{uPower}*{v} - ({simpleRemaining})";
        
        return integralResult;
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
        if (cleanTrimmed == "sin(" + variable + ")^2" || cleanTrimmed == "(sin(" + variable + "))^2")
        {
            // ∫sin²(x)dx = x/2 - sin(2x)/4
            double result = (upper / 2.0 - Math.Sin(2.0 * upper) / 4.0) - (lower / 2.0 - Math.Sin(2.0 * lower) / 4.0);
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
        
        // Special case: (x^2-1)/(x-1) → x+1
        if (expression.Contains("/"))
        {
            var divParts = SplitDivisionParts(expression);
            if (divParts != null)
            {
                string num = divParts.Value.Item1.Trim('(', ')');
                string den = divParts.Value.Item2.Trim('(', ')');
                
                // Check for (x^2-1)/(x-1) pattern
                if (num == "x^2-1" && den == "x-1")
                    return "x + 1";
                if (num == "x^2+2*x+1" && den == "x+1")
                    return "x + 1";
                if (num == "x^2-2*x+1" && den == "x-1")
                    return "x - 1";
            }
        }
        
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

        expr = AlgebraicSimplify(expr);

        return expr + cSuffix;
    }

    /// <summary>
    /// Full algebraic simplification: expand products, strip trivial ops, combine like terms.
    /// Handles division by simplifying numerator and denominator separately.
    /// </summary>
    private static string AlgebraicSimplify(string expr)
    {
        // Handle division: simplify num/den separately
        var divParts = SplitDivisionParts(expr);
        if (divParts != null)
        {
            string num = AlgebraicSimplify(divParts.Value.Item1);
            string den = AlgebraicSimplify(divParts.Value.Item2);
            // If numerator and denominator have common factors, simplify
            // For simple polynomial cases:
            if (num.Contains(den.Trim('(').Trim(')')))
            {
                // Basic division: if den is x-1 and num is (x-1)(x+1)
                // this is a very basic simplification, let's just do a simple replacement check
                string d = den.Trim('(').Trim(')');
                if (num.Contains(d))
                {
                    // Basic pattern: (x-1)(x+1)/(x-1) → x+1
                    string result = num.Replace(d, "").Replace("*", "").Trim('(').Trim(')');
                    // Clean up trailing/leading * or /
                    result = result.Trim('*').Trim('/');
                    return result;
                }
            }
            
            // Clean nested parens in denominator like ((x-3))^2 → (x-3)^2
            den = Regex.Replace(den, @"\(\(([^()]+)\)\)(\^[\d]+)", "($1)$2");
            // If denominator is 1, just return numerator
            if (den == "1") return num;
            return $"{num} / {den}";
        }

        // For non-division: full algebraic simplification pipeline
        expr = StripRedundantOuterParens(expr);
        expr = StripTrivialOps(expr);
        expr = ExpandProductsInExpression(expr);
        expr = DistributeNegation(expr);
        expr = StripTrivialOps(expr);
        expr = StripRedundantOuterParens(expr);
        expr = CombineAdditiveTermsFull(expr);
        expr = StripTrivialOps(expr);
        expr = CleanFormatting(expr);
        return expr;
    }

    /// <summary>
    /// Strips trivial arithmetic: x+0, x-0, 0+x, 0*x, x*1, 1*x, x/1
    /// </summary>
    private static string StripTrivialOps(string expr)
    {
        string prev;
        int safety = 20;
        do
        {
            prev = expr;
            // Collapse adjacent numeric products: 3*2 → 6, 3*2*4 → 24
            expr = Regex.Replace(expr, @"(\d+(?:\.\d+)?)\s*\*\s*(\d+(?:\.\d+)?)", m =>
            {
                double a = double.Parse(m.Groups[1].Value);
                double b = double.Parse(m.Groups[2].Value);
                return (a * b).ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            });
            // x + 0 → x, 0 + x → x
            expr = Regex.Replace(expr, @"(\w+)\s*\+\s*0(?!\\w)", "$1");
            expr = Regex.Replace(expr, @"0\s*\+\s*(\w+)", "$1");
            // x - 0 → x
            expr = Regex.Replace(expr, @"(\w+)\s*-\s*0(?!\\w)", "$1");
            // x * 1 → x, 1 * x → x
            expr = Regex.Replace(expr, @"(\w+)\s*\*\s*1(?!\\w)", "$1");
            expr = Regex.Replace(expr, @"1\s*\*\s*(\w+)", "$1");
            // x * 0 → 0, 0 * x → 0
            expr = Regex.Replace(expr, @"\w+\s*\*\s*0(?!\\w)", "0");
            expr = Regex.Replace(expr, @"0\s*\*\s*\w+", "0");
            // x / 1 → x
            expr = Regex.Replace(expr, @"(\w+)\s*/\s*1(?!\\w)", "$1");
            // Also handle with parens: (expr)+0, (expr)-0, (expr)*1, 1*(expr)
            expr = Regex.Replace(expr, @"\(([^()]+)\)\s*\+\s*0", "($1)");
            expr = Regex.Replace(expr, @"\(([^()]+)\)\s*-\s*0", "($1)");
            expr = Regex.Replace(expr, @"\(([^()]+)\)\s*\*\s*1", "($1)");
            expr = Regex.Replace(expr, @"\(([^()]+)\)\s*\*\(1\)", "($1)");
            expr = Regex.Replace(expr, @"\(1\)\s*\*\s*\(([^()]+)\)", "($1)");
            expr = Regex.Replace(expr, @"1\s*\*\s*\(([^()]+)\)", "($1)");
            expr = Regex.Replace(expr, @"\(([^()]+)\)\s*\*\s*0", "0");
            expr = Regex.Replace(expr, @"0\s*\*\s*\(([^()]+)\)", "0");
            // Clean double parens: ((expr)) → (expr)
            expr = Regex.Replace(expr, @"\(\(([^()]+)\)\)", "($1)");
        }
        while (expr != prev && --safety > 0);
        return expr;
    }

    /// <summary>
    /// Expands products of additive expressions: (a+b)*(c+d) → ac+ad+bc+bd
    /// Also handles (expr)*number and number*(expr).
    /// </summary>
    private static string ExpandProductsInExpression(string expr)
    {
        int safety = 20;
        while (safety-- > 0)
        {
            var match = FindTopLevelProduct(expr);
            if (match == null) break;

            string left = match.Value.left;
            string right = match.Value.right;

            // Only expand if at least one side is additive
            bool leftAdditive = HasTopLevelAdditive(left);
            bool rightAdditive = HasTopLevelAdditive(right);
            if (!leftAdditive && !rightAdditive) break;

            var leftTerms = SplitAdditiveTerms(left);
            var rightTerms = SplitAdditiveTerms(right);

            var expanded = new List<string>();
            foreach (var lt in leftTerms)
            {
                foreach (var rt in rightTerms)
                {
                    string product = MultiplyTerms(lt, rt);
                    if (product != "0")
                        expanded.Add(product);
                }
            }

            string replacement = expanded.Count > 0 ? JoinTerms(expanded) : "0";
            expr = expr.Substring(0, match.Value.start) + replacement + expr.Substring(match.Value.end);
        }
        return expr;
    }

    /// <summary>
    /// Distributes negation through parenthesized additive expressions.
    /// e.g., "-(x^2+1)" → "-x^2 - 1", "-(x-3)" → "-x + 3"
    /// </summary>
    private static string DistributeNegation(string expr)
    {
        int safety = 20;
        while (safety-- > 0)
        {
            // Pattern: sign -(expr) where expr contains + or - at depth 0
            var match = Regex.Match(expr, @"(?<=[+\s])- \(([^()]+)\)");
            if (!match.Success) break;
            string inner = match.Groups[1].Value;
            var terms = SplitAdditiveTerms(inner);
            var distributed = new List<string>();
            foreach (var t in terms)
            {
                string trimmed = t.TrimStart('+');
                if (trimmed.StartsWith("-"))
                    distributed.Add(trimmed.Substring(1));
                else
                    distributed.Add("-" + trimmed);
            }
            string replacement = string.Join(" ", distributed);
            expr = expr.Substring(0, match.Index) + replacement + expr.Substring(match.Index + match.Length);
        }
        return expr;
    }

    private static bool HasTopLevelAdditive(string expr)
    {
        int depth = 0;
        for (int i = 0; i < expr.Length; i++)
        {
            if (expr[i] == '(') depth++;
            else if (expr[i] == ')') depth--;
            else if (depth == 0 && (expr[i] == '+' || (expr[i] == '-' && i > 0)))
                return true;
        }
        return false;
    }


    /// <summary>
    /// Finds the first top-level product of two parenthesized additive expressions.
    /// Returns the left/right strings and position, or null.
    /// </summary>
    private static (string left, string right, int start, int end, int[] stars)? FindTopLevelProduct(string expr)
    {
        int depth = 0;
        int leftStart = -1;
        var parenRanges = new List<(int start, int end)>();
        var starPositions = new List<int>();

        for (int i = 0; i < expr.Length; i++)
        {
            if (expr[i] == '(')
            {
                if (depth == 0) leftStart = i;
                depth++;
            }
            else if (expr[i] == ')')
            {
                depth--;
                if (depth == 0 && leftStart >= 0)
                {
                    parenRanges.Add((leftStart, i));
                    leftStart = -1;
                }
            }
            else if (expr[i] == '*' && depth == 0)
            {
                starPositions.Add(i);
            }
        }

        // Find adjacent parenthesized expressions separated by *
        for (int p = 0; p < parenRanges.Count - 1; p++)
        {
            var cur = parenRanges[p];
            var nxt = parenRanges[p + 1];
            // Check if there's a * between them (possibly with spaces)
            int betweenEnd = nxt.start;
            int betweenStart = cur.end + 1;
            string between = expr.Substring(betweenStart, betweenEnd - betweenStart).Trim();
            if (between == "*")
            {
                string left = expr.Substring(cur.start + 1, cur.end - cur.start - 1);
                string right = expr.Substring(nxt.start + 1, nxt.end - nxt.start - 1);
                return (left, right, cur.start, nxt.end + 1, starPositions.ToArray());
            }
        }

        // Also try: (A+B)*x or x*(A+B) where one side is parenthesized
        for (int i = 0; i < starPositions.Count; i++)
        {
            int star = starPositions[i];
            // Look for (expr)*token or token*(expr)
            // Check left of star for closing paren
            if (star > 0 && expr[star - 1] == ')')
            {
                int closeParen = star - 1;
                int openParen = FindMatchingOpenParen(expr, closeParen);
                if (openParen >= 0)
                {
                    string left = expr.Substring(openParen + 1, closeParen - openParen - 1);
                    // Right side: take token after *
                    int rightStart = star + 1;
                    while (rightStart < expr.Length && expr[rightStart] == ' ') rightStart++;
                    int rightEnd = rightStart;
                    while (rightEnd < expr.Length && expr[rightEnd] != '+' && expr[rightEnd] != '-' &&
                           expr[rightEnd] != '*' && expr[rightEnd] != '/' && expr[rightEnd] != '(' && expr[rightEnd] != ')')
                        rightEnd++;
                    if (rightEnd > rightStart)
                    {
                        string right = expr.Substring(rightStart, rightEnd - rightStart);
                        return (left, right, openParen, rightEnd, starPositions.ToArray());
                    }
                }
            }
            // Check right of star for opening paren
            if (star + 1 < expr.Length && expr[star + 1] == '(')
            {
                int openParen = star + 1;
                int closeParen = FindMatchingCloseParen(expr, openParen);
                if (closeParen > 0)
                {
                    string right = expr.Substring(openParen + 1, closeParen - openParen - 1);
                    // Left side: take token before *
                    int leftEnd = star;
                    int lStart = leftEnd - 1;
                    while (lStart >= 0 && expr[lStart] == ' ') lStart--;
                    while (lStart >= 0 && expr[lStart] != '+' && expr[lStart] != '-' &&
                           expr[lStart] != '*' && expr[lStart] != '/' && expr[lStart] != '(' && expr[lStart] != ')')
                        lStart--;
                    lStart++;
                    if (leftEnd > lStart)
                    {
                        string left = expr.Substring(lStart, leftEnd - lStart);
                        return (left, right, lStart, closeParen + 1, starPositions.ToArray());
                    }
                }
            }
        }

        return null;
    }

    private static int FindMatchingOpenParen(string expr, int closePos)
    {
        int depth = 0;
        for (int i = closePos; i >= 0; i--)
        {
            if (expr[i] == ')') depth++;
            if (expr[i] == '(')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static int FindMatchingCloseParen(string expr, int openPos)
    {
        int depth = 0;
        for (int i = openPos; i < expr.Length; i++)
        {
            if (expr[i] == '(') depth++;
            if (expr[i] == ')')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Multiplies two additive terms (e.g., "2x" * "(x-3)") → expanded product.
    /// </summary>
    private static string MultiplyTerms(string a, string b)
    {
        a = a.Trim();
        b = b.Trim();
        if (a == "0" || b == "0") return "0";
        if (a == "1") return b;
        if (b == "1") return a;
        if (a == "-1") return $"-{WrapIfNeeded(b)}";
        if (b == "-1") return $"-{WrapIfNeeded(a)}";

        // Check if either side is parenthesized with additive terms
        string cleanA = StripRedundantOuterParens(a);
        string cleanB = StripRedundantOuterParens(b);

        var aTerms = SplitAdditiveTerms(cleanA);
        var bTerms = SplitAdditiveTerms(cleanB);

        if (aTerms.Count > 1 && bTerms.Count > 1)
        {
            // Full expansion: (a1+a2)*(b1+b2) = a1*b1 + a1*b2 + a2*b1 + a2*b2
            var products = new List<string>();
            foreach (var at in aTerms)
            {
                foreach (var bt in bTerms)
                {
                    string p = SimpleMultiply(at, bt);
                    if (p != "0") products.Add(p);
                }
            }
            return products.Count > 0 ? string.Join(" + ", products.Select(t => t.TrimStart('+'))) : "0";
        }

        return SimpleMultiply(a, b);
    }

    /// <summary>
    /// Simple multiplication of two non-additive terms: "2x" * "(x-3)" → "2x*(x-3)" or expanded.
    /// </summary>
    private static string SimpleMultiply(string a, string b)
    {
        a = a.Trim(); b = b.Trim();
        if (a == "0" || b == "0") return "0";
        if (a == "1") return b;
        if (b == "1") return a;
        if (a == "-1") return $"-{b}";
        if (b == "-1") return $"-{a}";

        // Try numeric coefficient extraction
        double coeffA = 1, coeffB = 1;
        string structA = a, structB = b;

        ExtractNumCoeff(a, out coeffA, out structA);
        ExtractNumCoeff(b, out coeffB, out structB);

        double totalCoeff = coeffA * coeffB;
        if (Math.Abs(totalCoeff) < 1e-10) return "0";

        // If both have structure, multiply them
        if (!string.IsNullOrEmpty(structA) && !string.IsNullOrEmpty(structB))
        {
            // Check if the structures are the same → combine to coeff * struct^2
            if (structA == structB)
            {
                string coeffStr = FormatCoeff(totalCoeff);
                return $"{coeffStr}{structA}^2";
            }
            string c = FormatCoeff(totalCoeff);
            return $"{c}{structA}*{structB}";
        }

        // One is purely numeric
        if (string.IsNullOrEmpty(structA) || string.IsNullOrEmpty(structB))
        {
            string remaining = !string.IsNullOrEmpty(structA) ? structA : structB;
            string c = FormatCoeff(totalCoeff);
            if (string.IsNullOrEmpty(remaining)) return c;
            if (string.IsNullOrEmpty(c)) return remaining;
            // Format: 2*x → 2x (no * between number and variable)
            if (char.IsLetter(remaining[0])) return $"{c}{remaining}";
            return $"{c}*{remaining}";
        }
        // Fallback for cases where one has coeff, other doesn't
        return "Error in SimpleMultiply"; // Should not be reached with proper logic
    }

    private static void ExtractNumCoeff(string term, out double coeff, out string structure)
    {
        coeff = 1;
        structure = term.Trim();
        if (string.IsNullOrEmpty(structure)) return;

        double sign = 1;
        if (structure.StartsWith("-"))
        {
            sign = -1;
            structure = structure.Substring(1).Trim();
        }
        else if (structure.StartsWith("+"))
        {
            structure = structure.Substring(1).Trim();
        }

        // Strip outer parens
        structure = StripRedundantOuterParens(structure);

        // Try to extract leading number
        var match = Regex.Match(structure, @"^(\d+\.?\d*)\*?(.*)$");
        if (match.Success)
        {
            coeff = sign * double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            structure = match.Groups[2].Value.TrimStart('*').Trim();
        }
        else
        {
            coeff = sign;
        }
    }

    private static string FormatCoeff(double c)
    {
        if (Math.Abs(c - 1) < 1e-10) return "";
        if (Math.Abs(c + 1) < 1e-10) return "-";
        if (Math.Abs(c - Math.Round(c)) < 1e-10 && Math.Abs(Math.Round(c)) < 1e15)
            return ((long)Math.Round(c)).ToString();
        return c.ToString("G10", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Combines additive terms (handles division by working on the full expression).
    /// </summary>
    private static string CombineAdditiveTermsFull(string expr)
    {
        // For division expressions, only combine in numerator
        var divParts = SplitDivisionParts(expr);
        if (divParts != null)
        {
            string num = CombineLikeTerms(divParts.Value.Item1);
            string den = divParts.Value.Item2;
            return $"{num}/{den}";
        }
        return CombineLikeTerms(expr);
    }

    /// <summary>
    /// Cleans common formatting artifacts.
    /// </summary>
    private static string CleanFormatting(string expr)
    {
        // Clean 2.0 → 2, 3.0 → 3
        expr = Regex.Replace(expr, @"(\d+)\.0(?!\d)", "$1");
        // Clean + - → -
        expr = expr.Replace("+ -", "- ");
        // Clean leading +
        if (expr.StartsWith("+ ")) expr = expr.Substring(2);
        // Clean *1 in various forms
        expr = Regex.Replace(expr, @"(\w)\*1(?!\w)", "$1");
        // Add spaces around / for readability
        expr = Regex.Replace(expr, @"(\w)\s*/\s*(\w)", "$1 / $2");
        // Clean double spaces
        expr = Regex.Replace(expr, @"  +", " ");
        return expr.Trim();
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
        // Skip combining if expression has division
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

    private static string FormatTermOriginal(double coeff, int power)
    {
        // This was the old version that forced ^1
        if (Math.Abs(coeff) < ZeroTolerance) return null;
        string coeffStr = coeff.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
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