using System.Globalization;
using System.Text.RegularExpressions;
using AdvancedCalculaterBot.Services.Mathematics;
using NCalc;

namespace AdvancedCalculaterBot.Services;

/// <summary>
/// Calculator service that integrates the new mathematical expression engine
/// while maintaining backward compatibility with existing functionality.
/// </summary>
public class CalculatorService
{
    private readonly string _expression;

    public CalculatorService(string expression)
    {
        _expression = expression;
    }

    /// <summary>
    /// Evaluates a mathematical expression. First checks if it's a special mathematical
    /// operation (solve, derivative, integral, etc.), then falls back to standard evaluation.
    /// </summary>
    public object Evaluate()
    {
        try
        {
            string expression = _expression.Trim();

            // Equations must be handled before operation detection.
            // Otherwise expressions like ln(x)=sin(x) are incorrectly
            // parsed as a function call named "ln".
            if (expression.Contains('=') && Regex.IsMatch(expression, "[a-zA-Z]"))
            {
                string solved = Equations.EquationSolverService.Solve(expression);
                if (!string.IsNullOrWhiteSpace(solved))
                    return solved;
            }

            // Check if this is a mathematical operation command
            if (MathOperationHandler.IsMathematicalOperation(expression))
            {
                // For operations that return complex results or strings, we need to handle them specially
                var operationResult = MathOperationHandler.HandleCommand(expression);
                if (operationResult.Success)
                    return operationResult.FormattedValue ?? operationResult.Value ?? "Result";
                else
                    throw new ArgumentException(operationResult.Message);
            }

            // Fall back to original calculator functionality for standard expressions
            var processed = ProcessFunctions(expression);
            var ncalcExpression = new Expression(processed);
            ncalcExpression.Parameters["pi"] = Math.PI;
            ncalcExpression.Parameters["e"] = Math.E;

            var evalResult = ncalcExpression.Evaluate();
            return evalResult ?? 0;
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid mathematical expression: {ex.Message}", ex);
        }
    }

    public static bool IsValidExpression(string expression)
    {
        try
        {
            new CalculatorService(expression).Evaluate();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ProcessFunctions(string expression)
    {
        string result = expression;

        result = ReplaceFunction(result, "sin", x => Math.Sin(x * Math.PI / 180d));
        result = ReplaceFunction(result, "cos", x => Math.Cos(x * Math.PI / 180d));
        result = ReplaceFunction(result, "tan", x => Math.Tan(x * Math.PI / 180d));
        result = ReplaceFunction(result, "sqrt", Math.Sqrt);
        result = ReplaceFunction(result, "abs", Math.Abs);
        result = ReplaceFunction(result, "ln", Math.Log);
        result = ReplaceFunction(result, "log", Math.Log10);
        result = ReplaceFunction(result, "floor", Math.Floor);
        result = ReplaceFunction(result, "ceil", Math.Ceiling);
        result = ReplaceFunction(result, "round", Math.Round);

        return result;
    }

    private static string ReplaceFunction(string expression, string functionName, Func<double, double> func)
    {
        return Regex.Replace(expression, $@"{functionName}\(([^()]+)\)", match =>
        {
            var innerExpression = new Expression(match.Groups[1].Value);
            innerExpression.Parameters["pi"] = Math.PI;
            innerExpression.Parameters["e"] = Math.E;
            double value = Convert.ToDouble(innerExpression.Evaluate(), CultureInfo.InvariantCulture);
            double result = func(value);
            return result.ToString(CultureInfo.InvariantCulture);
        }, RegexOptions.IgnoreCase);
    }
}