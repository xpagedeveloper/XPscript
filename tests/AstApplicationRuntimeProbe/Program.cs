using System.Diagnostics;
using System.Reflection;

var repo = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
var output = Path.Combine(repo, "out", "application-ast-probe", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(output);
var compiler = Path.Combine(repo, "src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll");
foreach (var (name, code) in new[] { ("application-member-error", "XPS2009"), ("application-readonly-error", "XPS2001"),
    ("application-args-readonly-error", "XPS2001"),
    ("application-arity-error", "XPS2004"), ("application-constructor-error", "XPS1009") })
{
    var failure = await Execute("dotnet", [compiler, "ast-compile", Path.Combine(repo, "tests/ast-compile-probe", name + ".xps"), "-o", Path.Combine(output, name)]);
    if (failure.Exit != 2 || !failure.Text.Contains(name + ".xps:2:")) throw new Exception("Application diagnostic mapping lost: " + name + code + failure.Text);
}
foreach (var name in new[] { "application-with", "application-native", "application-services", "application-metadata", "application-unused" })
{
    var source = Path.Combine(repo, "tests/ast-compile-probe", name + ".xps");
    var astDirectory = Path.Combine(output, name, "ast");
    await Run("dotnet", [compiler, "ast-compile", source, "-o", astDirectory]);
    var arguments = name == "application-native" ? new[] { "Åsa", "two words" } : Array.Empty<string>();
    var result = await Execute("dotnet", [Path.Combine(astDirectory, "Generated.dll"), .. arguments]);
    var expected = name switch { "application-with" => "APPLICATION_WITH_OK", "application-native" => "APPLICATION_NATIVE_OK", "application-services" => "APPLICATION_SERVICES_OK",
        "application-metadata" => "APPLICATION_METADATA_OK", _ => "Application.Debug Application.Registry" };
    var expectedExit = name == "application-metadata" ? 7 : 0;
    if (result.Exit != expectedExit || result.Text.Trim() != expected) throw new Exception("AST Application execution failed: " + name + result.Text);
    var assembly = Assembly.LoadFile(Path.Combine(astDirectory, "Generated.dll"));
    if ((assembly.GetType("XPScriptApplicationRuntime") is not null) != (name != "application-unused")) throw new Exception("Application inclusion incorrect.");
    if (name == "application-native" && (assembly.GetType("XPScriptApplicationSecretsRuntime") is not null || assembly.GetType("XPScriptApplicationCryptoRuntime") is not null)) throw new Exception("Unused Application services leaked.");
    if (assembly.GetReferencedAssemblies().Any(a => a.Name!.StartsWith("Magick.NET") || a.Name.StartsWith("Microsoft.Data.Sqlite"))) throw new Exception("Application pulled unrelated addons.");
    if (name == "application-metadata")
    {
        if (assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product != "AST Application Test" ||
            assembly.GetName().Version != new Version(1, 2, 3, 0)) throw new Exception("Executable metadata not embedded.");
    }
    if (name == "application-services")
    {
        var debug = await Execute("dotnet", [Path.Combine(astDirectory, "Generated.dll"), "--appdebug"]);
        if (debug.Exit != 0 || !debug.Text.Contains("XPScript: DEBUG: diagnostic") || !debug.Text.Contains("APPDEBUG_ENABLED")) throw new Exception("Shared debug activation lost.");
    }
    // Normal compilation currently rejects With Application (undefined namespace value).
    if (name == "application-with") continue;
    var normalDirectory = Path.Combine(output, name, "normal"); Directory.CreateDirectory(normalDirectory);
    var normalProgram = Path.Combine(normalDirectory, OperatingSystem.IsWindows() ? "program.exe" : "program");
    await Run("dotnet", [compiler, "legacy-compile", source, "-o", normalProgram, "--single-file=false", "--runtime=false"]);
    var normal = await Execute(normalProgram, arguments);
    if (normal.Exit != expectedExit || normal.Text.Trim() != result.Text.Trim()) throw new Exception("Normal/AST Application parity failed: " + name + normal.Text);
}
Console.WriteLine("AST_APPLICATION_RUNTIME_OK");

async Task Run(string file, string[] commandArguments)
{
    var result = await Execute(file, commandArguments);
    if (result.Exit != 0) throw new Exception($"Process failed ({result.Exit}): {result.Text}");
}
async Task<(int Exit, string Text)> Execute(string file, string[] commandArguments)
{
    var start = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = output };
    foreach (var argument in commandArguments) start.ArgumentList.Add(argument);
    foreach (var (variable, folder) in new[] { ("XDG_CONFIG_HOME", "config"), ("XDG_CACHE_HOME", "cache"), ("XDG_DATA_HOME", "data") })
    {
        var directory = Path.Combine(output, folder); Directory.CreateDirectory(directory); start.Environment[variable] = directory;
    }
    using var process = Process.Start(start)!;
    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
    try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(120)); } catch { process.Kill(true); throw; }
    return (process.ExitCode, await stdout + await stderr);
}
