using System.Globalization;
using System.Text.RegularExpressions;
using NCalc;
using ScottPlot;

namespace AdvancedCalculatorBot.Services.Plot;

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

        var xs = new List<double>();
        var ys = new List<double>();

        // First pass: evaluate all points and collect valid ones
        for (int i = 0; i < pointCount; i++)
        {
            double x = start + i * step;
            if (x > end) x = end;

            double y = EvaluateExpression(expression, x);

            // Skip NaN and Inf — don't plot them
            if (double.IsNaN(y) || double.IsInfinity(y))
                continue;

            xs.Add(x);
            ys.Add(y);
        }

        if (xs.Count == 0)
            throw new ArgumentException("No valid points to plot. Check your expression or range.");

        // Create plot
        using var plt = new ScottPlot.Plot();

        // Black background
        plt.FigureBackground.Color = Colors.Black;
        plt.DataBackground.Color = Colors.Black;

        // Add the line with red color
        var scatter = plt.Add.Scatter(xs.ToArray(), ys.ToArray());
        scatter.LineColor = Colors.Red;
        scatter.LineWidth = 2;
        scatter.MarkerSize = 0; // no markers, just the line

        // Style axes — thin, semi-transparent white lines and labels
        var faint     = Color.FromARGB(0x66FFFFFF);  // ~40% opacity white
        var gridColor = Color.FromARGB(0x33FFFFFF);  // ~20% opacity white
        var zeroColor = Color.FromARGB(0x55FF6600);  // ~33% orange for zero lines

        // ── Auto-scale axes with margins ──
        double yMin = ys.Min();
        double yMax = ys.Max();
        double yRange = yMax - yMin;

        // Add 15% margin above and below, minimum 0.5 total range
        if (yRange < 0.5) yRange = 0.5;
        double yPad = yRange * 0.15;
        double yLow = yMin - yPad;
        double yHigh = yMax + yPad;

        // Round to nice numbers
        double[] yLimits = RoundLimits(yLow, yHigh);
        double[] xLimits = RoundLimits(start, end);

        plt.Axes.SetLimits(xLimits[0], xLimits[1], yLimits[0], yLimits[1]);

        // ── Zero-crossing lines ──
        // Horizontal zero line
        if (yLimits[0] < 0 && yLimits[1] > 0)
        {
            var hLine = plt.Add.HorizontalLine(0);
            hLine.LineColor = zeroColor;
            hLine.LineWidth = 0.8f;
            hLine.LinePattern = LinePattern.Dashed;
        }
        // Vertical zero line
        if (xLimits[0] < 0 && xLimits[1] > 0)
        {
            var vLine = plt.Add.VerticalLine(0);
            vLine.LineColor = zeroColor;
            vLine.LineWidth = 0.8f;
            vLine.LinePattern = LinePattern.Dashed;
        }

        plt.Axes.Color(faint);
        plt.Axes.FrameColor(faint);
        plt.Axes.FrameWidth(0.5f);

        // Tick labels visible in semi-transparent white
        foreach (var axis in new IAxis[] { plt.Axes.Bottom, plt.Axes.Top, plt.Axes.Left, plt.Axes.Right })
        {
            axis.TickLabelStyle.ForeColor = faint;
            axis.Label.ForeColor = faint;
        }

        // Adjust tick spacing for readability
        plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();
        plt.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();

        plt.Axes.DefaultGrid.MajorLineColor = gridColor;
        plt.Axes.DefaultGrid.MajorLineWidth = 0.4f;
        plt.Axes.DefaultGrid.MinorLineColor = Color.FromARGB(0x1AFFFFFF);
        plt.Axes.DefaultGrid.MinorLineWidth = 0.2f;

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
    /// Rounds limits outward to nice tick-friendly numbers.
    /// </summary>
    private static double[] RoundLimits(double low, double high)
    {
        double range = high - low;
        if (range < 0.001) range = 0.001;

        // Determine order of magnitude
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(range)));
        double niceStep = magnitude;

        // Choose a nice step: 1, 2, or 5 times magnitude
        double normalized = range / magnitude;
        if (normalized < 1.5) niceStep = magnitude * 0.2;
        else if (normalized < 3) niceStep = magnitude * 0.5;
        else if (normalized < 7) niceStep = magnitude * 1;
        else niceStep = magnitude * 2;

        double newLow = Math.Floor(low / niceStep) * niceStep;
        double newHigh = Math.Ceiling(high / niceStep) * niceStep;

        // Ensure at least some range
        if (Math.Abs(newHigh - newLow) < niceStep * 2)
        {
            double mid = (newLow + newHigh) / 2;
            newLow = mid - niceStep * 2;
            newHigh = mid + niceStep * 2;
        }

        return new[] { newLow, newHigh };
    }

    /// <summary>
    /// Prepares a user expression for NCalc evaluation:
    /// - Converts ^ to ** for exponentiation
    /// - Inserts implicit multiplication (2x -> 2*x)
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
            string ncalcExpr = PrepareForNCalc(expression);

            var expr = new Expression(ncalcExpr);
            expr.Parameters["x"] = x;
            expr.Parameters["pi"] = Math.PI;
            expr.Parameters["e"] = Math.E;

            // NCalc 6.4 is case-sensitive: "sin" fails, only "Sin" works.
            // Register a handler so lowercase function names work too.
            expr.EvaluateFunction += (name, args) =>
            {
                double a0() => Convert.ToDouble(args.Parameters.Evaluate(0), CultureInfo.InvariantCulture);
                double a1() => Convert.ToDouble(args.Parameters.Evaluate(1), CultureInfo.InvariantCulture);
                switch (name.ToLowerInvariant())
                {
                    // Trig in degrees (matches CalculatorService convention)
                    case "sin":  args.Result = Math.Sin(a0() * Math.PI / 180.0); break;
                    case "cos":  args.Result = Math.Cos(a0() * Math.PI / 180.0); break;
                    case "tan":  args.Result = Math.Tan(a0() * Math.PI / 180.0); break;
                    // Inverse trig — return in degrees to stay consistent
                    case "asin": args.Result = Math.Asin(a0()) * 180.0 / Math.PI; break;
                    case "acos": args.Result = Math.Acos(a0()) * 180.0 / Math.PI; break;
                    case "atan": args.Result = Math.Atan(a0()) * 180.0 / Math.PI; break;
                    // Hyperbolic
                    case "sinh": args.Result = Math.Sinh(a0()); break;
                    case "cosh": args.Result = Math.Cosh(a0()); break;
                    case "tanh": args.Result = Math.Tanh(a0()); break;
                    // Algebraic
                    case "abs":    args.Result = Math.Abs(a0()); break;
                    case "sqrt":   args.Result = Math.Sqrt(a0()); break;
                    case "ln":     args.Result = Math.Log(a0()); break;
                    case "log":    args.Result = Math.Log10(a0()); break;
                    case "log10":  args.Result = Math.Log10(a0()); break;
                    case "log2":   args.Result = Math.Log2(a0()); break;
                    case "exp":    args.Result = Math.Exp(a0()); break;
                    case "floor":  args.Result = Math.Floor(a0()); break;
                    case "ceil":   args.Result = Math.Ceiling(a0()); break;
                    case "round":  args.Result = Math.Round(a0()); break;
                    case "sign":   args.Result = Math.Sign(a0()); break;
                    case "pow":    args.Result = Math.Pow(a0(), a1()); break;
                    case "min":    args.Result = Math.Min(a0(), a1()); break;
                    case "max":    args.Result = Math.Max(a0(), a1()); break;
                }
            };

            var result = expr.Evaluate();
            return Convert.ToDouble(result, CultureInfo.InvariantCulture);
        }
        catch { return double.NaN; }
    }
}
