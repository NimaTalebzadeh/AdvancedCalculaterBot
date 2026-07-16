using System.Numerics;

namespace AdvancedCalculatorBot.Services.Equations;

/// <summary>
/// Solves quintic (5th-degree) polynomial equations using the Durand-Kerner method.
/// Finds all 5 roots (real and complex) of the polynomial.
/// </summary>
public static class QuinticSolver
{
    private const double Tolerance = 1e-9;
    private const int MaxIterations = 100;

/// <summary>
/// Solves a quintic polynomial using the Durand-Kerner method.
/// Coefficients are assumed to be in ascending order: [a0, a1, a2, a3, a4, a5]
/// representing P(x) = a0 + a1*x + a2*x^2 + a3*x^3 + a4*x^4 + a5*x^5 = 0.
/// </summary>
public static Complex[] Solve(double[] coefficients)
{
    // Debug: print input coefficients
    System.Console.WriteLine($"QuinticSolver: Input coefficients: [{string.Join(", ", coefficients)}]");
    
    if (coefficients.Length < 6)
    {
        // Pad with zeros if needed
        Array.Resize(ref coefficients, 6);
    }
    
    // Debug: print resized coefficients
    System.Console.WriteLine($"QuinticSolver: Padded coefficients: [{string.Join(", ", coefficients)}]");
    
    // Extract coefficients: a5*x^5 + a4*x^4 + a3*x^3 + a2*x^2 + a1*x + a0 = 0
    double a0 = coefficients[0];
    double a1 = coefficients.Length > 1 ? coefficients[1] : 0;
    double a2 = coefficients.Length > 2 ? coefficients[2] : 0;
    double a3 = coefficients.Length > 3 ? coefficients[3] : 0;
    double a4 = coefficients.Length > 4 ? coefficients[4] : 0;
    double a5 = coefficients.Length > 5 ? coefficients[5] : 0;

    System.Console.WriteLine($"QuinticSolver: a0={a0}, a1={a1}, a2={a2}, a3={a3}, a4={a4}, a5={a5}");

    // Normalize to
    if (Math.Abs(a5) < 1e-15)
    {
        System.Console.WriteLine("QuinticSolver: a5 is zero or near-zero, returning empty");
        return Array.Empty<Complex>();
    }

    // Normalize to monic polynomial: divide by a5
    a0 /= a5;
    a1 /= a5;
    a2 /= a5;
    a3 /= a5;
    a4 /= a5;
    
    System.Console.WriteLine($"QuinticSolver: Normalized a0={a0}, a1={a1}, a2={a2}, a3={a3}, a4={a4}");

    // Initial guesses
    Complex[] roots = new Complex[5];
    roots[0] = new Complex(1.0, 0.0);    // 1 + 0i
    roots[1] = new Complex(0.0, 1.0);    // i
    roots[2] = new Complex(-1.0, 0.0);   // -1
    roots[3] = new Complex(0.0, -1.0);   // -i
    roots[4] = new Complex(1.0, 1.0);    // 1 + i

    // Durand-Kerner iteration
    Complex[] newRoots = new Complex[5];
    for (int iter = 0; iter < MaxIterations; iter++)
    {
        // Compute P(zi) for each root
        for (int i = 0; i < 5; i++)
        {
            Complex zi = roots[i];
            Complex p = EvaluatePolynomial(zi, a0, a1, a2, a3, a4, 1.0);

            // Compute product of (zi - zj) for j != i
            Complex product = Complex.One;
            for (int j = 0; j < 5; j++)
            {
                if (j != i)
                {
                    product *= (zi - roots[j]);
                }
            }

            if (Complex.Abs(product) > Tolerance)
            {
                newRoots[i] = zi - p / product;
            }
            else
            {
                newRoots[i] = zi;
            }
        }

        // Check convergence
        bool converged = true;
        for (int i = 0; i < 5; i++)
        {
            if (Complex.Abs(newRoots[i] - roots[i]) > Tolerance)
            {
                converged = false;
                break;
            }
        }

        // Copy new roots
        for (int i = 0; i < 5; i++)
        {
            roots[i] = newRoots[i];
        }

        if (converged)
        {
            System.Console.WriteLine($"QuinticSolver: Converged after {iter} iterations");
            break;
        }
    }

    System.Console.WriteLine($"QuinticSolver: Final roots: [{string.Join(", ", roots)}]");
    return roots;
}

    /// <summary>
    /// Evaluates P(x) = a0 + a1*x + a2*x^2 + a3*x^3 + a4*x^4 + a5*x^5.
    /// Note: a5 is always 1.0 since we normalize to monic polynomial.
    /// </summary>
    private static Complex EvaluatePolynomial(Complex x, double a0, double a1, double a2, double a3, double a4, double a5)
    {
        Complex result = a0;
        result += a1 * x;
        result += a2 * x * x;
        result += a3 * x * x * x;
        result += a4 * x * x * x * x;
        result += a5 * x * x * x * x * x;
        return result;
    }
}
