using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using XPScript.Compiler;
using XPScript.Web.Runtime;

namespace XPScript.Web.Compiler;

public sealed class XpsWebCompiler
{
    private static readonly Regex MainOrInitialize = new(@"(?im)^\s*(?:Public\s+|Private\s+)?Sub\s+(?:Main|Initialize)\b", RegexOptions.CultureInvariant);
    private static readonly Regex ScriptClassMarker = new(@"internal\s+static\s+class\s+Script\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex VariantDeclaration = new(@"(?im)^\s*Dim\s+([A-Za-z_]\w*)\s+As\s+Variant\s*$", RegexOptions.CultureInvariant);

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
        var parsed = new XpsWebRouteMetadataParser().Parse(source);
        if (string.Equals(parsed.Platform, "browser-wasm", StringComparison.OrdinalIgnoreCase) || parsed.Routes.Count == 0) return null;

        var artifactDirectory = PersistentArtifactDirectory(persistentCacheDirectory, fullSourceRoot, fullSourcePath, snapshotIdentity);
        var assemblyPath = Path.Combine(artifactDirectory, "XPScript.WebUnit.dll");
        if (!File.Exists(assemblyPath)) return null;

        try
        {
            var assemblyBytes = await File.ReadAllBytesAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
            var pdbPath = Path.Combine(artifactDirectory, "XPScript.WebUnit.pdb");
            var pdbBytes = File.Exists(pdbPath) ? await File.ReadAllBytesAsync(pdbPath, cancellationToken).ConfigureAwait(false) : null;
            return LoadCompiledUnit(assemblyBytes, pdbBytes, parsed);
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

        var source = await File.ReadAllTextAsync(fullSourcePath, cancellationToken).ConfigureAwait(false);
        var parsed = new XpsWebRouteMetadataParser().Parse(source);
        if (string.Equals(parsed.Platform, "browser-wasm", StringComparison.OrdinalIgnoreCase))
            return await CompileBrowserWasmAsync(fullSourcePath, fullSourceRoot, parsed, cancellationToken).ConfigureAwait(false);

        if (parsed.Routes.Count == 0) throw new XpsWebCompilationException("Web source must export at least one route using web route attributes.");

        var compilerSource = EnsureCompilerEntryPoint(NormalizeVariantSetAssignments(parsed.Source));
        string generated;
        try
        {
            generated = new XPScriptTranspiler().TranspileRestricted(compilerSource, fullSourcePath, CompilerDriver.CurrentRuntimeIdentifier(), [fullSourceRoot]);
            generated = InjectWebObjects(generated);
        }
        catch (CompilerException ex)
        {
            throw new XpsWebCompilationException("XPScript web compilation failed: " + ex.Message, ex);
        }

        ValidateRoutesExist(generated, parsed.Routes);
        var workspace = Path.Combine(Path.GetTempPath(), "XPScript", "web", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            var projectPath = Path.Combine(workspace, "WebUnit.csproj");
            var generatedPath = Path.Combine(workspace, "Generated.cs");
            await File.WriteAllTextAsync(projectPath, BuildProject(typeof(XpsWebContext).Assembly.Location), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(generatedPath, generated, cancellationToken).ConfigureAwait(false);

            var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = workspace, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            psi.ArgumentList.Add("build"); psi.ArgumentList.Add(projectPath); psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("Release"); psi.ArgumentList.Add("--nologo"); psi.ArgumentList.Add("--no-restore");
            await RunDotNetAsync(workspace, ["restore", projectPath, "--nologo"], cancellationToken).ConfigureAwait(false);
            await RunProcessAsync(psi, cancellationToken).ConfigureAwait(false);

            var assemblyPath = Path.Combine(workspace, "bin", "Release", "net10.0", "XPScript.WebUnit.dll");
            if (!File.Exists(assemblyPath)) throw new XpsWebCompilationException("Web compiler completed without producing a loadable assembly.");
            var assemblyBytes = await File.ReadAllBytesAsync(assemblyPath, cancellationToken).ConfigureAwait(false);
            var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            var pdbBytes = File.Exists(pdbPath) ? await File.ReadAllBytesAsync(pdbPath, cancellationToken).ConfigureAwait(false) : null;

            if (!string.IsNullOrWhiteSpace(snapshotIdentity) && !string.IsNullOrWhiteSpace(persistentCacheDirectory))
                await PersistArtifactAsync(persistentCacheDirectory, fullSourceRoot, fullSourcePath, snapshotIdentity, assemblyBytes, pdbBytes, cancellationToken).ConfigureAwait(false);

            return LoadCompiledUnit(assemblyBytes, pdbBytes, parsed);
        }
        finally { try { Directory.Delete(workspace, recursive: true); } catch { } }
    }

    private static XpsCompiledWebUnit LoadCompiledUnit(byte[] assemblyBytes, byte[]? pdbBytes, XpsWebRouteParseResult parsed)
    {
        var loadContext = new AssemblyLoadContext("XPScriptWeb-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        loadContext.Resolving += ResolveSharedAssembly;
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
            return new XpsCompiledWebUnit(loadContext, assembly, parsed.Routes, parsed.PrecompileTargets);
        }
        catch
        {
            loadContext.Resolving -= ResolveSharedAssembly;
            loadContext.Unload();
            throw;
        }
    }

    private static string PersistentArtifactDirectory(string cacheRoot, string siteRoot, string sourcePath, string snapshotIdentity)
    {
        var sourceKey = HashText(Path.GetFullPath(siteRoot) + "\0" + Path.GetFullPath(sourcePath));
        var compilerVersion = typeof(XpsWebCompiler).Assembly.GetName().Version?.ToString() ?? "unknown";
        var identityKey = HashText(snapshotIdentity + "\0" + compilerVersion);
        return Path.Combine(Path.GetFullPath(cacheRoot), sourceKey, identityKey);
    }

    private static string HashText(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static async Task PersistArtifactAsync(
        string cacheRoot, string siteRoot, string sourcePath, string snapshotIdentity, byte[] assemblyBytes, byte[]? pdbBytes, CancellationToken cancellationToken)
    {
        var artifactDirectory = PersistentArtifactDirectory(cacheRoot, siteRoot, sourcePath, snapshotIdentity);
        Directory.CreateDirectory(artifactDirectory);
        await WriteAtomicAsync(Path.Combine(artifactDirectory, "XPScript.WebUnit.dll"), assemblyBytes, cancellationToken).ConfigureAwait(false);
        if (pdbBytes is not null)
            await WriteAtomicAsync(Path.Combine(artifactDirectory, "XPScript.WebUnit.pdb"), pdbBytes, cancellationToken).ConfigureAwait(false);

        var sourceDirectory = Directory.GetParent(artifactDirectory)?.FullName;
        if (sourceDirectory is null) return;
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            if (Path.GetFullPath(directory).Equals(Path.GetFullPath(artifactDirectory), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) continue;
            try { Directory.Delete(directory, recursive: true); } catch { }
        }
    }

    private static async Task WriteAtomicAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static async Task<XpsCompiledWebUnit> CompileBrowserWasmAsync(string sourcePath, string webRoot, XpsWebRouteParseResult parsed, CancellationToken cancellationToken)
    {
        var bundle = await new XpsBrowserWasmCompiler(webRoot).GetOrBuildAsync(sourcePath, cancellationToken).ConfigureAwait(false);
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

            string assetPath;
            try { assetPath = bundle.ResolveAsset(relativeAsset); }
            catch (XpsWebCompilationException) { WriteBrowserNotFound(context); return; }
            if (!File.Exists(assetPath)) { WriteBrowserNotFound(context); return; }

            var bytes = await File.ReadAllBytesAsync(assetPath, context.Request.CancellationToken).ConfigureAwait(false);
            context.Response.Clear();
            context.Response.StatusCode = 200;
            context.Response.ContentType = BrowserContentType(assetPath);
            context.Response.SetHeader("Cache-Control", relativeAsset.Equals("index.html", StringComparison.OrdinalIgnoreCase) ? "no-cache" : "public, max-age=31536000, immutable");
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
        return string.Equals(name.Name, runtimeAssembly.GetName().Name, StringComparison.OrdinalIgnoreCase) ? runtimeAssembly : null;
    }

    private static string InjectWebObjects(string generated)
    {
        const string members = """
internal static class Script
{
    private static XPScript.Web.Runtime.XpsWebRequest Request => XPScript.Web.Runtime.XpsWebRuntimeObjects.Request;
    private static XPScript.Web.Runtime.XpsWebResponse Response => XPScript.Web.Runtime.XpsWebRuntimeObjects.Response;
    private static XPScript.Web.Runtime.XpsWebServer Server => XPScript.Web.Runtime.XpsWebRuntimeObjects.Server;
    private static XPScript.Web.Runtime.IXpsRequestState RequestScope => XPScript.Web.Runtime.XpsWebRuntimeObjects.RequestScope;
    private static XPScript.Web.Runtime.IXpsSession Session => XPScript.Web.Runtime.XpsWebRuntimeObjects.Session;
    private static XPScript.Web.Runtime.IXpsApplicationState Application => XPScript.Web.Runtime.XpsWebRuntimeObjects.Application;
""";
        var match = ScriptClassMarker.Match(generated);
        if (!match.Success) throw new XpsWebCompilationException("Generated web assembly does not contain the expected Script class marker.");
        return generated[..match.Index] + members + generated[(match.Index + match.Length)..];
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
            if (method.GetParameters().Length != 0) throw new XpsWebCompilationException($"Web route '{route}' must not declare parameters in the initial web runtime.");
        }
    }

    private static string BuildProject(string webRuntimeAssemblyPath)
    {
        var escapedPath = SecurityElement.Escape(webRuntimeAssemblyPath) ?? throw new XpsWebCompilationException("Unable to encode the web runtime assembly path.");
        return $$"""
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
    <Reference Include="XPScript.Web.Runtime"><HintPath>{{escapedPath}}</HintPath><Private>false</Private></Reference>
  </ItemGroup>
</Project>
""";
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
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0) throw new XpsWebCompilationException("Generated web assembly failed to compile." + Environment.NewLine + RedactBuildOutput(stdout + Environment.NewLine + stderr, psi.WorkingDirectory));
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
    public XpsWebCompilationException(string message) : base(message) { }
    public XpsWebCompilationException(string message, Exception innerException) : base(message, innerException) { }
}
