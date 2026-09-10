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
            var generated = new XPScriptTranspiler().TranspileRestricted(browserSource, sourcePath, Platform, [_webRoot]);
            generated = generated.Replace("XPScript.UI.Desktop.DesktopFormHost, XPScript.UI.Desktop", "XPScript.UI.Browser.BrowserFormHost, XPScript.UI.Browser", StringComparison.Ordinal);
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
            if (!IsValidAppRoot(appRoot)) throw new XpsWebCompilationException("browser-wasm persisted app bundle is incomplete.");
            await File.WriteAllTextAsync(marker, hash, cancellationToken).ConfigureAwait(false);
            return new XpsBrowserWasmBundle(sourcePath, hash, appRoot);
        }
        finally { if (workspace is not null) TryDelete(workspace); _buildGate.Release(); }
    }

    private static string CreateBuildWorkspace() { var path = Path.Combine(Path.GetTempPath(), "xw" + Guid.NewGuid().ToString("N")[..10]); Directory.CreateDirectory(path); return path; }
    private static bool IsValidAppRoot(string appRoot) => Directory.Exists(appRoot) && File.Exists(Path.Combine(appRoot, "main.js")) && File.Exists(Path.Combine(appRoot, "_framework", "dotnet.js"));
    private static string ResolveBuiltAppRoot(params string[] searchRoots)
    {
        foreach (var searchRoot in searchRoots)
        {
            if (!Directory.Exists(searchRoot)) continue;
            var fullSearchRoot = Path.GetFullPath(searchRoot);
            if (File.Exists(Path.Combine(fullSearchRoot, "_framework", "dotnet.js"))) return fullSearchRoot;
            var frameworkEntry = Directory.EnumerateFiles(fullSearchRoot, "dotnet.js", SearchOption.AllDirectories).FirstOrDefault(path => string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "_framework", StringComparison.OrdinalIgnoreCase));
            if (frameworkEntry is null) continue;
            var frameworkDirectory = Path.GetDirectoryName(frameworkEntry) ?? throw new XpsWebCompilationException("Unable to determine browser-wasm framework directory.");
            return Directory.GetParent(frameworkDirectory)?.FullName ?? throw new XpsWebCompilationException("Unable to determine browser-wasm application root.");
        }
        throw new XpsWebCompilationException("browser-wasm build output did not contain _framework/dotnet.js.");
    }
    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        var sourceRoot = Path.GetFullPath(sourceDirectory); var destinationRoot = Path.GetFullPath(destinationDirectory);
        if (Directory.Exists(destinationRoot)) Directory.Delete(destinationRoot, true); Directory.CreateDirectory(destinationRoot);
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)) { var destination = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file)); Directory.CreateDirectory(Path.GetDirectoryName(destination)!); File.Copy(file, destination, true); }
    }
    private string BuildIndexHtml(string sourcePath) => IndexHtml.Replace("__XPSCRIPT_BASE_HREF__", Uri.EscapeDataString(Path.GetFileName(sourcePath)) + "/", StringComparison.Ordinal);
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
    private static string BuildProject(string browserRuntimeAssemblyPath)
    {
        var escaped = SecurityElement.Escape(browserRuntimeAssemblyPath) ?? throw new XpsWebCompilationException("Unable to encode browser runtime path.");
        return $$"""
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><RuntimeIdentifier>browser-wasm</RuntimeIdentifier><OutputType>Exe</OutputType><StartupObject>Program</StartupObject><AllowUnsafeBlocks>true</AllowUnsafeBlocks><WasmMainJSPath>main.js</WasmMainJSPath><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><AssemblyName>XPScript.BrowserApp</AssemblyName></PropertyGroup><ItemGroup><Reference Include="XPScript.UI.Browser"><HintPath>{{escaped}}</HintPath><Private>true</Private></Reference><TrimmerRootAssembly Include="XPScript.UI.Browser" /><Content Include="index.html" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" /><Content Include="xpscript-browser.js" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" /></ItemGroup></Project>
""";
    }
    private static async Task RunDotNetAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }; foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        using var process = Process.Start(psi) ?? throw new XpsWebCompilationException("Unable to start dotnet for browser-wasm compilation."); var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken); var stderr = process.StandardError.ReadToEndAsync(cancellationToken); await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false); var output = await stdout.ConfigureAwait(false) + Environment.NewLine + await stderr.ConfigureAwait(false); if (process.ExitCode != 0) throw new XpsWebCompilationException("browser-wasm build failed." + Environment.NewLine + Redact(output, workingDirectory));
    }
    private static string Redact(string value, string workspace) { var result = value.Replace(workspace, "<wasm-build>", StringComparison.OrdinalIgnoreCase); return result.Length <= 16_384 ? result : result[..16_384] + Environment.NewLine + "<diagnostics truncated>"; }
    private static void TryDelete(string path) { try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { } }

    private const string IndexHtml = """
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><base href="__XPSCRIPT_BASE_HREF__"><title>XPScript</title><link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css" rel="stylesheet"></head><body><main id="xpscript-app" class="container py-4"></main><script type="module" src="./main.js"></script></body></html>
""";
    private const string MainJs = """
import { dotnet } from './_framework/dotnet.js';
import { applyApplicationMetadata, consumeRequestState, navigate, renderForm, setEventDispatcher, stageRequestState } from './xpscript-browser.js';
const { setModuleImports, getAssemblyExports, runMain } = await dotnet.create();
setModuleImports('xpscript-browser', { applyApplicationMetadata, consumeRequestState, navigate, renderForm, stageRequestState });
await runMain('XPScript.BrowserApp');
const browserExports = await getAssemblyExports('XPScript.UI.Browser.dll');
setEventDispatcher((eventToken, submittedValue) => browserExports.XPScript.UI.Browser.BrowserFormHost.DispatchEventAsync(eventToken, submittedValue));
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
function applyFieldState(field,editor){if(field.enabled===false)editor.disabled=true;if(field.readOnly===true){if('readOnly'in editor)editor.readOnly=true;else editor.disabled=true;}if(field.required===true)editor.required=true;if(field.minLength!=null&&'minLength'in editor)editor.minLength=Number(field.minLength);if(field.maxLength!=null&&'maxLength'in editor)editor.maxLength=Number(field.maxLength);if(field.minimum!=null&&'min'in editor)editor.min=String(field.minimum);if(field.maximum!=null&&'max'in editor)editor.max=String(field.maximum);if(field.placeholder&&'placeholder'in editor)editor.placeholder=String(field.placeholder);if(field.tooltip)editor.title=String(field.tooltip);}
function createEditor(field){const type=fieldType(field);if(type==='separator'){const e=document.createElement('hr');e.className='xpscript-uiform-separator my-2';return e;}if(type==='spacer'){const e=document.createElement('div');e.className='xpscript-uiform-spacer';e.style.height='1rem';return e;}if(type==='select'||type==='listbox'||type==='multilistbox'){const e=document.createElement('select');e.className='form-select';if(type!=='select')e.size=6;if(type==='multilistbox')e.multiple=true;const selected=new Set((field.values||[]).map(String));for(const v of field.options||[]){const o=document.createElement('option');o.value=String(v);o.textContent=String(v);o.selected=type==='multilistbox'?selected.has(o.value):field.value!=null&&String(field.value)===o.value;e.appendChild(o);}applyFieldState(field,e);return e;}if(type==='radiogroup'){const g=document.createElement('div');for(const v of field.options||[]){const i=document.createElement('input');i.type='radio';i.name=field.name;i.value=String(v);i.checked=field.value!=null&&String(field.value)===i.value;g.appendChild(i);}return g;}const e=document.createElement(type==='textarea'?'textarea':'input');e.className=type==='checkbox'?'form-check-input':'form-control';if(type==='checkbox')e.type='checkbox';else if(type==='passwordfield')e.type='password';else if(type==='numberfield'||type==='rangefield')e.type=type==='rangefield'?'range':'number';else if(type==='datefield')e.type='date';else if(type==='timefield')e.type='time';else if(type==='emailfield')e.type='email';else if(type==='urlfield')e.type='url';else e.type='text';if(type==='checkbox'){const v=String(field.value||'').toLowerCase();e.checked=v==='true'||v==='1';}else if(type!=='passwordfield'&&field.value!=null)e.value=String(field.value);applyFieldState(field,e);return e;}
function readFieldValue(field,editor){const type=fieldType(field);if(type==='checkbox')return Boolean(editor.checked);if(type==='radiogroup'){const s=editor.querySelector('input[type="radio"]:checked');return s?s.value:'';}if(type==='multilistbox')return Array.from(editor.selectedOptions).map(o=>o.value);if(type==='numberfield'||type==='rangefield'){if(editor.value==='')return '';const n=Number(editor.value);return Number.isFinite(n)?n:editor.value;}return editor.value??'';}
function toSubmittedValue(field,editor){const value=readFieldValue(field,editor);if(Array.isArray(value))return value.map(String).join('\u001f');if(typeof value==='boolean')return value?'true':'false';return value==null?'':String(value);}
function mergeByName(existing,updates){if(!Array.isArray(existing)||!Array.isArray(updates))return existing||[];const byName=new Map(updates.map(item=>[String(item?.name||'').toLowerCase(),item]));return existing.map(item=>{const update=byName.get(String(item?.name||'').toLowerCase());return update?{...item,...update}:item;});}
function buttonClass(style){switch(String(style||'').toLowerCase()){case'primary':return'btn btn-primary';case'success':return'btn btn-success';case'danger':return'btn btn-danger';default:return'btn btn-secondary';}}
export function renderForm(requestJson){const request=JSON.parse(requestJson);const root=document.getElementById('xpscript-app');if(!root)throw new Error('XPScript browser root element was not found.');root.replaceChildren();if(request.title){const h=document.createElement('h1');h.textContent=request.title;root.appendChild(h);}const form=document.createElement('form');form.className='xpscript-uiform';const editors=new Map();const dispatchUiEvent=async(eventToken,submittedValue)=>{if(typeof eventDispatcher!=='function')return false;try{const resultJson=await eventDispatcher(eventToken,submittedValue);if(!resultJson)return true;const state=JSON.parse(resultJson);request.fields=mergeByName(request.fields||[],state.fields||[]);request.buttons=mergeByName(request.buttons||[],state.buttons||[]);if(state.navigation?.target){navigate(String(state.navigation.target),'');return true;}renderForm(JSON.stringify(request));return true;}catch{root.dataset.xpscriptError='UI event callback failed';return true;}};for(const field of request.fields||[]){const type=fieldType(field);if(type==='hiddenfield')continue;const wrap=document.createElement('div');const editor=createEditor(field);if(type!=='radiogroup'&&type!=='separator'&&type!=='spacer')editor.name=field.name||'';if(type!=='separator'&&type!=='spacer')editors.set(field.name||'',editor);if(field.onChangeHandler&&type!=='separator'&&type!=='spacer')editor.addEventListener('change',async()=>{await dispatchUiEvent(`change:${field.name||''}`,toSubmittedValue(field,editor));});wrap.appendChild(editor);form.appendChild(wrap);}root.appendChild(form);const collectValues=()=>{const values={};for(const field of request.fields||[]){const type=fieldType(field);if(type==='separator'||type==='spacer')continue;if(type==='hiddenfield'){if(field.value!=null)values[field.name]=field.value;continue;}const editor=editors.get(field.name||'');if(editor)values[field.name]=readFieldValue(field,editor);}return values;};const publishResult=(result,actionName='')=>{const payload={result,values:collectValues()};if(actionName)payload.action=actionName;root.dataset.xpscriptResult=JSON.stringify(payload);root.dispatchEvent(new CustomEvent('xpscript:form-result',{detail:payload}));};if((request.buttons||[]).length>0){const actions=document.createElement('div');for(const definition of request.buttons){if(definition.visible===false)continue;const button=document.createElement('button');button.type='button';button.className=buttonClass(definition.style);button.textContent=definition.label||definition.name||'Action';button.disabled=definition.enabled===false;button.addEventListener('click',async()=>{const handled=await dispatchUiEvent(`button:${definition.name||''}`,JSON.stringify(collectValues()));if(!handled)publishResult('Action',definition.name||'');});actions.appendChild(button);}root.appendChild(actions);}return JSON.stringify({result:'Pending',values:{}});}
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