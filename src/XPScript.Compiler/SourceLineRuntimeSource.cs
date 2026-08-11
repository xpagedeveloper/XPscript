namespace XPScript.Compiler;

internal static class SourceLineRuntimeSource
{
    public const string Code = """
internal static class XPSourceLineRuntime
{
    [ThreadStatic] private static int _current;

    public static int Current => _current;

    public static void Set(int line)
    {
        _current = line < 0 ? 0 : line;
    }

    public static void Clear()
    {
        _current = 0;
    }
}
""";
}
