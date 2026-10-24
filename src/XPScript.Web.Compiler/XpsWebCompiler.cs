using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using XPScript.Compiler;
using XPScript.Web.Runtime;

namespace XPScript.Web.Compiler;

public sealed class XpsWebCompiler
{
    private const string MicrosoftDataSqliteVersion = "10.0.11";
    private const string MicrosoftDataSqlClientVersion = "7.0.2";
    private const long DefaultPersistentCacheMaxBytes = 512L * 1024 * 1024;
    private static readonly TimeSpan DefaultPersistentCacheMaxAge = TimeSpan.FromDays(7);
    private static readonly TimeSpan PersistentCacheSweepInterval = TimeSpan.FromMinutes(5);
    private static readonly Regex MainOrInitialize = new(@"(?im)^\s*(?:Public\s+|Private\s+)?Sub\s+(?:Main|Initialize)\b", RegexOptions.CultureInvariant);
    private static readonly Regex ScriptClassMarker = new(@"internal\s+static\s+class\s+Script\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex VariantDeclaration = new(@"(?im)^\s*Dim\s+([A-Za-z_]\w*)\s+As\s+Variant\s*$", RegexOptions.CultureInvariant);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> PersistentCacheSweeps = new(StringComparer.OrdinalIgnoreCase);

    public Task<XpsCompiledWebUnit> CompileAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var fullSourcePath = Path.GetFullPath(sourcePath);
        var defaultRoot = Path.GetDirectoryName(fullSourcePath) ?? throw new XpsWebCompilationException("Unable to determine web source root.");
        return CompileAsync(fullSourcePath, defaultRoot, cancellationToken);
    }

    public Task<XpsCompiledWebUnit> CompileAsync(string sourcePath, string allowedSourceRoot, CancellationToken cancellationToken = default)
        => CompileCoreAsync(sourcePath, allowedSourceRoot, null, null, cancellationToken);

    internal Task<XpsCompiledWebUnit> CompileAndPersistAsync(
        string sourcePath,
        string allowedSourceRoot,
        string snapshotIdentity,
        string persistentCacheDirectory,
        CancellationToken cancellationToken = default)
        => CompileCoreAsync(sourcePath, allowedSourceRoot, snapshotIdentity, persistentCacheDirectory, cancellationToken);

    internal async Task<XpsCompiledWebUnit?> TryLoadPersistentAsync(
        string sourcePath,
        string allowedSourceRoot,
        string snapshotIdentity,
        string persistentCacheDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedSourceRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(persistentCacheDirectory);

        var fullSourcePath = Path.GetFullPath(sourcePath);
        var fullSourceRoot = Path.GetFullPath(allowedSourceRoot);
        if (!File.Exists(fullSourcePath)) return null;
        var source = await File.ReadAllTextAsync(fullSourcePath, cancellationToken).ConfigureAwait(false);
        var parsed = new XpsWebRouteMetadataParser().Parse(new ServerSideMetadataPreprocessor().Transform(source));
        if (string.Equals(parsed.Platform, "browser-wasm", StringComparison.OrdinalIgnoreCase) || parsed.Routes.Count == 0) return null;

        var artifactDirectory = PersistentArtifactDirectory(persistentCacheDirectory, fullSourceRoot, fullSourcePath, snapshotIdentity);
        MaintainPersistentCache(persistentCacheDirectory, artifactDirectory);
        var assemblyPath = Path.Combine(artifactDirectory, "XPScript.WebUnit.dll");
        if (!File.Exists(assemblyPath)) return null;

        try
        {
            var assemblyBytes = await File.ReadAllBytesAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
            var pdbPath = Path.Combine(artifactDirectory, "XPScript.WebUnit.pdb");
            var pdbBytes = File.Exists(pdbPath) ? await File.ReadAllBytesAsync(pdbPath, cancellationToken).ConfigureAwait(false) : null;
            TouchPersistentArtifact(artifactDirectory);
            return LoadCompiledUnit(assemblyBytes, pdbBytes, parsed, artifactDirectory, fullSourceRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or XpsWebCompilationException)
        {
            try { Directory.Delete(artifactDirectory, recursive: true); } catch { }
            return null;
        }
    }

    private async Task<XpsCompiledWebUnit> CompileCoreAsync(
        string sourcePath,
        string allowedSourceRoot,
        string? snapshotIdentity,
        string? persistentCacheDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowedSourceRoot);
        var fullSourcePath = Path.GetFullPath(sourcePath);
        var fullSourceRoot = Path.GetFullPath(allowedSourceRoot);
        if (!File.Exists(fullSourcePath)) throw new FileNotFoundException("Web source file was not found.", fullSourcePath);
        if (!Directory.Exists(fullSourceRoot)) throw new DirectoryNotFoundException("Web source root was not found.");
        if (!Path.GetExtension(fullSourcePath).Equals(".xps", StringComparison.OrdinalIgnoreCase)) throw new XpsWebCompilationException("Web source files must use the .xps extension.");

        DebugStep("read-source:start", fullSourcePath);
        var source = await File.ReadAllTextAsync(fullSourcePath, cancellationToken).ConfigureAwait(false);
        DebugStep("read-source:done", fullSourcePath);
        XpsWebRecursionValidator.Validate(source, fullSourcePath);
        var metadataSource = new ServerSideMetadataPreprocessor().Transform(source);
        if (source.Contains("[Platform:browser-wasm]", StringComparison.OrdinalIgnoreCase))
            metadataSource = BrowserWasmServerSideMetadata.TransformAuthorizationMetadata(source);
        var parsed = new XpsWebRouteMetadataParser().Parse(metadataSource);
        if (string.Equals(parsed.Platform, "browser-wasm", StringComparison.OrdinalIgnoreCase))
            return await CompileBrowserWasmAsync(fullSourcePath, fullSourceRoot, parsed, cancellationToken).ConfigureAwait(false);

        if (parsed.Routes.Count == 0) throw new XpsWebCompilationException("Web source must export at least one route using web route attributes.");

        var compilerSource = NormalizeVariantSetAssignments(ForceRouteParametersByVal(parsed.Source, parsed.Routes.Values));
        if (parsed.Routes.Values.Any(route => route.JsonSchema is not null))
            compilerSource += "\nPrivate Sub XpsCompilerGeneratedJsonSchemaRuntime()\n    Dim xpsCompilerGeneratedJsonSchema As XPJsonSchema\nEnd Sub\n";
        if (compilerSource.Contains(".Attachments", StringComparison.OrdinalIgnoreCase))
            compilerSource += "\n' XPScript compiler runtime feature: XPDBSQLite Attachments\n";
        compilerSource = EnsureCompilerEntryPoint(compilerSource);
        // AST is the normal web compiler path. Keep an explicit legacy escape hatch
        // for compatibility diagnostics while the remaining migration corpus is
        // completed; production callers do not need an opt-in flag.
        if (!string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_WEB_LEGACY"), "1", StringComparison.Ordinal))
        {
            compilerSource += Environment.NewLine + "Reference \"XPScript.Web.Runtime.dll\"" + Environment.NewLine;
            return await CompileAstWebAsync(fullSourcePath, fullSourceRoot, compilerSource, parsed, snapshotIdentity, persistentCacheDirectory, cancellationToken).ConfigureAwait(false);
        }
        string generated;
        try
        {
            DebugStep("transpile:start", fullSourcePath);
            generated = new XPScriptTranspiler().TranspileRestricted(compilerSource, fullSourcePath, CompilerDriver.CurrentRuntimeIdentifier(), [fullSourceRoot]);
            generated = InjectWebObjects(generated);
            generated = AstExperimentalCompiler.EnsureAttachmentRuntime(generated);
            DebugStep("transpile:done", fullSourcePath);
        }
        catch (CompilerException ex)
        {
            throw new XpsWebCompilationException("XPScript web compilation failed: " + ex.Message, ex);
        }

        ValidateRoutesExist(generated, parsed.Routes);
        var configuredTempRoot = Environment.GetEnvironmentVariable("XPSCRIPT_TEMP_ROOT");
        var workspaceRoot = string.IsNullOrWhiteSpace(configuredTempRoot)
            ? Path.Combine(Path.GetTempPath(), "XPScript")
            : Path.Combine(Path.GetFullPath(configuredTempRoot), "XPScript");
        var workspace = Path.Combine(workspaceRoot, "web", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        CompilerPathSecurity.HardenTemporaryDirectory(workspace);
        try
        {
            // Generated diagnostics retain the original XPScript source name via
            // #line directives. Stage that document beside the generated source
            // so Roslyn can resolve the mapped file during web-unit builds.
            var stagedSourcePath = Path.Combine(workspace, Path.GetFileName(fullSourcePath));
            File.Copy(fullSourcePath, stagedSourcePath, overwrite: true);
            var projectPath = Path.Combine(workspace, "WebUnit.csproj");
            var generatedPath = Path.Combine(workspace, "Generated.cs");
            var usesSqlite = generated.Contains("internal sealed class XPScriptDbSqlite", StringComparison.Ordinal);
            var usesMsSql = generated.Contains("internal sealed class XPScriptDbMsSql", StringComparison.Ordinal);
            var usesImage = generated.Contains("internal sealed class XPImage", StringComparison.Ordinal);
            await File.WriteAllTextAsync(projectPath, BuildProject(typeof(XpsWebContext).Assembly.Location, usesSqlite, usesMsSql, usesImage), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(generatedPath, generated, cancellationToken).ConfigureAwait(false);
            DebugStep($"workspace-ready image={usesImage}", fullSourcePath);

            var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = workspace, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            var publishDirectory = Path.Combine(workspace, "publish");
            Directory.CreateDirectory(publishDirectory);
            CompilerPathSecurity.HardenTemporaryDirectory(publishDirectory);
            psi.ArgumentList.Add("publish"); psi.ArgumentList.Add(projectPath); psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("Release"); psi.ArgumentList.Add("--nologo"); psi.ArgumentList.Add("--no-restore"); psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(publishDirectory);
            DebugStep("restore:start", fullSourcePath);
            await RunDotNetAsync(workspace, ["restore", projectPath, "--nologo"], cancellationToken).ConfigureAwait(false);
            DebugStep("restore:done", fullSourcePath);
            DebugStep("build:start", fullSourcePath);
            await RunProcessAsync(psi, cancellationToken).ConfigureAwait(false);
            DebugStep("build:done", fullSourcePath);

            var assemblyPath = Path.Combine(workspace, "publish", "XPScript.WebUnit.dll");
            if (!File.Exists(assemblyPath)) throw new XpsWebCompilationException("Web compiler completed without producing a loadable assembly.");
            var assemblyBytes = await File.ReadAllBytesAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
            var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            var pdbBytes = File.Exists(pdbPath) ? await File.ReadAllBytesAsync(pdbPath, cancellationToken).ConfigureAwait(false) : null;

            string dependencyDirectory = Path.GetDirectoryName(assemblyPath)!;
            if (!string.IsNullOrWhiteSpace(snapshotIdentity) && !string.IsNullOrWhiteSpace(persistentCacheDirectory))
            {
                dependencyDirectory = await PersistArtifactAsync(persistentCacheDirectory, fullSourceRoot, fullSourcePath, snapshotIdentity, assemblyBytes, pdbBytes, Path.GetDirectoryName(assemblyPath)!, cancellationToken).ConfigureAwait(false);
                DebugStep("dependencies:persisted", fullSourcePath);
            }

            DebugStep("load:start", fullSourcePath);
            var unit = LoadCompiledUnit(assemblyBytes, pdbBytes, parsed, dependencyDirectory, fullSourceRoot);
            DebugStep("load:done", fullSourcePath);
            return unit;
        }
        finally { try { Directory.Delete(workspace, recursive: true); } catch { } }
    }

    private static XpsCompiledWebUnit LoadCompiledUnit(byte[] assemblyBytes, byte[]? pdbBytes, XpsWebRouteParseResult parsed, string dependencyDirectory, string webRootPath)
    {
        var loadContext = new AssemblyLoadContext("XPScriptWeb-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        var componentAssemblyPath = Path.Combine(dependencyDirectory, "XPScript.WebUnit.dll");
        var dependencyResolver = File.Exists(componentAssemblyPath) ? new AssemblyDependencyResolver(componentAssemblyPath) : null;
        Assembly? ResolveManaged(AssemblyLoadContext context, AssemblyName name)
        {
            var shared = ResolveSharedAssembly(context, name);
            if (shared is not null) return shared;
            if (string.IsNullOrWhiteSpace(name.Name)) return null;
            var resolved = dependencyResolver?.ResolveAssemblyToPath(name);
            if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
                return context.LoadFromAssemblyPath(resolved);
            var candidate = Path.Combine(dependencyDirectory, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        }
        IntPtr ResolveNative(Assembly assembly, string name)
        {
            var resolved = dependencyResolver?.ResolveUnmanagedDllToPath(name);
            if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved) && NativeLibrary.TryLoad(resolved, out var resolvedHandle))
                return resolvedHandle;
            foreach (var candidate in NativeLibraryCandidates(dependencyDirectory, name))
                if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out var handle)) return handle;
            return IntPtr.Zero;
        }
        loadContext.Resolving += ResolveManaged;
        loadContext.ResolvingUnmanagedDll += ResolveNative;
        try
        {
            using var assemblyStream = new MemoryStream(assemblyBytes, writable: false);
            Assembly assembly;
            if (pdbBytes is not null)
            {
                using var pdbStream = new MemoryStream(pdbBytes, writable: false);
                assembly = loadContext.LoadFromStream(assemblyStream, pdbStream);
            }
            else assembly = loadContext.LoadFromStream(assemblyStream);
            ValidateCompiledRoutes(assembly, parsed.Routes);
            return new XpsCompiledWebUnit(loadContext, assembly, parsed.Routes, parsed.PrecompileTargets, webRootPath);
        }
        catch
        {
            loadContext.Resolving -= ResolveManaged;
            loadContext.ResolvingUnmanagedDll -= ResolveNative;
            loadContext.Unload();
            throw;
        }
    }

    private static string PersistentArtifactDirectory(string cacheRoot, string siteRoot, string sourcePath, string snapshotIdentity)
    {
        var sourceKey = HashText(Path.GetFullPath(siteRoot) + "\0" + Path.GetFullPath(sourcePath));
        var compilerIdentity = typeof(XpsWebCompiler).Assembly.ManifestModule.ModuleVersionId.ToString("N");
        var runtimeIdentity = typeof(XpsWebContext).Assembly.ManifestModule.ModuleVersionId.ToString("N");
        var identityKey = HashText(snapshotIdentity + "\0" + compilerIdentity + "\0" + runtimeIdentity);
        return Path.Combine(Path.GetFullPath(cacheRoot), sourceKey, identityKey);
    }

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static async Task<string> PersistArtifactAsync(
        string cacheRoot, string siteRoot, string sourcePath, string snapshotIdentity, byte[] assemblyBytes, byte[]? pdbBytes, string buildOutputDirectory, CancellationToken cancellationToken)
    {
        var artifactDirectory = PersistentArtifactDirectory(cacheRoot, siteRoot, sourcePath, snapshotIdentity);
        Directory.CreateDirectory(artifactDirectory);
        await WriteAtomicAsync(Path.Combine(artifactDirectory, "XPScript.WebUnit.dll"), assemblyBytes, cancellationToken).ConfigureAwait(false);
        if (pdbBytes is not null)
            await WriteAtomicAsync(Path.Combine(artifactDirectory, "XPScript.WebUnit.pdb"), pdbBytes, cancellationToken).ConfigureAwait(false);

        await CopyRuntimeDependenciesAsync(buildOutputDirectory, artifactDirectory, cancellationToken).ConfigureAwait(false);

        var sourceDirectory = Directory.GetParent(artifactDirectory)?.FullName;
        if (sourceDirectory is null) return artifactDirectory;
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            if (Path.GetFullPath(directory).Equals(Path.GetFullPath(artifactDirectory), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) continue;
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
        TouchPersistentArtifact(artifactDirectory);
        MaintainPersistentCache(cacheRoot, artifactDirectory, force: true);
        return artifactDirectory;
    }

    private static async Task CopyRuntimeDependenciesAsync(string sourceDirectory, string destinationDirectory, CancellationToken cancellationToken)
    {
        foreach (var source in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, source);
            if (relative.Equals("XPScript.WebUnit.dll", StringComparison.OrdinalIgnoreCase) ||
                relative.Equals("XPScript.WebUnit.pdb", StringComparison.OrdinalIgnoreCase)) continue;
            var destination = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input = File.OpenRead(source);
            await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }
    }

    private static IEnumerable<string> NativeLibraryCandidates(string root, string name)
    {
        var fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name };
        if (OperatingSystem.IsWindows())
        {
            fileNames.Add(name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? name : name + ".dll");
        }
        else if (OperatingSystem.IsMacOS())
        {
            fileNames.Add(name.StartsWith("lib", StringComparison.Ordinal) ? name : "lib" + name);
            fileNames.Add(name.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase) ? name : name + ".dylib");
            fileNames.Add((name.StartsWith("lib", StringComparison.Ordinal) ? name : "lib" + name) + ".dylib");
        }
        else
        {
            fileNames.Add(name.StartsWith("lib", StringComparison.Ordinal) ? name : "lib" + name);
            fileNames.Add(name.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ? name : name + ".so");
            fileNames.Add((name.StartsWith("lib", StringComparison.Ordinal) ? name : "lib" + name) + ".so");
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            if (fileNames.Contains(Path.GetFileName(file))) yield return file;
    }

    private static void MaintainPersistentCache(string cacheRoot, string? preserveDirectory, bool force = false)
    {
        string root;
        try { root = Path.GetFullPath(cacheRoot); }
        catch { return; }
        if (!Directory.Exists(root)) return;

        var now = DateTimeOffset.UtcNow;
        var nowTicks = DateTime.UtcNow.Ticks;
        if (!force && PersistentCacheSweeps.TryGetValue(root, out var lastTicks) && nowTicks - lastTicks < PersistentCacheSweepInterval.Ticks)
            return;
        if (PersistentCacheSweeps.Count > 256) PersistentCacheSweeps.Clear();
        PersistentCacheSweeps[root] = nowTicks;

        var maxBytes = PersistentCacheMaxBytes();
        var cutoff = now - PersistentCacheMaxAge();
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string? preserve = null;
        try { if (!string.IsNullOrWhiteSpace(preserveDirectory)) preserve = Path.GetFullPath(preserveDirectory); } catch { }
        var artifacts = new List<(string Path, long Bytes, DateTimeOffset LastWrite)>();

        try
        {
            foreach (var sourceDirectory in Directory.EnumerateDirectories(root))
            {
                foreach (var artifactDirectory in Directory.EnumerateDirectories(sourceDirectory))
                {
                    string full;
                    try { full = Path.GetFullPath(artifactDirectory); } catch { continue; }
                    DateTimeOffset lastWrite;
                    try { lastWrite = Directory.GetLastWriteTimeUtc(full); } catch { continue; }

                    if (preserve is not null && full.Equals(preserve, comparison))
                    {
                        artifacts.Add((full, DirectoryBytes(full), lastWrite));
                        continue;
                    }
                    if (lastWrite < cutoff)
                    {
                        TryDeleteCacheDirectory(full);
                        continue;
                    }
                    artifacts.Add((full, DirectoryBytes(full), lastWrite));
                }
            }

            var total = artifacts.Sum(item => item.Bytes);
            if (total > maxBytes)
            {
                foreach (var item in artifacts.OrderBy(item => item.LastWrite))
                {
                    if (total <= maxBytes) break;
                    if (preserve is not null && item.Path.Equals(preserve, comparison)) continue;
                    if (TryDeleteCacheDirectory(item.Path)) total = Math.Max(0, total - item.Bytes);
                }
            }

            foreach (var sourceDirectory in Directory.EnumerateDirectories(root))
            {
                try { if (!Directory.EnumerateFileSystemEntries(sourceDirectory).Any()) Directory.Delete(sourceDirectory); } catch { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static long PersistentCacheMaxBytes()
    {
        var raw = Environment.GetEnvironmentVariable("XPSCRIPT_WEB_CACHE_MAX_BYTES");
        return long.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && parsed >= 16L * 1024 * 1024 && parsed <= 16L * 1024 * 1024 * 1024
            ? parsed
            : DefaultPersistentCacheMaxBytes;
    }

    private static TimeSpan PersistentCacheMaxAge()
    {
        var raw = Environment.GetEnvironmentVariable("XPSCRIPT_WEB_CACHE_MAX_AGE_HOURS");
        return int.TryParse(raw, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var hours)
            && hours >= 1 && hours <= 8760
            ? TimeSpan.FromHours(hours)
            : DefaultPersistentCacheMaxAge;
    }

    private static long DirectoryBytes(string directory)
    {
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                try { total = checked(total + new FileInfo(file).Length); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OverflowException) { }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return total;
    }

    private static bool TryDeleteCacheDirectory(string directory)
    {
        try { Directory.Delete(directory, recursive: true); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    private static void TouchPersistentArtifact(string artifactDirectory)
    {
        try { Directory.SetLastWriteTimeUtc(artifactDirectory, DateTime.UtcNow); } catch { }
    }

    private static async Task WriteAtomicAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
            if (File.Exists(path))
                File.Replace(temp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            else
            {
                try
                {
                    File.Move(temp, path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive: true);
                        File.Move(temp, path);
                    }
                    else if (File.Exists(path))
                        File.Replace(temp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                    else
                        throw;
                }
            }
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static async Task<XpsCompiledWebUnit> CompileBrowserWasmAsync(string sourcePath, string webRoot, XpsWebRouteParseResult parsed, CancellationToken cancellationToken)
    {
        var bundle = await XpsBrowserWasmServerBridgeCompiler.GetOrBuildAsync(sourcePath, webRoot, parsed, cancellationToken).ConfigureAwait(false);
        var methods = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD" };
        var policy = new XpsRoutePolicy(true, methods, [], []);
        var routes = new Dictionary<string, XpsWebRouteDescriptor>(StringComparer.OrdinalIgnoreCase)
        {
            ["Index"] = new("Index", policy),
            [XpsWebPathResolver.BrowserWasmAssetRoute] = new(XpsWebPathResolver.BrowserWasmAssetRoute, policy)
        };

        return new XpsCompiledWebUnit(routes, parsed.PrecompileTargets, async (route, context) =>
        {
            var relativeAsset = "index.html";
            if (route.Equals(XpsWebPathResolver.BrowserWasmAssetRoute, StringComparison.OrdinalIgnoreCase))
            {
                var path = context.Request.Path.Replace('\\', '/');
                var marker = path.IndexOf(".xps/", StringComparison.OrdinalIgnoreCase);
                if (marker < 0) throw new XpsWebRouteException("Invalid browser-wasm asset route.");
                relativeAsset = path[(marker + 5)..];
            }

            if (await bundle.TryHandleBridgeAsync(relativeAsset, context).ConfigureAwait(false)) return;

            string assetPath;
            try { assetPath = bundle.ResolveAsset(relativeAsset); }
            catch (XpsWebCompilationException) { WriteBrowserNotFound(context); return; }
            if (!File.Exists(assetPath)) { WriteBrowserNotFound(context); return; }

            var bytes = await File.ReadAllBytesAsync(assetPath, context.Request.CancellationToken).ConfigureAwait(false);
            context.Response.Clear();
            context.Response.StatusCode = 200;
            context.Response.ContentType = BrowserContentType(assetPath);
            context.Response.SetHeader("Cache-Control", relativeAsset.Equals("index.html", StringComparison.OrdinalIgnoreCase) ? "no-cache" : "public, max-age=31536000, immutable");
            if (relativeAsset.Equals("index.html", StringComparison.OrdinalIgnoreCase))
                XpsWebSecurity.ApplyBrowserWasmResponseSecurityHeaders(context.Response);
            if (!context.Request.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase)) context.Response.WriteBinary(bytes);
            context.Response.Complete();
        });
    }

    private static void WriteBrowserNotFound(XpsWebContext context)
    {
        context.Response.Clear();
        context.Response.StatusCode = 404;
        context.Response.ContentType = "text/plain; charset=utf-8";
        if (!context.Request.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase)) context.Response.Write("Not Found");
        context.Response.Complete();
    }

    private static string BrowserContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" or ".mjs" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".wasm" => "application/wasm",
        ".json" => "application/json; charset=utf-8",
        ".dll" => "application/octet-stream",
        ".pdb" => "application/octet-stream",
        _ => "application/octet-stream"
    };

    private static Assembly? ResolveSharedAssembly(AssemblyLoadContext context, AssemblyName name)
    {
        var runtimeAssembly = typeof(XpsWebContext).Assembly;
        if (string.Equals(name.Name, runtimeAssembly.GetName().Name, StringComparison.OrdinalIgnoreCase)) return runtimeAssembly;
        if (name.Name is null ||
            (!name.Name.Equals("Microsoft.Data.Sqlite", StringComparison.OrdinalIgnoreCase) &&
             !name.Name.StartsWith("SQLitePCLRaw.", StringComparison.OrdinalIgnoreCase) &&
             !name.Name.StartsWith("Microsoft.Data.SqlClient", StringComparison.OrdinalIgnoreCase)))
            return null;

        try
        {
            return AssemblyLoadContext.Default.LoadFromAssemblyName(name);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static string InjectWebObjects(string generated)
    {
        const string members = """
internal static class Script
{
    private static XPScript.Web.Runtime.XpsWebRequest Request => XPScript.Web.Runtime.XpsWebRuntimeObjects.Request;
    private static XPScript.Web.Runtime.XpsWebResponse Response => XPScript.Web.Runtime.XpsWebRuntimeObjects.Response;
    private static XPScript.Web.Runtime.XpsRequestBody Body => XPScript.Web.Runtime.XpsWebRuntimeObjects.Body;
    private static XPScript.Web.Runtime.XpsWebServer Server => XPScript.Web.Runtime.XpsWebRuntimeObjects.Server;
    private static XPScript.Web.Runtime.IXpsRequestState RequestScope => XPScript.Web.Runtime.XpsWebRuntimeObjects.RequestScope;
    private static XPScript.Web.Runtime.IXpsSession Session => XPScript.Web.Runtime.XpsWebRuntimeObjects.Session;
    private static XPScript.Web.Runtime.IXpsApplicationState Application => XPScript.Web.Runtime.XpsWebRuntimeObjects.Application;
""";
        var match = ScriptClassMarker.Match(generated);
        if (!match.Success) throw new XpsWebCompilationException("Generated web assembly does not contain the expected Script class marker.");
        return generated[..match.Index] + members + generated[(match.Index + match.Length)..];
    }

    private static async Task<XpsCompiledWebUnit> CompileAstWebAsync(
        string sourcePath,
        string sourceRoot,
        string compilerSource,
        XpsWebRouteParseResult parsed,
        string? snapshotIdentity,
        string? persistentCacheDirectory,
        CancellationToken cancellationToken)
    {
        var configuredTempRoot = Environment.GetEnvironmentVariable("XPSCRIPT_TEMP_ROOT");
        var root = string.IsNullOrWhiteSpace(configuredTempRoot)
            ? Path.Combine(Path.GetTempPath(), "XPScript")
            : Path.Combine(Path.GetFullPath(configuredTempRoot), "XPScript");
        var workspace = Path.Combine(root, "web-ast", Guid.NewGuid().ToString("N"));
        var output = Path.Combine(workspace, "publish");
        Directory.CreateDirectory(workspace);
        CompilerPathSecurity.HardenTemporaryDirectory(workspace);
        Directory.CreateDirectory(output);
        CompilerPathSecurity.HardenTemporaryDirectory(output);
        var relativeSourcePath = Path.GetRelativePath(sourceRoot, sourcePath);
        if (relativeSourcePath == ".." || relativeSourcePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new XpsWebCompilationException("Web source must be inside the configured web root.");
        var stagedSourcePath = Path.Combine(workspace, relativeSourcePath);
        StageWebSourceTree(sourceRoot, workspace, sourcePath, stagedSourcePath);
        var runtimeAssembly = typeof(XpsWebContext).Assembly.Location;
        File.Copy(runtimeAssembly, Path.Combine(workspace, "XPScript.Web.Runtime.dll"), overwrite: true);
        File.Copy(runtimeAssembly, Path.Combine(Path.GetDirectoryName(stagedSourcePath)!, "XPScript.Web.Runtime.dll"), overwrite: true);
        await File.WriteAllTextAsync(stagedSourcePath, compilerSource, cancellationToken).ConfigureAwait(false);
        try
        {
            var assemblyPath = await AstExperimentalCompiler.CompileWithGeneratedSourceTransformAsync(
                stagedSourcePath,
                output,
                CompilerDriver.CurrentRuntimeIdentifier(),
                generated => InjectWebObjects(TransformAstWebGeneratedSource(generated)),
                cancellationToken).ConfigureAwait(false);
            var webAssemblyPath = Path.Combine(Path.GetDirectoryName(assemblyPath)!, "XPScript.WebUnit.dll");
            File.Copy(assemblyPath, webAssemblyPath, overwrite: true);
            var generatedPdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            if (File.Exists(generatedPdbPath))
                File.Copy(generatedPdbPath, Path.ChangeExtension(webAssemblyPath, ".pdb"), overwrite: true);
            var assemblyBytes = await File.ReadAllBytesAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
            var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            var pdbBytes = File.Exists(pdbPath) ? await File.ReadAllBytesAsync(pdbPath, cancellationToken).ConfigureAwait(false) : null;
            var dependencyDirectory = Path.GetDirectoryName(assemblyPath)!;
            if (!string.IsNullOrWhiteSpace(snapshotIdentity) && !string.IsNullOrWhiteSpace(persistentCacheDirectory))
            {
                dependencyDirectory = await PersistArtifactAsync(
                    persistentCacheDirectory,
                    sourceRoot,
                    sourcePath,
                    snapshotIdentity,
                    assemblyBytes,
                    pdbBytes,
                    dependencyDirectory,
                    cancellationToken).ConfigureAwait(false);
            }
            return LoadCompiledUnit(assemblyBytes, pdbBytes, parsed, dependencyDirectory, sourceRoot);
        }
        catch (Exception ex) when (ex is CompilerException or XpsWebCompilationException or IOException or UnauthorizedAccessException)
        {
            throw new XpsWebCompilationException("AST web compilation failed: " + ex.Message, ex);
        }
    }

    private static void StageWebSourceTree(string sourceRoot, string workspace, string sourcePath, string stagedSourcePath)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.PlatformDefault
        };
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.xps", options))
        {
            var fullFile = Path.GetFullPath(file);
            var relative = Path.GetRelativePath(sourceRoot, fullFile);
            if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new XpsWebCompilationException("Web source tree contains a file outside the configured web root.");
            var destination = Path.Combine(workspace, relative);
            if (Path.GetFullPath(destination).Equals(Path.GetFullPath(stagedSourcePath), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(fullFile, destination, overwrite: true);
        }
    }

    private static string TransformAstWebGeneratedSource(string generated)
    {
        var transformed = Regex.Replace(generated, @"\bProgram\b", "Script", RegexOptions.CultureInvariant);
        transformed = "using XPScript.Web.Runtime;" + Environment.NewLine + transformed;
        transformed = Regex.Replace(
            transformed,
            @"\(\(dynamic\)\((Request|Response|Body|Server|RequestScope|Session|Application)\)\)\.(?=[A-Za-z_]\w*\s*\()",
            "$1.",
            RegexOptions.CultureInvariant);
        transformed = Regex.Replace(
            transformed,
            @"\(\(dynamic\)\(Response\)\)\.ContentType\s*=\s*\(object\)\(",
            "Response.ContentType = (string)(",
            RegexOptions.CultureInvariant);
        foreach (var method in new[] { "Json", "OK", "Ok", "Created", "NoContent", "BadRequest", "NotFound", "Unauthorized", "Forbidden", "Conflict", "Problem", "problem" })
        {
            var receiver = method.Equals("NoContent", StringComparison.OrdinalIgnoreCase) ? "Response" : "Response, ";
            transformed = transformed.Replace($"Response.{method}(", $"XpsWebResponseRestExtensions.{method}({receiver}", StringComparison.Ordinal);
            transformed = transformed.Replace($"((dynamic)(Response)).{method}(", $"XpsWebResponseRestExtensions.{method}({receiver}", StringComparison.Ordinal);
        }
        transformed = Regex.Replace(transformed, @"XpsWebResponseRestExtensions\.(?:Problem|problem)\(Response, (-?\d+)L,", "XpsWebResponseRestExtensions.Problem(Response, $1,", RegexOptions.CultureInvariant);
        return Regex.Replace(transformed, @"(?im)^\s*public static dynamic RequestScope = new ExpandoObject\(\);\s*\r?\n", string.Empty, RegexOptions.CultureInvariant);
    }

    private static string NormalizeVariantSetAssignments(string source)
    {
        var variantNames = VariantDeclaration.Matches(source).Select(match => match.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (variantNames.Length == 0) return source;
        var normalized = source;
        foreach (var name in variantNames)
            normalized = Regex.Replace(normalized, $@"(?im)^(\s*)Set\s+{Regex.Escape(name)}\s*=\s*(.+)$", $"$1{name} = $2", RegexOptions.CultureInvariant);
        return normalized;
    }

    private static string EnsureCompilerEntryPoint(string source) => MainOrInitialize.IsMatch(source) ? source : source + Environment.NewLine + Environment.NewLine + "Public Sub Main()" + Environment.NewLine + "End Sub" + Environment.NewLine;

    private static string ForceRouteParametersByVal(string source, IEnumerable<XpsWebRouteDescriptor> routes)
    {
        foreach (var route in routes)
        {
            foreach (var binding in route.ParameterBindings ?? [])
            {
                var pattern = $@"(?is)(\b(?:Sub|Function)\s+{Regex.Escape(route.ProcedureName)}\s*\([^)]*?)(?<![A-Za-z0-9_]){Regex.Escape(binding.ParameterName)}\s+(As\b)";
                source = Regex.Replace(source, pattern, $"$1ByVal {binding.ParameterName} $2", RegexOptions.CultureInvariant);
            }
        }
        return source;
    }

    private static void ValidateRoutesExist(string generated, IReadOnlyDictionary<string, XpsWebRouteDescriptor> routes)
    {
        foreach (var route in routes.Keys)
            if (!Regex.IsMatch(generated, $@"\b{Regex.Escape(route)}\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) throw new XpsWebCompilationException($"Exported route '{route}' was not emitted by the XPScript compiler.");
    }

    private static void ValidateCompiledRoutes(Assembly assembly, IReadOnlyDictionary<string, XpsWebRouteDescriptor> routes)
    {
        var script = assembly.GetType("Script", throwOnError: false, ignoreCase: false) ?? throw new XpsWebCompilationException("Generated web assembly does not contain Script.");
        foreach (var route in routes.Keys)
        {
            var method = script.GetMethod(route, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (method is null) throw new XpsWebCompilationException($"Exported route '{route}' is missing from the compiled web assembly.");
        }
    }

    private static string BuildProject(string webRuntimeAssemblyPath, bool usesSqlite, bool usesMsSql, bool usesImage)
    {
        var escapedPath = SecurityElement.Escape(webRuntimeAssemblyPath) ?? throw new XpsWebCompilationException("Unable to encode the web runtime assembly path.");
        var sqlitePackage = usesSqlite
            ? $"""
    <PackageReference Include="Microsoft.Data.Sqlite" Version="{MicrosoftDataSqliteVersion}" />
"""
            : string.Empty;
        var msSqlPackage = usesMsSql
            ? $"""
    <PackageReference Include="Microsoft.Data.SqlClient" Version="{MicrosoftDataSqlClientVersion}" />
"""
            : string.Empty;
        var imagePackage = usesImage
            ? "    <PackageReference Include=\"Magick.NET-Q16-AnyCPU\" Version=\"" + ApplicationDependencyCatalog.MagickNetVersion + "\" />" + Environment.NewLine
            : string.Empty;
        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <AssemblyName>XPScript.WebUnit</AssemblyName>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="XPScript.Web.Runtime"><HintPath>{escapedPath}</HintPath><Private>false</Private></Reference>
{sqlitePackage}{msSqlPackage}{imagePackage}  </ItemGroup>
</Project>
""";
    }

    private static void DebugStep(string step, string sourcePath)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_WEB_CONSOLE_ERRORS"), "1", StringComparison.Ordinal)) return;
        Console.Error.WriteLine($"[XPScript.Web.Compiler {DateTimeOffset.UtcNow:O}] {step}: {Path.GetFileName(sourcePath)}");
    }

    private static async Task RunDotNetAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        await RunProcessAsync(psi, cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunProcessAsync(ProcessStartInfo psi, CancellationToken cancellationToken)
    {
        using var process = Process.Start(psi) ?? throw new XpsWebCompilationException("Unable to start dotnet for web compilation.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKillProcessTree(process);
            try { await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
            throw;
        }
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0) throw new XpsWebCompilationException("Generated web assembly failed to compile." + Environment.NewLine + RedactBuildOutput(stdout + Environment.NewLine + stderr, psi.WorkingDirectory));
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
        }
    }

    private static string RedactBuildOutput(string value, string workspace)
    {
        var redacted = value.Replace(workspace, "<web-build>", StringComparison.OrdinalIgnoreCase);
        var lines = redacted.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var errors = lines.Where(line => line.Contains(": error ", StringComparison.OrdinalIgnoreCase)).Distinct().ToArray();
        var prioritized = errors.Length == 0 ? redacted : string.Join(Environment.NewLine, errors) + Environment.NewLine + Environment.NewLine + redacted;
        return prioritized.Length <= 16_384 ? prioritized : prioritized[..16_384] + Environment.NewLine + "<diagnostics truncated>";
    }
}

public sealed class XpsWebCompilationException : Exception
{
    public XpsWebCompilationException(string message) : this(message, null, null, null, null) { }
    public XpsWebCompilationException(string message, Exception innerException) : this(message, null, null, null, innerException) { }

    public XpsWebCompilationException(
        string message,
        string? diagnosticCode,
        string? category,
        IReadOnlyDictionary<string, string>? properties,
        Exception? innerException = null) : base(message, innerException)
    {
        DiagnosticCode = diagnosticCode ?? "";
        Category = category ?? "";
        Properties = properties ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public string DiagnosticCode { get; }
    public string Category { get; }
    public IReadOnlyDictionary<string, string> Properties { get; }
}
