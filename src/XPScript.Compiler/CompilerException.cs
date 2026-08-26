namespace XPScript.Compiler;

public sealed class CompilerException : Exception
{
    public CompilerException(string message) : this(message, null) { }

    public CompilerException(string message, IEnumerable<CompileDiagnostic>? generatedDiagnostics) : base(message)
    {
        GeneratedDiagnostics = generatedDiagnostics?.ToArray() ?? [];
    }

    public IReadOnlyList<CompileDiagnostic> GeneratedDiagnostics { get; }
}
