using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Binding;

public sealed class ExpressionBinder
{
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    public IReadOnlyList<SyntaxDiagnostic> Diagnostics => _diagnostics;

    public BoundExpression Bind(ExpressionSyntax syntax) => syntax switch
    {
        LiteralExpressionSyntax literal => BindLiteral(literal),
        UnaryExpressionSyntax unary => BindUnary(unary),
        BinaryExpressionSyntax binary => BindBinary(binary),
        _ => Error(syntax, $"Binding is not implemented for {syntax.Kind}.")
    };

    private BoundExpression BindLiteral(LiteralExpressionSyntax syntax)
    {
        if (syntax.LiteralToken.Kind == SyntaxKind.TrueKeyword)
            return new BoundLiteralExpression(true, typeof(bool));
        if (syntax.LiteralToken.Kind == SyntaxKind.FalseKeyword)
            return new BoundLiteralExpression(false, typeof(bool));
        if (syntax.LiteralToken.Kind == SyntaxKind.NumberToken)
            return new BoundLiteralExpression(syntax.Value, typeof(long));
        if (syntax.LiteralToken.Kind == SyntaxKind.StringToken)
            return new BoundLiteralExpression(syntax.Value, typeof(string));
        return Error(syntax, "Unsupported literal.");
    }

    private BoundExpression BindUnary(UnaryExpressionSyntax syntax)
    {
        var operand = Bind(syntax.Operand);
        if (syntax.OperatorToken.Kind == SyntaxKind.NotKeyword && operand.Type == typeof(bool))
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, typeof(bool));
        if (syntax.OperatorToken.Kind is SyntaxKind.PlusToken or SyntaxKind.MinusToken && operand.Type == typeof(long))
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, typeof(long));
        return Error(syntax, $"Unary operator {syntax.OperatorToken.Text} is not defined for {operand.Type.Name}.");
    }

    private BoundExpression BindBinary(BinaryExpressionSyntax syntax)
    {
        var left = Bind(syntax.Left);
        var right = Bind(syntax.Right);
        var op = syntax.OperatorToken.Kind;

        if (op is SyntaxKind.AndKeyword or SyntaxKind.OrKeyword && left.Type == typeof(bool) && right.Type == typeof(bool))
            return new BoundBinaryExpression(left, op, right, typeof(bool));
        if (op is SyntaxKind.PlusToken or SyntaxKind.MinusToken or SyntaxKind.StarToken or SyntaxKind.SlashToken &&
            left.Type == typeof(long) && right.Type == typeof(long))
            return new BoundBinaryExpression(left, op, right, typeof(long));
        if (op is SyntaxKind.EqualsToken or SyntaxKind.LessGreaterToken)
            return new BoundBinaryExpression(left, op, right, typeof(bool));

        return Error(syntax, $"Binary operator {syntax.OperatorToken.Text} is not defined for {left.Type.Name} and {right.Type.Name}.");
    }

    private BoundExpression Error(SyntaxNode syntax, string message)
    {
        _diagnostics.Add(new SyntaxDiagnostic("XPS1012", message, syntax.Span));
        return new BoundLiteralExpression(null, typeof(object));
    }
}
