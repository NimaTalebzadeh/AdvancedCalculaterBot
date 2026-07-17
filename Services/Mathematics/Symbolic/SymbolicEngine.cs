using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using AngouriMath;
using AngouriMath.Extensions;

namespace AdvancedCalculatorBot.Services.Mathematics.Symbolic;

public static class SymbolicEngine
{
    private static readonly Dictionary<string, string> _cache = new();
    private static readonly HttpClient _httpClient = new();
    private static readonly string _sympyUrl;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static SymbolicEngine()
    {
        _sympyUrl = Environment.GetEnvironmentVariable("SYMPY_ENGINE_URL") ?? "http://localhost:8010";
    }

    public static Entity Parse(string expr)
    {
        // Convert exp(...) to e^(...) for AngouriMath compatibility.
        // AngouriMath treats `exp` as a variable (Euler's constant),
        // not as the exponential function. e^(x) is the correct form.
        // Note: ln(x) is natively supported, no conversion needed.
        return MathS.FromString(NormalizeMath(expr));
    }

    /// <summary>
    /// Converts exp(...) → e^(...) for AngouriMath compatibility.
    /// AngouriMath treats `exp` as a variable (Euler's constant),
    /// not as the exponential function. Handles nested function calls.
    /// </summary>
    private static string NormalizeMath(string expr)
    {
        var result = new System.Text.StringBuilder();
        int i = 0;
        while (i < expr.Length)
        {
            // exp(...) → e^(...)
            if (i + 3 < expr.Length &&
                char.ToLowerInvariant(expr[i]) == 'e' &&
                char.ToLowerInvariant(expr[i+1]) == 'x' &&
                char.ToLowerInvariant(expr[i+2]) == 'p' &&
                expr[i+3] == '(')
            {
                result.Append("e^(");
                i += 4;
                int depth = 1;
                int innerStart = i;
                while (i < expr.Length && depth > 0)
                {
                    if (expr[i] == '(') depth++;
                    else if (expr[i] == ')') depth--;
                    if (depth > 0) i++;
                }
                string inner = expr.Substring(innerStart, i - innerStart);
                result.Append(NormalizeMath(inner));
                result.Append(')');
                i++;
            }
            else
            {
                result.Append(expr[i]);
                i++;
            }
        }
        return result.ToString();
    }

    private static string? TrySymPy(string action, string expr, string variable = "x",
        double? lower = null, double? upper = null)
    {
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["action"] = action,
                ["expr"] = expr,
                ["var"] = variable
            };
            if (lower.HasValue) payload["lower"] = lower.Value;
            if (upper.HasValue) payload["upper"] = upper.Value;

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = _httpClient.PostAsync($"{_sympyUrl}/", content)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            var responseBody = response.Content.ReadAsStringAsync()
                .ConfigureAwait(false).GetAwaiter().GetResult();
            var result = JsonSerializer.Deserialize<SymPyResult>(responseBody, _jsonOptions);

            if (result?.Ok == true && !string.IsNullOrWhiteSpace(result.Result))
            {
                // Convert SymPy notation back to bot notation:
                // ** → ^, sin**2 → sin^2
                string r = result.Result;
                r = r.Replace("**", "^");
                r = r.Replace("exp(", "e^(");
                r = r.Replace("*", "");
                return r;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static string Integrate(string expr, string variable)
    {
        // Try SymPy first for potentially non-elementary integrals.
        // SymPy handles special functions (erf, Ei, Si, etc.) reliably,
        // whereas AngouriMath hangs on non-elementary integrals.
        string? sympyResult = TrySymPy("integrate", expr, variable);
        if (sympyResult != null)
            return sympyResult + " + C";

        // Fallback to AngouriMath for elementary integrals
        try
        {
            Entity entity = Parse(expr);
            string angouriResult;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                var task = Task.Run(() =>
                {
                    Entity result = entity.Integrate(variable).Simplify();
                    return SymbolicFormatter.Format(result);
                }, cts.Token);

                if (task.Wait(TimeSpan.FromSeconds(3), cts.Token))
                    angouriResult = task.Result;
                else
                    throw new TimeoutException("AngouriMath integration timed out");
            }

            if (angouriResult.Contains("integral(", StringComparison.OrdinalIgnoreCase) ||
                angouriResult.Contains("integrate(", StringComparison.OrdinalIgnoreCase))
            {
                // Already tried SymPy above, nothing more to do
                return angouriResult + " + C";
            }

            return angouriResult + " + C";
        }
        catch
        {
            // If AngouriMath fails, SymPy already returned null above
            // Return the expression as integral
            return $"integral({expr}, {variable}) + C";
        }
    }

    /// <summary>
    /// Returns true if the result string contains a special function (erf, gamma, etc.)
    /// indicating it's a non-elementary integral.
    /// </summary>
    public static bool IsNonElementary(string result)
    {
        if (string.IsNullOrEmpty(result))
            return false;
        string lower = result.ToLowerInvariant();
        return lower.Contains("erf(") || lower.Contains("erfc(") || lower.Contains("erfi(")
            || lower.Contains("gamma(") || lower.Contains("gammainc(")
            || lower.Contains("si(") || lower.Contains("ci(")
            || lower.Contains("ei(") || lower.Contains("li(")
            || lower.Contains("polylog(") || lower.Contains("dilog(")
            || lower.Contains("fresnel(") || lower.Contains("elliptic(")
            || lower.Contains("hypergeometric(") || lower.Contains("zeta(");
    }

    public static string Differentiate(string expr, string variable, int order = 1)
    {
        Entity entity = Parse(expr);
        Entity current = entity;
        for (int i = 0; i < order; i++)
            current = current.Differentiate(variable).Simplify();
        return SymbolicFormatter.Format(current);
    }

    public static string Simplify(string expr)
    {
        string key = $"simplify:{expr}";
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        Entity entity = Parse(expr);
        string result = SymbolicFormatter.Format(entity.Simplify());
        _cache[key] = result;
        return result;
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

    public static string Factor(string expr)
    {
        Entity entity = Parse(expr);
        return SymbolicFormatter.Format(entity.Factorize());
    }

    public static string Taylor(string expr, string variable, int point, int order)
    {
        string cacheKey = $"taylor:{expr}:{variable}:{point}:{order}";
        if (_cache.TryGetValue(cacheKey, out var cachedTaylor))
            return cachedTaylor;

        Entity entity = Parse(expr);
        var varEntity = MathS.Var(variable);
        Entity result = MathS.Series.Taylor(entity, order, (varEntity, point));
        string resultStr = SymbolicFormatter.Format(result);
        _cache[cacheKey] = resultStr;
        return resultStr;
    }

    public static string Limit(string expr, string variable, double point, string direction = "x")
    {
        Entity entity = Parse(expr);
        var varEntity = MathS.Var(variable);
        var result = entity.Limit(varEntity, point);
        return SymbolicFormatter.Format(result);
    }

    private class SymPyResult
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
