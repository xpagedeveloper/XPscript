namespace XPScript.Compiler.Syntax;

/// <summary>Declares a procedure-local control-flow target.</summary>
public sealed class LabelStatementSyntax(SyntaxToken identifier, SyntaxToken colonToken) : StatementSyntax
{
    public SyntaxToken Identifier { get; } = identifier;
    public SyntaxToken ColonToken { get; } = colonToken;
    public override SyntaxKind Kind => SyntaxKind.LabelStatement;
    public override TextSpan Span => TextSpan.FromBounds(Identifier.Span.Start, ColonToken.Span.End);
}
