namespace AdvancedCalculatorBot.Services.Equations.TranscendentalSolving;

/// <summary>
/// Recursive-descent parser: string → ExprNode AST.
/// Supports +, -, *, /, ^, unary minus, parentheses, named functions, constants (pi, e).
/// Whitespace is ignored.
/// </summary>
public sealed class ExpressionParser
{
    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "sin","cos","tan","asin","acos","atan",
        "sinh","cosh","tanh","log","ln","sqrt",
        "exp","abs","floor","ceil","round"
    };

    private static readonly Dictionary<string, double> Constants = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pi"] = Math.PI,
        ["e"] = Math.E
    };

    private string _src = "";
    private int _pos;

    /// <summary>Parse a full expression string into an AST.</summary>
    public ExprNode Parse(string input)
    {
        _src = input.Replace(" ", "").Replace("\t", "").Replace("\n", "");
        _pos = 0;
        var node = ParseAddSub();
        if (_pos < _src.Length)
            throw new FormatException($"Unexpected character '{_src[_pos]}' at position {_pos}");
        return node;
    }

    // additive: term (('+' | '-') term)*
    private ExprNode ParseAddSub()
    {
        var left = ParseMulDiv();
        while (_pos < _src.Length && (_src[_pos] == '+' || _src[_pos] == '-'))
        {
            char op = _src[_pos++];
            var right = ParseMulDiv();
            left = new BinOpNode(op, left, right);
        }
        return left;
    }

    // multiplicative: power (('*' | '/') power)*
    private ExprNode ParseMulDiv()
    {
        var left = ParsePower();
        while (_pos < _src.Length && (_src[_pos] == '*' || _src[_pos] == '/'))
        {
            char op = _src[_pos++];
            var right = ParsePower();
            left = new BinOpNode(op, left, right);
        }
        return left;
    }

    // power: unary ('^' unary)*   (right-associative)
    private ExprNode ParsePower()
    {
        var base_node = ParseUnary();
        if (_pos < _src.Length && _src[_pos] == '^')
        {
            _pos++;
            var exp = ParsePower(); // right-associative recursion
            return new BinOpNode('^', base_node, exp);
        }
        return base_node;
    }

    // unary: '-' unary | primary
    private ExprNode ParseUnary()
    {
        if (_pos < _src.Length && _src[_pos] == '-')
        {
            _pos++;
            var operand = ParseUnary();
            return new UnaryMinusNode(operand);
        }
        if (_pos < _src.Length && _src[_pos] == '+')
        {
            _pos++;
            return ParseUnary();
        }
        return ParsePrimary();
    }

    // primary: number | constant | function '(' expr ')' | '(' expr ')'
    private ExprNode ParsePrimary()
    {
        if (_pos >= _src.Length)
            throw new FormatException("Unexpected end of expression");

        char c = _src[_pos];

        // Parenthesized expression
        if (c == '(')
        {
            _pos++;
            var inner = ParseAddSub();
            if (_pos >= _src.Length || _src[_pos] != ')')
                throw new FormatException("Missing closing parenthesis");
            _pos++;
            return inner;
        }

        // Number (integer or decimal)
        if (char.IsDigit(c) || c == '.')
            return ParseNumber();

        // Identifier: constant or function
        if (char.IsLetter(c) || c == '_')
            return ParseIdentifier();

        throw new FormatException($"Unexpected character '{c}' at position {_pos}");
    }

    private ExprNode ParseNumber()
    {
        int start = _pos;
        while (_pos < _src.Length && (char.IsDigit(_src[_pos]) || _src[_pos] == '.'))
            _pos++;

        // Scientific notation: 1e5, 1.5e-3
        if (_pos < _src.Length && (_src[_pos] == 'e' || _src[_pos] == 'E'))
        {
            _pos++;
            if (_pos < _src.Length && (_src[_pos] == '+' || _src[_pos] == '-'))
                _pos++;
            while (_pos < _src.Length && char.IsDigit(_src[_pos]))
                _pos++;
        }

        string numStr = _src.Substring(start, _pos - start);
        if (!double.TryParse(numStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double val))
            throw new FormatException($"Invalid number '{numStr}'");
        return new ConstNode(val);
    }

    private ExprNode ParseIdentifier()
    {
        int start = _pos;
        while (_pos < _src.Length && (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_'))
            _pos++;

        string name = _src.Substring(start, _pos - start);

        // Check for function call: name '('
        if (_pos < _src.Length && _src[_pos] == '(')
        {
            if (!Functions.Contains(name))
                throw new FormatException($"Unknown function '{name}'");
            _pos++; // skip '('
            var arg = ParseAddSub();
            if (_pos >= _src.Length || _src[_pos] != ')')
                throw new FormatException($"Missing closing parenthesis after {name}()");
            _pos++;
            return new FuncNode(name, arg);
        }

        // Check for constant
        if (Constants.TryGetValue(name, out double cVal))
            return new ConstNode(cVal);

        // Otherwise it's a variable
        return new VarNode(name);
    }

    /// <summary>
    /// Quick check: does this equation contain any transcendental functions?
    /// </summary>
    public static bool ContainsTranscendentalFunctions(string equation)
    {
        string lower = equation.ToLower();
        return lower.Contains("sin") || lower.Contains("cos") || lower.Contains("tan") ||
               lower.Contains("asin") || lower.Contains("acos") || lower.Contains("atan") ||
               lower.Contains("sinh") || lower.Contains("cosh") || lower.Contains("tanh") ||
               lower.Contains("log") || lower.Contains("ln") || lower.Contains("sqrt") ||
               lower.Contains("exp") || lower.Contains("abs") || lower.Contains("floor") ||
               lower.Contains("ceil") || lower.Contains("round");
    }
}
