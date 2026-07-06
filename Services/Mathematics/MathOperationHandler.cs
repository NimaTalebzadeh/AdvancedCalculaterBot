using System.Text.RegularExpressions;

namespace AdvancedCalculaterBot.Services.Mathematics;

/// <summary>
/// Handles mathematical operation commands like solve(), d(), int(), lim(), simplify(), expand(), factor().
/// </summary>
public static class MathOperationHandler
{
    private static readonly Dictionary<string, Func<string[], MathResult>> _operationHandlers;

    static MathOperationHandler()
    {
        _operationHandlers = new Dictionary<string, Func<string[], MathResult>>(StringComparer.OrdinalIgnoreCase)
        {
            ["solve"] = args => HandleSolve(args),
            ["d"] = args => HandleDerivative(args),
            ["int"] = args => HandleIntegral(args),
            ["lim"] = args => HandleLimit(args),
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

    private static MathResult HandleSolve(string[] args)
    {
        if (args.Length == 0)
            return MathResult.ErrorResult("solve() requires at least one argument.");

        string expression = args[0];
        string variable = args.Length > 1 ? args[1] : "x";

        return MathOperations.Solve(expression, variable);
    }

    private static MathResult HandleDerivative(string[] args)
    {
        if (args.Length == 0)
            return MathResult.ErrorResult("d() requires at least one argument.");

        string expression = args[0];
        string variable = args.Length > 1 ? args[1] : "x";

        return MathOperations.Derivative(expression, variable);
    }

    private static MathResult HandleIntegral(string[] args)
    {
        if (args.Length == 0)
            return MathResult.ErrorResult("int() requires at least one argument.");

        string expression = args[0];

        if (args.Length == 1)
            return MathOperations.Integral(expression, "x");
        else if (args.Length == 2)
        {
            // Second argument could be variable or upper bound
            string secondArg = args[1];
            if (double.TryParse(secondArg, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                return MathOperations.Integral(expression, "x", 0, double.Parse(secondArg, System.Globalization.CultureInfo.InvariantCulture));
            else
                return MathOperations.Integral(expression, secondArg);
        }
        else if (args.Length == 3)
        {
            if (!double.TryParse(args[1], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lower))
                return MathResult.ErrorResult("Lower limit must be a number.");

            if (!double.TryParse(args[2], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double upper))
                return MathResult.ErrorResult("Upper limit must be a number.");

            return MathOperations.Integral(expression, "x", lower, upper);
        }

        return MathResult.ErrorResult("int() takes 1, 2, or 3 arguments.");
    }

    private static MathResult HandleLimit(string[] args)
    {
        if (args.Length != 2)
            return MathResult.ErrorResult("lim() requires exactly two arguments: expression and point.");

        string expression = args[0];
        string pointStr = args[1];

        if (!double.TryParse(pointStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double point))
            return MathResult.ErrorResult("Point must be a number.");

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
}