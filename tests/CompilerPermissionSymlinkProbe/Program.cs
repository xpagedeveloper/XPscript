using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using XPScript.Compiler;

var securityType = typeof(CompilerDriver).Assembly.GetType("XPScript.Compiler.CompilerPathSecurity", throwOnError: true)!;
var hardenDirectory = securityType.GetMethod("HardenTemporaryDirectory", BindingFlags.Static | BindingFlags.Public)!;
var hardenFile = securityType.GetMethod("HardenTemporaryFile", BindingFlags.Static | BindingFlags.Public)!;
var createOwnedDirectory = securityType.GetMethod("CreateOwnedTemporaryDirectory", BindingFlags.Static | BindingFlags.Public)!;
var deleteOwnedDirectory = securityType.GetMethod("DeleteOwnedTemporaryDirectory", BindingFlags.Static | BindingFlags.Public)!;
var resolveProjectLocal = securityType.GetMethod("ResolveProjectLocalFile", BindingFlags.Static | BindingFlags.Public)!;
var resolveNative = securityType.GetMethod("ResolveApplicationLocalNativeFile", BindingFlags.Static | BindingFlags.Public)!;

var probeRoot = Path.Combine(Directory.GetCurrentDirectory(), ".permission-symlink-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(probeRoot);

try
{
    await VerifyPermissionsAsync();
    await VerifyDependencySymlinkEscapeAsync();
    VerifyBrokenSymlinkRejected();
    await VerifyCleanupDoesNotFollowLinksAsync();
    await VerifyLinkedWorkspaceRootRejectedAsync();
    Console.WriteLine("PERMISSION-SYMLINK=OK");
}
finally
{
    try { Directory.Delete(probeRoot, recursive: true); } catch { }
}

async Task VerifyPermissionsAsync()
{
    var dir = Path.Combine(probeRoot, "permission-dir");
    Directory.CreateDirectory(dir);
    Invoke(hardenDirectory, dir);
    var file = Path.Combine(dir, "generated.tmp");
    await File.WriteAllTextAsync(file, "secret");
    Invoke(hardenFile, file);

    if (OperatingSystem.IsWindows())
    {
        using var identity = WindowsIdentity.GetCurrent();
        var accountName = identity.Name;
        if (string.IsNullOrWhiteSpace(accountName))
            throw new Exception("Current Windows account name unavailable for ACL verification.");

        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "icacls.exe"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(dir);
        using var process = Process.Start(psi) ?? throw new Exception("Unable to start icacls.");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new Exception("icacls query failed: " + stderr);
        if (!stdout.Contains(accountName, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Hardened Windows ACL does not contain the current Windows identity: " + stdout);
        if (!stdout.Contains("(OI)(CI)(F)", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Hardened Windows ACL does not grant inheritable full control to the current identity: " + stdout);
    }
    else
    {
        var dirMode = File.GetUnixFileMode(dir);
        var fileMode = File.GetUnixFileMode(file);
        var expectedDir = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        var expectedFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        if (dirMode != expectedDir) throw new Exception($"Expected directory mode 0700, got {dirMode}.");
        if (fileMode != expectedFile) throw new Exception($"Expected file mode 0600, got {fileMode}.");
    }
}

async Task VerifyDependencySymlinkEscapeAsync()
{
    var sourceRoot = Path.Combine(probeRoot, "source-root");
    var outsideRoot = Path.Combine(probeRoot, "outside-root");
    Directory.CreateDirectory(sourceRoot);
    Directory.CreateDirectory(outsideRoot);
    var outsideFile = Path.Combine(outsideRoot, "outside.dll");
    await File.WriteAllTextAsync(outsideFile, "outside");
    var link = Path.Combine(sourceRoot, "escape");
    CreateDirectoryLink(link, outsideRoot);

    ExpectCompilerFailure(() => Invoke(resolveProjectLocal, sourceRoot, Path.Combine("escape", "outside.dll"), "Managed Reference"));
    ExpectCompilerFailure(() => Invoke(resolveNative, sourceRoot, Path.Combine("escape", "outside.dll")));
}

void VerifyBrokenSymlinkRejected()
{
    if (OperatingSystem.IsWindows()) return;

    var sourceRoot = Path.Combine(probeRoot, "broken-source-root");
    Directory.CreateDirectory(sourceRoot);
    var broken = Path.Combine(sourceRoot, "broken.dll");
    File.CreateSymbolicLink(broken, Path.Combine(probeRoot, "does-not-exist.dll"));
    ExpectCompilerFailure(() => Invoke(resolveProjectLocal, sourceRoot, "broken.dll", "Managed Reference"));
    ExpectCompilerFailure(() => Invoke(resolveNative, sourceRoot, "broken.dll"));
}

async Task VerifyCleanupDoesNotFollowLinksAsync()
{
    var outside = Path.Combine(probeRoot, "cleanup-outside");
    Directory.CreateDirectory(outside);
    var sentinel = Path.Combine(outside, "sentinel.txt");
    await File.WriteAllTextAsync(sentinel, "KEEP");

    var workspace = (string)Invoke(createOwnedDirectory, "link-child-")!;
    var link = Path.Combine(workspace, "external-link");
    CreateDirectoryLink(link, outside);
    Invoke(deleteOwnedDirectory, workspace);

    if (Directory.Exists(workspace)) throw new Exception("Owned workspace was not deleted.");
    if (!File.Exists(sentinel) || await File.ReadAllTextAsync(sentinel) != "KEEP")
        throw new Exception("Cleanup followed a link/reparse point and touched external data.");
}

async Task VerifyLinkedWorkspaceRootRejectedAsync()
{
    var external = Path.Combine(probeRoot, "linked-root-target");
    Directory.CreateDirectory(external);
    var sentinel = Path.Combine(external, "sentinel.txt");
    await File.WriteAllTextAsync(sentinel, "KEEP");

    var tempRoot = Path.Combine(Path.GetTempPath(), "XPScript");
    Directory.CreateDirectory(tempRoot);
    var linkedRoot = Path.Combine(tempRoot, "linked-root-" + Guid.NewGuid().ToString("N"));
    CreateDirectoryLink(linkedRoot, external);
    try
    {
        ExpectCompilerFailure(() => Invoke(deleteOwnedDirectory, linkedRoot));
        if (!File.Exists(sentinel)) throw new Exception("Linked workspace-root cleanup touched external data.");
    }
    finally
    {
        try { new DirectoryInfo(linkedRoot).Delete(recursive: false); } catch { }
    }
}

object? Invoke(MethodInfo method, params object?[] args)
{
    try { return method.Invoke(null, args); }
    catch (TargetInvocationException ex) when (ex.InnerException is not null) { throw ex.InnerException; }
}

void ExpectCompilerFailure(Action action)
{
    try
    {
        action();
        throw new Exception("Expected compiler security rejection did not occur.");
    }
    catch (Exception ex) when (ex.GetType().FullName == "XPScript.Compiler.CompilerException")
    {
    }
}

void CreateDirectoryLink(string linkPath, string targetPath)
{
    if (!OperatingSystem.IsWindows())
    {
        Directory.CreateSymbolicLink(linkPath, targetPath);
        return;
    }

    var psi = new ProcessStartInfo
    {
        FileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    psi.ArgumentList.Add("/d");
    psi.ArgumentList.Add("/c");
    psi.ArgumentList.Add("mklink");
    psi.ArgumentList.Add("/J");
    psi.ArgumentList.Add(linkPath);
    psi.ArgumentList.Add(targetPath);
    using var process = Process.Start(psi) ?? throw new Exception("Unable to create Windows junction.");
    process.WaitForExit();
    if (process.ExitCode != 0)
        throw new Exception("Unable to create Windows junction for filesystem security probe.");
}
