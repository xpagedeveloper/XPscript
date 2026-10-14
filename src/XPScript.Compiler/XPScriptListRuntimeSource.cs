namespace XPScript.Compiler;

public static class XPScriptListRuntimeSource
{
    public const string Code = """
internal interface ILSList
{
    bool ContainsTag(object? tag);
    object? GetValue(object? tag);
    void SetValue(object? tag, object? value);
    void Clear();
    IEnumerable<KeyValuePair<string, object?>> SnapshotEntries();
}

internal static class LSListRuntime
{
    public static bool ContainsTag(object? value, object? tag) => value is ILSList list
        ? list.ContainsTag(tag) : throw new InvalidOperationException("IsElement requires an XPScript List.");
}

// Capture the element while evaluating its argument, before later arguments.
// Named call wrappers retain normal ref parameters and copy back in finally.
internal sealed class LSListByRef<T>
{
    private readonly LSList<T>? _list;
    private readonly object? _tag;
    private readonly LSListAlias<T>? _alias;
    public T Value;

    public LSListByRef(LSList<T> list, object? tag)
    {
        _list = list;
        _tag = tag;
        Value = list[tag];
    }

    public LSListByRef(LSListAlias<T> alias)
    {
        _alias = alias;
        Value = alias.Value;
    }

    public void CopyBack()
    {
        if (_alias is not null) _alias.Value = Value;
        else _list![_tag] = Value;
    }
}

internal sealed class LSList<T> : ILSList, IXPScriptIterable, System.Collections.IEnumerable
{
    private readonly Dictionary<string, System.Runtime.CompilerServices.StrongBox<T>> _values = new(StringComparer.CurrentCulture);
    private readonly List<string> _order = [];

    public T this[object? tag]
    {
        get
        {
            var key = NormalizeTag(tag);
            if (!_values.TryGetValue(key, out var value))
                throw new KeyNotFoundException("List element does not exist: " + key);
            return value.Value;
        }
        set
        {
            var key = NormalizeTag(tag);
            if (!_values.TryGetValue(key, out var cell))
            {
                _order.Add(key);
                _values[key] = new System.Runtime.CompilerServices.StrongBox<T>(value);
            }
            else cell.Value = value;
        }
    }

    public ref T Reference(object? tag)
    {
        var key = NormalizeTag(tag);
        if (!_values.TryGetValue(key, out var cell))
            throw new KeyNotFoundException("List element does not exist: " + key);
        return ref cell.Value;
    }

    public bool ContainsTag(object? tag) => _values.ContainsKey(NormalizeTag(tag));
    public object? GetValue(object? tag) => this[tag];
    public void SetValue(object? tag, object? value) => this[tag] = Coerce(value);

    public void Erase(object? tag)
    {
        var key = NormalizeTag(tag);
        if (_values.Remove(key))
            _order.RemoveAll(x => string.Equals(x, key, StringComparison.CurrentCulture));
    }

    public void Clear()
    {
        _values.Clear();
        _order.Clear();
    }

    public IEnumerable<LSListAlias<T>> Aliases()
    {
        foreach (var tag in _order.ToArray())
        {
            if (_values.ContainsKey(tag))
                yield return new LSListAlias<T>(this, tag);
        }
    }

    public System.Collections.IEnumerable XPScriptItems() => Aliases();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        XPScriptItems().GetEnumerator();

    public IEnumerable<KeyValuePair<string, object?>> SnapshotEntries()
    {
        foreach (var tag in _order.ToArray())
        {
            if (_values.TryGetValue(tag, out var value))
                yield return new KeyValuePair<string, object?>(tag, value.Value);
        }
    }

    private static T Coerce(object? value)
    {
        if (typeof(T) == typeof(object)) return (T)value!;
        if (typeof(T) == typeof(string)) return (T)(object)XPScriptRuntime.CStr(value);
        if (typeof(T) == typeof(int)) return (T)(object)XPScriptRuntime.CInt(value);
        if (typeof(T) == typeof(long)) return (T)(object)XPScriptRuntime.CLng(value);
        if (typeof(T) == typeof(double)) return (T)(object)XPScriptRuntime.CDbl(value);
        if (typeof(T) == typeof(float)) return (T)(object)XPScriptRuntime.CSng(value);
        if (typeof(T) == typeof(bool)) return (T)(object)XPScriptRuntime.CBool(value);
        if (typeof(T) == typeof(byte)) return (T)(object)XPScriptRuntime.CByte(value);
        if (typeof(T) == typeof(decimal)) return (T)(object)XPScriptRuntime.CCur(value);
        if (typeof(T) == typeof(DateTime)) return (T)(object)XPScriptRuntime.CDat(value);
        if (value is T typed) return typed;
        throw new InvalidCastException("Value cannot be assigned to this XPScript List element type.");
    }

    private static string NormalizeTag(object? tag) =>
        Convert.ToString(tag, CultureInfo.CurrentCulture) ?? "";
}

internal sealed class LSListAlias<T>
{
    private readonly LSList<T> _list;

    public string Tag { get; }

    public ref T Value => ref _list.Reference(Tag);

    public LSListAlias(LSList<T> list, string tag)
    {
        _list = list;
        Tag = tag;
    }

    public override string ToString() =>
        Convert.ToString(Value, CultureInfo.CurrentCulture) ?? "";
}
""";
}
