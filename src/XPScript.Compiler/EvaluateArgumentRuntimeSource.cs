namespace XPScript.Compiler;

internal static class EvaluateArgumentRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptEvaluateArgument
{
    private readonly Action<object?>? _setter;

    private XPScriptEvaluateArgument(bool byRef, object? value, Action<object?>? setter)
    {
        IsByRef = byRef;
        Value = value;
        _setter = setter;
    }

    public bool IsByRef { get; }
    public object? Value { get; }

    public static XPScriptEvaluateArgument ByRef(object? value, Action<object?> setter) =>
        new(true, value, incoming => setter(CoerceLike(incoming, value)));

    public static XPScriptEvaluateArgument ByVal(object? value) =>
        new(false, value, null);

    public void WriteBack(object? value)
    {
        if (IsByRef)
            _setter!(value);
    }

    private static object? CoerceLike(object? value, object? original)
    {
        if (original is null) return value;
        return original switch
        {
            string => XPScriptRuntime.CStr(value),
            byte => XPScriptRuntime.CByte(value),
            int => XPScriptRuntime.CInt(value),
            long => XPScriptRuntime.CLng(value),
            float => XPScriptRuntime.CSng(value),
            double => XPScriptRuntime.CDbl(value),
            decimal => XPScriptRuntime.CCur(value),
            bool => XPScriptRuntime.CBool(value),
            DateTime => XPScriptRuntime.CDat(value),
            _ => value
        };
    }
}
""";
}
