using System.Numerics;

namespace AdvancedCalculaterBot.Services.Equations;

/// <summary>
/// Solves ax^2 + bx + c = 0 given coefficients [c, b, a] (low→high).
/// Handles real and complex roots via Complex.Sqrt. Adapted from
/// EquationSolvers/QuadraticEquation/Program.cs.
/// </summary>
public static class QuadraticSolver
{
    public static Complex[] Solve(double[] coeffs)
    {
        double c = coeffs[0];
        double b = coeffs[1];
        double a = coeffs[2];

        Complex delta = b * b - 4 * a * c;
        Complex sqrtDelta = Complex.Sqrt(delta);

        Complex x1 = (-b + sqrtDelta) / (2 * a);
        Complex x2 = (-b - sqrtDelta) / (2 * a);

        return new[] { x1, x2 };
    }
}
