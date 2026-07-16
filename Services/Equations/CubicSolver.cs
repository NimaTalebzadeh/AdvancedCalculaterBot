using System.Numerics;

namespace AdvancedCalculatorBot.Services.Equations;

/// <summary>
/// Solves ax^3 + bx^2 + cx + d = 0 given coefficients [d, c, b, a] (low→high)
/// using Cardano's method. Adapted from EquationSolvers/CubicEquation/Program.cs.
/// Returns deduplicated roots.
/// </summary>
public static class CubicSolver
{
    private const double DedupTolerance = 1e-6;

    public static Complex[] Solve(double[] coeffs)
    {
        double d = coeffs[0];
        double c = coeffs[1];
        double b = coeffs[2];
        double a = coeffs[3];

        // Normalize to monic: x^3 + A x^2 + B x + C
        double A = b / a;
        double B = c / a;
        double C = d / a;

        // Depress via x = t - A/3:  t^3 + p t + q = 0
        double p = B - A * A / 3.0;
        double q = (2 * A * A * A) / 27.0 - (A * B) / 3.0 + C;

        Complex delta = Complex.Pow(q / 2.0, 2) + Complex.Pow(p / 3.0, 3);

        Complex u = Complex.Pow(-q / 2.0 + Complex.Sqrt(delta), 1.0 / 3.0);
        Complex v = Complex.Pow(-q / 2.0 - Complex.Sqrt(delta), 1.0 / 3.0);

        Complex omega = new Complex(-0.5, Math.Sqrt(3) / 2.0);

        Complex t1 = u + v;
        Complex t2 = u * omega + v * Complex.Conjugate(omega);
        Complex t3 = u * Complex.Conjugate(omega) + v * omega;

        // Back-substitute x = t - A/3
        var roots = new[]
        {
            t1 - A / 3.0,
            t2 - A / 3.0,
            t3 - A / 3.0
        };

        return Deduplicate(roots);
    }

    private static Complex[] Deduplicate(Complex[] roots)
    {
        var unique = new List<Complex>();
        foreach (var r in roots)
        {
            bool dup = unique.Any(ur => Complex.Abs(r - ur) < DedupTolerance);
            if (!dup) unique.Add(r);
        }
        return unique.ToArray();
    }
}
