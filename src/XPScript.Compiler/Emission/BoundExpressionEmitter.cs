using System.Globalization;
using XPScript.Compiler.Binding;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Emission;

public sealed class BoundExpressionEmitter
{
    public string Emit(BoundExpression expression) => expression switch
    {
        BoundLiteralExpression literal => EmitLiteral(literal),
        BoundNameExpression name => name.Symbol.Name,
        BoundCallExpression call => $"{call.Function.Name}({string.Join(\", \", call.Arguments.Select(Emit))})",
        BoundUnaryExpression unary => $"({EmitUnaryOperator(unary.OperatorKind)}{Emit(unary.Operand)})",
        BoundBinaryExpression binary => $"({Emit(binary.Left)} {EmitBinaryOperator(binary.OperatorKind)} {Emit(binary.Right)})",
        _ => throw new NotSupportedException($"Emission is not implemented for {expression.Kind}.")
    };

    private static string EmitLiteral(BoundLiteralExpression expression) => expression.Value switch
    {
        null => "null",
        bool value => value ? "true" : "false",
        string value => $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"",
        IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
        _ => expression.Value.ToString() ?? "null"
    };

    private static string EmitUnaryOperator(SyntaxKind kind) => kind switch
    {
        SyntaxKind.NotKeyword => "!",
        SyntaxKind.PlusToken => "+",
        SyntaxKind.MinusToken => "-",
        _ => throw new NotSupportedException($"Unary operator {kind} is not supported.")
    };

    private static string EmitBinaryOperator(SyntaxKind kind) => kind switch
    {
        SyntaxKind.PlusToken => "+",
        SyntaxKind.MinusToken => "-",
        SyntaxKind.StarToken => "*",
        SyntaxKind.SlashToken => "/",
        SyntaxKind.AndKeyword => "&&",
        SyntaxKind.OrKeyword => "||",
        SyntaxKind.EqualsToken => "==",
        SyntaxKind.LessGreaterToken => "!=",
        _ => throw new NotSupportedException($"Binary operator {kind} is not supported.")
    };
}
