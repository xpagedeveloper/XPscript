namespace XPScript.Compiler.Syntax;

public sealed class DimStatementSyntax(
    SyntaxToken dimKeyword,
    SyntaxToken identifierToken,
    SyntaxToken? asKeyword,
    SyntaxToken? typeNameToken,
    SyntaxToken? equalsToken,
    ExpressionSyntax? initializer,
    bool isArray = false,
    bool isList = false,
    int? arrayLength = null) : StatementSyntax
{
    public SyntaxToken DimKeyword { get; } = dimKeyword;
    public bool IsStatic => DimKeyword.Text.Equals("Static", StringComparison.OrdinalIgnoreCase);
    public SyntaxToken IdentifierToken { get; } = identifierToken;
    public SyntaxToken? AsKeyword { get; } = asKeyword;
    public SyntaxToken? TypeNameToken { get; } = typeNameToken;
    public SyntaxToken? EqualsToken { get; } = equalsToken;
    public ExpressionSyntax? Initializer { get; } = initializer;
    public bool IsArray { get; } = isArray;
    public bool IsList { get; } = isList;
    public int? ArrayLength { get; } = arrayLength;

    public override SyntaxKind Kind => SyntaxKind.DimStatement;

    public override TextSpan Span
    {
        get
        {
            var end = Initializer?.Span.End
                ?? TypeNameToken?.Span.End
                ?? IdentifierToken.Span.End;
            return new TextSpan(DimKeyword.Span.Start, end - DimKeyword.Span.Start);
        }
    }
}
