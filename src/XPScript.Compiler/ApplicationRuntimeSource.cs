namespace XPScript.Compiler;

internal static class ApplicationRuntimeSource
{
    public const string Code = """
internal static class XPScriptApplicationRuntime
{
    private static readonly object Sync = new();
    private static string[] _args = [];
    private static LSArray? _argsArray;

    public static void SetArgs(string[]? args)
    {
        lock (Sync)
        {
            _args = args is null ? [] : [.. args];
            _argsArray = null;
        }
    }

    public static LSArray Args()
    {
        lock (Sync)
        {
            if (_argsArray is not null) return _argsArray;

            if (_args.Length == 0)
            {
                _argsArray = new LSArray("String", true);
                return _argsArray;
            }

            var array = new LSArray("String", true, [0], [_args.Length - 1]);
            for (var i = 0; i < _args.Length; i++)
                array.Set(_args[i], i);
            _argsArray = array;
            return array;
        }
    }

    public static string Arg(object? index)
    {
        var i = XPScriptRuntime.CInt(index);
        lock (Sync)
        {
            if (i < 0 || i >= _args.Length)
                throw new XPScriptRuntimeException(9, "Application.Args index is outside the available command-line arguments.");
            return _args[i];
        }
    }

    public static int ArgCount
    {
        get { lock (Sync) return _args.Length; }
    }

    public static string CommandLine
    {
        get { lock (Sync) return string.Join(" ", _args); }
    }

    public static string ExecutablePath => Environment.ProcessPath ?? Environment.GetCommandLineArgs().FirstOrDefault() ?? "";
    public static string ExecutableFileName => Path.GetFileName(ExecutablePath);
    public static string ExecutableDirectory => Path.GetDirectoryName(ExecutablePath) ?? "";
    public static string TempPath => Path.GetTempPath();

    // Convenience aliases.
    public static string Path => ExecutablePath;
    public static string FileName => ExecutableFileName;
}
""";
}
