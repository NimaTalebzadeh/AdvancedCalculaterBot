using System;
using System.Numerics;

namespace AdvancedCalculatorBot.Services.Equations;

/// <summary>
/// General polynomial root solver using the Durand-Kerner method.
/// Supports any degree polynomial.
/// </summary>
public static class PolynomialSolver
{
    /// <summary>
    /// Finds all roots of a polynomial with given coefficients.
    /// Coefficients are from highest degree to constant: [a_n, a_{n-1}, ..., a_0]
    /// </summary>
    public static Complex[] Solve(double[] coeffs)
    {
        if (coeffs.Length <= 1) return Array.Empty<Complex>();

        // Remove leading zeros
        int start = 0;
        while (start < coeffs.Length - 1 && Math.Abs(coeffs[start]) < 1e-15)
            start++;
        double[] poly = new double[coeffs.Length - start];
        Array.Copy(coeffs, start, poly, 0, poly.Length);

        int n = poly.Length - 1; // degree
        if (n <= 0) return Array.Empty<Complex>();

        // Handle degree 1: ax + b = 0 => x = -b/a
        if (n == 1) return new[] { new Complex(-poly[1] / poly[0], 0) };

        // Handle degree 2: ax^2 + bx + c = 0
        if (n == 2) return SolveQuadratic(poly[0], poly[1], poly[2]);

        // Normalize polynomial (leading coeff = 1)
        double leading = poly[0];
        double[] normPoly = new double[n + 1];
        for (int i = 0; i <= n; i++)
            normPoly[i] = poly[i] / leading;

        // Durand-Kerner (Weierstrass) method
        return DurandKerner(normPoly, n);
    }

    private static Complex[] DurandKerner(double[] p, int n)
    {
        // Initial guesses on a circle in complex plane
        Complex[] roots = new Complex[n];
        for (int i = 0; i < n; i++)
        {
            double angle = 2.0 * Math.PI * i / n;
            roots[i] = new Complex(0.5 + 0.5 * Math.Cos(angle), 0.5 * Math.Sin(angle));
        }

        // Iterate
        const int maxIter = 1000;
        const double tolerance = 1e-12;

        for (int iter = 0; iter < maxIter; iter++)
        {
            double maxDelta = 0;
            for (int i = 0; i < n; i++)
            {
                Complex fval = EvaluateComplex(p, roots[i]);
                Complex prod = 1;
                for (int j = 0; j < n; j++)
                {
                    if (j != i)
                        prod *= (roots[i] - roots[j]);
                }
                Complex delta = fval / prod;
                roots[i] -= delta;
                if (delta.Magnitude > maxDelta)
                    maxDelta = delta.Magnitude;
            }
            if (maxDelta < tolerance) break;
        }

        // Clean up near-zero imaginary parts and near-integer real parts
        for (int i = 0; i < n; i++)
        {
            if (Math.Abs(roots[i].Imaginary) < 1e-8)
                roots[i] = new Complex(roots[i].Real, 0);
            if (Math.Abs(roots[i].Real - Math.Round(roots[i].Real)) < 1e-8 && Math.Abs(roots[i].Imaginary) < 1e-8)
                roots[i] = new Complex(Math.Round(roots[i].Real), 0);
        }

        return roots;
    }

    private static Complex EvaluateComplex(double[] p, Complex x)
    {
        Complex result = 0;
        for (int i = 0; i < p.Length; i++)
            result = result * x + p[i];
        return result;
    }

    private static Complex[] SolveQuadratic(double a, double b, double c)
    {
        double disc = b * b - 4 * a * c;
        if (disc >= 0)
        {
            double sqrtDisc = Math.Sqrt(disc);
            return new[] { new Complex((-b + sqrtDisc) / (2 * a), 0), new Complex((-b - sqrtDisc) / (2 * a), 0) };
        }
        else
        {
            double sqrtDisc = Math.Sqrt(-disc);
            return new[] { new Complex(-b / (2 * a), sqrtDisc / (2 * a)), new Complex(-b / (2 * a), -sqrtDisc / (2 * a)) };
        }
    }
}
