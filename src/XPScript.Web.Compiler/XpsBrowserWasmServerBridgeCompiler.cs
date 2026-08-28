using System.Diagnostics;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using XPScript.Compiler;
using XPScript.UI.Browser;

namespace XPScript.Web.Compiler;

internal sealed class XpsBrowserWasmServerBridgeBundle
{
    public XpsBrowserWasmServerBridgeBundle(
        XpsBrowserWasmBundle browserBundle,
        BrowserWasmServerBridgePlan bridgePlan,
        string? serverAssemblyPath)
    {
        BrowserBundle = browserBundle;
        BridgePlan = bridgePlan;
        ServerAssemblyPath = serverAssemblyPath;
    }

    public XpsBrowserWasmBundle BrowserBundle { get; }
    public BrowserWasmServerBridgePlan BridgePlan { get; }
    public string? ServerAssemblyPath { get; }
    public string SourcePath => BrowserBundle.SourcePath;
    public string SourceHash => BrowserBundle.SourceHash;
    public IReadOnlyDictionary<string, BrowserWasmServerBridgeProcedure> ServerBridgeProcedures => BridgePlan.Procedures;
    public string ResolveAsset(string relativePath) => BrowserBundle.ResolveAsset(relativePath);

    public Task<bool> TryHandleBridgeAsync(string relativePath, XPScript.Web.Runtime.XpsWebContext context) =>
        XpsBrowserWasmServerBridgeHost.TryHandleAsync(this, relativePath, context);
}

internal static class XpsBrowserWasmServerBridgeCompiler
{
    private const string Platform = "browser-wasm";
    private const string BridgeCompilerVersion = "3";
    private const string AvaloniaVersion = "12.0.3";
    private const string MicrosoftDataSqliteVersion = "10.0.11";
    private const string MicrosoftDataSqlClientVersion = "7.0.2";
    private static readonly Regex VariantDeclaration = new(@"(?im)^\s*Dim\s+([A-Za-z_]\w*)\s+As\s+Variant\s*$", RegexOptions.CultureInvariant);

    private sealed class BuildGate
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int Users { get; set; }
    }

    private static readonly object BuildGateSync = new();
    private static readonly Dictionary<string, BuildGate> BuildGates = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<XpsBrowserWasmServerBridgeBundle> GetOrBuildAsync(
        string sourcePath,
        string webRoot,
        XpsWebRouteParseResult parsed,
        CancellationToken cancellationToken)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        webRoot = Path.GetFullPath(webRoot);
        var source = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var compilerIdentity = typeof(XpsBrowserWasmServerBridgeCompiler).Assembly.ManifestModule.ModuleVersionId.ToString("N");
        var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source + "\0" + compilerIdentity + "\0" + BridgeCompilerVersion)));
        var annotatedProcedures = BrowserWasmServerSideMetadata.ReadAnnotatedProcedures(source);
        var normalizedSource = NormalizeVariantSetAssignments(parsed.Source);
        var planningSource = BrowserWasmServerSideMetadata.InjectPlanningMarkers(normalizedSource, annotatedProcedures);
        var plan = BrowserWasmServerBridgePlan.Create(planningSource, sourceHash);
        BrowserWasmServerSideMetadata.ValidateExplicitBoundary(plan, annotatedProcedures);

        if (plan.Procedures.Count == 0)
        {
            var plain = await new XpsBrowserWasmCompiler(webRoot).GetOrBuildAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            return new XpsBrowserWasmServerBridgeBundle(plain, plan, null);
        }

        var sourceKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetRelativePath(webRoot, sourcePath).Replace('\\', '/').ToLowerInvariant())))[..24];
        var cacheRoot = Path.Combine(webRoot, ".xpscript-cache", "wasm-bridge", sourceKey, sourceHash);
        var appRoot = Path.Combine(cacheRoot, "app");
        var serverRoot = Path.Combine(cacheRoot, "server");
        var serverAssembly = Path.Combine(serverRoot, "XPScript.BrowserServer.dll");
        var marker = Path.Combine(cacheRoot, "source.sha256");
        var bundle = new XpsBrowserWasmBundle(sourcePath, sourceHash, appRoot);
        if (File.Exists(marker) && IsValidAppRoot(appRoot) && File.Exists(serverAssembly))
            return new XpsBrowserWasmServerBridgeBundle(bundle, plan, serverAssembly);

        var gate = RentBuildGate(cacheRoot);
        try
        {
            await gate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ReturnBuildGate(cacheRoot, gate);
            throw;
        }

        string? workspace = null;
        try
        {
            if (File.Exists(marker) && IsValidAppRoot(appRoot) && File.Exists(serverAssembly))
                return new XpsBrowserWasmServerBridgeBundle(bundle, plan, serverAssembly);

            workspace = CreateBuildWorkspace();
            var browserPublishRoot = Path.Combine(workspace, "browser-pub");
            var serverOutputRoot = Path.Combine(workspace, "server-out");
            Directory.CreateDirectory(browserPublishRoot);
            Directory.CreateDirectory(serverOutputRoot);
            TryDelete(appRoot);
            TryDelete(serverRoot);

            var browserSource = EnsureEntryPoint(NormalizeVariantSetAssignments(plan.BrowserSource), parsed.Routes);
            var browserGenerated = new XPScriptTranspiler().TranspileRestricted(browserSource, sourcePath, Platform, [webRoot]);
            browserGenerated = browserGenerated.Replace(
                "XPScript.UI.Desktop.DesktopFormHost, XPScript.UI.Desktop",
                "XPScript.UI.Browser.BrowserFormHost, XPScript.UI.Browser",
                StringComparison.Ordinal);
            browserGenerated = BrowserWasmServerBridgeTransportInstaller.TransformGenerated(browserGenerated);
            var browserModule = BrowserWasmServerBridgeTransportInstaller.TransformBrowserModule(BrowserRuntimeConstant("BrowserModuleJs"));

            await File.WriteAllTextAsync(Path.Combine(workspace, "Generated.cs"), browserGenerated, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workspace, "BrowserApp.csproj"), BuildBrowserProject(typeof(BrowserFormHost).Assembly.Location), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workspace, "main.js"), BrowserRuntimeConstant("MainJs"), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workspace, "xpscript-browser.js"), browserModule, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workspace, "index.html"), BuildIndexHtml(sourcePath), cancellationToken).ConfigureAwait(false);

            await RunDotNetAsync(workspace, ["restore", "BrowserApp.csproj", "--nologo"], "browser-wasm restore", cancellationToken).ConfigureAwait(false);
            await RunDotNetAsync(workspace, ["publish", "BrowserApp.csproj", "-c", "Release", "--no-restore", "--nologo", "-o", browserPublishRoot], "browser-wasm publish", cancellationToken).ConfigureAwait(false);

            var builtAppRoot = ResolveBuiltAppRoot(browserPublishRoot, workspace);
            CopyDirectory(builtAppRoot, appRoot);
            await File.WriteAllTextAsync(Path.Combine(appRoot, "index.html"), BuildIndexHtml(sourcePath), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(appRoot, "main.js"), BrowserRuntimeConstant("MainJs"), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(appRoot, "xpscript-browser.js"), browserModule, cancellationToken).ConfigureAwait(false);

            var serverSource = EnsureEntryPoint(normalizedSource, parsed.Routes);
            var serverGenerated = new XPScriptTranspiler().TranspileRestricted(
                serverSource,
                sourcePath,
                CompilerDriver.CurrentRuntimeIdentifier(),
                [webRoot]);
            await File.WriteAllTextAsync(Path.Combine(workspace, "ServerGenerated.cs"), serverGenerated, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workspace, "ServerCompanion.csproj"), BuildServerProject(serverGenerated), cancellationToken).ConfigureAwait(false);
            await RunDotNetAsync(workspace, ["restore", "ServerCompanion.csproj", "--nologo"], "server companion restore", cancellationToken).ConfigureAwait(false);
            await RunDotNetAsync(workspace, ["build", "ServerCompanion.csproj", "-c", "Release", "--no-restore", "--nologo", "-o", serverOutputRoot], "server companion build", cancellationToken).ConfigureAwait(false);

            var builtServerAssembly = Path.Combine(serverOutputRoot, "XPScript.BrowserServer.dll");
            if (!File.Exists(builtServerAssembly))
                throw new XpsWebCompilationException("browser-wasm server companion build completed without producing an assembly.");
            Directory.CreateDirectory(serverRoot);
            File.Copy(builtServerAssembly, serverAssembly, true);
            var builtPdb = Path.Combine(serverOutputRoot, "XPScript.BrowserServer.pdb");
            if (File.Exists(builtPdb)) File.Copy(builtPdb, Path.Combine(serverRoot, "XPScript.BrowserServer.pdb"), true);

            if (!IsValidAppRoot(appRoot))
                throw new XpsWebCompilationException("browser-wasm bridge persisted app bundle is incomplete.");
            await File.WriteAllTextAsync(marker, sourceHash, cancellationToken).ConfigureAwait(false);
            return new XpsBrowserWasmServerBridgeBundle(bundle, plan, serverAssembly);
        }
        finally
        {
            if (workspace is not null) TryDelete(workspace);
            gate.Semaphore.Release();
            ReturnBuildGate(cacheRoot, gate);
        }
    }

    private static BuildGate RentBuildGate(string key)
    {
        lock (BuildGateSync)
        {
            if (!BuildGates.TryGetValue(key, out var gate))
            {
                gate = new BuildGate();
                BuildGates.Add(key, gate);
            }
            gate.Users++;
            return gate;
        }
    }

    private static void ReturnBuildGate(string key, BuildGate gate)
    {
        lock (BuildGateSync)
        {
            if (gate.Users <= 0) return;
            gate.Users--;
            if (gate.Users != 0) return;
            if (!BuildGates.TryGetValue(key, out var current) || !ReferenceEquals(current, gate)) return;
            BuildGates.Remove(key);
            gate.Semaphore.Dispose();
        }
    }

    private static string BuildBrowserProject(string browserRuntimeAssemblyPath)
    {
        var escaped = SecurityElement.Escape(browserRuntimeAssemblyPath) ?? throw new XpsWebCompilationException("Unable to encode browser runtime path.");
        return $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <StartupObject>Program</StartupObject>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <WasmMainJSPath>main.js</WasmMainJSPath>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AssemblyName>XPScript.BrowserApp</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="XPScript.UI.Browser">
      <HintPath>{{escaped}}</HintPath>
      <Private>true</Private>
    </Reference>
    <TrimmerRootAssembly Include="XPScript.UI.Browser" />
    <Content Include="index.html" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
    <Content Include="xpscript-browser.js" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
""";
    }

    private static string BuildServerProject(string generated)
    {
        var usesUi = generated.Contains("XPScriptUI.CreateForm(", StringComparison.Ordinal) ||
                     generated.Contains("XPScriptUIList.CreateListView(", StringComparison.Ordinal) ||
                     generated.Contains("XPScriptUIDialogRuntime.", StringComparison.Ordinal);
        var usesSqlite = generated.Contains("internal sealed class XPScriptDbSqlite", StringComparison.Ordinal);
        var usesMsSql = generated.Contains("internal sealed class XPScriptDbMsSql", StringComparison.Ordinal);
        var items = new StringBuilder();
        if (usesUi)
        {
            var desktopAssembly = typeof(XPScript.UI.Desktop.DesktopFormHost).Assembly.Location;
            var escaped = SecurityElement.Escape(desktopAssembly) ?? throw new XpsWebCompilationException("Unable to encode desktop runtime path.");
            items.AppendLine($"    <Reference Include=\"XPScript.UI.Desktop\"><HintPath>{escaped}</HintPath><Private>true</Private></Reference>");
            items.AppendLine($"    <PackageReference Include=\"Avalonia\" Version=\"{AvaloniaVersion}\" />");
            items.AppendLine($"    <PackageReference Include=\"Avalonia.Desktop\" Version=\"{AvaloniaVersion}\" />");
            items.AppendLine($"    <PackageReference Include=\"Avalonia.Themes.Fluent\" Version=\"{AvaloniaVersion}\" />");
        }
        if (usesSqlite) items.AppendLine($"    <PackageReference Include=\"Microsoft.Data.Sqlite\" Version=\"{MicrosoftDataSqliteVersion}\" />");
        if (usesMsSql) items.AppendLine($"    <PackageReference Include=\"Microsoft.Data.SqlClient\" Version=\"{MicrosoftDataSqlClientVersion}\" />");
        var itemGroup = items.Length == 0 ? string.Empty : $"  <ItemGroup>\n{items}  </ItemGroup>\n";
        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Library</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AssemblyName>XPScript.BrowserServer</AssemblyName>
  </PropertyGroup>
{itemGroup}  <ItemGroup>
    <Compile Include="ServerGenerated.cs" />
  </ItemGroup>
</Project>
""";
    }

    private static string EnsureEntryPoint(string source, IReadOnlyDictionary<string, XpsWebRouteDescriptor> routes)
    {
        if (Regex.IsMatch(source, @"(?im)^\s*(?:Public\s+|Private\s+)?Sub\s+Main\b")) return source;
        var entry = routes.ContainsKey("Index") ? "Index" : routes.Count == 1 ? routes.Keys.Single() : null;
        if (entry is null)
            throw new XpsWebCompilationException("browser-wasm source must define Main, Index, or exactly one exported route.");
        return source + Environment.NewLine + Environment.NewLine + "Public Sub Main()" + Environment.NewLine + "    Call " + entry + "()" + Environment.NewLine + "End Sub" + Environment.NewLine;
    }

    private static string NormalizeVariantSetAssignments(string source)
    {
        var variantNames = VariantDeclaration.Matches(source).Select(match => match.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var normalized = source;
        foreach (var name in variantNames)
            normalized = Regex.Replace(normalized, $@"(?im)^(\s*)Set\s+{Regex.Escape(name)}\s*=\s*(.+)$", $"$1{name} = $2", RegexOptions.CultureInvariant);
        return normalized;
    }

    private static string BrowserRuntimeConstant(string name)
    {
        var field = typeof(XpsBrowserWasmCompiler).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new XpsWebCompilationException("browser-wasm runtime template was not found: " + name);
        return field.GetRawConstantValue() as string
            ?? throw new XpsWebCompilationException("browser-wasm runtime template has an invalid value: " + name);
    }

    private static string BuildIndexHtml(string sourcePath)
    {
        var template = BrowserRuntimeConstant("IndexHtml");
        var scriptName = Uri.EscapeDataString(Path.GetFileName(sourcePath));
        return template.Replace("__XPSCRIPT_BASE_HREF__", scriptName + "/", StringComparison.Ordinal);
    }

    private static async Task RunDotNetAsync(string workingDirectory, IReadOnlyList<string> arguments, string operation, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        using var process = Process.Start(psi) ?? throw new XpsWebCompilationException("Unable to start dotnet for " + operation + ".");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
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
        var output = await stdout.ConfigureAwait(false) + Environment.NewLine + await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new XpsWebCompilationException(operation + " failed." + Environment.NewLine + Redact(output, workingDirectory));
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

    private static string CreateBuildWorkspace()
    {
        var path = Path.Combine(Path.GetTempPath(), "xwb" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static bool IsValidAppRoot(string appRoot) => Directory.Exists(appRoot) && File.Exists(Path.Combine(appRoot, "main.js")) && File.Exists(Path.Combine(appRoot, "_framework", "dotnet.js"));

    private static string ResolveBuiltAppRoot(params string[] searchRoots)
    {
        foreach (var searchRoot in searchRoots)
        {
            if (!Directory.Exists(searchRoot)) continue;
            var fullSearchRoot = Path.GetFullPath(searchRoot);
            if (File.Exists(Path.Combine(fullSearchRoot, "_framework", "dotnet.js"))) return fullSearchRoot;
            var frameworkEntry = Directory.EnumerateFiles(fullSearchRoot, "dotnet.js", SearchOption.AllDirectories)
                .FirstOrDefault(path => string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "_framework", StringComparison.OrdinalIgnoreCase));
            if (frameworkEntry is null) continue;
            var frameworkDirectory = Path.GetDirectoryName(frameworkEntry) ?? throw new XpsWebCompilationException("Unable to determine browser-wasm framework directory.");
            return Directory.GetParent(frameworkDirectory)?.FullName ?? throw new XpsWebCompilationException("Unable to determine browser-wasm application root.");
        }
        throw new XpsWebCompilationException("browser-wasm build output did not contain _framework/dotnet.js.");
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        var sourceRoot = Path.GetFullPath(sourceDirectory);
        var destinationRoot = Path.GetFullPath(destinationDirectory);
        if (Directory.Exists(destinationRoot)) Directory.Delete(destinationRoot, true);
        Directory.CreateDirectory(destinationRoot);
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }

    private static string Redact(string value, string workspace)
    {
        var result = value.Replace(workspace, "<wasm-bridge-build>", StringComparison.OrdinalIgnoreCase);
        return result.Length <= 16_384 ? result : result[..16_384] + Environment.NewLine + "<diagnostics truncated>";
    }

    private static void TryDelete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
}
