namespace AdvancedCalculatorBot.Services.Equations.TranscendentalSolving;

/// <summary>
/// Newton-Raphson refinement. Uses symbolic derivative if available, otherwise central differences.
/// </summary>
public static class NewtonRefiner
{
    private const double H = 1e-7; // Step for numerical derivative

    /// <summary>
    /// Refine a root estimate using Newton-Raphson.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="df">Optional symbolic derivative. If null, numerical derivative is used.</param>
    /// <param name="x0">Initial estimate.</param>
    /// <param name="tolerance">Convergence tolerance on |f(x)|.</param>
    /// <param name="maxIter">Maximum iterations.</param>
    /// <returns>Refined root, or NaN if diverged.</returns>
    public static double Refine(Func<double, double> f, Func<double, double>? df,
        double x0, double tolerance = 1e-12, int maxIter = 100)
    {
        double x = x0;

        for (int iter = 0; iter < maxIter; iter++)
        {
            double fx = SafeEval(f, x);
            if (double.IsNaN(fx) || double.IsInfinity(fx))
                return double.NaN;

            if (Math.Abs(fx) < tolerance)
                return x;

            // Compute derivative
            double dfx;
            if (df != null)
            {
                dfx = SafeEval(df, x);
                if (double.IsNaN(dfx) || Math.Abs(dfx) < 1e-15)
                {
                    // Fall back to numerical derivative
                    dfx = NumericalDerivative(f, x);
                }
            }
            else
            {
                dfx = NumericalDerivative(f, x);
            }

            if (Math.Abs(dfx) < 1e-15)
                return x; // Flat derivative, can't improve

            double xNew = x - fx / dfx;

            // Divergence check
            if (double.IsNaN(xNew) || double.IsInfinity(xNew))
                return double.IsNaN(fx) || Math.Abs(fx) < tolerance ? x : double.NaN;

            // Convergence check on step size
            if (Math.Abs(xNew - x) < tolerance * 0.01)
                return xNew;

            x = xNew;
        }

        return x;
    }

    /// <summary>
    /// Central-difference numerical derivative.
    /// </summary>
    private static double NumericalDerivative(Func<double, double> f, double x)
    {
        double fp = SafeEval(f, x + H);
        double fm = SafeEval(f, x - H);
        if (double.IsNaN(fp) || double.IsNaN(fm))
            return double.NaN;
        return (fp - fm) / (2.0 * H);
    }

    private static double SafeEval(Func<double, double> f, double x)
    {
        try { return f(x); }
        catch { return double.NaN; }
    }
}
