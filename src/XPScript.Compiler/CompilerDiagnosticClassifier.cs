namespace XPScript.Compiler;

internal readonly record struct CompilerDiagnosticClassification(string DiagnosticCode, string Category);

internal static class CompilerDiagnosticClassifier
{
    public static CompilerDiagnosticClassification ClassifyUpstream(string upstreamCode, bool sourceMapped)
    {
        if (!sourceMapped || string.IsNullOrWhiteSpace(upstreamCode))
            return default;

        return upstreamCode.Trim().ToUpperInvariant() switch
        {
            "CS0103" => new(CompilerDiagnosticCodes.UnknownSymbol, "symbol-resolution"),
            "CS1061" or "CS0117" => new(CompilerDiagnosticCodes.UnknownMember, "member-resolution"),
            "CS0029" => new(CompilerDiagnosticCodes.TypeMismatch, "type"),
            "CS1503" => new(CompilerDiagnosticCodes.ArgumentTypeMismatch, "type"),
            _ => default
        };
    }
}
