using System.IO.Compression;
using XPScript.Compiler;

var root = Path.Combine(Path.GetTempPath(), "xps-webiis-" + Guid.NewGuid().ToString("N"));
var app = Path.Combine(root, "app");
var output = Path.Combine(root, "deploy");
Directory.CreateDirectory(app);

try
{
    await File.WriteAllTextAsync(Path.Combine(app, "main.xps"), "Sub Main()\n    Print \"ok\"\nEnd Sub\n");
    await File.WriteAllTextAsync(Path.Combine(app, "page2.xps"), "Sub Main()\n    Print \"page2\"\nEnd Sub\n");
    Directory.CreateDirectory(Path.Combine(app, "assets"));
    await File.WriteAllTextAsync(Path.Combine(app, "assets", "site.css"), "body{}\n");

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
    Require(File.Exists(Path.Combine(site, "page2.xps")), "additional xps files were not packaged");
    Require(File.Exists(Path.Combine(site, "assets", "site.css")), "static assets were not packaged");
    Require(File.Exists(Path.Combine(site, "host", "xpscript.dll")), "framework-dependent XPscript host was not published");
    Require(File.Exists(Path.Combine(output, "deploy.cmd")), "Web Deploy command was not generated");
    Require(File.Exists(Path.Combine(output, "SetParameters.xml")), "SetParameters.xml was not generated");
    Require(File.Exists(output + ".zip"), "deployment ZIP was not generated");

    var config = await File.ReadAllTextAsync(Path.Combine(site, "web.config"));
    Require(config.Contains("AspNetCoreModuleV2", StringComparison.Ordinal), "web.config does not use ASP.NET Core Module V2");
    Require(config.Contains("xpscript.dll", StringComparison.OrdinalIgnoreCase), "web.config does not start the XPscript host");
    Require(config.Contains("hostingModel=\"outofprocess\"", StringComparison.OrdinalIgnoreCase), "web.config must use out-of-process hosting because XPscript uses Kestrel");
    Require(!config.Contains("--host localhost", StringComparison.OrdinalIgnoreCase), "WebIIS package must let IIS bindings control public hostnames");

    using var archive = ZipFile.OpenRead(output + ".zip");
    Require(archive.Entries.Any(entry => entry.FullName.Replace('\\', '/').Equals("site/main.xps", StringComparison.OrdinalIgnoreCase)), "ZIP does not contain main.xps");
    Require(archive.Entries.Any(entry => entry.FullName.Replace('\\', '/').Equals("site/web.config", StringComparison.OrdinalIgnoreCase)), "ZIP does not contain web.config");

    var invalidOutput = Path.Combine(root, "invalid");
    await File.WriteAllTextAsync(Path.Combine(app, "index.xps"), "Sub Main()\nEnd Sub\n");
    var invalidExit = await XPScriptCompilerCommandLine.CompileAsync([
        Path.Combine(app, "index.xps"),
        "--target", "webiis",
        "--framework-dependent",
        "--output", invalidOutput
    ]);
    Require(invalidExit != 0, "webiis accepted an application without main.xps as the entry file");

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
