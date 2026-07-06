using System.Text.RegularExpressions;

namespace AdvancedCalculaterBot.Services.Mathematics;

/// <summary>
/// Registry for mathematical functions that can be extended with new functions
/// following the Single Responsibility Principle.
/// </summary>
public static class MathFunctionRegistry
{
    private static readonly Dictionary<string, MathFunctionInfo> _functions = new(StringComparer.OrdinalIgnoreCase);

    static MathFunctionRegistry()
    {
        RegisterDefaultFunctions();
    }

    /// <summary>
    /// Registers a mathematical function with its implementation.
    /// </summary>
    public static void RegisterFunction(string name, Func<double[], double> implementation, int requiredArgs)
    {
        _functions[name.ToLowerInvariant()] = new MathFunctionInfo
        {
            Name = name,
            Implementation = implementation,
            RequiredArgs = requiredArgs
        };
    }

    /// <summary>
    /// Gets the function info for a given function name.
    /// </summary>
    public static MathFunctionInfo? GetFunction(string name)
    {
        return _functions.TryGetValue(name.ToLowerInvariant(), out var func) ? func : null;
    }

    /// <summary>
    /// Checks if a function is registered.
    /// </summary>
    public static bool IsFunctionRegistered(string name)
    {
        return _functions.ContainsKey(name.ToLowerInvariant());
    }

    /// <summary>
    /// Gets all registered function names.
    /// </summary>
    public static string[] GetRegisteredFunctions()
    {
        return _functions.Keys.ToArray();
    }

    /// <summary>
    /// Registers all default mathematical functions.
    /// </summary>
    private static void RegisterDefaultFunctions()
    {
        RegisterFunction("sin", args => Math.Sin(args[0] * Math.PI / 180d), 1);
        RegisterFunction("cos", args => Math.Cos(args[0] * Math.PI / 180d), 1);
        RegisterFunction("tan", args => Math.Tan(args[0] * Math.PI / 180d), 1);
        RegisterFunction("asin", args => Math.Asin(args[0]) * 180d / Math.PI, 1);
        RegisterFunction("acos", args => Math.Acos(args[0]) * 180d / Math.PI, 1);
        RegisterFunction("atan", args => Math.Atan(args[0]) * 180d / Math.PI, 1);
        RegisterFunction("sinh", args => Math.Sinh(args[0]), 1);
        RegisterFunction("cosh", args => Math.Cosh(args[0]), 1);
        RegisterFunction("tanh", args => Math.Tanh(args[0]), 1);
        RegisterFunction("log", args => Math.Log10(args[0]), 1);
        RegisterFunction("ln", args => Math.Log(args[0]), 1);
        RegisterFunction("sqrt", args => Math.Sqrt(args[0]), 1);
        RegisterFunction("abs", args => Math.Abs(args[0]), 1);
        RegisterFunction("exp", args => Math.Exp(args[0]), 1);
        RegisterFunction("floor", args => Math.Floor(args[0]), 1);
        RegisterFunction("ceil", args => Math.Ceiling(args[0]), 1);
        RegisterFunction("round", args => Math.Round(args[0]), 1);
    }

    /// <summary>
    /// Information about a registered mathematical function.
    /// </summary>
    public class MathFunctionInfo
    {
        public string Name { get; set; } = string.Empty;
        public Func<double[], double> Implementation { get; set; } = args => 0;
        public int RequiredArgs { get; set; }
    }
}
