using System.Numerics;

namespace AdvancedCalculatorBot.Services.Equations;

/// <summary>Solves ax + b = 0 given coefficients [b, a]. Returns a single root.</summary>
public static class LinearSolver
{
    public static Complex[] Solve(double[] coeffs)
    {
        // coeffs = [b, a]; the orchestrator guarantees degree 1, so a != 0 here.
        double b = coeffs[0];
        double a = coeffs[1];
        return new[] { new Complex(-b / a, 0) };
    }
}
