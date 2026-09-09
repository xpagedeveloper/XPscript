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
    answer = 41
    answer = 42
End Sub
""",
    source,
    CompilerDriver.CurrentRuntimeIdentifier());

if (!generated.Contains("XPScriptDebugRuntime.TrackValue(\"answer\", answer);", StringComparison.Ordinal))
    throw new Exception("Simple scalar assignments were not instrumented for debugger value history.");
if (!generated.Contains("XPSourceLineRuntime.Set(", StringComparison.Ordinal))
    throw new Exception("Debugger source-line mapping was not emitted.");
if (!generated.Contains("MaxTrackedValueChars = 2048", StringComparison.Ordinal))
    throw new Exception("Debugger value snapshots are not bounded.");
if (!generated.Contains("MaxHistoryCharsPerVariable = 32768", StringComparison.Ordinal))
    throw new Exception("Debugger value history does not expose a per-variable memory budget.");
if (!generated.Contains("<byte[", StringComparison.Ordinal))
    throw new Exception("Debugger byte arrays are not represented without retaining their contents.");

VerifyMutationPostProcessor();

Console.WriteLine("DebuggerCoreProbe passed.");

static void VerifyMutationPostProcessor()
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

    if (!transformed.Contains(
            "XPScriptDebugArrayMutationRuntime.Set(\"values\", values, ComputeValue(), NextIndex());",
            StringComparison.Ordinal))
        throw new Exception("Array writes were not routed through debugger mutation tracking.");
    if (Count(transformed, "NextIndex()") != 1)
        throw new Exception("Array index expressions were duplicated by debugger instrumentation.");
    if (Count(transformed, "ComputeValue()") != 3)
        throw new Exception("Mutation right-hand expressions were duplicated by debugger instrumentation.");
    if (!transformed.Contains("XPScriptDebugRuntime.TrackValue(\"box.Value\"", StringComparison.Ordinal))
        throw new Exception("Simple member/property writes were not tracked.");
    if (!transformed.Contains("XPScriptDebugRuntime.TrackValue(\"parameter\", parameter.Value);", StringComparison.Ordinal))
        throw new Exception("ByRef parameter writes were not tracked.");
    if (!transformed.Contains("XPScriptDebugByRefMutationRuntime.Create(\"answer\"", StringComparison.Ordinal))
        throw new Exception("ByRef callers do not retain their caller variable name for data breakpoints.");
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
