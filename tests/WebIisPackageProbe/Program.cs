using System.IO.Compression;
using XPScript.Compiler;

var root = Path.Combine(Path.GetTempPath(), "xps-webiis-" + Guid.NewGuid().ToString("N"));
var app = Path.Combine(root, "app");
var output = Path.Combine(root, "deploy");
Directory.CreateDirectory(app);

try
{
    await File.WriteAllTextAsync(Path.Combine(app, "main.xps"), "Sub Main()\n    Print \"ok\"\nEnd Sub\n");
    await File.WriteAllTextAsync(Path.Combine(app, "index.xps"), "[Anonymous]\n[Get]\nSub Index()\n    Response.Write(\"index\")\nEnd Sub\n");
    await File.WriteAllTextAsync(Path.Combine(app, "page2.xps"), "Sub Main()\n    Print \"page2\"\nEnd Sub\n");
    Directory.CreateDirectory(Path.Combine(app, "assets"));
    await File.WriteAllTextAsync(Path.Combine(app, "assets", "site.css"), "body{}\n");
    Directory.CreateDirectory(Path.Combine(app, ".xpscript-cache"));
    await File.WriteAllTextAsync(Path.Combine(app, ".xpscript-cache", "stale-cache.txt"), "must-not-deploy\n");

    var exitCode = await XPScriptCompilerCommandLine.CompileAsync([
        Path.Combine(app, "main.xps"),
        "--target", "webiis",
        "--framework-dependent",
        "--output", output
    ]);
    Require(exitCode == 0, "webiis compiler target returned an error");

    var site = Path.Combine(output, "site");
    Require(File.Exists(Path.Combine(site, "web.config")), "web.config was not generated");
    Require(File.Exists(Path.Combine(site, "main.xps")), "main.xps was not packaged");
    Require(File.Exists(Path.Combine(site, "index.xps")), "index.xps was not packaged");
    Require(File.Exists(Path.Combine(site, "page2.xps")), "additional xps files were not packaged");
    Require(File.Exists(Path.Combine(site, "assets", "site.css")), "static assets were not packaged");
    Require(!Directory.Exists(Path.Combine(site, ".xpscript-cache")), "source .xpscript-cache must not be copied into a WebIIS deployment");
    Require(File.Exists(Path.Combine(site, "host", "xpscript.dll")), "framework-dependent XPscript host was not published");
    Require(File.Exists(Path.Combine(output, "deploy.cmd")), "Web Deploy command was not generated");
    Require(File.Exists(Path.Combine(output, "SetParameters.xml")), "SetParameters.xml was not generated");
    Require(File.Exists(output + ".zip"), "deployment ZIP was not generated");

    var config = await File.ReadAllTextAsync(Path.Combine(site, "web.config"));
    Require(config.Contains("AspNetCoreModuleV2", StringComparison.Ordinal), "web.config does not use ASP.NET Core Module V2");
    Require(config.Contains("xpscript.dll", StringComparison.OrdinalIgnoreCase), "web.config does not start the XPscript host");
    Require(config.Contains("hostingModel=\"outofprocess\"", StringComparison.OrdinalIgnoreCase), "web.config must use out-of-process hosting because XPscript uses Kestrel");
    Require(config.Contains("--default-document index.xps", StringComparison.OrdinalIgnoreCase), "WebIIS host must resolve / through index.xps");
    Require(config.Contains("XPSCRIPT_WEB_CACHE_DIRECTORY", StringComparison.Ordinal), "WebIIS must configure an explicit writable compilation cache");
    Require(config.Contains(".xpscript-cache", StringComparison.Ordinal), "WebIIS compilation cache must be site-local .xpscript-cache");
    Require(config.Contains("DOTNET_CLI_HOME", StringComparison.Ordinal), "WebIIS must give child dotnet processes a writable CLI home");
    Require(config.Contains("NUGET_PACKAGES", StringComparison.Ordinal), "WebIIS must give child dotnet processes a writable NuGet package directory");
    Require(config.Contains("NUGET_HTTP_CACHE_PATH", StringComparison.Ordinal), "WebIIS must isolate the NuGet HTTP cache");
    Require(config.Contains("NUGET_PLUGINS_CACHE_PATH", StringComparison.Ordinal), "WebIIS must isolate the NuGet plugin cache");
    Require(config.Contains("USERPROFILE", StringComparison.Ordinal), "WebIIS must isolate the Windows user profile for runtime compilation");
    Require(config.Contains("HOME", StringComparison.Ordinal), "WebIIS must isolate HOME for runtime compilation");
    Require(config.Contains("APPDATA", StringComparison.Ordinal), "WebIIS must isolate APPDATA so NuGet.Config does not resolve through systemprofile");
    Require(config.Contains("LOCALAPPDATA", StringComparison.Ordinal), "WebIIS must isolate LOCALAPPDATA for runtime compilation");
    Require(config.Contains("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", StringComparison.Ordinal), "WebIIS must disable dotnet first-time profile initialization");
    Require(config.Contains("DOTNET_CLI_TELEMETRY_OPTOUT", StringComparison.Ordinal), "WebIIS must disable dotnet CLI telemetry for runtime compilation");
    Require(config.Contains("DOTNET_NOLOGO", StringComparison.Ordinal), "WebIIS runtime compilation must suppress dotnet first-run output");
    Require(!config.Contains("systemprofile", StringComparison.OrdinalIgnoreCase), "WebIIS build environment must not depend on the Windows system profile");
    Require(!config.Contains("--default-document main.xps", StringComparison.OrdinalIgnoreCase), "main.xps must remain the build entry and must not be the HTTP default document");
    Require(!config.Contains("--host localhost", StringComparison.OrdinalIgnoreCase), "WebIIS package must let IIS bindings control public hostnames");

    using var archive = ZipFile.OpenRead(output + ".zip");
    Require(archive.Entries.Any(entry => entry.FullName.Replace('\\', '/').Equals("site/main.xps", StringComparison.OrdinalIgnoreCase)), "ZIP does not contain main.xps");
    Require(archive.Entries.Any(entry => entry.FullName.Replace('\\', '/').Equals("site/index.xps", StringComparison.OrdinalIgnoreCase)), "ZIP does not contain index.xps");
    Require(archive.Entries.Any(entry => entry.FullName.Replace('\\', '/').Equals("site/web.config", StringComparison.OrdinalIgnoreCase)), "ZIP does not contain web.config");
    Require(!archive.Entries.Any(entry => entry.FullName.Replace('\\', '/').Contains("/.xpscript-cache/", StringComparison.OrdinalIgnoreCase)), "ZIP must not contain a source .xpscript-cache directory");

    var invalidApp = Path.Combine(root, "invalid-app");
    var invalidOutput = Path.Combine(root, "invalid");
    Directory.CreateDirectory(invalidApp);
    await File.WriteAllTextAsync(Path.Combine(invalidApp, "index.xps"), "Sub Main()\nEnd Sub\n");
    var invalidExit = await XPScriptCompilerCommandLine.CompileAsync([
        Path.Combine(invalidApp, "index.xps"),
        "--target", "webiis",
        "--framework-dependent",
        "--output", invalidOutput
    ]);
    Require(invalidExit != 0, "webiis accepted index.xps as the build entry instead of requiring main.xps");

    Console.WriteLine("WEBIIS-PACKAGE=OK");
    return 0;
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
