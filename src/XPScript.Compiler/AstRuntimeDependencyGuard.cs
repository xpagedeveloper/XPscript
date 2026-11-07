using System.Text.RegularExpressions;

namespace XPScript.Compiler;

/// <summary>
/// Checks the final AST-generated C# before Roslyn sees it. Runtime providers
/// are emitted as source, so a missing provider otherwise appears as a noisy
/// downstream C# error and can be mistaken for a source-language failure.
/// </summary>
internal static class AstRuntimeDependencyGuard
{
    private static readonly (string TypeName, string ProviderMarker)[] Providers =
    [
        ("XPScriptJsonSchema", "class XPScriptJsonSchema"),
        ("XPScriptJsonValidationResult", "class XPScriptJsonValidationResult"),
        ("XPScriptUIForm", "class XPScriptUIForm"),
        ("XPScriptUIField", "class XPScriptUIField"),
        ("XPScriptTlsValidationState", "class XPScriptTlsValidationState")
    ];

    public static void Validate(string generated, string sourcePath)
    {
        foreach (var (typeName, providerMarker) in Providers)
        {
            if (!Regex.IsMatch(generated, $@"\b{Regex.Escape(typeName)}\b") ||
                generated.Contains(providerMarker, StringComparison.Ordinal))
                continue;

            throw new CompilerException(
                $"AST generated runtime references '{typeName}', but its provider was not emitted (source: {sourcePath}).",
                CompilerDiagnosticCodes.GeneratedRuntimeDependencyMissing,
                "generated-runtime-dependencies");
        }
    }
}
