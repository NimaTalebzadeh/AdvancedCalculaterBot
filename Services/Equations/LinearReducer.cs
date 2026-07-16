using System.Globalization;
using System.Text;
using NCalc;

namespace AdvancedCalculatorBot.Services.Equations;

/// <summary>
/// Reduces a structured linear equation (one that may contain parentheses,
/// division, or other operations valid in NCalc but not in <see cref="PolynomialParser"/>)
/// into coefficients [b, a] of ax + b = 0, by sampling f(x) = LHS(x) - RHS(x).
/// </summary>
public static class LinearReducer
{
    private const double Tolerance = 1e-9;

    /// <summary>
    /// Returns coefficients [b, a] such that LHS - RHS is equivalent to a*x + b.
    /// Throws <see cref="InvalidOperationException"/> if the expression is not linear.
    /// </summary>
    public static double[] Reduce(string lhs, string rhs)
    {
        double f0 = Evaluate(lhs, 0) - Evaluate(rhs, 0);
        double f1 = Evaluate(lhs, 1) - Evaluate(rhs, 1);
        double f2 = Evaluate(lhs, 2) - Evaluate(rhs, 2);

        double b = f0;
        double a = f1 - f0;

        // Verify linearity: f(2) must equal 2*a + b = 2*f1 - f0.
        double expected2 = 2 * f1 - f0;
        if (Math.Abs(f2 - expected2) > Tolerance)
        {
            throw new InvalidOperationException(
                "The equation is not linear in x.");
        }

        return new[] { b, a };
    }

    private static double Evaluate(string expression, double xValue)
    {
        // NCalc does not support implicit multiplication like "2x"; insert '*'.
        string processed = InsertImplicitMultiplication(expression);
        var expr = new Expression(processed);
        expr.Parameters["x"] = xValue;
        var result = expr.Evaluate();
        return Convert.ToDouble(result, CultureInfo.InvariantCulture);
    }

    // Converts "2x", "3(x", ")x", "x(" etc. into explicit "2*x" form so NCalc can parse it.
    private static string InsertImplicitMultiplication(string expression)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < expression.Length; i++)
        {
            char c = expression[i];
            sb.Append(c);
            if (i == expression.Length - 1) break;

            char next = expression[i + 1];
            bool currentEndsTerm =
                char.IsDigit(c) || c == 'x' || c == 'X' || c == ')';
            bool nextStartsTerm =
                next == 'x' || next == 'X' || next == '(' ||
                char.IsLetter(next);

            // "2x" or "2(" or ")x" or ")(" or "x(" -> insert '*'. Skip if next is '^'.
            if (currentEndsTerm && nextStartsTerm && c != '^')
            {
                // Don't insert between a digit and a function name like "3sin"
                // — but for our domain (polynomials/linear in x) this is safe.
                if (!(char.IsDigit(c) && char.IsLetter(next) && next != 'x' && next != 'X'))
                {
                    sb.Append('*');
                }
            }
        }
        return sb.ToString();
    }
}
