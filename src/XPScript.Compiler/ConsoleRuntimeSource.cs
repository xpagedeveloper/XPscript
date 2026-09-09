namespace XPScript.Compiler;

internal static class ConsoleRuntimeSource
{
    public const string Code = """
internal static class Console
{
    public static void Write(object? value) => global::System.Console.Write(XPScriptRuntime.PrintText(value));
    public static void WriteLine() => global::System.Console.WriteLine();
    public static void WriteLine(object? value) => global::System.Console.WriteLine(XPScriptRuntime.PrintText(value));
    public static void Error(object? value) => global::System.Console.Error.WriteLine(XPScriptRuntime.PrintText(value));

    public static void Clear() => global::System.Console.Clear();

    public static void ClearLine()
    {
        if (global::System.Console.IsOutputRedirected) return;
        var top = global::System.Console.CursorTop;
        var left = global::System.Console.CursorLeft;
        var width = global::System.Console.WindowWidth;
        if (width <= 0) return;
        global::System.Console.SetCursorPosition(0, top);
        global::System.Console.Write(new string(' ', width));
        global::System.Console.SetCursorPosition(System.Math.Min(left, width - 1), top);
    }

    public static string ForegroundColor
    {
        get => global::System.Console.ForegroundColor.ToString();
        set => global::System.Console.ForegroundColor = ParseColor(value);
    }

    public static string BackgroundColor
    {
        get => global::System.Console.BackgroundColor.ToString();
        set => global::System.Console.BackgroundColor = ParseColor(value);
    }

    public static void ResetColor() => global::System.Console.ResetColor();

    public static void SetCursorPosition(int left, int top) => global::System.Console.SetCursorPosition(left, top);

    public static int CursorLeft
    {
        get => global::System.Console.CursorLeft;
        set => global::System.Console.CursorLeft = value;
    }

    public static int CursorTop
    {
        get => global::System.Console.CursorTop;
        set => global::System.Console.CursorTop = value;
    }

    public static bool CursorVisible
    {
        get => global::System.Console.CursorVisible;
        set => global::System.Console.CursorVisible = value;
    }

    public static int WindowWidth => global::System.Console.WindowWidth;
    public static int WindowHeight => global::System.Console.WindowHeight;

    public static string Title
    {
        get => global::System.Console.Title;
        set => global::System.Console.Title = value ?? string.Empty;
    }

    public static ConsoleKeyInfoValue ReadKey() => ReadKey(false);

    public static ConsoleKeyInfoValue ReadKey(bool intercept)
    {
        var key = global::System.Console.ReadKey(intercept);
        return new ConsoleKeyInfoValue(key);
    }

    public static bool KeyAvailable => global::System.Console.KeyAvailable;
    public static bool IsInputRedirected => global::System.Console.IsInputRedirected;
    public static bool IsOutputRedirected => global::System.Console.IsOutputRedirected;
    public static bool IsErrorRedirected => global::System.Console.IsErrorRedirected;

    private static global::System.ConsoleColor ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !global::System.Enum.TryParse<global::System.ConsoleColor>(value, true, out var color) ||
            !global::System.Enum.IsDefined(color))
            throw new XPScriptRuntimeException(5, "Invalid console color: " + (value ?? string.Empty));
        return color;
    }
}

internal sealed class ConsoleKeyInfoValue
{
    private readonly global::System.ConsoleKeyInfo _value;

    public ConsoleKeyInfoValue(global::System.ConsoleKeyInfo value) => _value = value;

    public string Key => _value.Key.ToString();
    public string Char => _value.KeyChar == '\0' ? string.Empty : _value.KeyChar.ToString();
    public bool Control => (_value.Modifiers & global::System.ConsoleModifiers.Control) != 0;
    public bool Alt => (_value.Modifiers & global::System.ConsoleModifiers.Alt) != 0;
    public bool Shift => (_value.Modifiers & global::System.ConsoleModifiers.Shift) != 0;
}
""";
}
