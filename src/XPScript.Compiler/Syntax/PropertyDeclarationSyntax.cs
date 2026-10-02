namespace XPScript.Compiler.Syntax;

public sealed class PropertyDeclarationSyntax(SyntaxToken? visibility, SyntaxToken propertyKeyword, SyntaxToken accessorKeyword, SyntaxToken identifier, SyntaxToken? asKeyword, TypeSyntax? type, IReadOnlyList<StatementSyntax> statements, SyntaxToken endKeyword, SyntaxToken endPropertyKeyword) : SyntaxNode
{
    public SyntaxToken? Visibility { get; } = visibility;
    public SyntaxToken PropertyKeyword { get; } = propertyKeyword;
    public SyntaxToken AccessorKeyword { get; } = accessorKeyword;
    public SyntaxToken Identifier { get; } = identifier;
    public SyntaxToken? AsKeyword { get; } = asKeyword;
    public TypeSyntax? Type { get; } = type;
    public IReadOnlyList<StatementSyntax> Statements { get; } = statements;
    public SyntaxToken EndKeyword { get; } = endKeyword;
    public SyntaxToken EndPropertyKeyword { get; } = endPropertyKeyword;
    public bool IsGetter => AccessorKeyword.Kind == SyntaxKind.GetKeyword;
    public bool IsSetter => AccessorKeyword.Kind is SyntaxKind.LetKeyword or SyntaxKind.SetKeyword;
    public override SyntaxKind Kind => SyntaxKind.PropertyDeclaration;
    public override TextSpan Span => TextSpan.FromBounds(Visibility?.Span.Start ?? PropertyKeyword.Span.Start, EndPropertyKeyword.Span.End);
}
