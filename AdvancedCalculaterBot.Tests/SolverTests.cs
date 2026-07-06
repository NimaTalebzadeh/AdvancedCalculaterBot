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

    // ---- QuarticSolver ----

    [Fact]
    public void QuarticSolver_Four_Real_Roots()
    {
        // x^4 - 10x^2 + 9 = 0  ->  x in {-3, -1, 1, 3}
        var roots = QuarticSolver.Solve(new[] { 9.0, 0.0, -10.0, 0.0, 1.0 });
        Assert.Equal(4, roots.Length);
        Assert.Contains(roots, r => Near(r, new Complex(-3, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(-1, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(1, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(3, 0)));
    }

    [Fact]
    public void QuarticSolver_Complex_Roots()
    {
        // x^4 + 1 = 0  ->  x in {(±1 ± i)/sqrt(2)} -> 4 distinct complex roots
        var roots = QuarticSolver.Solve(new[] { 1.0, 0.0, 0.0, 0.0, 1.0 });
        Assert.Equal(4, roots.Length);
        double m = 1.0 / Math.Sqrt(2);
        Assert.Contains(roots, r => Near(r, new Complex(m, m)));
        Assert.Contains(roots, r => Near(r, new Complex(-m, m)));
        Assert.Contains(roots, r => Near(r, new Complex(-m, -m)));
        Assert.Contains(roots, r => Near(r, new Complex(m, -m)));
    }

    [Fact]
    public void QuarticSolver_Repeated_Roots()
    {
        // (x-2)^2 (x-3)^2 = (x^2 - 5x + 6)^2 = x^4 - 10x^3 + 37x^2 - 60x + 36
        var roots = QuarticSolver.Solve(new[] { 36.0, -60.0, 37.0, -10.0, 1.0 });
        // After dedup: exactly {2, 3}
        Assert.Equal(2, roots.Length);
        Assert.Contains(roots, r => Near(r, new Complex(2, 0)));
        Assert.Contains(roots, r => Near(r, new Complex(3, 0)));
    }
}
