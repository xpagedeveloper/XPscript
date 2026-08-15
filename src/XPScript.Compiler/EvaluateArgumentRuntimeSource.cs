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
        new(true, value, setter);

    public static XPScriptEvaluateArgument ByVal(object? value) =>
        new(false, value, null);

    public void WriteBack(object? value)
    {
        if (IsByRef)
            _setter!(value);
    }
}
""";
}
