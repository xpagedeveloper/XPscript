using System.Diagnostics;
using XPScript.Compiler;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var fixtureProject = Path.Combine(repoRoot, "tests", "ManagedReferenceFixture", "ManagedReferenceFixture.csproj");
var probeRoot = Path.Combine(repoRoot, ".managed-reference-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(probeRoot);

try
{
    await RunProcessAsync("dotnet", ["build", fixtureProject, "-c", "Release", "--nologo"]);
    var fixtureDll = Path.Combine(repoRoot, "tests", "ManagedReferenceFixture", "bin", "Release", "net10.0", "ManagedReferenceFixture.dll");
    if (!File.Exists(fixtureDll)) throw new Exception("Managed fixture DLL was not built.");

    var managedDir = Path.Combine(probeRoot, "managed");
    Directory.CreateDirectory(managedDir);
    File.Copy(fixtureDll, Path.Combine(managedDir, "ManagedReferenceFixture.dll"), overwrite: true);

    var rid = CompilerDriver.CurrentRuntimeIdentifier();
    var nativeName = rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) ? "fixture-native.dll"
        : rid.StartsWith("linux-", StringComparison.OrdinalIgnoreCase) ? "libfixture-native.so"
        : "libfixture-native.dylib";
    await File.WriteAllTextAsync(Path.Combine(managedDir, nativeName), "native-fixture");

    var mismatchRid = rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) ? "linux-x64" : "win-x64";
    var sourcePath = Path.Combine(probeRoot, "managed-reference.xps");
    await File.WriteAllTextAsync(sourcePath, $"""
Reference "managed/ManagedReferenceFixture.dll"
ReferenceNative "managed/{nativeName}" Runtime "{rid}"
ReferenceNative "managed/this-file-does-not-exist.bin" Runtime "{mismatchRid}"

Sub Main()
    Print "MANAGED-REFERENCE=OK"
End Sub
""");

    await VerifyModeAsync(sourcePath, rid, nativeName, selfContained: false, "framework-dependent");
    await VerifyModeAsync(sourcePath, rid, nativeName, selfContained: true, "self-contained");

    Console.WriteLine("MANAGED-REFERENCE-DEPLOYMENT=OK");
}
finally
{
    try { Directory.Delete(probeRoot, recursive: true); } catch { }
}

async Task VerifyModeAsync(string sourcePath, string rid, string nativeName, bool selfContained, string mode)
{
    var outputDir = Path.Combine(probeRoot, mode);
    Directory.CreateDirectory(outputDir);
    var outputPath = Path.Combine(outputDir, OperatingSystem.IsWindows() ? "app.exe" : "app");
    var driver = new CompilerDriver();
    await driver.CompileAsync(sourcePath, outputPath, selfContained, rid);

    if (!File.Exists(outputPath)) throw new Exception($"{mode}: compiler output missing.");
    if (!File.Exists(Path.Combine(outputDir, nativeName))) throw new Exception($"{mode}: matching ReferenceNative file was not deployed.");
    if (File.Exists(Path.Combine(outputDir, "this-file-does-not-exist.bin"))) throw new Exception($"{mode}: non-matching RID native reference was packaged.");

    var psi = new ProcessStartInfo(outputPath) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    using var process = Process.Start(psi) ?? throw new Exception($"{mode}: unable to start generated executable.");
    var stdout = await process.StandardOutput.ReadToEndAsync();
    var stderr = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new Exception($"{mode}: generated executable failed: {stderr}");
    if (!stdout.Contains("MANAGED-REFERENCE=OK", StringComparison.Ordinal)) throw new Exception($"{mode}: generated executable output mismatch.");
}

async Task RunProcessAsync(string fileName, IReadOnlyList<string> arguments)
{
    var psi = new ProcessStartInfo(fileName) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var argument in arguments) psi.ArgumentList.Add(argument);
    using var process = Process.Start(psi) ?? throw new Exception("Unable to start " + fileName);
    var stdout = await process.StandardOutput.ReadToEndAsync();
    var stderr = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new Exception(fileName + " failed: " + stdout + Environment.NewLine + stderr);
}
