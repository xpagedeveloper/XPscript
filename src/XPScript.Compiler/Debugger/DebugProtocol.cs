namespace XPScript.Compiler.Debugger;

public enum DebugStepKind
{
    Continue,
    Into,
    Over,
    Out
}

public enum DebugStopReason
{
    Entry,
    Breakpoint,
    Step,
    Exception,
    Pause
}

public sealed record DebugSourceLocation(string SourcePath, int Line, int Column = 1);

public sealed record DebugBreakpoint(
    int Id,
    DebugSourceLocation Location,
    string? Condition = null,
    bool Enabled = true);

public sealed record DebugStackFrame(
    int Id,
    string Name,
    DebugSourceLocation Location);

public sealed record DebugVariable(
    string Name,
    string Value,
    string? Type = null,
    int VariablesReference = 0);

public sealed record DebugStoppedEvent(
    DebugStopReason Reason,
    DebugSourceLocation Location,
    string? Description = null);
