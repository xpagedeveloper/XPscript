namespace XPScript.Compiler;

public static class XPScriptListRuntimeSource
{
    public const string Code = """
internal interface ILSList
{
    bool ContainsTag(object? tag);
    void Clear();
}

internal sealed class LSList<T> : ILSList
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
