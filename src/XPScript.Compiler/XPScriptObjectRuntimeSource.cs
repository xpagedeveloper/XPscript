namespace XPScript.Compiler;

public static class XPScriptObjectRuntimeSource
{
    public const string Code = """
internal interface IXPScriptIterable
{
    System.Collections.IEnumerable XPScriptItems();
}

internal abstract class LSObjectBase : IXPScriptIterable, System.Collections.IEnumerable
{
    private bool _deleted;

    public bool __IsDeleted => _deleted;

    public virtual void __Delete()
    {
        _deleted = true;
    }

    public System.Collections.IEnumerable XPScriptItems()
    {
        var method = GetType().GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .FirstOrDefault(candidate =>
                candidate.Name.Equals("Iterator", StringComparison.OrdinalIgnoreCase) &&
                candidate.GetParameters().Length == 0);

        if (method is null)
            throw new XPScriptRuntimeException(13, "ForAll requires an iterable value. XPscript classes must expose Public Function Iterator().");

        object? value;
        try
        {
            value = method.Invoke(this, null);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }

        if (ReferenceEquals(value, this))
            throw new XPScriptRuntimeException(13, "Iterator() must return another iterable value, not the object itself.");

        return LSForAllRuntime.Enumerate(value);
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        XPScriptItems().GetEnumerator();
}

internal interface ILSObjectReference
{
    bool IsNothing { get; }
}

internal static class LSObjectIdentityRuntime
{
    public static bool IsNothing(object? value) =>
        value is ILSObjectReference reference ? reference.IsNothing : value is null;

    public static bool IsNotNothing(object? value) => !IsNothing(value);
}

internal sealed class LSRef<T> : ILSObjectReference, IXPScriptIterable, System.Collections.IEnumerable where T : LSObjectBase
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

    public System.Collections.IEnumerable XPScriptItems()
    {
        var value = Value;
        if (value is null)
            throw new XPScriptRuntimeException(13, "ForAll cannot iterate Nothing.");
        return value.XPScriptItems();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        XPScriptItems().GetEnumerator();

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