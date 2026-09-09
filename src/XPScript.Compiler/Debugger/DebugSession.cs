namespace XPScript.Compiler.Debugger;

public sealed class DebugSession
{
    private readonly List<DebugStackFrame> _frames = new();
    private readonly Dictionary<int, IReadOnlyList<DebugVariable>> _variables = new();

    public BreakpointManager Breakpoints { get; } = new();
    public bool IsPaused { get; private set; }
    public DebugSourceLocation? CurrentLocation { get; private set; }
    public DebugStepKind RequestedStep { get; private set; } = DebugStepKind.Continue;

    public event EventHandler<DebugStoppedEvent>? Stopped;

    public void RequestPause() => IsPaused = true;

    public void Continue()
    {
        RequestedStep = DebugStepKind.Continue;
        IsPaused = false;
    }

    public void StepInto() => Resume(DebugStepKind.Into);
    public void StepOver() => Resume(DebugStepKind.Over);
    public void StepOut() => Resume(DebugStepKind.Out);

    public IReadOnlyList<DebugStackFrame> GetStackTrace() => _frames;

    public IReadOnlyList<DebugVariable> GetVariables(int frameId) =>
        _variables.TryGetValue(frameId, out var variables) ? variables : Array.Empty<DebugVariable>();

    public void UpdateFrame(DebugStackFrame frame, IReadOnlyList<DebugVariable>? variables = null)
    {
        var index = _frames.FindIndex(existing => existing.Id == frame.Id);
        if (index >= 0) _frames[index] = frame;
        else _frames.Insert(0, frame);

        if (variables is not null) _variables[frame.Id] = variables;
    }

    public bool BeforeExecute(DebugSourceLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        CurrentLocation = location;

        var breakpoint = Breakpoints.Find(location.SourcePath, location.Line);
        var shouldStop = IsPaused || breakpoint is not null || RequestedStep != DebugStepKind.Continue;
        if (!shouldStop) return false;

        IsPaused = true;
        var reason = breakpoint is not null ? DebugStopReason.Breakpoint :
            RequestedStep != DebugStepKind.Continue ? DebugStopReason.Step : DebugStopReason.Pause;
        RequestedStep = DebugStepKind.Continue;
        Stopped?.Invoke(this, new DebugStoppedEvent(reason, location));
        return true;
    }

    private void Resume(DebugStepKind step)
    {
        RequestedStep = step;
        IsPaused = false;
    }
}
