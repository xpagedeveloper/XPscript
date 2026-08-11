namespace XPScript.Compiler;

internal static class EvaluateSemanticsRuntimeSource
{
    public const string Code = """
internal static class XPScriptEvaluateSemanticsRuntime
{
    public static Exception Normalize(Exception exception)
    {
        var normalized = LSExtendedErrorRuntime.Normalize(exception);
        if (normalized is XPScriptRuntimeException)
            return normalized;

        return new XPScriptRuntimeException(5, "Evaluate failed: " + normalized.Message);
    }

    public static bool Compare(object? left, object? right, string operation) =>
        LSCoreCompare.Rel(left, operation, right);
}
""";
}