namespace XPScript.Compiler.Syntax;

public sealed record SyntaxToken(SyntaxKind Kind, string Text, object? Value, TextSpan Span);
