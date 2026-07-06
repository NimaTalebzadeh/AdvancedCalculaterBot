using AdvancedCalculaterBot.Services.Equations;
using Xunit;

namespace AdvancedCalculaterBot.Tests;

public class PolynomialParserTests
{
    [Theory]
    [InlineData("x^2 - 5x + 6", new[] { 6.0, -5.0, 1.0 })]
    [InlineData("-x^4", new[] { 0.0, 0.0, 0.0, 0.0, -1.0 })]
    [InlineData("x", new[] { 0.0, 1.0 })]
    [InlineData("3x^3 - x + 5", new[] { 5.0, -1.0, 0.0, 3.0 })]
    [InlineData("7", new[] { 7.0 })]
    [InlineData("2x", new[] { 0.0, 2.0 })]
    [InlineData("x^2 + x + 1", new[] { 1.0, 1.0, 1.0 })]
    [InlineData("-3", new[] { -3.0 })]
    [InlineData("x^4 - 10x^2 + 9", new[] { 9.0, 0.0, -10.0, 0.0, 1.0 })]
    public void Parse_Returns_Coefficients_Low_To_High(string input, double[] expected)
    {
        var coeffs = PolynomialParser.Parse(input);
        Assert.Equal(expected, coeffs);
    }

    [Theory]
    [InlineData("x^2 - 5x + 6", true)]
    [InlineData("-x^4", true)]
    [InlineData("7", true)]
    [InlineData("(x + 3) / 12", false)]
    [InlineData("x / 9", false)]
    [InlineData("sin(x)", false)]
    public void CanParse_Detects_Polynomial_Form(string input, bool expected)
    {
        Assert.Equal(expected, PolynomialParser.CanParse(input));
    }

    [Fact]
    public void Parse_Throws_On_Invalid_Term()
    {
        Assert.Throws<FormatException>(() => PolynomialParser.Parse("x^2 + foo"));
    }
}
