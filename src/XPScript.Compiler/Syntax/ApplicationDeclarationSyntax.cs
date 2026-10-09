namespace XPScript.Compiler.Syntax;

public abstract class ApplicationDeclarationSyntax(string text, TextSpan span) : DeclarationSyntax
{
    public string Text { get; } = text;
    protected TextSpan DeclarationSpan { get; } = span;
}

public sealed class ApplicationOptionDeclarationSyntax(string text, TextSpan span) : ApplicationDeclarationSyntax(text, span)
{
    public override SyntaxKind Kind => SyntaxKind.ApplicationOptionDeclaration;
    public override TextSpan Span => DeclarationSpan;
}

public sealed class ApplicationConstDeclarationSyntax(string text, TextSpan span) : ApplicationDeclarationSyntax(text, span)
{
    public override SyntaxKind Kind => SyntaxKind.ApplicationConstDeclaration;
    public override TextSpan Span => DeclarationSpan;
}

public sealed class ApplicationDeclareDeclarationSyntax(string text, TextSpan span) : ApplicationDeclarationSyntax(text, span)
{
    public override SyntaxKind Kind => SyntaxKind.ApplicationDeclareDeclaration;
    public override TextSpan Span => DeclarationSpan;
}
