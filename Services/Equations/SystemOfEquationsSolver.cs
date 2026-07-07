using System.Globalization;
using System.Text.RegularExpressions;

namespace AdvancedCalculaterBot.Services.Equations;

/// <summary>
/// Solves systems of linear equations using Gaussian elimination with partial pivoting.
/// Supports systems like: "x + y = 3, x - y = 1" or "2x + 3y - z = 1, x - y + 2z = 3, 3x + 2y + z = 4"
/// </summary>
public static class SystemOfEquationsSolver
{
    private const double ZeroTolerance = 1e-9;

    /// <summary>
    /// Checks if the input looks like a system of equations (contains comma-separated equations with = signs).
    /// </summary>
    public static bool IsSystemOfEquations(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;

        // Must contain at least one comma
        if (!input.Contains(',')) return false;

        // Split by comma and check each part is an equation with a variable
        var equations = SplitEquations(input);
        if (equations.Length < 2) return false;

        // All parts must contain '='
        return equations.All(eq => eq.Contains('='));
    }

    /// <summary>
    /// Solves a system of linear equations and returns a human-readable result.
    /// </summary>
    public static string Solve(string input)
    {
        try
        {
            var equations = SplitEquations(input);
            if (equations.Length < 2)
                return "A system of equations requires at least 2 equations.";

            // Detect all variables
            var variables = DetectVariables(equations);
            if (variables.Count == 0)
                return "No variables found in the equations.";

            if (equations.Length != variables.Count)
                return $"Found {equations.Length} equations but {variables.Count} variables. " +
                       $"Need equal numbers for a unique solution.";

            // Parse each equation into coefficients
            int n = equations.Length;
            double[,] matrix = new double[n, n + 1]; // augmented matrix

            for (int i = 0; i < n; i++)
            {
                var coeffs = ParseEquationCoefficients(equations[i], variables);
                for (int j = 0; j < variables.Count; j++)
                {
                    matrix[i, j] = coeffs.ContainsKey(variables[j]) ? coeffs[variables[j]] : 0;
                }
                matrix[i, n] = coeffs.ContainsKey("__constant") ? -coeffs["__constant"] : 0;
            }

            // Solve using Gaussian elimination
            var solution = GaussianElimination(matrix, n);
            if (solution == null)
                return "This system has no unique solution (inconsistent or dependent).";

            // Format result
            var resultLines = new List<string>();
            for (int i = 0; i < variables.Count; i++)
            {
                string val = FormatNumber(solution[i]);
                resultLines.Add($"{variables[i]} = {val}");
            }

            return string.Join("\n", resultLines);
        }
        catch (Exception ex)
        {
            return $"Error solving system: {ex.Message}";
        }
    }

    private static string[] SplitEquations(string input)
    {
        // Split by comma, but respect parentheses
        var equations = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '(') depth++;
            else if (input[i] == ')') depth--;
            else if (input[i] == ',' && depth == 0)
            {
                string eq = input.Substring(start, i - start).Trim();
                if (eq.Length > 0) equations.Add(eq);
                start = i + 1;
            }
        }

        string last = input.Substring(start).Trim();
        if (last.Length > 0) equations.Add(last);

        return equations.ToArray();
    }

    private static List<string> DetectVariables(string[] equations)
    {
        var varSet = new HashSet<string>();

        foreach (var eq in equations)
        {
            var matches = Regex.Matches(eq, @"(?<![a-zA-Z])([a-zA-Z])(?![a-zA-Z(])");
            foreach (Match m in matches)
            {
                string v = m.Value.ToLower();
                if (v.Length == 1 && char.IsLetter(v[0]))
                    varSet.Add(v);
            }
        }

        // Sort alphabetically for consistent ordering
        return varSet.OrderBy(v => v).ToList();
    }

    private static Dictionary<string, double> ParseEquationCoefficients(string equation, List<string> variables)
    {
        var result = new Dictionary<string, double>();

        int eqIndex = equation.IndexOf('=');
        if (eqIndex < 0) return result;

        string lhs = equation.Substring(0, eqIndex).Trim();
        string rhs = equation.Substring(eqIndex + 1).Trim();

        // Parse LHS coefficients (with sign), then subtract RHS
        var lhsCoeffs = ParseExpressionCoefficients(lhs, variables);
        var rhsCoeffs = ParseExpressionCoefficients(rhs, variables);

        foreach (var kv in lhsCoeffs)
        {
            double rhsVal = rhsCoeffs.ContainsKey(kv.Key) ? rhsCoeffs[kv.Key] : 0;
            result[kv.Key] = kv.Value - rhsVal;
        }

        foreach (var kv in rhsCoeffs)
        {
            if (!result.ContainsKey(kv.Key))
                result[kv.Key] = -kv.Value;
        }

        return result;
    }

    private static Dictionary<string, double> ParseExpressionCoefficients(string expression, List<string> variables)
    {
        var result = new Dictionary<string, double>();
        expression = expression.Replace(" ", "");

        // Split into additive terms, keeping the sign with each term
        var terms = new List<string>();
        int start = 0;
        for (int i = 1; i < expression.Length; i++)
        {
            if ((expression[i] == '+' || expression[i] == '-') && i > 0)
            {
                terms.Add(expression.Substring(start, i - start));
                start = i;
            }
        }
        terms.Add(expression.Substring(start));

        foreach (var term in terms)
        {
            if (string.IsNullOrEmpty(term)) continue;

            bool negative = term[0] == '-';
            string body = term[0] == '+' || term[0] == '-' ? term.Substring(1) : term;

            // Try to match: (number)(variable) or just (number) or just (variable)
            var m = Regex.Match(body, @"^(\d*\.?\d*)([a-zA-Z]?)$");
            if (!m.Success) continue;

            string numStr = m.Groups[1].Value;
            string varName = m.Groups[2].Value.ToLower();

            double coeff = 1;
            if (!string.IsNullOrEmpty(numStr))
                coeff = double.Parse(numStr, CultureInfo.InvariantCulture);
            if (negative) coeff = -coeff;

            if (!string.IsNullOrEmpty(varName))
            {
                if (result.ContainsKey(varName))
                    result[varName] += coeff;
                else
                    result[varName] = coeff;
            }
            else
            {
                if (result.ContainsKey("__constant"))
                    result["__constant"] += coeff;
                else
                    result["__constant"] = coeff;
            }
        }

        return result;
    }

    /// <summary>
    /// Gaussian elimination with partial pivoting. Returns null if no unique solution.
    /// </summary>
    private static double[]? GaussianElimination(double[,] matrix, int n)
    {
        // Forward elimination
        for (int col = 0; col < n; col++)
        {
            // Find pivot
            int maxRow = col;
            double maxVal = Math.Abs(matrix[col, col]);
            for (int row = col + 1; row < n; row++)
            {
                if (Math.Abs(matrix[row, col]) > maxVal)
                {
                    maxVal = Math.Abs(matrix[row, col]);
                    maxRow = row;
                }
            }

            // Swap rows
            if (maxRow != col)
            {
                for (int j = 0; j <= n; j++)
                {
                    double temp = matrix[col, j];
                    matrix[col, j] = matrix[maxRow, j];
                    matrix[maxRow, j] = temp;
                }
            }

            // Check for zero pivot
            if (Math.Abs(matrix[col, col]) < ZeroTolerance)
                return null; // No unique solution

            // Eliminate below
            for (int row = col + 1; row < n; row++)
            {
                double factor = matrix[row, col] / matrix[col, col];
                for (int j = col; j <= n; j++)
                {
                    matrix[row, j] -= factor * matrix[col, j];
                }
            }
        }

        // Back substitution
        var solution = new double[n];
        for (int row = n - 1; row >= 0; row--)
        {
            if (Math.Abs(matrix[row, row]) < ZeroTolerance)
                return null;

            double sum = matrix[row, n];
            for (int j = row + 1; j < n; j++)
            {
                sum -= matrix[row, j] * solution[j];
            }
            solution[row] = sum / matrix[row, row];
        }

        return solution;
    }

    private static string FormatNumber(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < ZeroTolerance)
            return Math.Round(value).ToString(CultureInfo.InvariantCulture);
        return value.ToString("G10", CultureInfo.InvariantCulture);
    }
}
