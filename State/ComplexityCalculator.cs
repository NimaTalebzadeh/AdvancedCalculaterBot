using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AdvancedCalculaterBot.State;

public static class ComplexityCalculator
{
    private static readonly Regex FunctionPattern = new(@"[a-zA-Z]+\(", RegexOptions.Compiled);

    public static int Compute(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return 0;

        int lengthScore = Math.Min(expression.Length, 50); // Cap length contribution
        int operatorCount = expression.Count(c => "+-*/^%".Contains(c));
        int functionCount = FunctionPattern.Matches(expression).Count;
        int parenthesesDepth = 0;
        int maxDepth = 0;

        foreach (char c in expression)
        {
            if (c == '(') { parenthesesDepth++; maxDepth = Math.Max(maxDepth, parenthesesDepth); }
            else if (c == ')') parenthesesDepth--;
        }

        int rawScore = (int)(lengthScore * 0.5)
                     + operatorCount * 3
                     + functionCount * 12
                     + maxDepth * 4;

        return Math.Min(100, Math.Max(0, rawScore));
    }
}
