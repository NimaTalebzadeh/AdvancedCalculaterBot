namespace AdvancedCalculatorBot.Services.Equations.TranscendentalSolving;

/// <summary>
/// Abstract base for all expression tree nodes.
/// </summary>
public abstract record ExprNode
{
    /// <summary>Evaluate this node with the given variable bindings.</summary>
    public abstract double Eval(Dictionary<string, double> vars);
}

/// <summary>Constant numeric value (e.g. 5, 3.14).</summary>
public sealed record ConstNode(double Value) : ExprNode
{
    public override double Eval(Dictionary<string, double> vars) => Value;
}

/// <summary>Variable reference (e.g. x, y).</summary>
public sealed record VarNode(string Name) : ExprNode
{
    public override double Eval(Dictionary<string, double> vars)
    {
        return vars.TryGetValue(Name, out double v) ? v : double.NaN;
    }
}

/// <summary>Binary operation: left op right.</summary>
public sealed record BinOpNode(char Op, ExprNode Left, ExprNode Right) : ExprNode
{
    public override double Eval(Dictionary<string, double> vars)
    {
        double l = Left.Eval(vars);
        double r = Right.Eval(vars);
        return Op switch
        {
            '+' => l + r,
            '-' => l - r,
            '*' => l * r,
            '/' => r == 0 ? double.NaN : l / r,
            '^' => Math.Pow(l, r),
            _ => double.NaN
        };
    }
}

/// <summary>Unary minus: -operand.</summary>
public sealed record UnaryMinusNode(ExprNode Operand) : ExprNode
{
    public override double Eval(Dictionary<string, double> vars) => -Operand.Eval(vars);
}

/// <summary>Named function call (e.g. sin, cos, exp).</summary>
public sealed record FuncNode(string Name, ExprNode Arg) : ExprNode
{
    public override double Eval(Dictionary<string, double> vars)
    {
        double a = Arg.Eval(vars);
        return Name.ToLowerInvariant() switch
        {
            "sin" => Math.Sin(a * Math.PI / 180.0),
            "cos" => Math.Cos(a * Math.PI / 180.0),
            "tan" => Math.Tan(a * Math.PI / 180.0),
            "asin" => Math.Asin(Math.Clamp(a, -1, 1)) * 180.0 / Math.PI,
            "acos" => Math.Acos(Math.Clamp(a, -1, 1)) * 180.0 / Math.PI,
            "atan" => Math.Atan(a) * 180.0 / Math.PI,
            "sinh" => Math.Sinh(a),
            "cosh" => Math.Cosh(a),
            "tanh" => Math.Tanh(a),
            "log" => a <= 0 ? double.NaN : Math.Log10(a),
            "ln" => a <= 0 ? double.NaN : Math.Log(a),
            "sqrt" => a < 0 ? double.NaN : Math.Sqrt(a),
            "exp" => Math.Exp(a),
            "abs" => Math.Abs(a),
            "floor" => Math.Floor(a),
            "ceil" => Math.Ceiling(a),
            "round" => Math.Round(a),
            _ => double.NaN
        };
    }
}
