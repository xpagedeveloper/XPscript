using System.Diagnostics;
using System.Reflection;
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

    var rid = CompilerDriver.CurrentRuntimeIdentifier();
    var nativeName = rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) ? "fixture-native.dll"
        : rid.StartsWith("linux-", StringComparison.OrdinalIgnoreCase) ? "libfixture-native.so"
        : "libfixture-native.dylib";

    await VerifyPositiveDeploymentAsync(fixtureDll, rid, nativeName);
    await VerifyPathBoundaryFailuresAsync(fixtureDll, rid, nativeName);
    await VerifyCollisionAndMissingFailuresAsync(fixtureDll, rid, nativeName);
    await VerifySymlinkFailuresAsync(fixtureDll, rid, nativeName);
    await VerifyExistingFinalTargetPreservedAsync(rid);
    VerifyOpenedHandleCannotBeRedirected();

    Console.WriteLine("PROJECT-LOCAL-DEPENDENCY-SECURITY=OK");
}
finally
{
    try { Directory.Delete(probeRoot, recursive: true); } catch { }
}

async Task VerifyPositiveDeploymentAsync(string fixtureDll, string rid, string nativeName)
{
    var root = NewCase("positive");
    var managedDir = Path.Combine(root, "managed");
    Directory.CreateDirectory(managedDir);
    File.Copy(fixtureDll, Path.Combine(managedDir, "ManagedReferenceFixture.dll"), true);
    await File.WriteAllTextAsync(Path.Combine(managedDir, nativeName), "native-fixture");

    var mismatchRid = rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) ? "linux-x64" : "win-x64";
    var sourcePath = Path.Combine(root, "managed-reference.xps");
    await File.WriteAllTextAsync(sourcePath, $"""
Reference "managed/ManagedReferenceFixture.dll"
ReferenceNative "managed/{nativeName}" Runtime "{rid}"
ReferenceNative "managed/this-file-does-not-exist.bin" Runtime "{mismatchRid}"
Sub Main()
    Print "MANAGED-REFERENCE=OK"
End Sub
""");

    await VerifyModeAsync(sourcePath, rid, nativeName, false, "framework-dependent");
    await VerifyModeAsync(sourcePath, rid, nativeName, true, "self-contained");
}

async Task VerifyPathBoundaryFailuresAsync(string fixtureDll, string rid, string nativeName)
{
    var root = NewCase("path-boundaries");
    var source = Path.Combine(root, "test.xps");
    var outside = Path.Combine(probeRoot, "outside-managed.dll");
    File.Copy(fixtureDll, outside, true);
    var absoluteManaged = Portable(outside);
    await ExpectCompileFailureAsync(source, $"Reference \"{absoluteManaged}\"\nSub Main()\nEnd Sub", rid, "must be relative");

    var absNative = Portable(Path.Combine(probeRoot, nativeName));
    await File.WriteAllTextAsync(Path.Combine(probeRoot, nativeName), "native");
    await ExpectCompileFailureAsync(source, $"ReferenceNative \"{absNative}\" Runtime \"{rid}\"\nSub Main()\nEnd Sub", rid, "must be relative");
    await ExpectCompileFailureAsync(source, $"Declare Function X Lib \"{absNative}\" () As Integer\nSub Main()\nEnd Sub", rid, "must be relative");

    await ExpectCompileFailureAsync(source, "Reference \"../outside-managed.dll\"\nSub Main()\nEnd Sub", rid, "remain inside");
}

async Task VerifyCollisionAndMissingFailuresAsync(string fixtureDll, string rid, string nativeName)
{
    var root = NewCase("collisions");
    var source = Path.Combine(root, "test.xps");
    await ExpectCompileFailureAsync(source, "Reference \"missing.dll\"\nSub Main()\nEnd Sub", rid, "Managed Reference");
    await ExpectCompileFailureAsync(source, $"ReferenceNative \"missing-{nativeName}\" Runtime \"{rid}\"\nSub Main()\nEnd Sub", rid, "ReferenceNative");

    Directory.CreateDirectory(Path.Combine(root, "a"));
    Directory.CreateDirectory(Path.Combine(root, "b"));
    File.Copy(fixtureDll, Path.Combine(root, "a", "same.dll"), true);
    File.Copy(fixtureDll, Path.Combine(root, "b", "same.dll"), true);
    await ExpectCompileFailureAsync(source, "Reference \"a/same.dll\"\nReference \"b/same.dll\"\nSub Main()\nEnd Sub", rid, "same file name");

    var outputName = OperatingSystem.IsWindows() ? "app.exe" : "app";
    await File.WriteAllTextAsync(Path.Combine(root, outputName), "dependency-collision");
    await File.WriteAllTextAsync(source, $"ReferenceNative \"{outputName}\" Runtime \"{rid}\"\nSub Main()\n Print \"X\"\nEnd Sub");
    var driver = new CompilerDriver();
    try
    {
        await driver.CompileAsync(source, Path.Combine(root, outputName), false, rid);
        throw new Exception("Expected executable/dependency overwrite collision to fail.");
    }
    catch (CompilerException) { }
}

async Task VerifySymlinkFailuresAsync(string fixtureDll, string rid, string nativeName)
{
    var root = NewCase("symlinks");
    var source = Path.Combine(root, "test.xps");
    var outsideDir = Path.Combine(probeRoot, "outside-dir");
    Directory.CreateDirectory(outsideDir);
    File.Copy(fixtureDll, Path.Combine(outsideDir, "ManagedReferenceFixture.dll"), true);

    var linkDir = Path.Combine(root, "outside-link");
    Directory.CreateSymbolicLink(linkDir, outsideDir);
    await ExpectCompileFailureAsync(source, "Reference \"outside-link/ManagedReferenceFixture.dll\"\nSub Main()\nEnd Sub", rid, "symbolic link");

    var broken = Path.Combine(root, "broken.dll");
    File.CreateSymbolicLink(broken, Path.Combine(root, "does-not-exist.dll"));
    await ExpectCompileFailureAsync(source, "Reference \"broken.dll\"\nSub Main()\nEnd Sub", rid, "symbolic link");

    var realManaged = Path.Combine(root, "real.dll");
    File.Copy(fixtureDll, realManaged, true);
    var managedLink = Path.Combine(root, "managed-link.dll");
    File.CreateSymbolicLink(managedLink, realManaged);
    await ExpectCompileFailureAsync(source, "Reference \"managed-link.dll\"\nSub Main()\nEnd Sub", rid, "symbolic link");

    var realNative = Path.Combine(root, nativeName);
    await File.WriteAllTextAsync(realNative, "trusted-native");
    var nativeLinkName = "link-" + nativeName;
    var nativeLink = Path.Combine(root, nativeLinkName);
    File.CreateSymbolicLink(nativeLink, realNative);
    await ExpectCompileFailureAsync(source, $"ReferenceNative \"{nativeLinkName}\" Runtime \"{rid}\"\nSub Main()\nEnd Sub", rid, "symbolic link");
}

async Task VerifyExistingFinalTargetPreservedAsync(string rid)
{
    var root = NewCase("same-target");
    var outputName = OperatingSystem.IsWindows() ? "app.exe" : "app";
    var dependencyName = rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) ? "same-target.dll"
        : rid.StartsWith("linux-", StringComparison.OrdinalIgnoreCase) ? "libsame-target.so"
        : "libsame-target.dylib";
    var dependency = Path.Combine(root, dependencyName);
    const string payload = "same-target-preserved";
    await File.WriteAllTextAsync(dependency, payload);
    var source = Path.Combine(root, "same-target.xps");
    await File.WriteAllTextAsync(source, $"ReferenceNative \"{dependencyName}\" Runtime \"{rid}\"\nSub Main()\n Print \"OK\"\nEnd Sub");
    await new CompilerDriver().CompileAsync(source, Path.Combine(root, outputName), false, rid);
    if (await File.ReadAllTextAsync(dependency) != payload) throw new Exception("Dependency already at final target was modified.");
}

void VerifyOpenedHandleCannotBeRedirected()
{
    var compilerAssembly = typeof(CompilerDriver).Assembly;
    var type = compilerAssembly.GetType("XPScript.Compiler.CompilerSecureFileCopy", throwOnError: true)!;
    var hookField = type.GetField("AfterSourceOpenedForTesting", BindingFlags.Static | BindingFlags.NonPublic)!;
    var copy = type.GetMethod("CopyValidatedRegularFile", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
    var root = NewCase("handle-race");
    var source = Path.Combine(root, "source.bin");
    var moved = Path.Combine(root, "opened-original.bin");
    var destination = Path.Combine(root, "staged.bin");
    File.WriteAllText(source, "ORIGINAL");

    Action<string> hook = opened =>
    {
        File.Move(opened, moved);
        File.WriteAllText(opened, "REPLACEMENT");
    };
    hookField.SetValue(null, hook);
    try
    {
        try
        {
            copy.Invoke(null, [source, destination, "Dependency"]);
        }
        catch (TargetInvocationException ex) when (OperatingSystem.IsWindows() && ex.InnerException is CompilerException)
        {
            return;
        }

        if (!File.Exists(destination)) throw new Exception("Secure copy did not produce staged file.");
        if (File.ReadAllText(destination) != "ORIGINAL") throw new Exception("Opened dependency handle was redirected to replacement pathname content.");
    }
    finally
    {
        hookField.SetValue(null, null);
    }
}

async Task VerifyModeAsync(string sourcePath, string rid, string nativeName, bool selfContained, string mode)
{
    var outputDir = Path.Combine(Path.GetDirectoryName(sourcePath)!, mode);
    Directory.CreateDirectory(outputDir);
    var outputPath = Path.Combine(outputDir, OperatingSystem.IsWindows() ? "app.exe" : "app");
    await new CompilerDriver().CompileAsync(sourcePath, outputPath, selfContained, rid);
    if (!File.Exists(outputPath)) throw new Exception($"{mode}: compiler output missing.");
    if (!File.Exists(Path.Combine(outputDir, nativeName))) throw new Exception($"{mode}: matching ReferenceNative file was not deployed.");

    var psi = new ProcessStartInfo(outputPath) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
    using var process = Process.Start(psi) ?? throw new Exception($"{mode}: unable to start generated executable.");
    var stdout = await process.StandardOutput.ReadToEndAsync();
    var stderr = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new Exception($"{mode}: generated executable failed: {stderr}");
    if (!stdout.Contains("MANAGED-REFERENCE=OK", StringComparison.Ordinal)) throw new Exception($"{mode}: generated executable output mismatch.");
}

async Task ExpectCompileFailureAsync(string sourcePath, string content, string rid, string expected)
{
    await File.WriteAllTextAsync(sourcePath, content);
    var output = Path.Combine(Path.GetDirectoryName(sourcePath)!, OperatingSystem.IsWindows() ? "failure.exe" : "failure");
    try
    {
        await new CompilerDriver().CompileAsync(sourcePath, output, false, rid);
        throw new Exception("Expected compile failure containing: " + expected);
    }
    catch (CompilerException ex)
    {
        if (!ex.Message.Contains(expected, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Unexpected compiler error. Expected fragment '" + expected + "', got: " + ex.Message);
    }
}

string NewCase(string name)
{
    var path = Path.Combine(probeRoot, name + "-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static string Portable(string path) => Path.GetFullPath(path).Replace('\\', '/');

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
