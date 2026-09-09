namespace XPScript.Compiler.Debugger;

public sealed class BreakpointManager
{
    private readonly Dictionary<int, DebugBreakpoint> _breakpoints = new();
    private int _nextId = 1;

    public IReadOnlyCollection<DebugBreakpoint> Breakpoints => _breakpoints.Values;

    public DebugBreakpoint Add(DebugSourceLocation location, string? condition = null)
    {
        ArgumentNullException.ThrowIfNull(location);
        var breakpoint = new DebugBreakpoint(_nextId++, location, condition);
        _breakpoints.Add(breakpoint.Id, breakpoint);
        return breakpoint;
    }

    public bool Remove(int id) => _breakpoints.Remove(id);

    public void Clear() => _breakpoints.Clear();

    public DebugBreakpoint? Find(string sourcePath, int line)
    {
        return _breakpoints.Values.FirstOrDefault(b =>
            b.Enabled &&
            b.Location.Line == line &&
            string.Equals(b.Location.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));
    }
}
