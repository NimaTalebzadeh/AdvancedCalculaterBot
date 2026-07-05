using System.Globalization;
using System.Text.RegularExpressions;
using NCalc;

namespace AdvancedCalculaterBot.Services;

public class CalculatorService
{
    private readonly string _expression;

    public CalculatorService(string expression)
    {
        _expression = expression;
    }

    public object Evaluate()
    {
        try
        {
            var processed = ProcessFunctions(_expression);
            var expression = new Expression(processed);
            expression.Parameters["pi"] = Math.PI;
            expression.Parameters["e"] = Math.E;

            var result = expression.Evaluate();
            return result ?? 0;
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
