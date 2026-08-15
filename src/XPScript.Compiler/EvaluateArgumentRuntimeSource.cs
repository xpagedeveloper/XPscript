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

    public static XPScriptEvaluateArgument ByRef<T>(T value, Action<T> setter) =>
        new(true, value, incoming => setter(Coerce<T>(incoming)));

    public static XPScriptEvaluateArgument ByVal(object? value) =>
        new(false, value, null);

    public void WriteBack(object? value)
    {
        if (IsByRef)
            _setter!(value);
    }

    private static T Coerce<T>(object? value)
    {
        var target = typeof(T);
        object? converted = target == typeof(string) ? XPScriptRuntime.CStr(value)
            : target == typeof(byte) ? XPScriptRuntime.CByte(value)
            : target == typeof(int) ? XPScriptRuntime.CInt(value)
            : target == typeof(long) ? XPScriptRuntime.CLng(value)
            : target == typeof(float) ? XPScriptRuntime.CSng(value)
            : target == typeof(double) ? XPScriptRuntime.CDbl(value)
            : target == typeof(decimal) ? XPScriptRuntime.CCur(value)
            : target == typeof(bool) ? XPScriptRuntime.CBool(value)
            : target == typeof(DateTime) ? XPScriptRuntime.CDat(value)
            : value;

        if (converted is T typed) return typed;
        if (converted is null) return default!;
        return (T)converted;
    }
}
""";
}
