using System.Diagnostics;
using System.Reflection;
using XPScript.Compiler;

var runtimeSourceType = typeof(CompilerDriver).Assembly.GetType("XPScript.Compiler.ApplicationRuntimeSource", throwOnError: true)!;
var codeField = runtimeSourceType.GetField("Code", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("ApplicationRuntimeSource.Code was not found.");
var runtimeSource = codeField.GetRawConstantValue() as string
    ?? throw new InvalidOperationException("ApplicationRuntimeSource.Code was not a constant string.");

var tempRoot = Path.Combine(Path.GetTempPath(), "XPScriptApplicationIsolation", Guid.NewGuid().ToString("N"));
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
internal sealed class LSArray
{
    private readonly Dictionary<int, object?> _values = new();
    private readonly int _lower;
    private readonly int _upper;
    public LSArray(string typeName, bool dynamic) { ElementType = typeName; IsAllocated = false; }
    public LSArray(string typeName, bool dynamic, int[] lower, int[] upper)
    {
        ElementType = typeName;
        IsAllocated = true;
        _lower = lower.Length == 0 ? 0 : lower[0];
        _upper = upper.Length == 0 ? -1 : upper[0];
    }
    public string ElementType { get; }
    public bool IsAllocated { get; }
    public int Rank => IsAllocated ? 1 : 0;
    public int LBound(int dimension = 1) => _lower;
    public int UBound(int dimension = 1) => _upper;
    public void Set(object? value, int index) => _values[index] = value;
    public object? Get(int index) => _values.TryGetValue(index, out var value) ? value : null;
}

internal static class XPScriptRuntime
{
    public static byte CByte(object? value) => Convert.ToByte(value);
    public static int CInt(object? value) => Convert.ToInt32(value);
    public static long CLng(object? value) => Convert.ToInt64(value);
    public static string CStr(object? value) => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
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
        var mainArgs = new[] { "alpha", "two words", "ÅÄÖ-漢字", "" };
        var expected = mainArgs.ToArray();

        XPScriptApplicationRuntime.SetArgs(mainArgs);

        // The runtime must own an independent copy of the array passed by .NET Main.
        mainArgs[0] = "MUTATED-SOURCE-0";
        mainArgs[1] = "MUTATED-SOURCE-1";
        mainArgs[2] = "MUTATED-SOURCE-2";
        mainArgs[3] = "MUTATED-SOURCE-3";

        if (XPScriptApplicationRuntime.ArgCount != expected.Length)
            throw new Exception("Runtime argument count changed after mutating the original .NET argument array.");

        for (var i = 0; i < expected.Length; i++)
        {
            if (XPScriptApplicationRuntime.Arg(i) != expected[i])
                throw new Exception($"Runtime argument {i} changed after mutating the original .NET argument array.");
        }

        if (XPScriptApplicationRuntime.CommandLine != string.Join(" ", expected))
            throw new Exception("Application.CommandLine changed after mutating the original .NET argument array.");

        Console.WriteLine("APPLICATION-DOTNET-ARGS-COPY=OK");

        // Every full Application.Args read must be detached from runtime-owned storage and from other reads.
        var first = XPScriptApplicationRuntime.Args();
        var second = XPScriptApplicationRuntime.Args();
        if (!Equals(first.Get(0), expected[0]) || !Equals(second.Get(0), expected[0]))
            throw new Exception("Application.Args copies did not contain the expected first value.");

        first.Set("MUTATED-COPY", 0);
        if (!Equals(first.Get(0), "MUTATED-COPY"))
            throw new Exception("Returned Application.Args copy was not independently mutable.");
        if (!Equals(second.Get(0), expected[0]))
            throw new Exception("Mutating one Application.Args copy changed another returned copy.");
        if (XPScriptApplicationRuntime.Arg(0) != expected[0])
            throw new Exception("Mutating an Application.Args copy changed runtime-owned argument storage.");

        var fresh = XPScriptApplicationRuntime.Args();
        if (!Equals(fresh.Get(0), expected[0]))
            throw new Exception("A fresh Application.Args read observed mutation from an older returned copy.");

        Console.WriteLine("APPLICATION-FULL-ARGS-DETACHED=OK");
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

    using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start Application runtime isolation probe.");
    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    var stdout = await stdoutTask;
    var stderr = await stderrTask;

    if (process.ExitCode != 0)
        throw new Exception($"Application runtime isolation probe failed with exit code {process.ExitCode}.\n{stdout}\n{stderr}");
    if (!stdout.Contains("APPLICATION-DOTNET-ARGS-COPY=OK", StringComparison.Ordinal))
        throw new Exception("Application runtime isolation probe did not verify the .NET argument copy.\n" + stdout + "\n" + stderr);
    if (!stdout.Contains("APPLICATION-FULL-ARGS-DETACHED=OK", StringComparison.Ordinal))
        throw new Exception("Application runtime isolation probe did not verify detached Application.Args arrays.\n" + stdout + "\n" + stderr);

    Console.WriteLine("APPLICATION-RUNTIME-ISOLATION=OK");
}
finally
{
    try { Directory.Delete(tempRoot, recursive: true); } catch { }
}
