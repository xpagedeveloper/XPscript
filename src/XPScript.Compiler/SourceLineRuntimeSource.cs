namespace XPScript.Compiler;

internal static class SourceLineRuntimeSource
{
    public const string Code = """
internal static class XPSourceLineRuntime
{
    [ThreadStatic] private static int _current;
    [ThreadStatic] private static string? _currentFile;

    public static int Current => _current;
    public static string CurrentFile => _currentFile ?? "";

    public static void Set(int line)
    {
        _current = line < 0 ? 0 : line;
    }

    public static void Set(int line, string? file)
    {
        _current = line < 0 ? 0 : line;
        _currentFile = file ?? "";
    }

    public static void Clear()
    {
        _current = 0;
        _currentFile = "";
    }
}
""";
}