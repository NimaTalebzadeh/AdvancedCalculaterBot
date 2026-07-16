namespace AdvancedCalculatorBot.Services.Equations.TranscendentalSolving;

/// <summary>
/// Hybrid root finder: bisection (guaranteed convergence) + Newton-Raphson polish.
/// Falls back to bisection when Newton fails.
/// </summary>
public static class BrentSolver
{
    /// <summary>
    /// Find a root of f in [a, b] where f(a)*f(b) &lt; 0.
    /// Uses bisection for guaranteed convergence, then Newton-Raphson to polish.
    /// </summary>
    public static double Solve(Func<double, double> f, double a, double b,
        double tolerance = 1e-12, int maxIter = 200)
    {
        double fa = f(a);
        double fb = f(b);

        if (Math.Abs(fa) < tolerance) return a;
        if (Math.Abs(fb) < tolerance) return b;
        if (fa * fb > 0) return double.NaN;

        // Phase 1: Bisection — guaranteed convergence to tolerance
        double lo = a, hi = b;
        double flo = fa, fhi = fb;

        for (int i = 0; i < maxIter; i++)
        {
            double mid = (lo + hi) / 2.0;
            double fmid = f(mid);

            if (Math.Abs(fmid) < tolerance || (hi - lo) / 2.0 < tolerance)
                return mid;

            if (flo * fmid < 0)
            {
                hi = mid; fhi = fmid;
            }
            else
            {
                lo = mid; flo = fmid;
            }
        }

        double bisectResult = (lo + hi) / 2.0;

        // Phase 2: Newton-Raphson polish from bisection result
        double x = bisectResult;
        for (int i = 0; i < 50; i++)
        {
            double fx = f(x);
            if (Math.Abs(fx) < tolerance) return x;

            double df = (f(x + 1e-7) - f(x - 1e-7)) / 2e-7;
            if (Math.Abs(df) < 1e-15) break;

            double xNew = x - fx / df;
            if (double.IsNaN(xNew) || double.IsInfinity(xNew)) break;
            if (Math.Abs(xNew - x) < tolerance * 0.01) return xNew;
            x = xNew;
        }

        // Return whichever is better
        double fBisect = Math.Abs(f(bisectResult));
        double fNewton = Math.Abs(f(x));
        return fNewton < fBisect ? x : bisectResult;
    }
}
