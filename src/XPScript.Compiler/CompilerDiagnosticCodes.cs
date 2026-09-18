namespace XPScript.Compiler;

/// <summary>
/// Stable XPScript-owned diagnostic identifiers used by the public machine interface.
/// Codes are part of the compiler result contract and must not be repurposed.
/// </summary>
internal static class CompilerDiagnosticCodes
{
    // XPS8xxx: project/configuration and compiler invocation.
    public const string SourceFileRequired = "XPS8001";
    public const string SourceExtensionInvalid = "XPS8002";
    public const string SourceFileNotFound = "XPS8003";
    public const string RuntimeIdentifierUnsupported = "XPS8004";
    public const string WebIisEntryFileInvalid = "XPS8005";
    public const string WebIisOutputInsideSource = "XPS8006";

    // XPS9xxx: compiler pipeline failures where a more specific language
    // diagnostic could not be preserved.
    public const string CompilationFailed = "XPS9001";
    public const string InternalCompilationFailed = "XPS9002";
}
