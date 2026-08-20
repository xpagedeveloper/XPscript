namespace XPScript.Compiler;

internal static class HclPrintFormattingRuntimeSource
{
    public const string Code = """
internal static class LSHclPrintRuntime
{
    internal sealed record Part(string Kind, object? Value);

    public static Part Text(object? value) => new("text", value);

    public static Part Spc(object? value)
    {
        var count = XPScriptRuntime.CInt(value);
        if (count < 0 || count > 32000) throw new XPScriptRuntimeException(5, "Spc argument must be between 0 and 32000.");
        return new Part("spc", count);
    }

    public static Part Tab(object? value)
    {
        var column = XPScriptRuntime.CInt(value);
        if (column < 1) column = 1;
        if (column > 32000) throw new XPScriptRuntimeException(5, "Tab argument must be between 1 and 32000.");
        return new Part("tab", column);
    }

    public static string Format(params object?[] values)
    {
        var output = new StringBuilder();
        var column = 1;

        foreach (var raw in values)
        {
            var part = raw as Part ?? Text(raw);
            if (part.Kind == "text")
            {
                var text = XPScriptRuntime.CStr(part.Value);
                output.Append(text);
                var lastBreak = text.LastIndexOf('\n');
                column = lastBreak >= 0 ? text.Length - lastBreak : column + text.Length;
                continue;
            }

            if (part.Kind == "spc")
            {
                var count = XPScriptRuntime.CInt(part.Value);
                output.Append(' ', count);
                column += count;
                continue;
            }

            if (part.Kind == "tab")
            {
                var target = XPScriptRuntime.CInt(part.Value);
                if (target < column)
                {
                    output.Append(Environment.NewLine);
                    column = 1;
                }
                if (target > column)
                {
                    output.Append(' ', target - column);
                    column = target;
                }
                continue;
            }

            throw new XPScriptRuntimeException(5, "Unknown Print formatting part.");
        }

        return output.ToString();
    }
}
""";
}
