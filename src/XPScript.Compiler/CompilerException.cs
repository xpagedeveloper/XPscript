namespace XPScript.Compiler;

public sealed class CompilerException : Exception
{
    public CompilerException(string message) : this(message, null, null, null) { }

    public CompilerException(string message, IEnumerable<CompileDiagnostic>? generatedDiagnostics)
        : this(message, null, null, generatedDiagnostics) { }

    public CompilerException(
        string message,
        string? diagnosticCode,
        string? category = null,
        IEnumerable<CompileDiagnostic>? generatedDiagnostics = null) : base(message)
    {
        DiagnosticCode = diagnosticCode ?? "";
        Category = category ?? "";
        GeneratedDiagnostics = generatedDiagnostics?.ToArray() ?? [];
    }

    public string DiagnosticCode { get; }

    public string Category { get; }

    public IReadOnlyList<CompileDiagnostic> GeneratedDiagnostics { get; }
}
