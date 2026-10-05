using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Emission;

public sealed record GeneratedSourceMapping(int GeneratedLine, string SourcePath, TextSpan Span, SourcePosition Position);

public sealed record BoundEmissionResult(string Code, IReadOnlyList<GeneratedSourceMapping> SourceMappings);
