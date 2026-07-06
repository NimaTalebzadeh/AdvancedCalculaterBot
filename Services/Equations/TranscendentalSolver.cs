using System;
using System.Globalization;
using System.Numerics;
using NCalc;

namespace AdvancedCalculaterBot.Services.Equations
{
    /// <summary>
    /// Solves transcendental equations using the Newton-Raphson method.
    /// Expects equations in the form f(x) = g(x), where f and g may contain
    /// transcendental functions (sin, cos, tan, log, ln, exp, asin, acos, atan).
    /// All trigonometric functions accept degrees and return degrees.
    /// </summary>
    public static class TranscendentalSolver
    {
        private const double Tolerance = 1e-9;
        private const int MaxIterations = 100;
        private const double InitialGuess = 1.0;
        private const double H = 1e-7;

        /// <summary>
        /// Checks if the equation contains transcendental functions.
        /// </summary>
        public static bool IsTranscendental(string equation)
        {
            var lower = equation.ToLowerInvariant();
            return lower.Contains("sin") || lower.Contains("cos") || lower.Contains("tan") ||
                   lower.Contains("log") || lower.Contains("ln") || lower.Contains("exp") ||
                   lower.Contains("asin") || lower.Contains("acos") || lower.Contains("atan");
        }

        /// <summary>
        /// Solves the equation using Newton-Raphson method.
        /// Returns an array of solutions (typically one for transcendental equations).
        /// </summary>
        public static double[] Solve(string equation)
        {
            int eq = equation.IndexOf('=');
            if (eq < 0) return Array.Empty<double>();

            var lhs = equation.Substring(0, eq).Trim();
            var rhs = equation.Substring(eq + 1).Trim();

            // Preprocess expressions for NCalc
            string leftExpr = PreprocessForNCalc(lhs);
            string rightExpr = PreprocessForNCalc(rhs);

            double x = InitialGuess;
            double f = EvaluateDifference(leftExpr, rightExpr, x);

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                double fPrime = NumericalDerivative(leftExpr, rightExpr, x);

                if (Math.Abs(fPrime) < Tolerance)
                {
                    // Derivative too small, try a different guess
                    x = x + 0.5;
                    f = EvaluateDifference(leftExpr, rightExpr, x);
                    continue;
                }

                double xNew = x - f / fPrime;
                f = EvaluateDifference(leftExpr, rightExpr, xNew);

                if (Math.Abs(xNew - x) < Tolerance && Math.Abs(f) < Tolerance)
                {
                    return new[] { xNew };
                }

                x = xNew;
            }

            // Try alternative starting points
            double[] altGuesses = { -1.0, 2.0, -2.0, 0.5, -0.5, 3.0, -3.0, 0.0 };
            foreach (var guess in altGuesses)
            {
                x = guess;
                f = EvaluateDifference(leftExpr, rightExpr, x);

                for (int iter = 0; iter < MaxIterations; iter++)
                {
                    double fPrime = NumericalDerivative(leftExpr, rightExpr, x);

                    if (Math.Abs(fPrime) < Tolerance)
                        break;

                    double xNew = x - f / fPrime;
                    f = EvaluateDifference(leftExpr, rightExpr, xNew);

                    if (Math.Abs(xNew - x) < Tolerance && Math.Abs(f) < Tolerance)
                    {
                        return new[] { xNew };
                    }

                    x = xNew;
                }
            }

            return Array.Empty<double>(); // No solution found
        }

        /// <summary>
        /// Preprocesses the expression for NCalc by inserting multiplication operators where needed.
        /// Handles cases like ""2sin(x)"" -> ""2*sin(x)"", ""x2"" -> ""x2"" (treated as single variable), etc.
        /// </summary>
        private static string PreprocessForNCalc(string expression)
        {
            var sb = new System.Text.StringBuilder();
            expression = expression.Trim();

            for (int i = 0; i < expression.Length; i++)
            {
                char current = expression[i];
                sb.Append(current);

                if (i == expression.Length - 1) break;

                char next = expression[i + 1];
                bool shouldAddStar = false;

                // Case 1: Digit followed by letter or parenthesis (e.g., ""2x"" -> ""2*x"", ""2sin"" -> ""2*sin"", ""2("" -> ""2*(")
                if (char.IsDigit(current) && (char.IsLetter(next) || next == '('))
                {
                    shouldAddStar = true;
                }
                // Case 2: Closing parenthesis followed by letter, digit, or parenthesis (e.g., "")x"" -> "")*x"", "")2"" -> "")*2"", "")("" -> "")*(")
                else if (current == ')' && (char.IsLetter(next) || char.IsDigit(next) || next == '('))
                {
                    shouldAddStar = true;
                }
                // Case 3: Letter followed by parenthesis, but ONLY if it's NOT a known function name
                // (e.g., ""x("" -> ""x*("" but ""sin("" should remain ""sin("")
                else if (char.IsLetter(current) && next == '(')
                {
                    // Look backwards to see if we have a complete function name
                    int j = i;
                    while (j >= 0 && char.IsLetter(expression[j]))
                    {
                        j--;
                    }
                    j++; // j now points to the first letter of the potential function name
                    
                    int length = i - j + 1;
                    if (length > 0 && length <= 20) // Reasonable limit for function name length
                    {
                        string potentialFunc = expression.Substring(j, length).ToLowerInvariant();
                        
                        // Check if this is a known function name
                        bool isKnownFunction = 
                            IsEqualIgnoreCase(potentialFunc, "sin") || IsEqualIgnoreCase(potentialFunc, "cos") ||
                            IsEqualIgnoreCase(potentialFunc, "tan") || IsEqualIgnoreCase(potentialFunc, "log") ||
                            IsEqualIgnoreCase(potentialFunc, "ln") || IsEqualIgnoreCase(potentialFunc, "exp") ||
                            IsEqualIgnoreCase(potentialFunc, "asin") || IsEqualIgnoreCase(potentialFunc, "acos") ||
                            IsEqualIgnoreCase(potentialFunc, "atan");
                            
                        shouldAddStar = !isKnownFunction; // Add * ONLY if it's NOT a known function
                    }
                }

                if (shouldAddStar)
                {
                    sb.Append('*');
                }
            }

            return sb.ToString();
        }

        private static bool IsEqualIgnoreCase(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Evaluates f(x) = LHS(x) - RHS(x) using NCalc.
        /// </summary>
        private static double EvaluateDifference(string leftExpr, string rightExpr, double x)
        {
            double leftValue = EvaluateExpression(leftExpr, x);
            double rightValue = EvaluateExpression(rightExpr, x);
            return leftValue - rightValue;
        }

        /// <summary>
        /// Evaluates an expression at a given x using NCalc.
        /// Handles degree-to-radian conversion for trigonometric functions.
        /// </summary>
        private static double EvaluateExpression(string expression, double x)
        {
            var expr = new Expression(expression);
            expr.Parameters["x"] = x;

            // Define trigonometric functions that accept degrees
            expr.EvaluateFunction += (name, args) =>
            {
                // Get the string representation of the parameter expression
                string paramString = args.Parameters[0]?.ToString() ?? string.Empty;
                double paramValue = 0.0;

                // Try to evaluate the parameter
                try
                {
                    // Create a new expression from the parameter string and evaluate it with the current x
                    var paramExpr = new Expression(paramString);
                    paramExpr.Parameters["x"] = x;
                    paramValue = Convert.ToDouble(paramExpr.Evaluate(), CultureInfo.InvariantCulture);
                }
                catch
                {
                    // If evaluation fails, try to parse the string as a number
                    if (double.TryParse(paramString, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                    {
                        paramValue = parsed;
                    }
                    else
                    {
                        // If all else fails, use 0 to avoid breaking the solver
                        paramValue = 0.0;
                    }
                }

                switch (name.ToLowerInvariant())
                {
                    case "sin":
                        args.Result = Math.Sin(DegreesToRadians(paramValue));
                        break;
                    case "cos":
                        args.Result = Math.Cos(DegreesToRadians(paramValue));
                        break;
                    case "tan":
                        args.Result = Math.Tan(DegreesToRadians(paramValue));
                        break;
                    case "asin":
                        // Clamp the value to [-1, 1] to avoid domain errors
                        double clamped = Math.Max(-1, Math.Min(1, paramValue));
                        double asinResult = Math.Asin(clamped);
                        args.Result = RadiansToDegrees(asinResult);
                        break;
                    case "acos":
                        // Clamp the value to [-1, 1] to avoid domain errors
                        double clampedAcos = Math.Max(-1, Math.Min(1, paramValue));
                        double acosResult = Math.Acos(clampedAcos);
                        args.Result = RadiansToDegrees(acosResult);
                        break;
                    case "atan":
                        args.Result = RadiansToDegrees(Math.Atan(paramValue));
                        break;
                    case "log":
                        // log base 10, protect against non-positive values
                        if (paramValue <= 0)
                            args.Result = double.NaN;
                        else
                            args.Result = Math.Log10(paramValue);
                        break;
                    case "ln":
                        // natural log, protect against non-positive values
                        if (paramValue <= 0)
                            args.Result = double.NaN;
                        else
                            args.Result = Math.Log(paramValue);
                        break;
                    case "exp":
                        args.Result = Math.Exp(paramValue);
                        break;
                }
            };

            var result = expr.Evaluate();
            return Convert.ToDouble(result, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Computes the numerical derivative using central difference.
        /// </summary>
        private static double NumericalDerivative(string leftExpr, string rightExpr, double x)
        {
            double h = 1e-7;
            double fPlusH = EvaluateDifference(leftExpr, rightExpr, x + h);
            double fMinusH = EvaluateDifference(leftExpr, rightExpr, x - h);
            return (fPlusH - fMinusH) / (2 * h);
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
        private static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
    }
}
