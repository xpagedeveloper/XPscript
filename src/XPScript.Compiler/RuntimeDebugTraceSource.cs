namespace XPScript.Compiler;

internal static class RuntimeDebugTraceSource
{
    public const string Code = """
internal static class XPScriptRuntimeDebugTrace
{
    public static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_RUNTIME_DEBUG"), "1", StringComparison.Ordinal);

    public static void TraceHandled(Exception original, Exception normalized, int sourceLine)
    {
        if (!Enabled) return;

        Console.Error.WriteLine(sourceLine > 0
            ? "DEBUG runtime exception trapped at XPScript line " + sourceLine.ToString(System.Globalization.CultureInfo.InvariantCulture) + " (handled by On Error):"
            : "DEBUG runtime exception trapped (handled by On Error):");
        Console.Error.WriteLine(normalized.ToString());

        if (!ReferenceEquals(original, normalized))
        {
            Console.Error.WriteLine("DEBUG underlying managed exception:");
            Console.Error.WriteLine(original.ToString());
        }
    }
}
""";
}
