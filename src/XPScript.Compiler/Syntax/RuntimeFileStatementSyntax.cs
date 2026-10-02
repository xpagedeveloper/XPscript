namespace XPScript.Compiler.Syntax;

public sealed class RuntimeFileStatementSyntax(
    SyntaxToken command,
    IReadOnlyList<ExpressionSyntax> arguments) : StatementSyntax
{
    public SyntaxToken Command { get; } = command;
    public IReadOnlyList<ExpressionSyntax> Arguments { get; } = arguments;
    public override SyntaxKind Kind => SyntaxKind.RuntimeFileStatement;
    public override TextSpan Span => Arguments.Count == 0 ? Command.Span : TextSpan.FromBounds(Command.Span.Start, Arguments[^1].Span.End);
}
