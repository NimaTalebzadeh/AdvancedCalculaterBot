using System.Numerics;
using AdvancedCalculaterBot.Services.Equations;
using Xunit;

namespace AdvancedCalculaterBot.Tests;

public class ComplexFormatterTests
{
    [Theory]
    [InlineData(2.0, 0.0, "2")]
    [InlineData(-3.5, 0.0, "-3.5")]
    [InlineData(0.0, 3.0, "3i")]
    [InlineData(0.0, -3.0, "-3i")]
    [InlineData(2.0, 3.0, "2 + 3i")]
    [InlineData(2.0, -3.0, "2 - 3i")]
    public void Format_Single_Complex(double real, double imag, string expected)
    {
        var z = new Complex(real, imag);
        Assert.Equal(expected, ComplexFormatter.Format(z));
    }

    [Fact]
    public void Format_Snaps_Near_Zero_Parts_To_Zero()
    {
        var z = new Complex(1e-8, 1e-8);
        Assert.Equal("0", ComplexFormatter.Format(z));
    }

    [Fact]
    public void Format_Rounds_To_Integers_When_Close()
    {
        var z = new Complex(2.0000001, 0);
        Assert.Equal("2", ComplexFormatter.Format(z));
    }

    [Fact]
    public void FormatRoots_Single_Root_Uses_Bare_Label()
    {
        var roots = new[] { new Complex(5, 0) };
        var output = ComplexFormatter.FormatRoots(roots);
        Assert.Equal("x = 5", output);
    }

    [Fact]
    public void FormatRoots_Multiple_Roots_Use_Subscripts()
    {
        var roots = new[] { new Complex(2, 0), new Complex(3, 0) };
        var output = ComplexFormatter.FormatRoots(roots);
        Assert.Equal("x₁ = 2\nx₂ = 3", output);
    }

    [Fact]
    public void FormatRoots_Deduplicates_Near_Equal_Roots()
    {
        var roots = new[] { new Complex(2.0, 0), new Complex(2.0 + 1e-9, 0) };
        var output = ComplexFormatter.FormatRoots(roots);
        Assert.Equal("x = 2", output);
    }
}
