using System.Numerics;

namespace AdvancedCalculaterBot.Services.Equations;

/// <summary>
/// Formats <see cref="Complex"/> roots for display, mirroring the style of the
/// reference EquationSolvers projects (e.g. "2 + 3i", "-4", "5i").
/// </summary>
public static class ComplexFormatter
{
    private const double ZeroTolerance = 1e-6;
    private const double RoundTolerance = 1e-5;

    /// <summary>Formats a single complex number.</summary>
    public static string Format(Complex z)
    {
        double r = z.Real;
        double i = z.Imaginary;

        if (Math.Abs(r) < ZeroTolerance) r = 0;
        if (Math.Abs(i) < ZeroTolerance) i = 0;

        if (Math.Abs(r - Math.Round(r)) < RoundTolerance) r = Math.Round(r);
        if (Math.Abs(i - Math.Round(i)) < RoundTolerance) i = Math.Round(i);

        if (i == 0) return r.ToString();
        if (r == 0) return $"{i}i";

        return i > 0 ? $"{r} + {i}i" : $"{r} - {Math.Abs(i)}i";
    }

    /// <summary>
    /// Formats an array of roots into a multi-line reply, deduplicating roots
    /// that are within <see cref="ZeroTolerance"/> of each other. Uses a bare
    /// "x = ..." label for a single root and "x₁ = ...", "x₂ = ..." otherwise.
    /// </summary>
    public static string FormatRoots(Complex[] roots)
    {
        var unique = Deduplicate(roots);

        if (unique.Count == 1)
            return $"x = {Format(unique[0])}";

        var labels = new[] { "x₁", "x₂", "x₃", "x₄" };
        var lines = unique.Select((r, idx) => $"{labels[idx]} = {Format(r)}");
        return string.Join("\n", lines);
    }

    private static List<Complex> Deduplicate(Complex[] roots)
    {
        var unique = new List<Complex>();
        foreach (var r in roots)
        {
            bool dup = unique.Any(ur => Complex.Abs(r - ur) < ZeroTolerance);
            if (!dup) unique.Add(r);
        }
        return unique;
    }
}
