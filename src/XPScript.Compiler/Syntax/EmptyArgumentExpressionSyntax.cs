namespace XPScript.Compiler.Syntax;

public sealed class EmptyArgumentExpressionSyntax(TextSpan span) : ExpressionSyntax
{
    public override SyntaxKind Kind => SyntaxKind.EmptyArgument;
    public override TextSpan Span => span;
}
