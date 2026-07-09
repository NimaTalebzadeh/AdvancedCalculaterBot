using System.Text.RegularExpressions;

namespace AdvancedCalculaterBot.Services.Mathematics;

/// <summary>
/// Handles mathematical operation commands like d(), int(), lim(), simplify(), expand(), factor().
/// Equations (e.g. x^2=4) are handled directly by EquationSolverService in Program.cs.
/// </summary>
public static class MathOperationHandler
{
    private static readonly Dictionary<string, Func<string[], MathResult>> _operationHandlers;

    static MathOperationHandler()
    {
        _operationHandlers = new Dictionary<string, Func<string[], MathResult>>(StringComparer.OrdinalIgnoreCase)
        {
            ["d"] = args => HandleDerivative(args),
            ["int"] = args => HandleIntegral(args),
            ["lim"] = args => HandleLimit(args),
            ["taylor"] = args => HandleTaylor(args),
            ["simplify"] = args => HandleSimplify(args),
            ["expand"] = args => HandleExpand(args),
            ["factor"] = args => HandleFactor(args)
        };
    }

    /// <summary>
    /// Handles a mathematical operation command.
    /// </summary>
    public static MathResult HandleCommand(string command)
    {
        command = command.Trim();
        if (string.IsNullOrEmpty(command))
            return MathResult.ErrorResult("Empty command");

        // Match function calls like solve(x^2-4=0) or d(x^2)
        var match = Regex.Match(command, @"^([a-zA-Z]+)\s*\((.*)\)$");
        if (!match.Success)
            return MathResult.ErrorResult("Invalid command format. Expected format: function(args)");

        string functionName = match.Groups[1].Value;
        string argsString = match.Groups[2].Value;

        if (!_operationHandlers.TryGetValue(functionName, out var handler))
            return MathResult.ErrorResult($"Unknown function '{functionName}'. Supported functions: {string.Join(", ", _operationHandlers.Keys)}");

        try
        {
            string[] args = SplitArguments(argsString);
            return handler(args);
        }
        catch (Exception ex)
        {
            return MathResult.ErrorResult($"Error processing arguments: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if the string represents a mathematical operation (function call).
    /// </summary>
    public static bool IsMathematicalOperation(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Match function calls like solve(x^2-4=0) or d(x^2)
        var match = Regex.Match(input.Trim(), @"^([a-zA-Z]+)\s*\(.*\)$");
        return match.Success;
    }

    private static string[] SplitArguments(string argsString)
    {
        if (string.IsNullOrWhiteSpace(argsString))
            return Array.Empty<string>();

        var args = new List<string>();
        int parenDepth = 0;
        int startIndex = 0;

        for (int i = 0; i < argsString.Length; i++)
        {
            char c = argsString[i];

            if (c == '(')
                parenDepth++;
            else if (c == ')')
                parenDepth--;
            else if (c == ',' && parenDepth == 0)
            {
                args.Add(argsString.Substring(startIndex, i - startIndex).Trim());
                startIndex = i + 1;
            }
        }

        args.Add(argsString.Substring(startIndex).Trim());
        return args.ToArray();
    }

    private static MathResult HandleDerivative(string[] args)
    {
        if (args.Length == 0)
            return MathResult.ErrorResult("d() requires at least one argument.");

        string expression = args[0].Replace(" ", "");

        // Phase 2 migration: generalized symbolic derivatives are now
        // handled through SymbolicEngine/AngouriMath instead of hardcoded
        // expression-specific derivative answers.

        string variable = "x";
        int order = 1;

        if (args.Length >= 2)
        {
            // Second arg could be variable name or order number
            if (int.TryParse(args[1], out int parsedOrder) && parsedOrder > 0)
            {
                order = parsedOrder;
            }
            else
            {
                variable = args[1];
            }
        }

        if (args.Length >= 3)
        {
            // Third arg is the order
            if (int.TryParse(args[2], out int parsedOrder) && parsedOrder > 0)
                order = parsedOrder;
            else
                return MathResult.ErrorResult("Derivative order must be a positive integer.");
        }

        return MathOperations.Derivative(expression, variable, order);
    }

    private static MathResult HandleIntegral(string[] args)
    {
        if (args.Length == 0)
            return MathResult.ErrorResult("int() requires at least one argument.");

        string expression = args[0].Replace(" ", "");

        // Phase 2 migration: common symbolic integrals are now delegated
        // to SymbolicEngine/AngouriMath instead of expression-specific
        // hardcoded textbook answers.

        // All remaining integral cases now route through SymbolicEngine

        if (args.Length == 1)
            return MathOperations.Integral(expression, "x");
        else if (args.Length == 2)
        {
            // Second argument could be variable or upper bound
            string secondArg = args[1];
            if (TryParseMathConstant(secondArg, out double val))
                return MathOperations.Integral(expression, "x", 0, val);
            else
                return MathOperations.Integral(expression, secondArg);
        }
        else if (args.Length == 3)
        {
            // Could be int(expr, var1, var2) for multiple integral
            // or int(expr, lower, upper) for definite integral
            string secondArg = args[1];
            string thirdArg = args[2];

            bool secondIsNum = TryParseMathConstant(secondArg, out double lower);
            bool thirdIsNum = TryParseMathConstant(thirdArg, out double upper);

            if (secondIsNum && thirdIsNum)
            {
                // int(expr, lower, upper) - definite single integral
                return MathOperations.Integral(expression, "x", lower, upper);
            }
            else if (!secondIsNum && !thirdIsNum)
            {
                // int(expr, var1, var2) - multiple integral
                return MathOperations.MultipleIntegral(expression, new[] { secondArg, thirdArg });
            }
            else
            {
                return MathResult.ErrorResult("Invalid arguments. Use int(expr, var1, var2) for multiple integral or int(expr, lower, upper) for definite integral.");
            }
        }
        else if (args.Length == 4)
        {
            // int(expr, var, lower, upper) - definite integral with specific variable
            string variable = args[1];
            if (TryParseMathConstant(args[2], out double lo) &&
                TryParseMathConstant(args[3], out double hi))
            {
                return MathOperations.Integral(expression, variable, lo, hi);
            }
            return MathResult.ErrorResult("Lower and upper limits must be numbers.");
        }
        else if (args.Length == 5)
        {
            // int(expr, var1, var2, var3, var4) - could be double integral with mixed args
            // Try: int(expr, var1, a, b, var2) -> error or int(expr, var1, var2, var3, var4) -> triple indefinite?
            // Let's support: int(expr, x, 0, 1, y) - not standard, skip for now
            return MathResult.ErrorResult("int() with 5 arguments not supported. Use int(expr, var1, var2) for multiple integral.");
        }
        else if (args.Length == 6)
        {
            // int(expr, var1, a, b, var2, c, d) -> 7 args, but we check 6 here
            // Actually int(expr, x, 0, 1, y, 0, 1) would be 7 args
            // int(expr, x, y, z) would be 4 args for triple integral
            return MathResult.ErrorResult("int() with 6 arguments not supported.");
        }
        else if (args.Length == 7)
        {
            // int(expr, var1, lower1, upper1, var2, lower2, upper2) - double definite integral
            string var1 = args[1];
            string var2 = args[4];

            if (double.TryParse(args[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lo1) &&
                double.TryParse(args[3], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hi1) &&
                double.TryParse(args[5], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lo2) &&
                double.TryParse(args[6], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double hi2))
            {
                return MathOperations.DoubleDefiniteIntegral(expression, var1, lo1, hi1, var2, lo2, hi2);
            }
            return MathResult.ErrorResult("All limits must be numbers.");
        }
        else if (args.Length == 8)
        {
            // int(expr, var1, var2, var3) for triple indefinite integral
            // Actually with 8 args: int(expr, x, 0, 1, y, 0, 1, z) - unlikely
            // Let's just support triple indefinite: int(expr, x, y, z)
            // which is 4 args, not 8
            return MathResult.ErrorResult("int() with 8 arguments not supported.");
        }

        return MathResult.ErrorResult("int() takes 1 to 7 arguments.");
    }

    private static MathResult HandleLimit(string[] args)
    {
        if (args.Length != 2)
            return MathResult.ErrorResult("lim() requires exactly two arguments: expression and point.");

        string expression = args[0];

        string normalized = expression.Replace(" ", "");

        string pointStr = args[1].Trim().ToLowerInvariant();

        // Check for infinity
        double point;
        if (pointStr is "inf" or "infinity" or "+inf" or "+infinity" or "∞" or "+∞")
            point = double.PositiveInfinity;
        else if (pointStr is "-inf" or "-infinity" or "-∞")
            point = double.NegativeInfinity;
        else if (!double.TryParse(args[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out point))
            return MathResult.ErrorResult("Point must be a number, or inf/∞ for infinity.");

        string variable = "x";
        if (expression.Contains('x'))
            variable = "x";
        else if (expression.Contains('y'))
            variable = "y";
        else if (expression.Contains('z'))
            variable = "z";

        return MathOperations.Limit(expression, variable, point);
    }

    private static MathResult HandleSimplify(string[] args)
    {
        if (args.Length != 1)
            return MathResult.ErrorResult("simplify() requires exactly one argument.");

        string expression = args[0];
        return MathOperations.Simplify(expression);
    }

    private static MathResult HandleTaylor(string[] args)
    {
        if (args.Length != 4)
            return MathResult.ErrorResult("taylor() requires 4 arguments: expression, variable, point, order.");

        string expression = args[0];
        string variable = args[1];

        // Phase 2 migration: Taylor expansions now route through
        // SymbolicEngine instead of expression-specific expansions.

        if (!double.TryParse(args[2], out double point))
            return MathResult.ErrorResult("Taylor expansion point must be numeric.");

        if (!int.TryParse(args[3], out int order))
            return MathResult.ErrorResult("Taylor order must be an integer.");

        return MathOperations.TaylorSeries(expression, variable, point, order);
    }

    private static MathResult HandleExpand(string[] args)
    {
        if (args.Length != 1)
            return MathResult.ErrorResult("expand() requires exactly one argument.");

        string expression = args[0];
        return MathOperations.Expand(expression);
    }

    private static MathResult HandleFactor(string[] args)
    {
        if (args.Length != 1)
            return MathResult.ErrorResult("factor() requires exactly one argument.");
        string expression = args[0];
        return MathOperations.Factor(expression);
    }

    /// <summary>
    /// Parses a string as a double, recognizing math constants like pi, e, π.
    /// </summary>
    private static bool TryParseMathConstant(string value, out double result)
    {
        string trimmed = value.Trim().ToLowerInvariant();
        if (trimmed == "pi" || trimmed == "π")
        {
            result = Math.PI;
            return true;
        }
        if (trimmed == "e")
        {
            result = Math.E;
            return true;
        }
        if (double.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out result))
            return true;

        // Try evaluating simple expressions like "2*pi", "pi/2", "3*pi"
        try
        {
            string expr = trimmed
                .Replace("pi", Math.PI.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("π", Math.PI.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            // Only allow digits, dots, operators, and whitespace
            if (expr.All(c => char.IsDigit(c) || c == '.' || c == '+' || c == '-' || c == '*' || c == '/' || c == ' ' || c == 'e' || c == 'E'))
            {
                var ncalc = new NCalc.Expression(expr);
                result = Convert.ToDouble(ncalc.Evaluate(), System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch { }

        result = 0;
        return false;
    }
}