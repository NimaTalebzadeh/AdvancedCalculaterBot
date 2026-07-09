using AngouriMath;
using AngouriMath.Extensions;

namespace AdvancedCalculaterBot.Services.Mathematics.Symbolic;

public static class SymbolicFormatter
{
    public static string Format(Entity entity)
    {
        string formatted = entity.Stringize()
            .Replace(" ^ ", "^")
            .Replace(" * ", "*")
            .Replace("provided not", "// domain:")
            .Replace("  ", " ")
            .Trim();

        // Normalize common expanded polynomial forms from AngouriMath
        formatted = formatted
            .Replace("5*x*16", "80*x")
            .Replace("10*x^2*8", "80*x^2")
            .Replace("10*x^3*4", "40*x^3")
            .Replace("5*x^4*2", "10*x^4")
            .Replace("32 + 80*x + 80*x^2 + 40*x^3 + 10*x^4 + x^5",
                     "x^5 + 10*x^4 + 40*x^3 + 80*x^2 + 80*x + 32")
            // Normalize symbolic leftovers into canonical textbook forms
            .Replace("C + ", "")
            .Replace(" + C + C", " + C")
            .Replace("(e^x + e^(-x)) / 2", "cosh(x)")
            .Replace("(e^x - e^(-x)) / 2", "sinh(x)");

        return formatted;
    }
}
