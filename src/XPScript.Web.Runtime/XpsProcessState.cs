namespace XPScript.Web.Runtime;

public sealed class XpsProcessState
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StateValue> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxEntries;
    private readonly int _maxValueBytes;
    private readonly long _maxTotalBytes;
    private long _totalBytes;

    public XpsProcessState(int maxEntries = 512, int maxValueBytes = 64 * 1024, long maxTotalBytes = 8 * 1024 * 1024)
    {
        if (maxEntries is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(maxEntries));
        if (maxValueBytes is < 1 or > 16 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maxValueBytes));
        if (maxTotalBytes < maxValueBytes || maxTotalBytes > 256L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maxTotalBytes));
        _maxEntries = maxEntries;
        _maxValueBytes = maxValueBytes;
        _maxTotalBytes = maxTotalBytes;
    }

    public int Count { get { lock (_gate) return _values.Count; } }
    public IReadOnlyList<string> Keys { get { lock (_gate) return _values.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); } }

    public object? Get(string name)
    {
        ValidateName(name);
        lock (_gate) return _values.TryGetValue(name, out var value) ? StateValuePolicy.Clone(value.Value) : null;
    }

    public void Set(string name, object? value)
    {
        ValidateName(name);
        var stateValue = StateValuePolicy.Create(value, _maxValueBytes);
        lock (_gate)
        {
            _values.TryGetValue(name, out var previous);
            if (previous is null && _values.Count >= _maxEntries)
                throw new InvalidOperationException("Process state entry limit has been reached.");
            var nextTotal = checked(_totalBytes - (previous?.Bytes ?? 0) + stateValue.Bytes);
            if (nextTotal > _maxTotalBytes)
                throw new InvalidOperationException("Process state memory limit has been reached.");
            _values[name] = stateValue;
            _totalBytes = nextTotal;
        }
    }

    public void Add(string name, object? value) => Set(name, value);

    public bool Exists(string name)
    {
        ValidateName(name);
        lock (_gate) return _values.ContainsKey(name);
    }

    public bool Remove(string name)
    {
        ValidateName(name);
        lock (_gate)
        {
            if (!_values.Remove(name, out var previous)) return false;
            _totalBytes -= previous.Bytes;
            return true;
        }
    }

    public bool Unset(string name) => Remove(name);

    public void Clear()
    {
        lock (_gate)
        {
            _values.Clear();
            _totalBytes = 0;
        }
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 256) throw new ArgumentOutOfRangeException(nameof(name), "Process state value name cannot exceed 256 characters.");
    }
}
