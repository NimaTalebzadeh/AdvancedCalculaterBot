namespace AdvancedCalculatorBot.Services.Equations.TranscendentalSolving;

/// <summary>
/// Scans an interval [lo, hi] for sign changes in f(x).
/// Returns sub-intervals [a, b] where f(a)*f(b) &lt; 0 (guaranteed root by IVT).
/// </summary>
public static class IntervalScanner
{
    /// <summary>
    /// Find all sign-change intervals in [lo, hi] with given step size.
    /// </summary>
    public static List<(double Lo, double Hi)> FindRootIntervals(
        Func<double, double> f, double lo, double hi, double step)
    {
        var intervals = new List<(double Lo, double Hi)>();

        // Collect all valid evaluation points
        var points = new List<(double X, double F)>();
        for (double x = lo; x <= hi + step * 0.01; x += step)
        {
            double fx = SafeEval(f, x);
            if (!double.IsNaN(fx) && !double.IsInfinity(fx))
                points.Add((x, fx));
        }

        // Find sign changes between consecutive valid points
        for (int i = 0; i < points.Count - 1; i++)
        {
            double x1 = points[i].X, f1 = points[i].F;
            double x2 = points[i + 1].X, f2 = points[i + 1].F;

            if (f1 * f2 < 0)
            {
                // Sign change — root exists in [x1, x2]
                bool dup = intervals.Any(iv =>
                    Math.Abs(iv.Lo - x1) < step * 0.5 &&
                    Math.Abs(iv.Hi - x2) < step * 0.5);
                if (!dup)
                    intervals.Add((x1, x2));
            }
            else if (Math.Abs(f2) < 1e-10)
            {
                // Near-exact zero
                double halfStep = step * 0.01;
                double cLo = x2 - halfStep;
                double cHi = x2 + halfStep;
                bool dup = intervals.Any(iv =>
                    Math.Abs(iv.Lo - cLo) < step * 0.5 &&
                    Math.Abs(iv.Hi - cHi) < step * 0.5);
                if (!dup)
                    intervals.Add((cLo, cHi));
            }
        }

        return intervals;
    }

    private static double SafeEval(Func<double, double> f, double x)
    {
        try { return f(x); }
        catch { return double.NaN; }
    }
}
