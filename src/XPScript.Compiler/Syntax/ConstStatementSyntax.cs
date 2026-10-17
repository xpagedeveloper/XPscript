namespace XPScript.Compiler.Syntax;

public sealed class ConstStatementSyntax(TextSpan span) : StatementSyntax
{
    public override SyntaxKind Kind => SyntaxKind.ConstStatement;
    public override TextSpan Span { get; } = span;
}
