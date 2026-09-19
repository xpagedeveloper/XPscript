using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class CompilerDiagnosticRedactor
{
    private const string Redacted = "[REDACTED]";

    private static readonly Regex AuthorizationHeader = new(
        @"(?im)(\bAuthorization\s*:\s*)([^\r\n]+)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BearerToken = new(
        @"(?i)(\bBearer\s+)[A-Za-z0-9._~+\-/]+=*",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SecretAssignment = new(
        @"(?i)(\b(?:api[_-]?key|access[_-]?token|refresh[_-]?token|client[_-]?secret|password|passwd|pwd)\b\s*(?:=|:)\s*)([""']?)([^\s,""';]+)\2",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        var result = AuthorizationHeader.Replace(value, "$1" + Redacted);
        result = BearerToken.Replace(result, "$1" + Redacted);
        return SecretAssignment.Replace(result, "$1" + Redacted);
    }
}
