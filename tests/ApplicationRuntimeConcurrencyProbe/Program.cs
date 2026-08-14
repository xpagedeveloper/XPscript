using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using XPScript.Compiler;

var runtimeSourceType = typeof(CompilerDriver).Assembly.GetType("XPScript.Compiler.ApplicationRuntimeSource", throwOnError: true)!;
var codeField = runtimeSourceType.GetField("Code", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("ApplicationRuntimeSource.Code was not found.");
var runtimeSource = codeField.GetRawConstantValue() as string
    ?? throw new InvalidOperationException("ApplicationRuntimeSource.Code was not a constant string.");

var tempRoot = Path.Combine(Path.GetTempPath(), "XPScriptApplicationConcurrency", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempRoot);
try
{
    await File.WriteAllTextAsync(Path.Combine(tempRoot, "Probe.csproj"), """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
""");

    var probeSource = """
using System.Collections.Concurrent;

internal sealed class LSArray
{
    private readonly ConcurrentDictionary<int, object?> _values = new();
    public LSArray(string typeName, bool dynamic) { }
    public LSArray(string typeName, bool dynamic, int[] lower, int[] upper) { }
    public void Set(object? value, int index) => _values[index] = value;
    public object? Get(int index) => _values.TryGetValue(index, out var value) ? value : null;
}

internal static class XPScriptRuntime
{
    public static int CInt(object? value) => Convert.ToInt32(value);
}

internal sealed class XPScriptRuntimeException : Exception
{
    public XPScriptRuntimeException(int number, string message) : base(message) => Number = number;
    public int Number { get; }
}

""" + runtimeSource + """

internal static class Program
{
    public static void Main()
    {
        var expected = new[] { "alpha", "two words", "ÅÄÖ-漢字", "" };
        XPScriptApplicationRuntime.SetArgs(expected);

        var expectedPath = XPScriptApplicationRuntime.ExecutablePath;
        var expectedFile = XPScriptApplicationRuntime.ExecutableFileName;
        var expectedDirectory = XPScriptApplicationRuntime.ExecutableDirectory;
        var expectedTemp = XPScriptApplicationRuntime.TempPath;
        var expectedCommandLine = XPScriptApplicationRuntime.CommandLine;
        var errors = new ConcurrentQueue<string>();

        Parallel.For(0, 20000, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(4, Environment.ProcessorCount) }, iteration =>
        {
            try
            {
                if (XPScriptApplicationRuntime.ArgCount != expected.Length) errors.Enqueue("ArgCount changed");
                for (var i = 0; i < expected.Length; i++)
                    if (XPScriptApplicationRuntime.Arg(i) != expected[i]) errors.Enqueue($"Arg({i}) changed");

                var copy = XPScriptApplicationRuntime.Args();
                if (!Equals(copy.Get(0), "alpha")) errors.Enqueue("Args copy did not contain expected value");
                copy.Set("mutated-" + iteration, 0);
                if (XPScriptApplicationRuntime.Arg(0) != "alpha") errors.Enqueue("Mutating Args copy changed runtime state");

                if (XPScriptApplicationRuntime.CommandLine != expectedCommandLine) errors.Enqueue("CommandLine changed");
                if (XPScriptApplicationRuntime.ExecutablePath != expectedPath) errors.Enqueue("ExecutablePath changed");
                if (XPScriptApplicationRuntime.ExecutableFileName != expectedFile) errors.Enqueue("ExecutableFileName changed");
                if (XPScriptApplicationRuntime.ExecutableDirectory != expectedDirectory) errors.Enqueue("ExecutableDirectory changed");
                if (XPScriptApplicationRuntime.TempPath != expectedTemp) errors.Enqueue("TempPath changed");
                if (XPScriptApplicationRuntime.Path != expectedPath) errors.Enqueue("Path alias changed");
                if (XPScriptApplicationRuntime.FileName != expectedFile) errors.Enqueue("FileName alias changed");
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex.GetType().Name + ": " + ex.Message);
            }
        });

        if (!errors.IsEmpty)
            throw new Exception("Concurrent Application reads were not stable: " + string.Join(" | ", errors.Take(10)));

        if (XPScriptApplicationRuntime.ArgCount != expected.Length || XPScriptApplicationRuntime.Arg(0) != "alpha")
            throw new Exception("Application state changed after concurrent reads.");

        Console.WriteLine("APPLICATION-CONCURRENT-READS=OK");
    }
}
""";

    await File.WriteAllTextAsync(Path.Combine(tempRoot, "Program.cs"), probeSource);

    var start = new ProcessStartInfo
    {
        FileName = "dotnet",
        WorkingDirectory = tempRoot,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    start.ArgumentList.Add("run");
    start.ArgumentList.Add("--project");
    start.ArgumentList.Add("Probe.csproj");
    start.ArgumentList.Add("-c");
    start.ArgumentList.Add("Release");

    using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start concurrency probe.");
    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    var stdout = await stdoutTask;
    var stderr = await stderrTask;
    if (process.ExitCode != 0)
        throw new Exception($"Application runtime concurrency probe failed with exit code {process.ExitCode}.\n{stdout}\n{stderr}");
    if (!stdout.Contains("APPLICATION-CONCURRENT-READS=OK", StringComparison.Ordinal))
        throw new Exception("Application runtime concurrency probe did not report success.\n" + stdout + "\n" + stderr);

    Console.WriteLine("APPLICATION-RUNTIME-SOURCE-CONCURRENCY=OK");
}
finally
{
    try { Directory.Delete(tempRoot, recursive: true); } catch { }
}
