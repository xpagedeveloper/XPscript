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
function clampInteger(value, minimum, maximum, fallback) {
  const parsed = Number.parseInt(value, 10);
  if (!Number.isFinite(parsed)) return fallback;
  return Math.max(minimum, Math.min(maximum, parsed));
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
      button.addEventListener('click', () => publishResult('Action', definition.name || ''));
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
        var candidate = Path.GetFullPath(relativePath.Replace('/', Path.DirectorySeparatorChar), root);
        var relative = Path.GetRelativePath(root, candidate);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new XpsWebCompilationException("browser-wasm asset path resolves outside the bundle.");
        return candidate;
    }
}