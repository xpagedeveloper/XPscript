using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using XPScript.Compiler;
using XPScript.UI.Browser;
using XPScript.Web.Runtime;

namespace XPScript.Web.Compiler;

public sealed class XpsBrowserWasmCompiler
{
    private const string Platform = "browser-wasm";
    private static readonly Regex VariantDeclaration = new(@"(?im)^\s*Dim\s+([A-Za-z_]\w*)\s+As\s+Variant\s*$", RegexOptions.CultureInvariant);
    private readonly string _webRoot;
    private readonly string _cacheRoot;
    private readonly SemaphoreSlim _buildGate = new(1, 1);

    public XpsBrowserWasmCompiler(string webRoot)
    {
        _webRoot = Path.GetFullPath(webRoot);
        _cacheRoot = Path.Combine(_webRoot, ".xpscript-cache", "wasm");
    }

    public static async Task<bool> IsBrowserWasmAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var source = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        return string.Equals(new XpsWebRouteMetadataParser().Parse(source).Platform, Platform, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<XpsBrowserWasmBundle> GetOrBuildAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        EnsureInsideWebRoot(sourcePath);
        var source = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var parsed = new XpsWebRouteMetadataParser().Parse(source);
        if (!string.Equals(parsed.Platform, Platform, StringComparison.OrdinalIgnoreCase))
            throw new XpsWebCompilationException("Source is not marked [Platform:browser-wasm].");

        var compilerIdentity = typeof(XpsBrowserWasmCompiler).Assembly.ManifestModule.ModuleVersionId.ToString("N");
        var assetFingerprint = UIFormAppAssets.ComputeFingerprint(sourcePath);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source + "\0" + compilerIdentity + "\0" + assetFingerprint + "\0" + SelectedTargetFramework())));
        var sourceKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetRelativePath(_webRoot, sourcePath).Replace('\\', '/').ToLowerInvariant())))[..24];
        var bundleRoot = Path.Combine(_cacheRoot, sourceKey, hash);
        var appRoot = Path.Combine(bundleRoot, "app");
        var marker = Path.Combine(bundleRoot, "source.sha256");
        if (File.Exists(marker) && IsValidAppRoot(appRoot)) return new XpsBrowserWasmBundle(sourcePath, hash, appRoot);

        await _buildGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? workspace = null;
        try
        {
            if (File.Exists(marker) && IsValidAppRoot(appRoot)) return new XpsBrowserWasmBundle(sourcePath, hash, appRoot);
            workspace = CreateBuildWorkspace();
            var publishRoot = Path.Combine(workspace, "pub");
            TryDelete(appRoot);
            Directory.CreateDirectory(publishRoot);
            var browserSource = EnsureBrowserEntryPoint(NormalizeVariantSetAssignments(parsed.Source), parsed.Routes);
            var generated = await AstCompiler.GenerateRestrictedSourceAsync(browserSource, sourcePath,
                Path.Combine(workspace, "ast-emission"), Platform, [_webRoot], cancellationToken).ConfigureAwait(false);
            generated = generated.Replace("XPScript.UI.Desktop.DesktopFormHost, XPScript.UI.Desktop", "XPScript.UI.Browser.BrowserFormHost, XPScript.UI.Browser", StringComparison.Ordinal);
            generated = AstCompiler.EnsureUiComponentHierarchy(generated);
            // The restricted browser transpiler can emit attachment API calls
            // without the companion API class when the V3 collection runtime is
            // already present. Ensure the generated browser module is complete.
            generated = AstCompiler.EnsureAttachmentRuntime(generated, includeNativeDatabaseProviders: false);
            await File.WriteAllTextAsync(Path.Combine(workspace, "Generated.cs"), generated, cancellationToken).ConfigureAwait(false);
            // Generated #line directives retain the original XPScript filename.
            // Stage that source beside the generated file so Roslyn can resolve
            // diagnostics during the isolated browser build.
            File.Copy(sourcePath, Path.Combine(workspace, Path.GetFileName(sourcePath)), overwrite: true);
            var usesExtendedArchive = generated.Contains("XPScriptExtendedArchive", StringComparison.Ordinal);
            await File.WriteAllTextAsync(Path.Combine(workspace, "BrowserApp.csproj"), BuildProjectForWebAssemblySdk(typeof(BrowserFormHost).Assembly.Location, usesExtendedArchive), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workspace, "main.js"), MainJs, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workspace, "xpscript-browser.js"), BrowserModuleSource(), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workspace, "index.html"), BuildIndexHtml(sourcePath), cancellationToken).ConfigureAwait(false);
            var targetFramework = SelectedTargetFramework();
            await RunDotNetAsync(workspace, ["restore", "BrowserApp.csproj", "--nologo", $"-p:TargetFramework={targetFramework}", "-p:UseMonoRuntime=true", "-p:GenerateRuntimeConfigurationFiles=true", "-p:WasmGenerateAppBundle=true", "-p:NuGetAudit=false", "-p:RestoreIgnoreFailedSources=true"], cancellationToken).ConfigureAwait(false);
            await RunDotNetAsync(workspace, ["publish", "BrowserApp.csproj", "-c", "Release", "--no-restore", "--nologo", $"-p:TargetFramework={targetFramework}", "-p:UseMonoRuntime=true", "-p:GenerateRuntimeConfigurationFiles=true", "-p:WasmGenerateAppBundle=true", "-p:NuGetAudit=false", "-o", publishRoot], cancellationToken).ConfigureAwait(false);
            NormalizePublishedLayout(publishRoot);
            var builtAppRoot = ResolveBuiltAppRoot(publishRoot, workspace);
            EnsureBrowserRuntimeBundle(builtAppRoot, targetFramework);
            CopyDirectory(builtAppRoot, appRoot);
            // Some SDK versions emit the complete AppBundle beside a partial
            // publish layout. Copy the framework directory explicitly from
            // the resolved complete root so the browser never receives a
            // mixed bundle (for example dotnet.js from pub and only one wasm).
            var frameworkSource = Path.Combine(builtAppRoot, "_framework");
            var frameworkTarget = Path.Combine(appRoot, "_framework");
            if (!Directory.Exists(frameworkSource))
                throw new XpsWebCompilationException("browser-wasm AppBundle is missing its _framework directory.");
            CopyDirectory(frameworkSource, frameworkTarget);
            await File.WriteAllTextAsync(Path.Combine(appRoot, "index.html"), BuildIndexHtml(sourcePath), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(appRoot, "main.js"), MainJs, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(appRoot, "xpscript-browser.js"), BrowserModuleSource(), cancellationToken).ConfigureAwait(false);
            if (UIFormAppAssets.UsesUIForm(sourcePath)) UIFormAppAssets.CopyAssetsToDirectory(sourcePath, appRoot);
            if (!IsValidAppRoot(appRoot)) throw new XpsWebCompilationException("browser-wasm persisted app bundle is incomplete.");
            await File.WriteAllTextAsync(marker, hash, cancellationToken).ConfigureAwait(false);
            return new XpsBrowserWasmBundle(sourcePath, hash, appRoot);
        }
        finally { if (workspace is not null) TryDelete(workspace); _buildGate.Release(); }
    }

    private static string CreateBuildWorkspace()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("XPSCRIPT_TEMP_ROOT");
        var baseRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XPScript")
            : Path.GetFullPath(configuredRoot);
        var path = Path.Combine(baseRoot, "wasm-build", "browser", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    private static bool IsValidAppRoot(string appRoot) =>
        Directory.Exists(appRoot) &&
        File.Exists(Path.Combine(appRoot, "main.js")) &&
        HasBrowserFramework(Path.Combine(appRoot, "_framework"));
    private static void NormalizePublishedLayout(string root)
    {
        var framework = Path.Combine(root, "_framework");
        if (File.Exists(Path.Combine(root, "dotnet.js")) && !Directory.Exists(framework))
        {
            Directory.CreateDirectory(framework);
            foreach (var file in Directory.EnumerateFiles(root))
            {
                var name = Path.GetFileName(file);
                if (name is "index.html" or "main.js" or "xpscript-browser.js") continue;
                File.Move(file, Path.Combine(framework, name));
            }
        }
    }
    private static string ResolveBuiltAppRoot(params string[] searchRoots)
    {
        foreach (var searchRoot in searchRoots)
        {
            if (!Directory.Exists(searchRoot)) continue;
            // The SDK can emit a partial publish directory beside its runnable AppBundle.
            var framework = Directory.EnumerateDirectories(Path.GetFullPath(searchRoot), "_framework", SearchOption.AllDirectories)
                .Where(HasBrowserFramework)
                .OrderByDescending(path => File.Exists(Path.Combine(path, "dotnet.boot.js")) ||
                    File.ReadAllText(Path.Combine(path, "dotnet.js")).Contains("/*json-start*/", StringComparison.Ordinal))
                .FirstOrDefault();
            if (framework is not null)
                return Path.GetDirectoryName(framework)!;
        }
        throw new XpsWebCompilationException("browser-wasm build output did not contain a framework bundle with System.Private.CoreLib.");
    }
    private static bool HasBrowserFramework(string frameworkDirectory) =>
        Directory.Exists(frameworkDirectory) &&
        File.Exists(Path.Combine(frameworkDirectory, "dotnet.js")) &&
        (File.Exists(Path.Combine(frameworkDirectory, "dotnet.native.wasm")) ||
         Directory.EnumerateFiles(frameworkDirectory, "dotnet.native.*.wasm", SearchOption.TopDirectoryOnly).Any()) &&
        Directory.EnumerateFiles(frameworkDirectory, "System.Private.CoreLib.*", SearchOption.TopDirectoryOnly)
            .Any(path => Path.GetExtension(path) is ".dll" or ".wasm");

    internal static void EnsureBrowserRuntimeBundle(string appRoot, string targetFramework)
    {
        var framework = Path.Combine(appRoot, "_framework");
        if (!Directory.Exists(framework) || File.Exists(Path.Combine(framework, "dotnet.boot.js"))) return;
        var dotnetModule = Path.Combine(framework, "dotnet.js");
        if (File.Exists(dotnetModule) && File.ReadAllText(dotnetModule).Contains("/*json-start*/", StringComparison.Ordinal)) return;

        var major = Regex.Match(targetFramework, @"^net(?<major>\d+)", RegexOptions.CultureInvariant).Groups["major"].Value;
        var dotnetRoots = new[]
        {
            Environment.GetEnvironmentVariable("DOTNET_ROOT"),
            Environment.GetEnvironmentVariable("DOTNET_ROOT(x86)"),
            Path.GetDirectoryName(Environment.ProcessPath),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet")
        }.Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => Path.GetFullPath(path!)).Distinct(StringComparer.OrdinalIgnoreCase);
        var runtimePack = dotnetRoots
            .Select(root => Path.Combine(root, "packs", "Microsoft.NETCore.App.Runtime.Mono.browser-wasm"))
            .FirstOrDefault(Directory.Exists);
        var runtimeRoot = Directory.Exists(runtimePack)
            ? Directory.EnumerateDirectories(runtimePack).Where(path => Path.GetFileName(path).StartsWith(major + ".", StringComparison.Ordinal)).OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
            : null;
        var native = runtimeRoot is null ? null : Path.Combine(runtimeRoot, "runtimes", "browser-wasm", "native");
        if (native is null || !File.Exists(Path.Combine(native, "dotnet.native.js")) || !File.Exists(Path.Combine(native, "dotnet.runtime.js")))
            throw new XpsWebCompilationException($"The .NET {major} browser runtime is installed, but its WASM runtime modules could not be located.");

        foreach (var name in new[] { "dotnet.js", "dotnet.native.js", "dotnet.runtime.js", "dotnet.diagnostics.js", "dotnet.native.wasm" })
        {
            var source = Path.Combine(native, name);
            if (File.Exists(source)) File.Copy(source, Path.Combine(framework, name), true);
        }

        var assemblyFiles = Directory.EnumerateFiles(framework, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(path => new Dictionary<string, object?> { ["name"] = Path.GetFileName(path) })
            .ToArray();
        var applicationAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "XPScript.BrowserApp.dll",
            "XPScript.UI.Browser.dll"
        };
        var coreAssemblies = assemblyFiles.Where(asset => !applicationAssemblyNames.Contains((string)asset["name"]!)).ToArray();
        var managedAssemblies = assemblyFiles.Where(asset => applicationAssemblyNames.Contains((string)asset["name"]!)).ToArray();
        var isModernBoot = int.TryParse(major, out var majorVersion) && majorVersion >= 11;
        var assets = new List<Dictionary<string, object?>>
        {
            new() { ["name"] = "dotnet.native.wasm", ["behavior"] = "dotnetwasm", ["isCore"] = true },
            new() { ["name"] = "dotnet.native.js", ["behavior"] = "js-module-native", ["isCore"] = true },
            new() { ["name"] = "dotnet.runtime.js", ["behavior"] = "js-module-runtime", ["isCore"] = true },
            new() { ["name"] = "dotnet.diagnostics.js", ["behavior"] = "js-module-diagnostics", ["isCore"] = true }
        };
        assets.AddRange(coreAssemblies.Select(asset => new Dictionary<string, object?>
        {
            ["name"] = asset["name"], ["behavior"] = "assembly", ["isCore"] = true
        }));
        assets.AddRange(managedAssemblies.Select(asset => new Dictionary<string, object?>
        {
            ["name"] = asset["name"], ["behavior"] = "assembly", ["isCore"] = false
        }));
        var resources = new Dictionary<string, object?>
        {
            ["jsModuleNative"] = new[] { new Dictionary<string, object?> { ["name"] = "dotnet.native.js" } },
            ["jsModuleRuntime"] = new[] { new Dictionary<string, object?> { ["name"] = "dotnet.runtime.js" } },
            ["jsModuleDiagnostics"] = new[] { new Dictionary<string, object?> { ["name"] = "dotnet.diagnostics.js" } },
            ["wasmNative"] = new[] { new Dictionary<string, object?> { ["name"] = "dotnet.native.wasm" } },
            ["coreAssembly"] = coreAssemblies,
            ["assembly"] = managedAssemblies
        };
        var config = new Dictionary<string, object?>
        {
            ["mainAssemblyName"] = "XPScript.BrowserApp.dll",
            ["assets"] = isModernBoot ? assets : null,
            ["resources"] = resources,
            ["debugLevel"] = 0,
            ["globalizationMode"] = "invariant"
        };
        var boot = "export const config = " + JsonSerializer.Serialize(config) + ";" + Environment.NewLine;
        File.WriteAllText(Path.Combine(framework, "dotnet.boot.js"), boot);
    }
    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        var sourceRoot = Path.GetFullPath(sourceDirectory); var destinationRoot = Path.GetFullPath(destinationDirectory);
        if (Directory.Exists(destinationRoot)) Directory.Delete(destinationRoot, true); Directory.CreateDirectory(destinationRoot);
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)) { var destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file)); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(file, destination, true); }
    }
    private string BuildIndexHtml(string sourcePath) => IndexHtml
        .Replace("__XPSCRIPT_BASE_HREF__", "/" + Uri.EscapeDataString(Path.GetFileName(sourcePath)) + "/", StringComparison.Ordinal)
        .Replace("__XPSCRIPT_BOOTSTRAP__", UIFormAppAssets.UsesUIForm(sourcePath) ? "<link href=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css\" rel=\"stylesheet\">" : string.Empty, StringComparison.Ordinal);
    private static string NormalizeVariantSetAssignments(string source)
    {
        var variantNames = VariantDeclaration.Matches(source).Select(match => match.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var normalized = source;
        foreach (var name in variantNames) normalized = Regex.Replace(normalized, $@"(?im)^(\s*)Set\s+{Regex.Escape(name)}\s*=\s*(.+)$", $"$1{name} = $2", RegexOptions.CultureInvariant);
        return normalized;
    }
    private void EnsureInsideWebRoot(string path) { var relative = Path.GetRelativePath(_webRoot, path); if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new XpsWebCompilationException("browser-wasm source resolves outside the web root."); }
    private static string EnsureBrowserEntryPoint(string source, IReadOnlyDictionary<string, XpsWebRouteDescriptor> routes)
    {
        if (Regex.IsMatch(source, @"(?im)^\s*(?:Public\s+|Private\s+)?Sub\s+Main\b")) return source;
        var entry = routes.ContainsKey("Index") ? "Index" : routes.Count == 1 ? routes.Keys.Single() : null;
        if (entry is null) throw new XpsWebCompilationException("browser-wasm source must define Main, Index, or exactly one exported route.");
        return source + Environment.NewLine + Environment.NewLine + "Public Sub Main()" + Environment.NewLine + "    Call " + entry + "()" + Environment.NewLine + "End Sub" + Environment.NewLine;
    }
    private static string BuildProject(string browserRuntimeAssemblyPath, bool usesExtendedArchive)
    {
        var escaped = SecurityElement.Escape(browserRuntimeAssemblyPath) ?? throw new XpsWebCompilationException("Unable to encode browser runtime path.");
        var archiveItems = usesExtendedArchive
            ? "<PackageReference Include=\"SharpCompress\" Version=\"" + ApplicationDependencyCatalog.ResolveVersion("SharpCompress", ApplicationDependencyCatalog.SharpCompressVersion) + "\" /><TrimmerRootAssembly Include=\"SharpCompress\" />"
            : string.Empty;
        return "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><RuntimeIdentifier>browser-wasm</RuntimeIdentifier><SelfContained>true</SelfContained><PublishTrimmed>false</PublishTrimmed><GenerateRuntimeConfigurationFiles>false</GenerateRuntimeConfigurationFiles><OutputType>Exe</OutputType><StartupObject>Program</StartupObject><AllowUnsafeBlocks>true</AllowUnsafeBlocks><WasmMainJSPath>main.js</WasmMainJSPath><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><NoWarn>CA1416;CS8600;CS8601;CS8603;CS8618;$(NoWarn)</NoWarn><AssemblyName>XPScript.BrowserApp</AssemblyName></PropertyGroup><ItemGroup><Reference Include=\"XPScript.UI.Browser\"><HintPath>" + escaped + "</HintPath><Private>true</Private></Reference><TrimmerRootAssembly Include=\"XPScript.UI.Browser\" />" + archiveItems + "<Content Include=\"index.html\" CopyToOutputDirectory=\"PreserveNewest\" CopyToPublishDirectory=\"PreserveNewest\" /><Content Include=\"main.js\" CopyToOutputDirectory=\"PreserveNewest\" CopyToPublishDirectory=\"PreserveNewest\" /><Content Include=\"xpscript-browser.js\" CopyToOutputDirectory=\"PreserveNewest\" CopyToPublishDirectory=\"PreserveNewest\" /></ItemGroup></Project>";
    }
    private static string BuildProjectForWebAssemblySdk(string browserRuntimeAssemblyPath, bool usesExtendedArchive) =>
        BuildProject(browserRuntimeAssemblyPath, usesExtendedArchive)
            .Replace("Microsoft.NET.Sdk\"", "Microsoft.NET.Sdk.WebAssembly\"", StringComparison.Ordinal)
            .Replace("<WasmMainJSPath>main.js</WasmMainJSPath>", "<WasmMainJSPath>main.js</WasmMainJSPath><WasmBuildNative>false</WasmBuildNative>", StringComparison.Ordinal)
            .Replace("<TargetFramework>net10.0</TargetFramework>", "<TargetFramework>" + SelectedTargetFramework() + "</TargetFramework>", StringComparison.Ordinal);

    private static string SelectedTargetFramework() =>
        Environment.GetEnvironmentVariable("XPSCRIPT_TARGET_FRAMEWORK") is { Length: > 0 } value ? value : "net10.0";

    private static async Task RunDotNetAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        CopySdkSelectionFile(workingDirectory);
        var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }; foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        // Keep generated SDK invocations isolated from the host profile. In
        // restricted Windows environments the default CLI telemetry lookup
        // touches HKCU and aborts before the WASM build even starts.
        var cliHome = Path.Combine(workingDirectory, "dotnet-home");
        Directory.CreateDirectory(cliHome);
        psi.Environment["DOTNET_CLI_HOME"] = cliHome;
        psi.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        psi.Environment["DOTNET_NOLOGO"] = "1";
        using var process = Process.Start(psi) ?? throw new XpsWebCompilationException("Unable to start dotnet for browser-wasm compilation."); var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken); var stderr = process.StandardError.ReadToEndAsync(cancellationToken); await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false); var output = await stdout.ConfigureAwait(false) + Environment.NewLine + await stderr.ConfigureAwait(false); if (process.ExitCode != 0) { var details = Redact(output, workingDirectory); if (details.Contains("browser-wasm", StringComparison.OrdinalIgnoreCase) || details.Contains("wasm-tools", StringComparison.OrdinalIgnoreCase) || details.Contains("Microsoft.NETCore.App.Runtime.Mono.browser-wasm", StringComparison.OrdinalIgnoreCase)) throw new XpsWebCompilationException("Browser WASM requires the matching .NET WASM workload, but it is not available. XPScript does not install workloads; install it separately and retry." + Environment.NewLine + details); throw new XpsWebCompilationException("browser-wasm build failed." + Environment.NewLine + details); }
    }
    private static void CopySdkSelectionFile(string workingDirectory)
    {
        var source = Path.Combine(Directory.GetCurrentDirectory(), "global.json");
        if (File.Exists(source)) File.Copy(source, Path.Combine(workingDirectory, "global.json"), overwrite: true);
    }
    private static string Redact(string value, string workspace) { var result = value.Replace(workspace, "<wasm-build>", StringComparison.OrdinalIgnoreCase); return result.Length <= 16_384 ? result : result[..16_384] + Environment.NewLine + "<diagnostics truncated>"; }
    private static void TryDelete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }
    private static string BrowserModuleSource()
    {
        var source = BrowserModuleJs.Replace(
            "editor.addEventListener('change',async()=>{",
            "editor.addEventListener((type==='lookupfield'||type==='autocompletefield')?'input':'change',async()=>{",
            StringComparison.Ordinal);
        source = source.Replace(
            "export function renderForm(requestJson){",
            "function applyLookupOptions(request){for(const f of request.fields||[]){const t=fieldType(f);if(t!=='lookupfield'&&t!=='autocompletefield')continue;const e=document.querySelector('[name=\\\"'+CSS.escape(String(f.name||''))+'\\\"]');if(!e)continue;const id='xps_'+String(f.name||'')+'_options';let d=document.getElementById(id);if(!d){d=document.createElement('datalist');d.id=id;document.body.appendChild(d);}e.setAttribute('list',id);d.replaceChildren();for(const v of f.options||[]){const o=document.createElement('option');o.value=String(v);d.appendChild(o);}}}\nexport function renderForm(requestJson){",
            StringComparison.Ordinal);
        source = source.Replace(
            "if(type==='spacer'){const e=document.createElement('div');e.className='xpscript-uiform-spacer';e.style.height='1rem';return e;}",
            "if(type==='spacer'){const e=document.createElement('div');e.className='xpscript-uiform-spacer';e.style.height='1rem';return e;}if(type==='card'||type==='panel'||type==='scrollview'){const e=document.createElement('div');e.className='xpscript-uiform-container xpscript-uiform-'+type;e.style.padding='0.75rem';e.style.border='1px solid currentColor';e.style.borderRadius='0.5rem';if(type==='scrollview'){e.style.maxHeight='15rem';e.style.overflow='auto';}if(field.label){const h=document.createElement('div');h.textContent=String(field.label);h.style.fontWeight='600';e.appendChild(h);}return e;}",
            StringComparison.Ordinal);
        source = source.Replace(
            "const editors=new Map();",
            "const editors=new Map();const componentContainers=new Map();",
            StringComparison.Ordinal);
        source = source.Replace(
            "if(type!=='radiogroup'&&type!=='separator'&&type!=='spacer')editor.name=field.name||'';",
            "if(field.id&&(type==='card'||type==='panel'||type==='scrollview'))componentContainers.set(String(field.id),editor);if(type!=='radiogroup'&&type!=='separator'&&type!=='spacer')editor.name=field.name||'';",
            StringComparison.Ordinal);
        source = source.Replace(
            "form.appendChild(wrap);if(field.schemaValidationError)",
            "(field.parentId&&componentContainers.get(String(field.parentId))||form).appendChild(wrap);if(field.schemaValidationError)",
            StringComparison.Ordinal);
        source = source.Replace(
            "const wrap=document.createElement('div');if(field.columnSpan)",
            "const wrap=document.createElement('div');if(field.tabName){wrap.setAttribute('data-xps-tab-panel',String(field.tabName));const activeTab=String(request.activeTab||request.tabs?.[0]?.name||'');if(String(field.tabName)!==activeTab)wrap.style.display='none';}if(field.columnSpan)",
            StringComparison.Ordinal);
        source = source.Replace(
            "root.appendChild(actions);}return JSON.stringify({result:'Pending',values:{}});}",
            "root.appendChild(actions);}applyLookupOptions(request);return JSON.stringify({result:'Pending',values:{}});}",
            StringComparison.Ordinal);
        source = source.Replace("renderForm(requestJson){", "renderForm(requestJson, targetRoot = null){", StringComparison.Ordinal)
            .Replace("const root=document.getElementById('xpscript-app');", "const root=targetRoot||document.getElementById('xpscript-app');", StringComparison.Ordinal)
            .Replace("renderForm(JSON.stringify(request));", "renderForm(JSON.stringify(request),root);", StringComparison.Ordinal);
        source = source.Replace("root.appendChild(form);const collectValues", "renderTabs(request,form);root.appendChild(form);const collectValues", StringComparison.Ordinal)
            .Replace("request.fields=mergeByName(request.fields||[],state.fields||[]);request.buttons=mergeByName(request.buttons||[],state.buttons||[]);", "request.fields=mergeByName(request.fields||[],state.fields||[]);request.buttons=mergeByName(request.buttons||[],state.buttons||[]);if(Array.isArray(state.tabs))request.tabs=state.tabs;if(state.tabPlacement)request.tabPlacement=state.tabPlacement;if(state.activeTab)request.activeTab=state.activeTab;", StringComparison.Ordinal);
        return source;
    }

    private const string IndexHtml = """
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><base href="__XPSCRIPT_BASE_HREF__"><title>XPScript</title>__XPSCRIPT_BOOTSTRAP__</head><body><main id="xpscript-app"></main><script type="module" src="./main.js"></script></body></html>
""";
    private const string MainJs = """
import { dotnet } from './_framework/dotnet.js';
import { applyApplicationMetadata, consumeRequestState, navigate, renderForm, setEventDispatcher, stageRequestState } from './xpscript-browser.js';
const dotnetModuleText = await fetch('./_framework/dotnet.js').then(response => response.text());
if (!dotnetModuleText.includes('/*json-start*/')) {
    const bootConfig = (await import('./_framework/dotnet.boot.js')).config;
    if (typeof dotnet.withConfig === 'function') dotnet.withConfig(bootConfig); else dotnet.withModuleConfig({ config: bootConfig });
}
const { setModuleImports, getAssemblyExports, runMain } = await dotnet.create();
setModuleImports('xpscript-browser', { applyApplicationMetadata, consumeRequestState, navigate, renderForm, stageRequestState });
const browserExports = await getAssemblyExports('XPScript.UI.Browser.dll');
setEventDispatcher((eventToken, submittedValue) => browserExports.XPScript.UI.Browser.BrowserFormHost.DispatchEventAsync(eventToken, submittedValue));
await runMain('XPScript.BrowserApp');
""";
    private const string BrowserModuleJs = """
let eventDispatcher = null;
export function setEventDispatcher(callback) { if (typeof callback !== 'function') throw new Error('XPScript browser event dispatcher must be a function.'); eventDispatcher = callback; }
function clampInteger(value, minimum, maximum, fallback) { const parsed = Number.parseInt(value, 10); if (!Number.isFinite(parsed)) return fallback; return Math.max(minimum, Math.min(maximum, parsed)); }
function clearRequestState(key) { sessionStorage.removeItem(key); sessionStorage.removeItem(key + '.target'); sessionStorage.removeItem(key + '.created'); }
export function stageRequestState(key, stateJson) { if (stateJson === '{}') { clearRequestState(key); return; } sessionStorage.setItem(key, stateJson); }
export function consumeRequestState(key, lifetimeMilliseconds) { const value=sessionStorage.getItem(key)||''; const target=sessionStorage.getItem(key+'.target')||''; const created=Number(sessionStorage.getItem(key+'.created')||'0'); if(!value||!target||!created)return ''; const lifetime=Number(lifetimeMilliseconds); if(!Number.isFinite(lifetime)||lifetime<0||(Date.now()-created)>lifetime){clearRequestState(key);return '';} if(window.location.pathname!==target)return ''; clearRequestState(key);return value; }
export function navigate(target,key){const current=window.location.pathname;const slash=current.lastIndexOf('/');const basePath=slash>=0?current.substring(0,slash+1):'/';const next=basePath+target;if(sessionStorage.getItem(key)){sessionStorage.setItem(key+'.target',next);sessionStorage.setItem(key+'.created',String(Date.now()));}window.location.href=next;}
export function applyApplicationMetadata(title,icon){if(title)document.title=title;if(!icon)return;let link=document.querySelector('link[rel~="icon"]');if(!link){link=document.createElement('link');link.rel='icon';document.head.appendChild(link);}link.href=icon;}
function fieldType(field){return String(field.type||'TextField').toLowerCase();}
function renderTabs(request,form){if(!Array.isArray(request.tabs)||request.tabs.length===0)return;const active=String(request.activeTab||request.tabs[0].name||'');const strip=document.createElement('div');strip.className='xpscript-uiform-tabs nav nav-tabs';strip.setAttribute('role','tablist');for(const tab of request.tabs){const button=document.createElement('button');button.type='button';button.className='nav-link'+(String(tab.name)===active?' active':'');button.textContent=tab.label||tab.name;button.setAttribute('data-xps-tab',String(tab.name));button.addEventListener('click',()=>{for(const item of strip.querySelectorAll('[data-xps-tab]'))item.classList.toggle('active',item===button);for(const panel of form.querySelectorAll('[data-xps-tab-panel]'))panel.style.display=panel.getAttribute('data-xps-tab-panel')===String(tab.name)?'':'none';});strip.appendChild(button);}if(String(request.tabPlacement||'Top').toLowerCase()==='bottom')form.appendChild(strip);else form.insertBefore(strip,form.firstChild);}
function applyFieldState(field,editor){if(field.regexPattern&&'pattern'in editor)editor.pattern=String(field.regexPattern);if(field.dateMinimum&&fieldType(field)==='datefield')editor.min=String(field.dateMinimum);if(field.dateMaximum&&fieldType(field)==='datefield')editor.max=String(field.dateMaximum);if(field.timeMinimum&&fieldType(field)==='timefield')editor.min=String(field.timeMinimum);if(field.timeMaximum&&fieldType(field)==='timefield')editor.max=String(field.timeMaximum);if(field.dateTimeMinimum&&fieldType(field)==='datetimefield')editor.min=String(field.dateTimeMinimum);if(field.dateTimeMaximum&&fieldType(field)==='datetimefield')editor.max=String(field.dateTimeMaximum);if(field.monthMinimum&&fieldType(field)==='monthfield')editor.min=String(field.monthMinimum);if(field.monthMaximum&&fieldType(field)==='monthfield')editor.max=String(field.monthMaximum);if(field.enabled===false)editor.disabled=true;if(field.readOnly===true){if('readOnly'in editor)editor.readOnly=true;else editor.disabled=true;}if(field.required===true)editor.required=true;if(field.minLength!=null&&'minLength'in editor)editor.minLength=Number(field.minLength);if(field.maxLength!=null&&'maxLength'in editor)editor.maxLength=Number(field.maxLength);if(field.minimum!=null&&'min'in editor)editor.min=String(field.minimum);if(field.maximum!=null&&'max'in editor)editor.max=String(field.maximum);if(field.placeholder&&'placeholder'in editor)editor.placeholder=String(field.placeholder);if(field.tooltip)editor.title=String(field.tooltip);if(field.schemaValidationError){editor.setAttribute('aria-invalid','true');editor.setAttribute('aria-describedby',`xps_${field.name||''}_schema_error`);}}
function createEditor(field){const type=fieldType(field);if(type==='separator'){const e=document.createElement('hr');e.className='xpscript-uiform-separator my-2';return e;}if(type==='spacer'){const e=document.createElement('div');e.className='xpscript-uiform-spacer';e.style.height='1rem';return e;}if(type==='select'||type==='listbox'||type==='multilistbox'||type==='multiselect'||type==='checkboxgroup'){const e=document.createElement('select');e.className='form-select';if(type!=='select')e.size=6;if(type==='multilistbox'||type==='multiselect'||type==='checkboxgroup')e.multiple=true;if(!e.multiple&&type!=='select')throw new Error('select.multiple could not be enabled');const selected=new Set((field.values||[]).map(String));for(const v of field.options||[]){const o=document.createElement('option');o.value=String(v);o.textContent=String(v);o.selected=e.multiple?selected.has(o.value):field.value!=null&&String(field.value)===o.value;e.appendChild(o);}applyFieldState(field,e);return e;}if(type==='radiogroup'){const g=document.createElement('div');for(const v of field.options||[]){const i=document.createElement('input');i.type='radio';i.name=field.name;i.value=String(v);i.checked=field.value!=null&&String(field.value)===i.value;g.appendChild(i);}return g;}if(type==='filefield'){const e=document.createElement('input');e.type='file';e.className='form-control';applyFieldState(field,e);return e;}const e=document.createElement(type==='textarea'?'textarea':'input');e.className=type==='checkbox'?'form-check-input':'form-control';if(type==='checkbox')e.type='checkbox';else if(type==='passwordfield')e.type='password';else if(type==='numberfield'||type==='rangefield')e.type=type==='rangefield'?'range':'number';else if(type==='datefield')e.type='date';else if(type==='timefield')e.type='time';else if(type==='datetimefield')e.type='datetime-local';else if(type==='monthfield')e.type='month';else if(type==='emailfield')e.type='email';else if(type==='urlfield')e.type='url';else e.type='text';if(type==='checkbox'){const v=String(field.value||'').toLowerCase();e.checked=v==='true'||v==='1';}else if(type!=='passwordfield'&&field.value!=null)e.value=String(field.value);applyFieldState(field,e);return e;}
function readFieldValue(field,editor){const type=fieldType(field);if(type==='checkbox')return Boolean(editor.checked);if(type==='filefield')return editor.files&&editor.files.length>0?editor.files[0].name:'';if(type==='radiogroup'){const s=editor.querySelector('input[type="radio"]:checked');return s?s.value:'';}if(type==='multilistbox'||type==='multiselect'||type==='checkboxgroup')return Array.from(editor.selectedOptions).map(o=>o.value);if(type==='numberfield'||type==='rangefield'){if(editor.value==='')return '';const n=Number(editor.value);return Number.isFinite(n)?n:editor.value;}return editor.value??'';}
function toSubmittedValue(field,editor){const value=readFieldValue(field,editor);if(Array.isArray(value))return value.map(String).join('\u001f');if(typeof value==='boolean')return value?'true':'false';return value==null?'':String(value);}
function mergeByName(existing,updates){if(!Array.isArray(existing)||!Array.isArray(updates))return existing||[];const byName=new Map(updates.map(item=>[String(item?.name||'').toLowerCase(),item]));return existing.map(item=>{const update=byName.get(String(item?.name||'').toLowerCase());return update?{...item,...update}:item;});}
function buttonClass(style){switch(String(style||'').toLowerCase()){case'primary':return'btn btn-primary';case'success':return'btn btn-success';case'danger':return'btn btn-danger';default:return'btn btn-secondary';}}
export function renderForm(requestJson){const request=JSON.parse(requestJson);const root=document.getElementById('xpscript-app');if(!root)throw new Error('XPScript browser root element was not found.');root.replaceChildren();if(request.title){const h=document.createElement('h1');h.textContent=request.title;root.appendChild(h);}const form=document.createElement('form');form.className='xpscript-uiform';form.style.display='grid';form.style.gridTemplateColumns=`repeat(${Math.max(1,Number(request.gridColumns)||1)},minmax(0,1fr))`;form.style.gap='0.5rem';const editors=new Map();const dispatchUiEvent=async(eventToken,submittedValue)=>{if(typeof eventDispatcher!=='function')return false;try{const resultJson=await eventDispatcher(eventToken,submittedValue);if(!resultJson)return true;const state=JSON.parse(resultJson);request.fields=mergeByName(request.fields||[],state.fields||[]);request.buttons=mergeByName(request.buttons||[],state.buttons||[]);if(state.navigation?.target){navigate(String(state.navigation.target),'');return true;}renderForm(JSON.stringify(request));return true;}catch{root.dataset.xpscriptError='UI event callback failed';root.dispatchEvent(new CustomEvent('xpscript:form-error'));return true;}};for(const field of request.fields||[]){const type=fieldType(field);if(type==='hiddenfield')continue;const wrap=document.createElement('div');if(field.columnSpan)wrap.style.gridColumn=`span ${Math.max(1,Number(field.columnSpan)||1)}`;if(field.rowSpan)wrap.style.gridRow=`span ${Math.max(1,Number(field.rowSpan)||1)}`;const editor=createEditor(field);if(type!=='radiogroup'&&type!=='separator'&&type!=='spacer')editor.name=field.name||'';if(type!=='separator'&&type!=='spacer')editors.set(field.name||'',editor);if(field.onChangeHandler&&type!=='separator'&&type!=='spacer')editor.addEventListener('change',async()=>{await dispatchUiEvent(`change:${field.name||''}`,toSubmittedValue(field,editor));});wrap.appendChild(editor);if(field.schemaValidationError){const error=document.createElement('div');error.className='xpscript-uiform-error';error.id=`xps_${field.name||''}_schema_error`;error.setAttribute('role','alert');error.textContent=String(field.schemaValidationError);wrap.appendChild(error);}form.appendChild(wrap);}root.appendChild(form);const collectValues=()=>{const values={};for(const field of request.fields||[]){const type=fieldType(field);if(type === 'separator'||type === 'spacer')continue;if(type==='hiddenfield'){if(field.value!=null)values[field.name]=field.value;continue;}const editor=editors.get(field.name||'');if(editor)values[field.name]=readFieldValue(field,editor);}return values;};const publishResult=(result,actionName='')=>{const payload={result,values:collectValues()};if(actionName)payload.action=actionName;root.dataset.xpscriptResult=JSON.stringify(payload);root.dispatchEvent(new CustomEvent('xpscript:form-result',{detail:payload}));};if((request.buttons||[]).length>0){const actions=document.createElement('div');for(const definition of request.buttons){if(definition.visible===false)continue;const button=document.createElement('button');button.type='button';button.className=buttonClass(definition.style);button.textContent=definition.label||definition.name||'Action';button.disabled=definition.enabled===false;button.addEventListener('click',async()=>{const handled=await dispatchUiEvent(`button:${definition.name||''}`,JSON.stringify(collectValues()));if(!handled)publishResult('Action',definition.name||'');});actions.appendChild(button);}root.appendChild(actions);}return JSON.stringify({result:'Pending',values:{}});}
""";
}

public sealed record XpsBrowserWasmBundle(string SourcePath, string SourceHash, string PublishRoot)
{
    public string ResolveAsset(string relativePath)
    {
        var root = Path.GetFullPath(PublishRoot); var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar); var candidate = Path.GetFullPath(normalized, root); var relative = Path.GetRelativePath(root, candidate);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new XpsWebCompilationException("browser-wasm asset path resolves outside the bundle.");
        if (File.Exists(candidate)) return candidate;
        var webRelative = relativePath.Replace('\\', '/'); if (!webRelative.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) return candidate;
        var sourceDirectory = Path.GetFullPath(Path.GetDirectoryName(SourcePath) ?? Environment.CurrentDirectory); var assetRoot = Path.Combine(sourceDirectory, UIFormAppAssets.DirectoryName); var sourceCandidate = Path.GetFullPath(Path.Combine(sourceDirectory, normalized)); var assetPrefix = Path.GetFullPath(assetRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!sourceCandidate.StartsWith(assetPrefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) throw new XpsWebCompilationException("browser-wasm application asset path escapes the assets directory."); return sourceCandidate;
    }
}
