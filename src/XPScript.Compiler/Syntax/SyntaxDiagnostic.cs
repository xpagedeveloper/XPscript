namespace XPScript.Compiler.Syntax;

public sealed record SyntaxDiagnostic(string Code, string Message, TextSpan Span);
