namespace XPScript.Compiler;

public static class XPScriptObjectRuntimeSource
{
    public const string Code = """
internal abstract class LSObjectBase
{
    private bool _deleted;

    public bool __IsDeleted => _deleted;

    public virtual void __Delete()
    {
        _deleted = true;
    }
}

internal interface ILSObjectReference
{
    bool IsNothing { get; }
}

internal sealed class LSRef<T> : ILSObjectReference where T : LSObjectBase
{
    public T? Value { get; private set; }

    public bool IsNothing => Value is null;

    public LSRef()
    {
    }

    private LSRef(T value)
    {
        Value = value;
    }

    public static LSRef<T> Create(T value) => new(value);

    public void Delete()
    {
        var value = Value;
        if (value is null)
            return;

        try
        {
            value.__Delete();
        }
        finally
        {
            Value = null;
        }
    }

    public bool IsSameReference(LSRef<T>? other) =>
        other is not null && ReferenceEquals(this, other);

    public override string ToString() =>
        throw new InvalidCastException("Object references cannot be converted to String implicitly.");
}

internal static class LSObjectRuntime
{
    public static void AssignRef<T>(ref LSRef<T> target, LSRef<T> source) where T : LSObjectBase
    {
        target = source;
    }
}
""";
}
