namespace XPScript.Compiler;

internal static class EvaluateSemanticsRuntimeSource
{
    public const string Code = """
internal static class XPScriptEvaluateSemanticsRuntime
{
    public static Exception Normalize(Exception exception) => Sanitize(exception);

    public static Exception Sanitize(Exception exception)
    {
        var normalized = LSExtendedErrorRuntime.Normalize(exception);
        if (normalized is not XPScriptRuntimeException runtime)
            return new XPScriptRuntimeException(5, "Evaluate failed without exposing input values.");

        var description = SanitizeDescription(runtime.Number, runtime.Message);
        return new XPScriptRuntimeException(runtime.Number, description);
    }

    private static string SanitizeDescription(int number, string? description)
    {
        var text = description ?? "";

        // Errors whose underlying CLR/runtime message may contain the actual value are
        // deliberately collapsed to stable XPScript diagnostics. This prevents secrets
        // supplied through callvar from being reflected into logs or error responses.
        if (number == 13) return "Evaluate type mismatch.";
        if (number == 6) return "Evaluate overflow.";
        if (number == 11) return "Evaluate division by zero.";
        if (number == 70) return "Evaluate access or permission denied.";
        if (number == 9) return "Evaluate subscript or List tag was not found.";

        // Preserve only diagnostic families that describe syntax/API shape rather than
        // runtime values. Identifiers and function names are source-code metadata, not
        // callvar payloads. All other error-5 text is intentionally generalized.
        if (number == 5 && IsSafeStructuralDiagnostic(text))
            return Limit(text, 512);

        if (number == 5) return "Invalid procedure call in Evaluate.";
        return "Evaluate failed with XPScript error " + number.ToString(CultureInfo.InvariantCulture) + ".";
    }

    private static bool IsSafeStructuralDiagnostic(string text)
    {
        string[] prefixes =
        [
            "Evaluate function ",
            "Function is not available inside Evaluate: ",
            "Unknown identifier in Evaluate scope: ",
            "Assignment requires a local variable declared with Dim: ",
            "Variable already declared in Evaluate scope: ",
            "Unsupported Evaluate local type: ",
            "Expected ",
            "callvar is read-only inside Evaluate.",
            "callvar is reserved and cannot be declared inside Evaluate.",
            "callvar is not an indexed value.",
            "Evaluate List callvar requires exactly one tag.",
            "Evaluate array callvar received the wrong number of indexes.",
            "Evaluate collection snapshot exceeds the maximum ",
            "Evaluate callvar contains an unsupported mutable object type: ",
            "Unsupported character in Evaluate source: ",
            "Unterminated string literal in Evaluate.",
            "Unterminated date literal in Evaluate.",
            "Unsupported Evaluate comparison operator."
        ];

        return prefixes.Any(prefix => text.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";

    public static bool Compare(object? left, object? right, string operation) =>
        LSCoreCompare.Rel(left, operation, right);
}
""";
}
