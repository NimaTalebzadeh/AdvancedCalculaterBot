using System.Globalization;
using System.Text.RegularExpressions;

namespace AdvancedCalculaterBot.Services.Equations;

/// <summary>
/// Parses a single-variable (x) polynomial from text into a coefficient array,
/// lowest-degree first (index i = coefficient of x^i).
/// Supports explicit multiplication forms like 3*x^2.
/// </summary>
public static class PolynomialParser
{
    /// <summary>
    /// True if <paramref name="input"/> is a bare polynomial (no parentheses,
    // no division/parentheses, only polynomial-safe operators.
    /// </summary>
    public static bool CanParse(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return true;
        foreach (char c in input)
        {
            bool ok = char.IsDigit(c) || char.IsLetter(c) ||
                      c == 'x' || c == 'X' || c == '^' || c == '*' ||
                      c == '+' || c == '-' || c == '.' || c == ',' ||
                      char.IsWhiteSpace(c);
            if (!ok) return false;
        }
        return true;
    }

    /// <summary>Parses a polynomial into coefficients (low→high).</summary>
    /// <exception cref="FormatException">A term cannot be parsed.</exception>
    public static double[] Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new double[] { 0 };

        // Normalize: ensure a leading sign for uniform splitting, remove spaces.
        string s = input.Replace(" ", "")
                        .Replace("\t", "")
                        .Replace("*", "");
        if (s.Length == 0) return new double[] { 0 };
        if (s[0] != '+' && s[0] != '-') s = "+" + s;

        // Split into terms while keeping the leading sign on each.
        var terms = new List<string>();
        int start = 0;
        for (int i = 1; i < s.Length; i++)
        {
            if (s[i] == '+' || s[i] == '-')
            {
                terms.Add(s.Substring(start, i - start));
                start = i;
            }
        }
        terms.Add(s.Substring(start));

        var coeffs = new List<double>();
        foreach (var term in terms)
        {
            if (string.IsNullOrEmpty(term)) continue;
            var (coef, power) = ParseTerm(term);
            while (coeffs.Count <= power) coeffs.Add(0);
            coeffs[power] += coef;
        }

        return coeffs.Count == 0 ? new double[] { 0 } : coeffs.ToArray();
    }

    private static (double coef, int power) ParseTerm(string term)
    {
        // term looks like: "+6", "-5x", "+x", "-x^4", "3x^3", "+2x"
        bool negative = term[0] == '-';
        string body = term.Substring(1); // drop the sign char

        int xIndex = body.IndexOf('x');
        if (xIndex < 0)
        {
            // pure constant
            if (!double.TryParse(body, NumberStyles.Any, CultureInfo.InvariantCulture, out double c))
                throw new FormatException($"Cannot parse term '{term}'.");
            return (negative ? -c : c, 0);
        }

        // coefficient part (before x)
        double coef;
        string coefPart = body.Substring(0, xIndex);
        if (string.IsNullOrEmpty(coefPart)) coef = 1;
        else if (!double.TryParse(coefPart, NumberStyles.Any, CultureInfo.InvariantCulture, out coef))
            throw new FormatException($"Cannot parse coefficient in term '{term}'.");

        coef = negative ? -coef : coef;

        // exponent part (after x, optionally after ^)
        int power = 1;
        int caret = body.IndexOf('^', xIndex);
        if (caret >= 0)
        {
            string expPart = body.Substring(caret + 1);
            if (!int.TryParse(expPart, out power) || power < 0)
                throw new FormatException($"Cannot parse exponent in term '{term}'.");
        }
        else if (body.Length > xIndex + 1)
        {
            throw new FormatException($"Cannot parse term '{term}'.");
        }

        return (coef, power);
    }
}
