using System.Text.RegularExpressions;

namespace AdvancedCalculatorBot.Services.Mathematics;

/// <summary>
/// Parses and validates mathematical expressions with comprehensive error checking.
/// </summary>
public static class MathExpressionParser
{
    private static readonly string[] ValidVariables = { "x", "y", "z", "a", "b", "c" };

    /// <summary>
    /// Validates a mathematical expression.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) Validate(string expression)
    {
        expression = expression?.Trim() ?? "";
        if (string.IsNullOrEmpty(expression))
            return (false, "Expression cannot be empty.");

        if (expression.Length > 5000)
            return (false, "Expression is too long.");

        try
        {
            CheckForMatchingParentheses(expression);
            CheckForValidCharacters(expression);
            CheckForValidOperators(expression);
            CheckForValidFunctionNames(expression);
            CheckForValidVariables(expression);
            CheckForValidNumbers(expression);
            CheckForValidSpacing(expression);

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Checks if parentheses are properly matched.
    /// </summary>
    private static void CheckForMatchingParentheses(string expression)
    {
        int openCount = 0;
        int closeCount = 0;

        foreach (char c in expression)
        {
            if (c == '(')
                openCount++;
            else if (c == ')')
                closeCount++;

            if (closeCount > openCount)
                throw new ArgumentException("Unmatched closing parenthesis ')'");
        }

        if (openCount != closeCount)
            throw new ArgumentException($"Mismatched parentheses: {openCount} open, {closeCount} close");
    }

    /// <summary>
    /// Checks for valid characters in the expression.
    /// </summary>
    private static void CheckForValidCharacters(string expression)
    {
        foreach (char c in expression)
        {
            if (!IsValidCharacter(c))
                throw new ArgumentException($"Invalid character '{c}' in expression.");
        }
    }

    /// <summary>
    /// Checks if a character is valid in a mathematical expression.
    /// </summary>
    private static bool IsValidCharacter(char c)
    {
        return char.IsDigit(c) ||
               char.IsLetter(c) ||
               c == '+' || c == '-' || c == '*' || c == '/' || c == '^' ||
               c == '(' || c == ')' ||
               c == '.' || c == ',' ||
               char.IsWhiteSpace(c);
    }

    /// <summary>
    /// Checks for valid operators.
    /// </summary>
    private static void CheckForValidOperators(string expression)
    {
        string operators = "+-*/^";
        for (int i = 0; i < expression.Length - 1; i++)
        {
            char current = expression[i];
            char next = expression[i + 1];

            if (operators.Contains(current) && operators.Contains(next))
                throw new ArgumentException($"Invalid operator sequence: '{current}{next}'");
        }
    }

    /// <summary>
    /// Checks for valid function names.
    /// </summary>
    private static void CheckForValidFunctionNames(string expression)
    {
        var matches = Regex.Matches(expression, @"\b[a-zA-Z_][a-zA-Z0-9_]*\s*\(");

        foreach (Match match in matches)
        {
            string functionName = match.Groups[0].Value.Split('(')[0].Trim();
            if (!IsMathematicalFunction(functionName) && !ValidVariables.Contains(functionName.ToLower()))
                throw new ArgumentException($"Unknown function or variable: '{functionName}'");
        }
    }

    /// <summary>
    /// Checks for valid variable names.
    /// </summary>
    private static void CheckForValidVariables(string expression)
    {
        var matches = Regex.Matches(expression, @"\b[a-zA-Z_][a-zA-Z0-9_]*\b");

        foreach (Match match in matches)
        {
            string variableName = match.Value.ToLower();

            if (IsMathematicalFunction(variableName))
                continue;

            if (!ValidVariables.Contains(variableName))
                throw new ArgumentException($"Variable '{match.Value}' is not supported. Only {string.Join(", ", ValidVariables)} are supported.");
        }
    }

    /// <summary>
    /// Checks for valid numbers.
    /// </summary>
    private static void CheckForValidNumbers(string expression)
    {
        var matches = Regex.Matches(expression, @"\b\d+\.\d+\b|\b\d+\b|\b\.\d+\b");

        foreach (Match match in matches)
        {
            string numberStr = match.Value;
            if (numberStr.Contains('.') && numberStr.Count(c => c == '.') > 1)
                throw new ArgumentException($"Invalid number format: '{numberStr}'");
        }
    }

    /// <summary>
    /// Checks for valid spacing.
    /// </summary>
    private static void CheckForValidSpacing(string expression)
    {
        string validSpacing = @"^\s*([+-]?\s*\d*\.?\d*\s*x\^?\d*\s*|\s*[\(\)\+\-\*\/\^\s]*)+$";
        if (!Regex.IsMatch(expression, validSpacing))
            throw new ArgumentException("Invalid spacing or structure in expression.");
    }

    /// <summary>
    /// Checks if a name is a mathematical function.
    /// </summary>
    private static bool IsMathematicalFunction(string name)
    {
        return MathFunctionRegistry.IsFunctionRegistered(name);
    }

    /// <summary>
    /// Extracts the mathematical operation from the expression.
    /// </summary>
    public static (string Operation, string Arguments) ExtractOperation(string expression)
    {
        expression = expression.Trim();

        foreach (var operation in new[] { "solve", "d(", "int(", "lim(", "simplify(", "expand(", "factor(" })
        {
            if (expression.StartsWith(operation, StringComparison.OrdinalIgnoreCase))
            {
                string args = expression.Substring(operation.Length);

                if (args.EndsWith(')'))
                    args = args.Substring(0, args.Length - 1);

                return (operation.ToLowerInvariant(), args);
            }
        }

        return ("evaluate", expression);
    }

    /// <summary>
    /// Normalizes the expression by removing whitespace.
    /// </summary>
    public static string Normalize(string expression)
    {
        return expression?.Replace(" ", "").Replace("\t", "").Replace("\n", "") ?? "";
    }

    /// <summary>
    /// Checks if the expression contains a valid mathematical operation.
    /// </summary>
    public static bool IsMathematicalOperation(string expression)
    {
        return expression.StartsWith("solve(", StringComparison.OrdinalIgnoreCase) ||
               expression.StartsWith("d(", StringComparison.OrdinalIgnoreCase) ||
               expression.StartsWith("int(", StringComparison.OrdinalIgnoreCase) ||
               expression.StartsWith("lim(", StringComparison.OrdinalIgnoreCase) ||
               expression.StartsWith("simplify(", StringComparison.OrdinalIgnoreCase) ||
               expression.StartsWith("expand(", StringComparison.OrdinalIgnoreCase) ||
               expression.StartsWith("factor(", StringComparison.OrdinalIgnoreCase);
    }
}
