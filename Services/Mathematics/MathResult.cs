using System.Numerics;

namespace AdvancedCalculatorBot.Services.Mathematics;

/// <summary>
/// Represents a result from mathematical operations with consistent formatting.
/// </summary>
public class MathResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Expression { get; set; }
    public object? Value { get; set; }
    public string? FormattedValue { get; set; }
    public string? ErrorDetails { get; set; }

    public static MathResult SuccessResult(string expression, object value)
    {
        return new MathResult
        {
            Success = true,
            Expression = expression,
            Value = value,
            FormattedValue = FormatValue(value)
        };
    }

    public static MathResult ErrorResult(string message, string? errorDetails = null)
    {
        return new MathResult
        {
            Success = false,
            Message = message,
            ErrorDetails = errorDetails
        };
    }

    private static string FormatValue(object? value)
    {
        if (value == null)
            return "null";

        if (value is double d)
        {
            if (double.IsNaN(d))
                return "NaN";
            if (double.IsInfinity(d))
                return double.IsPositiveInfinity(d) ? "+∞" : "-∞";
            return d.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
        }

        if (value is Complex complex)
        {
            if (complex.Imaginary == 0)
                return complex.Real.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
            if (complex.Real == 0)
                return $"{complex.Imaginary.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)}i";
            return $"{complex.Real.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)}+{complex.Imaginary.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)}i";
        }

        if (value is Complex[] complexArray)
            return Equations.ComplexFormatter.FormatRoots(complexArray);

        if (value is string s)
            return s;

        return value.ToString() ?? "null";
    }

    public override string ToString()
    {
        if (Success)
        {
            return FormattedValue ?? "Success";
        }
        return $"Error: {Message}";
    }
}