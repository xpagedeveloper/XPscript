using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text;
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

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        var sourceKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetRelativePath(_webRoot, sourcePath).Replace('\\', '/').ToLowerInvariant())))[..24];
        var bundleRoot = Path.Combine(_cacheRoot, sourceKey, hash);
        var publishRoot = Path.Combine(bundleRoot, "publish");
        var marker = Path.Combine(bundleRoot, "source.sha256");
        if (File.Exists(marker) && Directory.Exists(publishRoot))
            return new XpsBrowserWasmBundle(sourcePath, hash, publishRoot);

        await _buildGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(marker) && Directory.Exists(publishRoot))
                return new XpsBrowserWasmBundle(sourcePath, hash, publishRoot);

            var workspace = Path.Combine(bundleRoot, "build");
            Directory.CreateDirectory(workspace);
            Directory.CreateDirectory(publishRoot);

            var browserSource = EnsureBrowserEntryPoint(NormalizeVariantSetAssignments(parsed.Source), parsed.Routes);
            var generated = new XPScriptTranspiler().TranspileRestricted(browserSource, sourcePath, Platform, [_webRoot]);
            generated = generated.Replace(
                "XPScript.UI.Desktop.DesktopFormHost, XPScript.UI.Desktop",
                "XPScript.UI.Browser.BrowserFormHost, XPScript.UI.Browser",
                StringComparison.Ordinal);

            await File.WriteAllTextAsync(Path.Combine(workspace, "Generated.cs"), generated, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workspace, "BrowserApp.csproj"), BuildProject(typeof(BrowserFormHost).Assembly.Location), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workspace, "main.js"), MainJs, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workspace, "xpscript-browser.js"), BrowserModuleJs, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(workspace, "index.html"), IndexHtml, cancellationToken).ConfigureAwait(false);

            await RunDotNetAsync(workspace, ["restore", "BrowserApp.csproj", "--nologo"], cancellationToken).ConfigureAwait(false);
            await RunDotNetAsync(workspace, ["publish", "BrowserApp.csproj", "-c", "Release", "--no-restore", "--nologo", "-o", publishRoot], cancellationToken).ConfigureAwait(false);

            await File.WriteAllTextAsync(marker, hash, cancellationToken).ConfigureAwait(false);
            TryDelete(workspace);
            return new XpsBrowserWasmBundle(sourcePath, hash, publishRoot);
        }
        finally
        {
            _buildGate.Release();
        }
    }

    private static string NormalizeVariantSetAssignments(string source)
    {
        var variantNames = VariantDeclaration.Matches(source)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (variantNames.Length == 0) return source;

        var normalized = source;
        foreach (var name in variantNames)
        {
            normalized = Regex.Replace(
                normalized,
                $@"(?im)^(\s*)Set\s+{Regex.Escape(name)}\s*=\s*(.+)$",
                $"$1{name} = $2",
                RegexOptions.CultureInvariant);
        }
        return normalized;
    }

    private void EnsureInsideWebRoot(string path)
    {
        var relative = Path.GetRelativePath(_webRoot, path);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new XpsWebCompilationException("browser-wasm source resolves outside the web root.");
    }

    private static string EnsureBrowserEntryPoint(string source, IReadOnlyDictionary<string, XpsWebRouteDescriptor> routes)
    {
        if (Regex.IsMatch(source, @"(?im)^\s*(?:Public\s+|Private\s+)?Sub\s+Main\b"))
            return source;
        var entry = routes.ContainsKey("Index") ? "Index" : routes.Count == 1 ? routes.Keys.Single() : null;
        if (entry is null)
            throw new XpsWebCompilationException("browser-wasm source must define Main, Index, or exactly one exported route.");
        return source + Environment.NewLine + Environment.NewLine + "Public Sub Main()" + Environment.NewLine + "    Call " + entry + "()" + Environment.NewLine + "End Sub" + Environment.NewLine;
    }

    private static string BuildProject(string browserRuntimeAssemblyPath)
    {
        var escaped = SecurityElement.Escape(browserRuntimeAssemblyPath) ?? throw new XpsWebCompilationException("Unable to encode browser runtime path.");
        return $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RuntimeIdentifier>browser-wasm</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
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
    <Content Include="index.html" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
    <Content Include="xpscript-browser.js" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
""";
    }

    private static async Task RunDotNetAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        using var process = Process.Start(psi) ?? throw new XpsWebCompilationException("Unable to start dotnet for browser-wasm compilation.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var output = await stdout.ConfigureAwait(false) + Environment.NewLine + await stderr.ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new XpsWebCompilationException("browser-wasm build failed." + Environment.NewLine + Redact(output, workingDirectory));
    }

    private static string Redact(string value, string workspace)
    {
        var result = value.Replace(workspace, "<wasm-build>", StringComparison.OrdinalIgnoreCase);
        return result.Length <= 16_384 ? result : result[..16_384] + Environment.NewLine + "<diagnostics truncated>";
    }

    private static void TryDelete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }

    private const string IndexHtml = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>XPScript</title>
  <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css" rel="stylesheet">
</head>
<body><main id="xpscript-app" class="container py-4"></main><script type="module" src="./main.js"></script></body>
</html>
""";

    private const string MainJs = """
import { dotnet } from './_framework/dotnet.js';
import { renderForm } from './xpscript-browser.js';
const { setModuleImports, runMain } = await dotnet.create();
setModuleImports('xpscript-browser', { renderForm });
await runMain();
""";

    private const string BrowserModuleJs = """
export function renderForm(requestJson) {
  const request = JSON.parse(requestJson);
  const root = document.getElementById('xpscript-app');
  root.replaceChildren();
  const form = document.createElement('form');
  form.className = 'row g-3';
  if (request.title) {
    const heading = document.createElement('h1');
    heading.className = 'mb-3';
    heading.textContent = request.title;
    root.appendChild(heading);
  }
  for (const field of request.fields || []) {
    const wrap = document.createElement('div');
    const span = Math.max(1, Math.min(12, Number(field.columnSpan || 12)));
    wrap.className = `col-12 col-md-${span}`;
    const label = document.createElement('label');
    label.className = 'form-label';
    label.textContent = field.label || field.name;
    const input = document.createElement(field.type === 'TextArea' ? 'textarea' : 'input');
    input.className = field.type === 'CheckBox' ? 'form-check-input' : 'form-control';
    input.name = field.name;
    if (field.type === 'PasswordField') input.type = 'password';
    else if (field.type === 'NumberField') input.type = 'number';
    else if (field.type === 'DateField') input.type = 'date';
    else if (field.type === 'CheckBox') input.type = 'checkbox';
    else input.type = 'text';
    if (field.required) input.required = true;
    if (field.value != null && input.type !== 'checkbox') input.value = field.value;
    wrap.append(label, input);
    form.appendChild(wrap);
  }
  root.appendChild(form);
  return JSON.stringify({ result: 'Pending', values: {} });
}
""";
}

public sealed record XpsBrowserWasmBundle(string SourcePath, string SourceHash, string PublishRoot)
{
    public string ResolveAsset(string relativePath)
    {
        var root = Path.GetFullPath(PublishRoot);
        var candidate = Path.GetFullPath(relativePath.Replace('/', Path.DirectorySeparatorChar), root);
        var relative = Path.GetRelativePath(root, candidate);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new XpsWebCompilationException("browser-wasm asset path resolves outside the bundle.");
        return candidate;
    }
}
