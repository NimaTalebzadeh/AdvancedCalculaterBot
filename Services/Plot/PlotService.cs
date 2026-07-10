using System.Globalization;
using System.Text.RegularExpressions;
using NCalc;
using ScottPlot;

namespace AdvancedCalculaterBot.Services.Plot;

/// <summary>
/// Generates function plots as PNG images using ScottPlot.
/// The expression is evaluated with NCalc for each x value in the range.
/// </summary>
public static class PlotService
{
    /// <summary>
    /// Evaluates the given expression for a range of x values and produces a PNG image.
    /// </summary>
    /// <param name="expression">Mathematical expression containing variable x (e.g. "x^2*sin(x)")</param>
    /// <param name="start">Start of x range</param>
    /// <param name="end">End of x range</param>
    /// <param name="step">Step size between x values</param>
    /// <returns>PNG image bytes</returns>
    public static byte[] GeneratePlot(string expression, double start, double end, double step)
    {
        if (step <= 0)
            throw new ArgumentException("Step must be a positive number.");
        if (start >= end)
            throw new ArgumentException("Start must be less than end.");

        int pointCount = (int)Math.Ceiling((end - start) / step) + 1;

        // Safety: cap at 100k points to avoid memory issues
        if (pointCount > 100_000)
            pointCount = 100_000;

        var xs = new double[pointCount];
        var ys = new double[pointCount];

        for (int i = 0; i < pointCount; i++)
        {
            double x = start + i * step;
            if (x > end) x = end;
            xs[i] = x;
            ys[i] = EvaluateExpression(expression, x);
        }

        // Create plot
        var plt = new ScottPlot.Plot();

        // Black background
        plt.FigureBackground.Color = Colors.Black;
        plt.DataBackground.Color = Colors.Black;

        // Add the line with red color
        var scatter = plt.Add.Scatter(xs, ys);
        scatter.LineColor = Colors.Red;
        scatter.LineWidth = 2;
        scatter.MarkerSize = 0; // no markers, just the line

        // Style axes for dark background
        plt.Axes.Color(Colors.White);
        plt.Axes.FrameColor(Colors.White);
        plt.Axes.DefaultGrid.MajorLineColor = Colors.DarkGray;

        plt.Title($"y = {expression}");
        plt.XLabel("x");
        plt.YLabel("y");

        // Generate PNG bytes
        int width = 800;
        int height = 500;

        string tempFile = Path.Combine(Path.GetTempPath(), $"plot_{Guid.NewGuid():N}.png");

        try
        {
            plt.SavePng(tempFile, width, height);
            return File.ReadAllBytes(tempFile);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    /// <summary>
    /// Prepares a user expression for NCalc evaluation:
    /// - Converts ^ to ** for exponentiation
    /// - Inserts implicit multiplication (2x -> 2*x)
    /// - Pre-evaluates trig functions with degree-to-radian conversion
    /// </summary>
    private static string PrepareForNCalc(string expression)
    {
        string result = expression;

        // Replace ^ with ** for exponentiation (NCalc uses ^ as XOR)
        result = result.Replace("^", "**");

        // Insert implicit multiplication: 2x -> 2*x, x(sin(x)) -> x*(sin(x))
        // digit followed by letter: 2x -> 2*x
        result = Regex.Replace(result, @"(\d)([a-zA-Z])", "$1*$2");
        // closing paren followed by letter or digit: )x -> )*x
        result = Regex.Replace(result, @"(\))([a-zA-Z\d(])", "$1*$2");
        // letter(s) followed by opening paren: if not a known function, insert *
        result = Regex.Replace(result, @"([a-zA-Z][a-zA-Z0-9]*)\s*\(", m =>
        {
            string name = m.Groups[1].Value;
            if (IsKnownFunction(name))
                return m.Value;
            return name + "*(";
        });

        return result;
    }

    private static bool IsKnownFunction(string name)
    {
        string[] functions = { "sin", "cos", "tan", "asin", "acos", "atan",
                               "sinh", "cosh", "tanh", "sqrt", "abs", "ln",
                               "log", "log10", "log2", "exp", "floor", "ceil",
                               "round", "sign", "pow", "min", "max" };
        return functions.Contains(name.ToLowerInvariant());
    }

    /// <summary>
    /// Evaluates a mathematical expression for a given x value.
    /// Trig functions use degrees (matching CalculatorService behavior).
    /// Returns NaN if the evaluation fails.
    /// </summary>
    private static double EvaluateExpression(string expression, double x)
    {
        try
        {
            // Convert ^ to ** for NCalc exponentiation
            string ncalcExpr = PrepareForNCalc(expression);

            // Pre-evaluate trig functions with degree-to-radian conversion
            ncalcExpr = PreEvaluateTrig(ncalcExpr, x);

            var expr = new Expression(ncalcExpr);
            expr.Parameters["x"] = x;
            expr.Parameters["pi"] = Math.PI;
            expr.Parameters["e"] = Math.E;

            var result = expr.Evaluate();
            return Convert.ToDouble(result, CultureInfo.InvariantCulture);
        }
        catch
        {
            return double.NaN;
        }
    }

    /// <summary>
    /// Replaces sin/cos/tan calls with their computed values for the given x,
    /// converting from degrees to radians (matching the bot's convention).
    /// </summary>
    private static string PreEvaluateTrig(string expression, double x)
    {
        string result = expression;

        // Process trig functions: sin(...), cos(...), tan(...)
        string[] trigFunctions = { "tan", "sin", "cos" };
        foreach (var func in trigFunctions)
        {
            int safety = 0;
            while (Regex.IsMatch(result, $@"{func}\(", RegexOptions.IgnoreCase) && safety < 20)
            {
                safety++;
                result = Regex.Replace(result, $@"{func}\(([^()]*)\)", m =>
                {
                    try
                    {
                        string inner = m.Groups[1].Value;
                        var innerExpr = new Expression(inner);
                        innerExpr.Parameters["x"] = x;
                        innerExpr.Parameters["pi"] = Math.PI;
                        innerExpr.Parameters["e"] = Math.E;
                        double val = Convert.ToDouble(innerExpr.Evaluate(), CultureInfo.InvariantCulture);
                        double rad = val * Math.PI / 180.0;
                        return func.ToLowerInvariant() switch
                        {
                            "sin" => Math.Sin(rad).ToString("R", CultureInfo.InvariantCulture),
                            "cos" => Math.Cos(rad).ToString("R", CultureInfo.InvariantCulture),
                            "tan" => Math.Tan(rad).ToString("R", CultureInfo.InvariantCulture),
                            _ => m.Value
                        };
                    }
                    catch
                    {
                        return m.Value;
                    }
                }, RegexOptions.IgnoreCase);
            }
        }

        return result;
    }
}
