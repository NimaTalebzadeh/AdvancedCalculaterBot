using System.Numerics;

namespace AdvancedCalculaterBot.Services.Equations;

/// <summary>
/// Solves ax^4 + bx^3 + cx^2 + dx + e = 0 given coefficients [e, d, c, b, a]
/// (low→high) using Ferrari's method: depress, solve the resolvent cubic,
/// factor into two quadratics (with complex coefficients where necessary),
/// back-substitute.
/// </summary>
public static class QuarticSolver
{
    private const double BiquadraticTolerance = 1e-9;
    private const double RealRootTolerance = 1e-6;
    private const double DedupTolerance = 1e-6;

    public static Complex[] Solve(double[] coeffs)
    {
        double e = coeffs[0];
        double d = coeffs[1];
        double c = coeffs[2];
        double b = coeffs[3];
        double a = coeffs[4];

        // Normalize to monic: x^4 + B x^3 + C x^2 + D x + E
        double B = b / a;
        double C = c / a;
        double D = d / a;
        double E = e / a;

        // Depress via x = y - B/4:  y^4 + p y^2 + q y + r = 0
        double p = C - 3.0 * B * B / 8.0;
        double q = D - B * C / 2.0 + B * B * B / 8.0;
        double r = E - B * D / 4.0 + B * B * C / 16.0 - 3.0 * B * B * B * B / 256.0;

        List<Complex> yRoots;

        if (Math.Abs(q) < BiquadraticTolerance)
        {
            // Biquadratic: y^4 + p y^2 + r = 0 -> z^2 + p z + r = 0 for z = y^2.
            var zRoots = QuadraticSolver.Solve(new[] { r, p, 1.0 });
            yRoots = new List<Complex>();
            foreach (var z in zRoots)
            {
                yRoots.Add(Complex.Sqrt(z));
                yRoots.Add(-Complex.Sqrt(z));
            }
        }
        else
        {
            // Ferrari resolvent cubic (in m):  8 m^3 + 8 p m^2 + (2 p^2 - 8 r) m - q^2 = 0
            double rc_a = 8.0;
            double rc_b = 8.0 * p;
            double rc_c = 2.0 * p * p - 8.0 * r;
            double rc_d = -q * q;

            var resolventRoots = CubicSolver.Solve(new[] { rc_d, rc_c, rc_b, rc_a });

            // Pick the resolvent root with the smallest imaginary part.
            Complex m = resolventRoots.OrderBy(rr => Math.Abs(rr.Imaginary)).First();

            yRoots = SolveFerrari(p, q, m);
        }

        // Back-substitute x = y - B/4.
        var xRoots = yRoots.Select(y => y - B / 4.0).ToList();
        return Deduplicate(xRoots);
    }

    // Given depressed y^4 + p y^2 + q y + r = 0 and a chosen resolvent root m,
    // factor into two quadratics and solve them. The factorization is
    //   (y^2 + α y + β)(y^2 − α y + γ)
    // where α² = 2m + p,  β + γ = m,  γ − β = q / α.
    private static List<Complex> SolveFerrari(double p, double q, Complex m)
    {
        Complex alphaSquared = 2.0 * m + p;
        Complex alpha = Complex.Sqrt(alphaSquared);

        Complex beta, gamma;
        if (Complex.Abs(alpha) < 1e-9)
        {
            // α == 0 implies q == 0 in exact arithmetic; symmetric fallback.
            beta = m / 2.0;
            gamma = m / 2.0;
        }
        else
        {
            beta = (m - q / alpha) / 2.0;
            gamma = (m + q / alpha) / 2.0;
        }

        var roots = new List<Complex>(4);
        roots.AddRange(SolveQuadraticComplex(1.0, alpha, beta));
        roots.AddRange(SolveQuadraticComplex(1.0, -alpha, gamma));
        return roots;
    }

    // Quadratic formula with arbitrary complex coefficients (a, b, c standard order).
    private static Complex[] SolveQuadraticComplex(Complex a, Complex b, Complex c)
    {
        Complex disc = Complex.Sqrt(b * b - 4 * a * c);
        return new[]
        {
            (-b + disc) / (2 * a),
            (-b - disc) / (2 * a)
        };
    }

    private static Complex[] Deduplicate(List<Complex> roots)
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
