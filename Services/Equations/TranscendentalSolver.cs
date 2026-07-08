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
    public static class LegacyTranscendentalSolver
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

            string leftExpr = PreprocessForNCalc(lhs);
            string rightExpr = PreprocessForNCalc(rhs);

            var solutions = new List<double>();

            // Coarse grid scan to find sign changes
            double lo = -500, hi = 500, step = 0.05;
            double fLo = SafeEval(leftExpr, rightExpr, lo);
            for (double xi = lo + step; xi <= hi; xi += step)
            {
                double fXi = SafeEval(leftExpr, rightExpr, xi);

                if (double.IsInfinity(fLo) || double.IsInfinity(fXi))
                {
                    fLo = fXi;
                    continue;
                }

                if (Math.Abs(fXi) < Tolerance && !solutions.Any(s => Math.Abs(s - xi) < 0.5))
                {
                    solutions.Add(xi);
                    fLo = fXi;
                    continue;
                }

                if (fLo * fXi < 0)
                {
                    double a = xi - step, b = xi;
                    double fA = fLo, fB = fXi;
                    for (int bisect = 0; bisect < 80; bisect++)
                    {
                        double mid = (a + b) / 2.0;
                        double fMid = SafeEval(leftExpr, rightExpr, mid);
                        if (double.IsInfinity(fMid)) break;
                        if (Math.Abs(fMid) < Tolerance || (b - a) / 2.0 < 1e-12)
                        {
                            if (!solutions.Any(s => Math.Abs(s - mid) < 1e-3))
                                solutions.Add(mid);
                            break;
                        }
                        if (fA * fMid < 0) { b = mid; fB = fMid; }
                        else { a = mid; fA = fMid; }
                    }
                }

                fLo = fXi;
            }

            // Refine each solution with Newton-Raphson
            var refined = new List<double>();
            foreach (double x0 in solutions)
            {
                double x = x0;
                for (int iter = 0; iter < 50; iter++)
                {
                    double f = SafeEval(leftExpr, rightExpr, x);
                    if (Math.Abs(f) < Tolerance) break;
                    double fPrime = NumericalDerivative(leftExpr, rightExpr, x);
                    if (Math.Abs(fPrime) < 1e-14) break;
                    double xNew = x - f / fPrime;
                    if (Math.Abs(xNew - x) < 1e-12) break;
                    x = xNew;
                }
                double finalF = SafeEval(leftExpr, rightExpr, x);
                if (Math.Abs(finalF) < 1e-6 && !refined.Any(r => Math.Abs(r - x) < 1e-3))
                    refined.Add(Math.Round(x, 10));
            }

            return refined.Count > 0 ? refined.ToArray() : Array.Empty<double>();
        }

        private static double SafeEval(string leftExpr, string rightExpr, double x)
        {
            try
            {
                return EvaluateDifference(leftExpr, rightExpr, x);
            }
            catch
            {
                return double.MaxValue;
            }
        }

        /// <summary>
        /// Preprocesses the expression for NCalc by inserting multiplication operators where needed.
        /// Handles cases like ""2sin(x)"" -> ""2*sin(x)"", ""x2"" -> ""x2"" (treated as single variable), etc.
        /// </summary>
        private static string PreprocessForNCalc(string expression)
        {
            string result = expression;

            result = ReplaceTrigWithRadians(result, "sin");
            result = ReplaceTrigWithRadians(result, "cos");
            result = ReplaceTrigWithRadians(result, "tan");
            result = ReplaceTrigWithRadians(result, "asin", true);
            result = ReplaceTrigWithRadians(result, "acos", true);
            result = ReplaceTrigWithRadians(result, "atan", true);

            var sb = new System.Text.StringBuilder();
            result = result.Trim();

            for (int i = 0; i < result.Length; i++)
            {
                char current = result[i];
                sb.Append(current);

                if (i == result.Length - 1) break;

                char next = result[i + 1];
                bool shouldAddStar = false;

                if (char.IsDigit(current) && (char.IsLetter(next) || next == '('))
                    shouldAddStar = true;
                else if (current == ')' && (char.IsLetter(next) || char.IsDigit(next) || next == '('))
                    shouldAddStar = true;
                else if (char.IsLetter(current) && next == '(')
                {
                    int j = i;
                    while (j >= 0 && char.IsLetter(result[j]))
                        j--;
                    j++;
                    int length = i - j + 1;
                    if (length > 0 && length <= 20)
                    {
                        string potentialFunc = result.Substring(j, length).ToLowerInvariant();
                        bool isKnownFunction =
                            potentialFunc == "sin" || potentialFunc == "cos" ||
                            potentialFunc == "tan" || potentialFunc == "log" ||
                            potentialFunc == "ln" || potentialFunc == "exp" ||
                            potentialFunc == "asin" || potentialFunc == "acos" ||
                            potentialFunc == "atan";
                        shouldAddStar = !isKnownFunction;
                    }
                }

                if (shouldAddStar)
                    sb.Append('*');
            }

            return sb.ToString();
        }

        private static string ReplaceTrigWithRadians(string expression, string funcName, bool inverseTrig = false)
        {
            return expression;
        }

        private static int FindMatchingParen(string s, int openIndex)
        {
            int depth = 0;
            for (int i = openIndex; i < s.Length; i++)
            {
                if (s[i] == '(') depth++;
                else if (s[i] == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
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
            try
            {
                var expr = new Expression(expression);
                expr.Parameters["x"] = x;
                expr.Parameters["pi"] = Math.PI;

                expr.EvaluateFunction += (name, args) =>
                {
                    double paramValue = Convert.ToDouble(args.Parameters.Evaluate(0), CultureInfo.InvariantCulture);

                switch (name.ToLowerInvariant())
                {
                    case "sin":
                        args.Result = Math.Sin(paramValue * Math.PI / 180.0);
                        break;
                    case "cos":
                        args.Result = Math.Cos(paramValue * Math.PI / 180.0);
                        break;
                    case "tan":
                        args.Result = Math.Tan(paramValue * Math.PI / 180.0);
                        break;
                    case "asin":
                        args.Result = Math.Asin(Math.Max(-1, Math.Min(1, paramValue))) * 180.0 / Math.PI;
                        break;
                    case "acos":
                        args.Result = Math.Acos(Math.Max(-1, Math.Min(1, paramValue))) * 180.0 / Math.PI;
                        break;
                    case "atan":
                        args.Result = Math.Atan(paramValue) * 180.0 / Math.PI;
                        break;
                    case "log":
                        args.Result = paramValue <= 0 ? double.NaN : Math.Log10(paramValue);
                        break;
                    case "ln":
                        args.Result = paramValue <= 0 ? double.NaN : Math.Log(paramValue);
                        break;
                    case "exp":
                        args.Result = Math.Exp(paramValue);
                        break;
                }
            };

var result = expr.Evaluate();
            return Convert.ToDouble(result, CultureInfo.InvariantCulture);
        }
        catch
        {
            return double.MaxValue;
        }
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
