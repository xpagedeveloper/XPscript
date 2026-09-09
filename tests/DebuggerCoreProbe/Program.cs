using XPScript.Compiler.Debugger;

var session = new DebugSession();
var source = Path.GetFullPath("sample.xps");
var breakpoint = session.Breakpoints.Add(new DebugSourceLocation(source, 12));

if (breakpoint.Id <= 0) throw new Exception("Breakpoint id was not assigned.");
if (session.Breakpoints.Find(source, 12) is null) throw new Exception("Breakpoint lookup failed.");

DebugStoppedEvent? stopped = null;
session.Stopped += (_, value) => stopped = value;

if (!session.BeforeExecute(new DebugSourceLocation(source, 12)))
    throw new Exception("Breakpoint did not stop execution.");
if (stopped?.Reason != DebugStopReason.Breakpoint)
    throw new Exception("Unexpected breakpoint stop reason.");

session.StepInto();
stopped = null;
if (!session.BeforeExecute(new DebugSourceLocation(source, 13)))
    throw new Exception("StepInto did not stop execution.");
if (stopped?.Reason != DebugStopReason.Step)
    throw new Exception("Unexpected step stop reason.");

session.UpdateFrame(
    new DebugStackFrame(1, "Main", new DebugSourceLocation(source, 13)),
    [new DebugVariable("answer", "42", "Integer")]);

if (session.GetStackTrace().Count != 1) throw new Exception("Stack frame was not recorded.");
if (session.GetVariables(1).Count != 1) throw new Exception("Frame variables were not recorded.");

Console.WriteLine("DebuggerCoreProbe passed.");
