using System.Numerics;
using AdvancedCalculaterBot.Services.Equations;
using Xunit;

namespace AdvancedCalculaterBot.Tests;

public class SolverTests
{
    private static bool Near(Complex a, Complex b, double tol = 1e-6) =>
        Complex.Abs(a - b) < tol;

    // ---- LinearSolver ----

    [Fact]
    public void LinearSolver_Returns_Single_Root()
    {
        // 2x + 6 = 0  ->  x = -3
        var roots = LinearSolver.Solve(new[] { 6.0, 2.0 });
        Assert.Single(roots);
        Assert.True(Near(roots[0], new Complex(-3, 0)));
    }

    // ---- QuadraticSolver ----

    [Fact]
    public void QuadraticSolver_Two_Real_Distinct_Roots()
    {
        // x^2 - 5x + 6 = 0  ->  x in {2, 3}
        var roots = QuadraticSolver.Solve(new[] { 6.0, -5.0, 1.0 });
        Assert.Equal(2, roots.Length);
        Assert.Contains(roots, r => Near(r, new Complex(2, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(3, 0)));
    }

    [Fact]
    public void QuadraticSolver_Complex_Roots()
    {
        // x^2 + 1 = 0  ->  x in {i, -i}
        var roots = QuadraticSolver.Solve(new[] { 1.0, 0.0, 1.0 });
        Assert.Equal(2, roots.Length);
        Assert.Contains(roots, r => Near(r, new Complex(0, 1)));
        Assert.Contains(roots, r => Near(r, new Complex(0, -1)));
    }

    // ---- CubicSolver ----

    [Fact]
    public void CubicSolver_Three_Real_Distinct_Roots()
    {
        // x^3 - 6x^2 + 11x - 6 = 0  ->  x in {1, 2, 3}
        var roots = CubicSolver.Solve(new[] { -6.0, 11.0, -6.0, 1.0 });
        Assert.Equal(3, roots.Length);
        Assert.Contains(roots, r => Near(r, new Complex(1, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(2, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(3, 0)));
    }

    [Fact]
    public void CubicSolver_One_Real_Root_Two_Complex()
    {
        // x^3 + 1 = 0  ->  x in {-1, (1 ± i*sqrt(3))/2 }
        var roots = CubicSolver.Solve(new[] { 1.0, 0.0, 0.0, 1.0 });
        Assert.Equal(3, roots.Length);
        Assert.Contains(roots, r => Near(r, new Complex(-1, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(0.5, Math.Sqrt(3) / 2)));
        Assert.Contains(roots, r => Near(r, new Complex(0.5, -Math.Sqrt(3) / 2)));
    }
}
