namespace XPScript.Compiler.Syntax;

public sealed record LexerDiagnostic(string Code, string Message, TextSpan Span);
