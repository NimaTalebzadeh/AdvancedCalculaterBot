using AdvancedCalculaterBot.Services.Equations;
using Xunit;

namespace AdvancedCalculaterBot.Tests;

public class EquationSolverServiceTests
{
    [Fact]
    public void Solve_Linear_Fractional_Returns_Correct_Root()
    {
        var output = EquationSolverService.Solve("(x + 3) / 12 = x / 9");
        Assert.Contains("x = 9", output);
    }

    [Fact]
    public void Solve_Simple_Linear()
    {
        var output = EquationSolverService.Solve("2x + 6 = 0");
        Assert.Contains("x = -3", output);
    }

    [Fact]
    public void Solve_Quadratic_Two_Roots()
    {
        var output = EquationSolverService.Solve("x^2 - 5x + 6 = 0");
        Assert.Contains("2", output);
        Assert.Contains("3", output);
        Assert.Contains("x₁", output);
        Assert.Contains("x₂", output);
    }

    [Fact]
    public void Solve_Cubic_Three_Roots()
    {
        var output = EquationSolverService.Solve("x^3 - 6x^2 + 11x - 6 = 0");
        Assert.Contains("1", output);
        Assert.Contains("2", output);
        Assert.Contains("3", output);
    }

    [Fact]
    public void Solve_Quartic_Four_Roots()
    {
        var output = EquationSolverService.Solve("x^4 - 10x^2 + 9 = 0");
        Assert.Contains("-3", output);
        Assert.Contains("-1", output);
        Assert.Contains("1", output);
        Assert.Contains("3", output);
    }

    [Fact]
    public void Solve_No_Solution_Contradiction()
    {
        // "2x = 2x + 1" has x but simplifies to 0 = 1 (no solution).
        var output = EquationSolverService.Solve("2x = 2x + 1");
        Assert.Equal("No solution.", output);
    }

    [Fact]
    public void Solve_Infinite_Solutions_Identity()
    {
        var output = EquationSolverService.Solve("2x = 2x");
        Assert.Equal("Infinite solutions.", output);
    }

    [Fact]
    public void Solve_Degree_Too_High()
    {
        var output = EquationSolverService.Solve("x^5 + 1 = 0");
        Assert.Contains("up to degree 4", output);
    }

    [Fact]
    public void Solve_Missing_Variable_Returns_Error()
    {
        var output = EquationSolverService.Solve("2 + 2 = 4");
        Assert.Contains("only", output.ToLower());
    }

    [Fact]
    public void Solve_Empty_Side_Returns_Error()
    {
        var output = EquationSolverService.Solve("= 5");
        Assert.Contains("left and right", output.ToLower());
    }
}
