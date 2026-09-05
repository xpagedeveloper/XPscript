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

        var compilerIdentity = typeof(XpsBrowserWasmCompiler).Assembly.ManifestModule.ModuleVersionId.ToString("N");
        var assetFingerprint = UIFormAppAssets.ComputeFingerprint(sourcePath);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source + "\0" + compilerIdentity + "\0" + assetFingerprint)));
        var sourceKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetRelativePath(_webRoot, sourcePath).Replace('\\', '/').ToLowerInvariant())))[..24];
        var bundleRoot = Path.Combine(_cacheRoot, sourceKey, hash);
        var appRoot = Path.Combine(bundleRoot, "app");
        var marker = Path.Combine(bundleRoot, "source.sha256");
        if (File.Exists(marker) && IsValidAppRoot(appRoot))
            return new XpsBrowserWasmBundle(sourcePath, hash, appRoot);

        await _buildGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? workspace = null;
        try
        {
            if (File.Exists(marker) && IsValidAppRoot(appRoot))
                return new XpsBrowserWasmBundle(sourcePath, hash, appRoot);

            workspace = CreateBuildWorkspace();
            var publishRoot = Path.Combine(workspace, "pub");
            TryDelete(appRoot);
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
            await File.WriteAllTextAsync(Path.Combine(workspace, "index.html"), BuildIndexHtml(sourcePath), cancellationToken).ConfigureAwait(false);

            await RunDotNetAsync(workspace, ["restore", "BrowserApp.csproj", "--nologo"], cancellationToken).ConfigureAwait(false);
            await RunDotNetAsync(workspace, ["publish", "BrowserApp.csproj", "-c", "Release", "--no-restore", "--nologo", "-o", publishRoot], cancellationToken).ConfigureAwait(false);

            var builtAppRoot = ResolveBuiltAppRoot(publishRoot, workspace);
            CopyDirectory(builtAppRoot, appRoot);
            await File.WriteAllTextAsync(Path.Combine(appRoot, "index.html"), BuildIndexHtml(sourcePath), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(appRoot, "main.js"), MainJs, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(appRoot, "xpscript-browser.js"), BrowserModuleJs, cancellationToken).ConfigureAwait(false);
            if (UIFormAppAssets.UsesUIForm(sourcePath)) UIFormAppAssets.CopyAssetsToDirectory(sourcePath, appRoot);

            if (!IsValidAppRoot(appRoot))
                throw new XpsWebCompilationException("browser-wasm persisted app bundle is incomplete.");

            await File.WriteAllTextAsync(marker, hash, cancellationToken).ConfigureAwait(false);
            return new XpsBrowserWasmBundle(sourcePath, hash, appRoot);
        }
        finally
        {
            if (workspace is not null) TryDelete(workspace);
            _buildGate.Release();
        }
    }

    private static string CreateBuildWorkspace()
    {
        var path = Path.Combine(Path.GetTempPath(), "xw" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static bool IsValidAppRoot(string appRoot) =>
        Directory.Exists(appRoot) &&
        File.Exists(Path.Combine(appRoot, "main.js")) &&
        File.Exists(Path.Combine(appRoot, "_framework", "dotnet.js"));

    private static string ResolveBuiltAppRoot(params string[] searchRoots)
    {
        foreach (var searchRoot in searchRoots)
        {
            if (!Directory.Exists(searchRoot)) continue;

            var fullSearchRoot = Path.GetFullPath(searchRoot);
            var directFramework = Path.Combine(fullSearchRoot, "_framework", "dotnet.js");
            if (File.Exists(directFramework)) return fullSearchRoot;

            var frameworkEntry = Directory.EnumerateFiles(fullSearchRoot, "dotnet.js", SearchOption.AllDirectories)
                .FirstOrDefault(path => string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "_framework", StringComparison.OrdinalIgnoreCase));
            if (frameworkEntry is null) continue;

            var frameworkDirectory = Path.GetDirectoryName(frameworkEntry)
                ?? throw new XpsWebCompilationException("Unable to determine browser-wasm framework directory.");
            return Directory.GetParent(frameworkDirectory)?.FullName
                ?? throw new XpsWebCompilationException("Unable to determine browser-wasm application root.");
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
        {
            var relative = Path.GetRelativePath(sourceRoot, directory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            var destination = Path.Combine(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }

    private string BuildIndexHtml(string sourcePath)
    {
        var scriptName = Uri.EscapeDataString(Path.GetFileName(sourcePath));
        return IndexHtml.Replace("__XPSCRIPT_BASE_HREF__", scriptName + "/", StringComparison.Ordinal);
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
  <base href="__XPSCRIPT_BASE_HREF__">
  <title>XPScript</title>
  <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css" rel="stylesheet">
</head>
<body><main id="xpscript-app" class="container py-4"></main><script type="module" src="./main.js"></script></body>
</html>
""";

    private const string MainJs = """
import { dotnet } from './_framework/dotnet.js';
import {
  applyApplicationMetadata,
  consumeRequestState,
  navigate,
  renderForm,
  setEventDispatcher,
  stageRequestState
} from './xpscript-browser.js';
const { setModuleImports, getAssemblyExports, runMain } = await dotnet.create();
setModuleImports('xpscript-browser', {
  applyApplicationMetadata,
  consumeRequestState,
  navigate,
  renderForm,
  stageRequestState
});
await runMain('XPScript.BrowserApp');
const browserExports = await getAssemblyExports('XPScript.UI.Browser.dll');
setEventDispatcher((eventToken, submittedValue) =>
  browserExports.XPScript.UI.Browser.BrowserFormHost.DispatchEvent(eventToken, submittedValue));
""";

    private const string BrowserModuleJs = """
let eventDispatcher = null;

export function setEventDispatcher(callback) {
  if (typeof callback !== 'function') throw new Error('XPScript browser event dispatcher must be a function.');
  eventDispatcher = callback;
}

function clampInteger(value, minimum, maximum, fallback) {
  const parsed = Number.parseInt(value, 10);
  if (!Number.isFinite(parsed)) return fallback;
  return Math.max(minimum, Math.min(maximum, parsed));
}

function clearRequestState(key) {
  sessionStorage.removeItem(key);
  sessionStorage.removeItem(key + '.target');
  sessionStorage.removeItem(key + '.created');
}

export function stageRequestState(key, stateJson) {
  if (stateJson === '{}') {
    clearRequestState(key);
    return;
  }

  sessionStorage.setItem(key, stateJson);
}

export function consumeRequestState(key, lifetimeMilliseconds) {
  const value = sessionStorage.getItem(key) || '';
  const target = sessionStorage.getItem(key + '.target') || '';
  const created = Number(sessionStorage.getItem(key + '.created') || '0');
  if (!value || !target || !created) return '';

  const lifetime = Number(lifetimeMilliseconds);
  if (!Number.isFinite(lifetime) || lifetime < 0 || (Date.now() - created) > lifetime) {
    clearRequestState(key);
    return '';
  }

  if (window.location.pathname !== target) return '';
  clearRequestState(key);
  return value;
}

export function navigate(target, key) {
  const current = window.location.pathname;
  const slash = current.lastIndexOf('/');
  const basePath = slash >= 0 ? current.substring(0, slash + 1) : '/';
  const next = basePath + target;
  if (sessionStorage.getItem(key)) {
    sessionStorage.setItem(key + '.target', next);
    sessionStorage.setItem(key + '.created', String(Date.now()));
  }

  window.location.href = next;
}

export function applyApplicationMetadata(title, icon) {
  if (title) document.title = title;
  if (!icon) return;

  let link = document.querySelector('link[rel~="icon"]');
  if (!link) {
    link = document.createElement('link');
    link.rel = 'icon';
    document.head.appendChild(link);
  }

  link.href = icon;
}

function fieldType(field) {
  return String(field.type || 'TextField').toLowerCase();
}

function applyFieldState(field, editor) {
  if (field.enabled === false) editor.disabled = true;
  if (field.readOnly === true) {
    if ('readOnly' in editor) editor.readOnly = true;
    else editor.disabled = true;
  }
  if (field.required === true) editor.required = true;
  if (field.minLength != null && 'minLength' in editor) editor.minLength = Number(field.minLength);
  if (field.maxLength != null && 'maxLength' in editor) editor.maxLength = Number(field.maxLength);
  if (field.minimum != null && 'min' in editor) editor.min = String(field.minimum);
  if (field.maximum != null && 'max' in editor) editor.max = String(field.maximum);
  if (field.dateMinimum && 'min' in editor) editor.min = String(field.dateMinimum);
  if (field.dateMaximum && 'max' in editor) editor.max = String(field.dateMaximum);
  if (field.timeMinimum && 'min' in editor) editor.min = String(field.timeMinimum);
  if (field.timeMaximum && 'max' in editor) editor.max = String(field.timeMaximum);
  if (field.dateTimeMinimum && 'min' in editor) editor.min = String(field.dateTimeMinimum);
  if (field.dateTimeMaximum && 'max' in editor) editor.max = String(field.dateTimeMaximum);
  if (field.monthMinimum && 'min' in editor) editor.min = String(field.monthMinimum);
  if (field.monthMaximum && 'max' in editor) editor.max = String(field.monthMaximum);
  if (field.placeholder && 'placeholder' in editor) editor.placeholder = String(field.placeholder);
  if (field.regexPattern && 'pattern' in editor) editor.pattern = String(field.regexPattern);
  if (field.tooltip) editor.title = String(field.tooltip);
}

function createEditor(field) {
  const type = fieldType(field);
  if (type === 'separator') {
    const separator = document.createElement('hr');
    separator.className = 'xpscript-uiform-separator my-2';
    separator.setAttribute('aria-hidden', 'true');
    return separator;
  }
  if (type === 'spacer') {
    const spacer = document.createElement('div');
    spacer.className = 'xpscript-uiform-spacer';
    spacer.style.height = '1rem';
    spacer.setAttribute('aria-hidden', 'true');
    return spacer;
  }
  if (type === 'select' || type === 'listbox' || type === 'multilistbox') {
    const select = document.createElement('select');
    select.className = 'form-select';
    if (type === 'listbox' || type === 'multilistbox') select.size = 6;
    if (type === 'multilistbox') select.multiple = true;
    const selectedValues = new Set((field.values || []).map(value => String(value)));
    for (const optionValue of field.options || []) {
      const option = document.createElement('option');
      option.value = String(optionValue);
      option.textContent = String(optionValue);
      option.selected = type === 'multilistbox'
        ? selectedValues.has(option.value)
        : field.value != null && String(field.value) === option.value;
      select.appendChild(option);
    }
    applyFieldState(field, select);
    return select;
  }

  if (type === 'radiogroup') {
    const group = document.createElement('div');
    group.className = 'd-flex flex-column gap-1';
    if (field.tooltip) group.title = String(field.tooltip);
    for (const optionValue of field.options || []) {
      const item = document.createElement('div');
      item.className = 'form-check';
      const radio = document.createElement('input');
      radio.className = 'form-check-input';
      radio.type = 'radio';
      radio.name = field.name;
      radio.value = String(optionValue);
      radio.checked = field.value != null && String(field.value) === radio.value;
      applyFieldState(field, radio);
      const optionLabel = document.createElement('label');
      optionLabel.className = 'form-check-label';
      optionLabel.textContent = String(optionValue);
      item.append(radio, optionLabel);
      group.appendChild(item);
    }
    return group;
  }

  const editor = document.createElement(type === 'textarea' ? 'textarea' : 'input');
  editor.className = type === 'checkbox' ? 'form-check-input' : 'form-control';
  if (type === 'passwordfield') editor.type = 'password';
  else if (type === 'numberfield' || type === 'rangefield') editor.type = type === 'rangefield' ? 'range' : 'number';
  else if (type === 'datefield') editor.type = 'date';
  else if (type === 'timefield') editor.type = 'time';
  else if (type === 'datetimefield') editor.type = 'datetime-local';
  else if (type === 'monthfield') editor.type = 'month';
  else if (type === 'colorfield') editor.type = 'color';
  else if (type === 'emailfield') editor.type = 'email';
  else if (type === 'urlfield') editor.type = 'url';
  else if (type === 'checkbox') editor.type = 'checkbox';
  else editor.type = 'text';
  if (type === 'checkbox') {
    const value = String(field.value || '').toLowerCase();
    editor.checked = value === 'true' || value === '1';
  } else if (type !== 'passwordfield' && field.value != null) {
    editor.value = String(field.value);
  }
  applyFieldState(field, editor);
  return editor;
}

function readFieldValue(field, editor) {
  const type = fieldType(field);
  if (type === 'checkbox') return Boolean(editor.checked);
  if (type === 'radiogroup') {
    const selected = editor.querySelector('input[type="radio"]:checked');
    return selected ? selected.value : '';
  }
  if (type === 'multilistbox') return Array.from(editor.selectedOptions).map(option => option.value);
  if (type === 'numberfield' || type === 'rangefield') {
    if (editor.value === '') return '';
    const number = Number(editor.value);
    return Number.isFinite(number) ? number : editor.value;
  }
  return editor.value ?? '';
}

function toSubmittedValue(field, editor) {
  const value = readFieldValue(field, editor);
  if (Array.isArray(value)) return value.map(item => String(item)).join('\u001f');
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  return value == null ? '' : String(value);
}

function mergeByName(existing, updates) {
  if (!Array.isArray(existing) || !Array.isArray(updates)) return existing || [];
  const byName = new Map(updates.map(item => [String(item?.name || '').toLowerCase(), item]));
  return existing.map(item => {
    const update = byName.get(String(item?.name || '').toLowerCase());
    return update ? { ...item, ...update } : item;
  });
}

function buttonClass(style) {
  switch (String(style || '').toLowerCase()) {
    case 'primary': return 'btn btn-primary';
    case 'success': return 'btn btn-success';
    case 'danger': return 'btn btn-danger';
    case 'warning': return 'btn btn-warning';
    case 'info': return 'btn btn-info';
    case 'light': return 'btn btn-light';
    case 'dark': return 'btn btn-dark';
    case 'link': return 'btn btn-link';
    default: return 'btn btn-secondary';
  }
}

export function renderForm(requestJson) {
  const request = JSON.parse(requestJson);
  const root = document.getElementById('xpscript-app');
  if (!root) throw new Error('XPScript browser root element was not found.');
  root.replaceChildren();

  if (request.title) {
    const heading = document.createElement('h1');
    heading.className = 'mb-3';
    heading.textContent = request.title;
    root.appendChild(heading);
  }

  const gridColumns = clampInteger(request.gridColumns, 1, 64, 1);
  const form = document.createElement('form');
  form.className = 'xpscript-uiform';
  form.style.display = 'grid';
  form.style.gridTemplateColumns = `repeat(${gridColumns}, minmax(0, 1fr))`;
  form.style.gap = '1rem';
  form.noValidate = false;

  const editors = new Map();
  const dispatchUiEvent = async (eventToken, submittedValue) => {
    if (typeof eventDispatcher !== 'function') return false;
    try {
      const resultJson = await Promise.resolve(eventDispatcher(eventToken, submittedValue));
      if (!resultJson) return true;
      const state = JSON.parse(resultJson);
      request.fields = mergeByName(request.fields || [], state.fields || []);
      request.buttons = mergeByName(request.buttons || [], state.buttons || []);
      if (state.navigation?.target) {
        navigate(String(state.navigation.target), '');
        return true;
      }
      renderForm(JSON.stringify(request));
      return true;
    } catch {
      root.dataset.xpscriptError = 'UI event callback failed';
      root.dispatchEvent(new CustomEvent('xpscript:form-error', { detail: { message: 'UI event callback failed' } }));
      return true;
    }
  };

  let automaticRow = 1;
  for (const field of request.fields || []) {
    const type = fieldType(field);
    if (type === 'hiddenfield') continue;

    const wrap = document.createElement('div');
    wrap.dataset.fieldName = field.name || '';
    if (field.regionId) wrap.dataset.regionId = field.regionId;
    if (field.visible === false) wrap.hidden = true;
    if (field.tooltip && type !== 'separator' && type !== 'spacer') wrap.title = String(field.tooltip);

    const row = Number(field.layoutRow) > 0 ? Number(field.layoutRow) : automaticRow++;
    const column = Number(field.layoutColumn) > 0 ? Number(field.layoutColumn) : 1;
    const columnSpan = Number(field.layoutColumn) > 0
      ? clampInteger(field.columnSpan, 1, gridColumns, 1)
      : gridColumns;
    const rowSpan = clampInteger(field.rowSpan, 1, 64, 1);
    wrap.style.gridColumn = `${column} / span ${columnSpan}`;
    wrap.style.gridRow = `${row} / span ${rowSpan}`;

    const editor = createEditor(field);
    if (type !== 'radiogroup' && type !== 'separator' && type !== 'spacer') editor.name = field.name || '';
    if (type !== 'separator' && type !== 'spacer') editors.set(field.name || '', editor);

    if (field.onChangeHandler && type !== 'separator' && type !== 'spacer') {
      editor.addEventListener('change', async () => {
        await dispatchUiEvent(`change:${field.name || ''}`, toSubmittedValue(field, editor));
      });
    }

    if (type === 'separator' || type === 'spacer') {
      wrap.appendChild(editor);
    } else if (type === 'checkbox') {
      const checkWrap = document.createElement('div');
      checkWrap.className = 'form-check';
      const label = document.createElement('label');
      label.className = 'form-check-label';
      label.textContent = field.label || field.name || '';
      checkWrap.append(editor, label);
      wrap.appendChild(checkWrap);
    } else {
      if (field.label) {
        const label = document.createElement('label');
        label.className = 'form-label';
        label.textContent = field.label;
        wrap.appendChild(label);
      }
      wrap.appendChild(editor);
    }
    form.appendChild(wrap);
  }

  root.appendChild(form);

  const collectValues = () => {
    const values = {};
    for (const field of request.fields || []) {
      const type = fieldType(field);
      if (type === 'separator' || type === 'spacer') continue;
      if (type === 'hiddenfield') {
        if (field.value != null) values[field.name] = field.value;
        continue;
      }
      const editor = editors.get(field.name || '');
      if (editor) values[field.name] = readFieldValue(field, editor);
    }
    return values;
  };

  const publishResult = (result, actionName = '') => {
    const payload = { result, values: collectValues() };
    if (actionName) payload.action = actionName;
    root.dataset.xpscriptResult = JSON.stringify(payload);
    root.dispatchEvent(new CustomEvent('xpscript:form-result', { detail: payload }));
  };

  if ((request.buttons || []).length > 0) {
    const actions = document.createElement('div');
    actions.className = 'd-flex justify-content-end gap-2 mt-3';
    for (const definition of request.buttons) {
      if (definition.visible === false) continue;
      const button = document.createElement('button');
      button.type = 'button';
      button.className = buttonClass(definition.style);
      button.textContent = definition.label || definition.name || 'Action';
      button.disabled = definition.enabled === false;
      button.dataset.actionName = definition.name || '';
      button.addEventListener('click', async () => {
        const handled = await dispatchUiEvent(`button:${definition.name || ''}`, JSON.stringify(collectValues()));
        if (!handled) publishResult('Action', definition.name || '');
      });
      actions.appendChild(button);
    }
    root.appendChild(actions);
  }

  const dialogButtons = document.createElement('div');
  dialogButtons.className = 'd-flex justify-content-end gap-2 mt-3';
  const ok = document.createElement('button');
  ok.type = 'button';
  ok.className = 'btn btn-primary';
  ok.textContent = 'OK';
  ok.addEventListener('click', () => {
    if (form.reportValidity()) publishResult('OK');
  });
  const cancel = document.createElement('button');
  cancel.type = 'button';
  cancel.className = 'btn btn-secondary';
  cancel.textContent = 'Cancel';
  cancel.addEventListener('click', () => publishResult('Cancel'));
  dialogButtons.append(ok, cancel);
  root.appendChild(dialogButtons);

  return JSON.stringify({ result: 'Pending', values: {} });
}
""";
}

public sealed record XpsBrowserWasmBundle(string SourcePath, string SourceHash, string PublishRoot)
{
    public string ResolveAsset(string relativePath)
    {
        var root = Path.GetFullPath(PublishRoot);
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(normalized, root);
        var relative = Path.GetRelativePath(root, candidate);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new XpsWebCompilationException("browser-wasm asset path resolves outside the bundle.");
        if (File.Exists(candidate)) return candidate;

        var webRelative = relativePath.Replace('\\', '/');
        if (!webRelative.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) return candidate;
        var sourceDirectory = Path.GetFullPath(Path.GetDirectoryName(SourcePath) ?? Environment.CurrentDirectory);
        var assetRoot = Path.Combine(sourceDirectory, UIFormAppAssets.DirectoryName);
        var sourceCandidate = Path.GetFullPath(Path.Combine(sourceDirectory, normalized));
        var assetPrefix = Path.GetFullPath(assetRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!sourceCandidate.StartsWith(assetPrefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new XpsWebCompilationException("browser-wasm application asset path escapes the assets directory.");
        return sourceCandidate;
    }
}