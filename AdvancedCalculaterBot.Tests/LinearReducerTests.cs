using AdvancedCalculaterBot.Services.Equations;
using Xunit;

namespace AdvancedCalculaterBot.Tests;

public class LinearReducerTests
{
    [Fact]
    public void Reduce_Linear_Fractional_Equation_Returns_Correct_Root()
    {
        // (x + 3) / 12 = x / 9  ->  x = 9
        // So f(x) = (x+3)/12 - x/9 has root 9: coeffs should be [b, a] with -b/a = 9.
        var coeffs = LinearReducer.Reduce("(x + 3) / 12", "x / 9");
        // coeffs = [b, a]; root = -b/a
        double root = -coeffs[0] / coeffs[1];
        Assert.Equal(9.0, root, precision: 6);
    }

    [Fact]
    public void Reduce_Simple_Linear_Parts_Works_Too()
    {
        // 2x + 1 = 3x - 4  ->  x = 5
        var coeffs = LinearReducer.Reduce("2x + 1", "3x - 4");
        double root = -coeffs[0] / coeffs[1];
        Assert.Equal(5.0, root, precision: 6);
    }

    [Fact]
    public void Reduce_Throws_When_Not_Linear()
    {
        // x^2 = 4 is not linear in x; samples will not be collinear.
        Assert.Throws<InvalidOperationException>(
            () => LinearReducer.Reduce("x^2", "4"));
    }
}
