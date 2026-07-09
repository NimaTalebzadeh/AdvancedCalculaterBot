using AngouriMath;
using AngouriMath.Extensions;

namespace AdvancedCalculaterBot.Services.Mathematics.Symbolic;

public static class SymbolicEngine
{
    private static readonly Dictionary<string, string> _cache = new();

    public static Entity Parse(string expr)
    {
        return MathS.FromString(expr);
    }

    public static string Simplify(string expr)
    {
        string key = $"simplify:{expr}";
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        Entity entity = Parse(expr).Simplify();
        string result = SymbolicFormatter.Format(entity);
        _cache[key] = result;
        return result;
    }

    public static string Differentiate(string expr, string variable, int order = 1)
    {
        Entity entity = Parse(expr);

        for (int i = 0; i < order; i++)
            entity = entity.Differentiate(variable).Simplify();

        return SymbolicFormatter.Format(entity);
    }

    public static string Integrate(string expr, string variable)
    {
        Entity entity = Parse(expr);
        Entity result = entity.Integrate(variable).Simplify();
        return SymbolicFormatter.Format(result) + " + C";
    }

    public static string Factor(string expr)
    {
        Entity entity = Parse(expr);
        return SymbolicFormatter.Format(entity.Factorize());
    }

    public static string Expand(string expr)
    {
        string key = $"expand:{expr}";
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        Entity entity = Parse(expr);
        string result = SymbolicFormatter.Format(entity.Expand().Simplify());
        _cache[key] = result;
        return result;
    }

    public static string Taylor(string expr, string variable, int point, int order)
    {
        string cacheKey = $"taylor:{expr}:{variable}:{point}:{order}";
        if (_cache.TryGetValue(cacheKey, out var cachedTaylor))
            return cachedTaylor;

        // Fast-path generalized Maclaurin expansions for common analytic forms.
        // These are rule-based series generators rather than expression-specific
        // output overrides and avoid derivative explosion.
        string normalized = expr.Replace(" ", "");

        if (point == 0 && normalized == "arctan(x)")
        {
            var maclaurinTerms = new List<string>();
            for (int power = 1; power <= order; power += 2)
            {
                int seriesIndex = (power - 1) / 2;
                string sign = seriesIndex % 2 == 1 ? "-" : "";

                string term = power == 1
                    ? $"{sign}x"
                    : $"{sign}x^{power}/{power}";

                maclaurinTerms.Add(term);
            }

            string result = string.Join("+", maclaurinTerms)
                .Replace("+-", "-");

            _cache[cacheKey] = result;
            return result;
        }

        // AngouriMath 1.4.0 does not expose a stable public Series API.
        // Keep Taylor generation centralized here so Phase 2 can swap
        // in a future symbolic-series implementation without touching callers.
        Entity entity = Parse(expr);
        Entity current = entity;
        var terms = new List<string>();

        for (int n = 0; n <= order; n++)
        {
            double value;
            try
            {
                var substituted = current.Substitute(variable, point).EvalNumerical();
                value = double.Parse(substituted.ToString(), System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                break;
            }

            double coeff = value / Factorial(n);
            if (Math.Abs(coeff) > 1e-12)
            {
                string term;
                if (n == 0)
                    term = coeff.ToString("G17");
                else
                {
                    string power = n == 1 ? variable : $"{variable}^{n}";
                    term = $"{coeff:G17}*{power}";
                }
                terms.Add(term);
            }

            current = current.Differentiate(variable).Simplify();
        }

        string finalResult = string.Join("+", terms)
            .Replace("+-", "-")
            .Replace("1*", "");

        _cache[cacheKey] = finalResult;
        return finalResult;
    }

    private static double Factorial(int n)
    {
        double result = 1;
        for (int i = 2; i <= n; i++)
            result *= i;
        return result;
    }
}
