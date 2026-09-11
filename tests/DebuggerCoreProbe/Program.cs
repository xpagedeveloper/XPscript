using System.Reflection;
using XPScript.Compiler;
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

var generated = new XPScriptTranspiler().Transpile(
    """
Sub Main()
    Dim answer As Integer
    Dim i As Integer
    answer = 41
    answer = 42
    For i = 1 To 3
        Debugger.Print("i=" & CStr(i))
    Next
    Debugger.Print("answer=" & CStr(answer))
    Debugger.UpdateVar("DocName", "Example")
End Sub
""",
    source,
    CompilerDriver.CurrentRuntimeIdentifier());

if (!generated.Contains("XPScriptDebugRuntime.TrackValue(\"answer\", answer);", StringComparison.Ordinal))
    throw new Exception("Simple scalar assignments were not instrumented for debugger value history.");
if (Count(generated, "XPScriptDebugRuntime.TrackValue(\"i\", i);") < 2)
    throw new Exception("For loop control variables are not refreshed in debugger locals on each iteration.");
if (!generated.Contains("XPSourceLineRuntime.Set(", StringComparison.Ordinal))
    throw new Exception("Debugger source-line mapping was not emitted.");
if (!generated.Contains("internal static class Debugger", StringComparison.Ordinal))
    throw new Exception("Debugger API class was not emitted.");
if (!generated.Contains("public static void Print(object? value)", StringComparison.Ordinal))
    throw new Exception("Debugger.Print API was not emitted.");
if (!generated.Contains("public static void UpdateVar(string name, object? value)", StringComparison.Ordinal))
    throw new Exception("Debugger.UpdateVar API was not emitted.");
if (!generated.Contains("if (XPScriptDebugRuntime.IsEnabled) Debugger.Print", StringComparison.Ordinal))
    throw new Exception("Debugger.Print arguments are not guarded when debugging is disabled.");
if (!generated.Contains("if (XPScriptDebugRuntime.IsEnabled) Debugger.UpdateVar", StringComparison.Ordinal))
    throw new Exception("Debugger.UpdateVar arguments are not guarded when debugging is disabled.");
if (!generated.Contains("ProtocolVersion = 7", StringComparison.Ordinal))
    throw new Exception("Debugger protocol v7 is not emitted.");
if (!generated.Contains("supportsDebuggerApi = true", StringComparison.Ordinal) ||
    !generated.Contains("supportsGlobalConditionBreakpoints = true", StringComparison.Ordinal) ||
    !generated.Contains("supportsDebuggerVariables = true", StringComparison.Ordinal) ||
    !generated.Contains("supportsExceptionBreakpoints = true", StringComparison.Ordinal) ||
    !generated.Contains("supportsConditionalBreakpoints = true", StringComparison.Ordinal) ||
    !generated.Contains("supportsHitConditionalBreakpoints = true", StringComparison.Ordinal) ||
    !generated.Contains("supportsLogPoints = true", StringComparison.Ordinal) ||
    !generated.Contains("supportsPause = true", StringComparison.Ordinal))
    throw new Exception("Debugger protocol capabilities are incomplete.");
if (!generated.Contains("BreakpointRule", StringComparison.Ordinal) ||
    !generated.Contains("EvaluateCondition", StringComparison.Ordinal) ||
    !generated.Contains("EvaluateHitCondition", StringComparison.Ordinal) ||
    !generated.Contains("ExpandLogMessage", StringComparison.Ordinal))
    throw new Exception("Runtime conditional breakpoint evaluation is missing.");
if (!generated.Contains("TryGetProperty(\"breakpoints\"", StringComparison.Ordinal) ||
    !generated.Contains("TryGetProperty(\"condition\"", StringComparison.Ordinal) ||
    !generated.Contains("TryGetProperty(\"hitCondition\"", StringComparison.Ordinal) ||
    !generated.Contains("TryGetProperty(\"logMessage\"", StringComparison.Ordinal))
    throw new Exception("Runtime breakpoint rule payload parsing is incomplete.");
if (!generated.Contains("case \"setGlobalConditionBreakpoints\"", StringComparison.Ordinal) ||
    !generated.Contains("GlobalConditionBreakpointRule", StringComparison.Ordinal) ||
    !generated.Contains("EvaluateGlobalConditionBreakpoints", StringComparison.Ordinal) ||
    !generated.Contains("var globalCondition = EvaluateGlobalConditionBreakpoints();", StringComparison.Ordinal))
    throw new Exception("Global condition breakpoint runtime support is missing.");
if (!generated.Contains("case \"setExceptionBreakpoints\"", StringComparison.Ordinal))
    throw new Exception("Exception breakpoint protocol command is missing.");
if (!generated.Contains("case \"debuggerVariables\"", StringComparison.Ordinal))
    throw new Exception("Debugger variable scope protocol command is missing.");
if (!generated.Contains("XPscript Debugger Command Reader", StringComparison.Ordinal) ||
    !generated.Contains("ConcurrentQueue<PendingCommand>", StringComparison.Ordinal) ||
    !generated.Contains("CommandSignal", StringComparison.Ordinal) ||
    !generated.Contains("_pauseRequested", StringComparison.Ordinal))
    throw new Exception("Single-reader asynchronous debugger command transport is missing.");
if (generated.Contains("PollRunningCommand", StringComparison.Ordinal))
    throw new Exception("Debugger runtime must not poll/read the socket from the XPscript execution thread.");
if (!generated.Contains("commandTransport = \"single-reader\"", StringComparison.Ordinal))
    throw new Exception("Debugger hello frame does not identify the single-reader command transport.");
if (!generated.Contains("XPScriptDebugRuntime.Exception(normalized", StringComparison.Ordinal))
    throw new Exception("XPscript runtime exceptions are not connected to debugger breakpoints.");
if (!generated.Contains("MaxTrackedValueChars = 2048", StringComparison.Ordinal))
    throw new Exception("Debugger value snapshots are not bounded.");
if (!generated.Contains("MaxHistoryCharsPerVariable = 32768", StringComparison.Ordinal))
    throw new Exception("Debugger value history does not expose a per-variable memory budget.");
if (!generated.Contains("<byte[", StringComparison.Ordinal))
    throw new Exception("Debugger byte arrays are not represented without retaining their contents.");
if (!generated.Contains("XPScriptDebugRuntime.Complete();", StringComparison.Ordinal))
    throw new Exception("Generated programs do not complete the debugger transport in a finally block.");
if (!generated.Contains("type = \"complete\"", StringComparison.Ordinal) ||
    !generated.Contains("supportsGracefulCompletion = true", StringComparison.Ordinal))
    throw new Exception("Debugger graceful completion protocol is missing.");
if (!generated.Contains("NormalizeSource(sourcePath)", StringComparison.Ordinal) ||
    !generated.Contains("NormalizeSource(sourceElement.GetString()", StringComparison.Ordinal))
    throw new Exception("Debugger breakpoint source identifiers are not normalized consistently.");

VerifyComplexObjectsAreNotAutoTracked();
Console.WriteLine("DebuggerCoreProbe passed.");

static void VerifyComplexObjectsAreNotAutoTracked()
{
    var compilerAssembly = typeof(XPScriptTranspiler).Assembly;
    var processorType = compilerAssembly.GetType("XPScript.Compiler.CompilerSourceLineDirectivePostProcessor", throwOnError: true)!;
    var processor = Activator.CreateInstance(processorType, nonPublic: true)!;
    var transform = processorType.GetMethod("Transform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new Exception("Debugger source-line postprocessor Transform method was not found.");

    var sourceName = Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes("sample.xps"));
    var input = $$"""
internal static class Script
{
    public static void Main()
    {
        XPSourceLineRuntime.__XPSOURCE_10_{{sourceName}}();
        LSArrayRuntime.Set(values, ComputeValue(), NextIndex());
        XPSourceLineRuntime.__XPSOURCE_11_{{sourceName}}();
        box.Value = ComputeValue();
        XPSourceLineRuntime.__XPSOURCE_12_{{sourceName}}();
        parameter.Value = ComputeValue();
        XPSourceLineRuntime.__XPSOURCE_13_{{sourceName}}();
        Use(LSByRefRuntime.Create(() => (object?)(answer), __lsv => answer = XPScriptRuntime.CInt(__lsv)));
    }
}
internal static class LSControlRuntime
{
}
""";

    var transformed = (string)(transform.Invoke(processor, [input])
        ?? throw new Exception("Debugger source-line postprocessor returned null."));

    if (transformed.Contains("XPScriptDebugArrayMutationRuntime", StringComparison.Ordinal))
        throw new Exception("Internal array mutations must not be exposed as debugger variables.");
    if (transformed.Contains("XPScriptDebugByRefMutationRuntime", StringComparison.Ordinal))
        throw new Exception("Internal ByRef wrappers must not be exposed as debugger variables.");
    if (transformed.Contains("TrackValue(\"box.Value\"", StringComparison.Ordinal))
        throw new Exception("Object member/property writes must not be auto-inspected by the debugger.");
    if (Count(transformed, "NextIndex()") != 1)
        throw new Exception("Debugger postprocessing changed array index evaluation.");
}

static int Count(string text, string value)
{
    var count = 0;
    var offset = 0;
    while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
    {
        count++;
        offset += value.Length;
    }
    return count;
}
