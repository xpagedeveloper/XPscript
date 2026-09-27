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

internal sealed class LSList<T> : ILSList, IXPScriptIterable, System.Collections.IEnumerable
{
    private readonly Dictionary<string, T> _values = new(StringComparer.CurrentCulture);
    private readonly List<string> _order = [];

    public T this[object? tag]
    {
        get
        {
            var key = NormalizeTag(tag);
            if (!_values.TryGetValue(key, out var value))
                throw new KeyNotFoundException("List element does not exist: " + key);
            return value;
        }
        set
        {
            var key = NormalizeTag(tag);
            if (!_values.ContainsKey(key))
                _order.Add(key);
            _values[key] = value;
        }
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
                yield return new KeyValuePair<string, object?>(tag, value);
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

    public T Value
    {
        get => _list[Tag];
        set => _list[Tag] = value;
    }

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
