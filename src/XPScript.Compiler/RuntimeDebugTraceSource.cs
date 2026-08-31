namespace XPScript.Compiler;

internal static class RuntimeDebugTraceSource
{
    public const string Code = """
internal static class XPScriptRuntimeDebugTrace
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Initialize()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_RUNTIME_DEBUG"), "1", StringComparison.Ordinal))
            return;

        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
    }

    private static void OnFirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs args)
    {
        var exception = args.Exception;
        var stack = exception.StackTrace ?? "";
        if (exception is not XPScriptRuntimeException &&
            !stack.Contains("XPScript", StringComparison.Ordinal) &&
            !stack.Contains("Script.", StringComparison.Ordinal))
            return;

        var sourceLine = XPSourceLineRuntime.Current;
        Console.Error.WriteLine(sourceLine > 0
            ? "DEBUG runtime exception trapped at XPScript line " + sourceLine.ToString(System.Globalization.CultureInfo.InvariantCulture) + " (may be handled by On Error):"
            : "DEBUG runtime exception trapped (may be handled by On Error):");
        Console.Error.WriteLine(exception.ToString());
    }
}
""";
}
