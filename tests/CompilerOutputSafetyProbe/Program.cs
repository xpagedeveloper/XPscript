using System.Collections;
using System.Diagnostics;
using System.Reflection;
using XPScript.Compiler;

var assembly = typeof(CompilerDriver).Assembly;
var publisherType = assembly.GetType("XPScript.Compiler.CompilerOutputPublisher", throwOnError: true)!;
var securityType = assembly.GetType("XPScript.Compiler.CompilerPathSecurity", throwOnError: true)!;
var secureCopyType = assembly.GetType("XPScript.Compiler.CompilerSecureFileCopy", throwOnError: true)!;

var publishMethod = publisherType.GetMethod("Publish", BindingFlags.Static | BindingFlags.Public)!;
var publishStaged = publisherType.GetMethod("PublishStaged", BindingFlags.Static | BindingFlags.NonPublic)!;
var rejectProtected = publisherType.GetMethod("RejectProtectedCompilerTarget", BindingFlags.Static | BindingFlags.NonPublic)!;
var dependencyType = publisherType.GetNestedType("Dependency", BindingFlags.NonPublic)!;
var nativeDependencyType = assembly.GetType("XPScript.Compiler.NativeDependencyPackager+Dependency", throwOnError: true)!;
var nativeReferenceType = assembly.GetType("XPScript.Compiler.ManagedAssemblyReferencePreprocessor+NativeReference", throwOnError: true)!;
var resolveProjectLocal = securityType.GetMethod("ResolveProjectLocalFile", BindingFlags.Static | BindingFlags.Public)!;
var resolveNative = securityType.GetMethod("ResolveApplicationLocalNativeFile", BindingFlags.Static | BindingFlags.Public)!;
var secureCopy = secureCopyType.GetMethod("CopyValidatedRegularFile", BindingFlags.Static | BindingFlags.Public)!;

var root = Path.Combine(Directory.GetCurrentDirectory(), ".output-safety-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    await VerifyOutputNormalizationAndOverwriteAsync();
    await VerifyDirectoryAndSourceCollisionRejectionAsync();
    await VerifyLinkedDestinationRejectionAsync();
    await VerifyDependencyNamesSelfTargetAndCollisionAsync();
    await VerifyRollbackAndExecutableLastAsync();
    VerifyProtectedCompilerTargetRejected();
    await VerifyProjectLocalContainmentAsync();
    await VerifySecureCopyAsync();
    Console.WriteLine("COMPILER-OUTPUT-SAFETY=OK");
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch { }
}

async Task VerifyOutputNormalizationAndOverwriteAsync()
{
    var area = Path.Combine(root, "normalize");
    Directory.CreateDirectory(area);
    var generated = Path.Combine(area, "generated.bin");
    await File.WriteAllTextAsync(generated, "NEW");
    var expected = Path.Combine(area, "final.bin");
    await File.WriteAllTextAsync(expected, "OLD");
    var requested = Path.Combine(area, "sub", "..", "final.bin");

    Invoke(publishStaged, generated, requested, NewDependencyList(), false);

    if (await File.ReadAllTextAsync(expected) != "NEW")
        throw new Exception("Normalized existing regular output was not replaced.");
    if (Directory.EnumerateDirectories(area, ".xpscript-publish-*", SearchOption.TopDirectoryOnly).Any())
        throw new Exception("Publication staging directory remained after success.");
}

async Task VerifyDirectoryAndSourceCollisionRejectionAsync()
{
    var area = Path.Combine(root, "basic-rejections");
    Directory.CreateDirectory(area);
    var generated = Path.Combine(area, "generated.bin");
    await File.WriteAllTextAsync(generated, "NEW");
    var outputDirectory = Path.Combine(area, "directory-target");
    Directory.CreateDirectory(outputDirectory);
    ExpectCompilerFailure(() => Invoke(publishStaged, generated, outputDirectory, NewDependencyList(), false));

    var source = Path.Combine(area, "source.xps");
    await File.WriteAllTextAsync(source, "Sub Main()\nEnd Sub\n");
    ExpectCompilerFailure(() => Invoke(
        publishMethod,
        generated,
        source,
        source,
        NewGenericList(nativeDependencyType),
        NewGenericList(nativeReferenceType),
        false));
}

async Task VerifyLinkedDestinationRejectionAsync()
{
    var area = Path.Combine(root, "linked-output");
    var real = Path.Combine(area, "real");
    var link = Path.Combine(area, "link");
    Directory.CreateDirectory(real);
    Directory.CreateDirectory(area);
    CreateDirectoryLink(link, real);
    var generated = Path.Combine(area, "generated.bin");
    await File.WriteAllTextAsync(generated, "NEW");
    ExpectCompilerFailure(() => Invoke(publishStaged, generated, Path.Combine(link, "out.bin"), NewDependencyList(), false));
}

async Task VerifyDependencyNamesSelfTargetAndCollisionAsync()
{
    var area = Path.Combine(root, "dependencies");
    Directory.CreateDirectory(area);
    var generated = Path.Combine(area, "generated.bin");
    await File.WriteAllTextAsync(generated, "EXE");

    var native = Path.Combine(area, "native.bin");
    await File.WriteAllTextAsync(native, "KEEP");
    var selfList = NewDependencyList();
    AddDependency(selfList, native, "native.bin");
    var output = Path.Combine(area, "app.bin");
    Invoke(publishStaged, generated, output, selfList, false);
    if (await File.ReadAllTextAsync(native) != "KEEP")
        throw new Exception("Dependency already at final target was modified.");
    if (!File.Exists(output)) throw new Exception("Executable was not published with self-target dependency.");

    var badNameSource = Path.Combine(area, "bad-name-source.bin");
    await File.WriteAllTextAsync(badNameSource, "X");
    var badNameList = NewDependencyList();
    AddDependency(badNameList, badNameSource, Path.Combine("sub", "bad.bin"));
    ExpectCompilerFailure(() => Invoke(publishStaged, generated, Path.Combine(area, "bad-name-app.bin"), badNameList, false));

    var first = Path.Combine(area, "first.bin");
    var second = Path.Combine(area, "second.bin");
    await File.WriteAllTextAsync(first, "FIRST");
    await File.WriteAllTextAsync(second, "SECOND");
    var collision = NewDependencyList();
    AddDependency(collision, first, "same.bin");
    AddDependency(collision, second, "same.bin");
    ExpectCompilerFailure(() => Invoke(publishStaged, generated, Path.Combine(area, "collision-app.bin"), collision, false));
}

async Task VerifyRollbackAndExecutableLastAsync()
{
    var area = Path.Combine(root, "rollback");
    Directory.CreateDirectory(area);
    var generated = Path.Combine(area, "generated.bin");
    await File.WriteAllTextAsync(generated, "NEW-EXE");
    var output = Path.Combine(area, "app.bin");
    await File.WriteAllTextAsync(output, "OLD-EXE");

    var dep1Source = Path.Combine(root, "dep1-new.bin");
    var dep2Source = Path.Combine(root, "dep2-new.bin");
    await File.WriteAllTextAsync(dep1Source, "NEW-DEP1");
    await File.WriteAllTextAsync(dep2Source, "NEW-DEP2");
    var dep1Target = Path.Combine(area, "dep1.bin");
    await File.WriteAllTextAsync(dep1Target, "OLD-DEP1");
    var dep2Target = Path.Combine(area, "dep2.bin");
    Directory.CreateDirectory(dep2Target); // Forces CommitBatch failure after dep1 has been installed.

    var deps = NewDependencyList();
    AddDependency(deps, dep1Source, "dep1.bin");
    AddDependency(deps, dep2Source, "dep2.bin");
    ExpectCompilerFailure(() => Invoke(publishStaged, generated, output, deps, false));

    if (await File.ReadAllTextAsync(dep1Target) != "OLD-DEP1")
        throw new Exception("Rollback did not restore the prior dependency.");
    if (await File.ReadAllTextAsync(output) != "OLD-EXE")
        throw new Exception("Executable changed before dependencies committed successfully.");
    if (Directory.EnumerateDirectories(area, ".xpscript-publish-*", SearchOption.TopDirectoryOnly).Any())
        throw new Exception("Publication staging directory remained after rollback.");
}

void VerifyProtectedCompilerTargetRejected()
{
    var compilerAssemblyPath = typeof(CompilerDriver).Assembly.Location;
    if (string.IsNullOrWhiteSpace(compilerAssemblyPath))
        throw new Exception("Compiler assembly path unavailable for protected-target verification.");
    ExpectCompilerFailure(() => Invoke(rejectProtected, compilerAssemblyPath));
}

async Task VerifyProjectLocalContainmentAsync()
{
    var sourceRoot = Path.Combine(root, "containment", "src");
    var outsideRoot = Path.Combine(root, "containment", "outside");
    Directory.CreateDirectory(sourceRoot);
    Directory.CreateDirectory(outsideRoot);
    var inside = Path.Combine(sourceRoot, "inside.dll");
    var outside = Path.Combine(outsideRoot, "outside.dll");
    await File.WriteAllTextAsync(inside, "IN");
    await File.WriteAllTextAsync(outside, "OUT");

    var resolved = (string)Invoke(resolveProjectLocal, sourceRoot, "inside.dll", "Managed Reference")!;
    if (!Path.GetFullPath(resolved).Equals(Path.GetFullPath(inside), PathComparison()))
        throw new Exception("Project-local regular path did not resolve correctly.");

    ExpectCompilerFailure(() => Invoke(resolveProjectLocal, sourceRoot, Path.GetFullPath(outside), "Managed Reference"));
    ExpectCompilerFailure(() => Invoke(resolveNative, sourceRoot, Path.GetFullPath(outside)));
    ExpectCompilerFailure(() => Invoke(resolveProjectLocal, sourceRoot, Path.Combine("..", "outside", "outside.dll"), "Managed Reference"));
    ExpectCompilerFailure(() => Invoke(resolveNative, sourceRoot, Path.Combine("..", "outside", "outside.dll")));
}

async Task VerifySecureCopyAsync()
{
    var area = Path.Combine(root, "secure-copy");
    Directory.CreateDirectory(area);
    var source = Path.Combine(area, "source.bin");
    var destination = Path.Combine(area, "destination.bin");
    await File.WriteAllTextAsync(source, "HANDLE-COPY");
    Invoke(secureCopy, source, destination, "Probe dependency");
    if (await File.ReadAllTextAsync(destination) != "HANDLE-COPY")
        throw new Exception("Secure dependency copy failed.");

    if (!OperatingSystem.IsWindows())
    {
        var link = Path.Combine(area, "source-link.bin");
        File.CreateSymbolicLink(link, source);
        ExpectCompilerFailure(() => Invoke(secureCopy, link, Path.Combine(area, "link-copy.bin"), "Probe dependency"));
    }
}

object NewDependencyList() => Activator.CreateInstance(typeof(List<>).MakeGenericType(dependencyType))!;
object NewGenericList(Type elementType) => Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;

void AddDependency(object list, string sourcePath, string fileName)
{
    var item = Activator.CreateInstance(dependencyType, sourcePath, fileName)
        ?? throw new Exception("Unable to create publisher dependency test value.");
    ((IList)list).Add(item);
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
        throw new Exception("Expected compiler security/publication rejection did not occur.");
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
        throw new Exception("Unable to create Windows junction for output safety probe.");
}

StringComparison PathComparison() => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
