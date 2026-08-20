namespace XPScript.Compiler;

internal sealed class EvaluateSecurityPostProcessor
{
    private const string Marker = "        var source = XPScriptRuntime.CStr(sourceText);\n        if (string.IsNullOrWhiteSpace(source)) return null;";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (!generated.Contains("internal static class XPScriptEvaluateRuntime", StringComparison.Ordinal)) return generated;
        if (generated.Contains("Evaluate input exceeds the 32768 character safety limit", StringComparison.Ordinal)) return generated;
        if (!generated.Contains(Marker, StringComparison.Ordinal))
            throw new CompilerException("Unable to install Evaluate security limits.");

        const string replacement = """
        var source = XPScriptRuntime.CStr(sourceText);
        if (source.Length > 32768)
            throw new XPScriptRuntimeException(5, "Evaluate input exceeds the 32768 character safety limit.");
        if (string.IsNullOrWhiteSpace(source)) return null;
""";
        return generated.Replace(Marker, replacement.TrimEnd('\n'), StringComparison.Ordinal);
    }
}
