using System.IO.Compression;
using System.Security;

namespace XPScript.Compiler;

internal static class WebIisPackageTarget
{
    public static async Task<CompileResult> BuildAsync(string sourcePath, string? outputPath, bool selfContained, string resultFormat)
    {
        if (!File.Exists(sourcePath))
            return CompileResult.Error([new CompileDiagnostic { File = Path.GetFileName(sourcePath), Description = "Source file not found." }]);
        if (!Path.GetFileName(sourcePath).Equals("main.xps", StringComparison.OrdinalIgnoreCase))
            return CompileResult.Error([new CompileDiagnostic { File = Path.GetFileName(sourcePath), Description = "webiis target requires main.xps as the application entry file." }]);

        var root = Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;
        var rootParent = Path.GetDirectoryName(root) ?? root;
        outputPath ??= Path.Combine(rootParent, Path.GetFileName(root) + "-webiis");
        outputPath = Path.GetFullPath(outputPath);
        if (IsInside(outputPath, root))
            return CompileResult.Error([new CompileDiagnostic { Description = "webiis output directory must be outside the application source directory to prevent deployment artifacts from being copied into the package." }]);

        if (Directory.Exists(outputPath)) Directory.Delete(outputPath, true);
        Directory.CreateDirectory(outputPath);

        var siteRoot = Path.Combine(outputPath, "site");
        Directory.CreateDirectory(siteRoot);
        CopyApplicationFiles(root, siteRoot);

        var hostRoot = Path.Combine(siteRoot, "host");
        Directory.CreateDirectory(hostRoot);
        var host = await PublishHostAsync(hostRoot, selfContained).ConfigureAwait(false);

        var webConfig = BuildWebConfig(host.ProcessPath, host.Arguments);
        await File.WriteAllTextAsync(Path.Combine(siteRoot, "web.config"), webConfig).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "SetParameters.xml"), BuildSetParameters()).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "deploy.cmd"), BuildDeployCmd()).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(outputPath, "README-IIS.txt"), BuildReadme(selfContained)).ConfigureAwait(false);

        var zipPath = outputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".zip";
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(outputPath, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

        return CompileResult.Ok(zipPath);
    }

    private static async Task<(string ProcessPath, string Arguments)> PublishHostAsync(string destination, bool selfContained)
    {
        var project = FindCliProject();
        if (project is null)
            throw new CompilerException("Unable to locate src/XPScript.Cli/XPScript.Cli.csproj required to create a webiis package.");

        var args = new List<string>
        {
            "publish", project, "-c", "Release", "-o", destination,
            "--self-contained", selfContained ? "true" : "false"
        };
        if (selfContained)
        {
            args.Add("-r");
            args.Add("win-x64");
        }

        var start = new System.Diagnostics.ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = System.Diagnostics.Process.Start(start)
            ?? throw new CompilerException("Unable to start dotnet publish for webiis target.");
        var stdout = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new CompilerException("Unable to publish IIS host: " + (stderr.Length > 0 ? stderr : stdout));

        if (selfContained)
        {
            var exe = Path.Combine(destination, "xpscript.exe");
            if (!File.Exists(exe)) throw new CompilerException("Self-contained IIS host did not produce xpscript.exe.");
            return (".\\host\\xpscript.exe", "web --root . --default-document index.xps --sessions --static-files");
        }

        var dll = Path.Combine(destination, "xpscript.dll");
        if (!File.Exists(dll)) throw new CompilerException("Framework-dependent IIS host did not produce xpscript.dll.");
        return ("dotnet", ".\\host\\xpscript.dll web --root . --default-document index.xps --sessions --static-files");
    }

    private static string? FindCliProject()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(Path.GetFullPath(start));
            for (var depth = 0; directory is not null && depth < 12; depth++, directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "src", "XPScript.Cli", "XPScript.Cli.csproj");
                if (File.Exists(candidate)) return candidate;
            }
        }
        return null;
    }

    private static void CopyApplicationFiles(string sourceRoot, string destinationRoot)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, directory);
            if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(IsExcludedDirectory)) continue;
            Directory.CreateDirectory(Path.Combine(destinationRoot, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(IsExcludedDirectory)) continue;
            if (Path.GetFileName(file).Equals("web.config", StringComparison.OrdinalIgnoreCase)) continue;
            var target = Path.Combine(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static bool IsExcludedDirectory(string segment)
        => segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
           segment.Equals(".xpscript-cache", StringComparison.OrdinalIgnoreCase) ||
           segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
           segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
           segment.Equals("publish-webiis", StringComparison.OrdinalIgnoreCase);

    private static bool IsInside(string candidate, string root)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return relative.Equals(".", StringComparison.Ordinal) ||
               (!Path.IsPathRooted(relative) && !relative.Equals("..", StringComparison.Ordinal) &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static string BuildWebConfig(string processPath, string arguments)
    {
        var escapedProcess = SecurityElement.Escape(processPath) ?? string.Empty;
        var escapedArgs = SecurityElement.Escape(arguments) ?? string.Empty;
        return $"""
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="{escapedProcess}" arguments="{escapedArgs}" stdoutLogEnabled="false" stdoutLogFile=".\\logs\\stdout" hostingModel="outofprocess">
        <environmentVariables>
          <environmentVariable name="XPSCRIPT_WEB_CACHE_DIRECTORY" value=".\\.xpscript-cache" />
          <environmentVariable name="DOTNET_CLI_HOME" value=".\\.xpscript-cache\\dotnet-home" />
          <environmentVariable name="NUGET_PACKAGES" value=".\\.xpscript-cache\\nuget-packages" />
          <environmentVariable name="NUGET_HTTP_CACHE_PATH" value=".\\.xpscript-cache\\nuget-http-cache" />
          <environmentVariable name="NUGET_PLUGINS_CACHE_PATH" value=".\\.xpscript-cache\\nuget-plugins-cache" />
          <environmentVariable name="USERPROFILE" value=".\\.xpscript-cache\\profile" />
          <environmentVariable name="HOME" value=".\\.xpscript-cache\\profile" />
          <environmentVariable name="APPDATA" value=".\\.xpscript-cache\\profile\\AppData\\Roaming" />
          <environmentVariable name="LOCALAPPDATA" value=".\\.xpscript-cache\\profile\\AppData\\Local" />
          <environmentVariable name="DOTNET_SKIP_FIRST_TIME_EXPERIENCE" value="1" />
          <environmentVariable name="DOTNET_CLI_TELEMETRY_OPTOUT" value="1" />
          <environmentVariable name="DOTNET_NOLOGO" value="1" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
""";
    }

    private static string BuildSetParameters() => """
<?xml version="1.0" encoding="utf-8"?>
<parameters>
  <setParameter name="IIS Web Application Name" value="Default Web Site/XPscriptApp" />
</parameters>
""";

    private static string BuildDeployCmd() => """
@echo off
setlocal
if "%~1"=="" (
  echo Usage: deploy.cmd "IIS Site/Application"
  exit /b 2
)
set MSDEPLOY=%ProgramFiles%\IIS\Microsoft Web Deploy V3\msdeploy.exe
if not exist "%MSDEPLOY%" (
  echo Web Deploy was not found at "%MSDEPLOY%".
  exit /b 3
)
"%MSDEPLOY%" -verb:sync -source:contentPath="%~dp0site" -dest:iisApp="%~1"
exit /b %ERRORLEVEL%
""";

    private static string BuildReadme(bool selfContained) => $"""
XPscript IIS deployment package

Deployment model: {(selfContained ? "self-contained win-x64" : "framework-dependent .NET 10")}
Hosting model: ASP.NET Core Module V2 out-of-process with Kestrel on the IIS-assigned loopback port.
Build entry: main.xps
Web default document: index.xps

Requirements:
- IIS with ASP.NET Core Module V2.
- For framework-dependent packages, install the .NET 10 Hosting Bundle.
- Web Deploy is required only when using deploy.cmd.
- If the application contains [Platform:browser-wasm] files and uses on-demand WASM compilation, install the matching .NET SDK and run: dotnet workload install wasm-tools

Manual installation:
1. Create or select an IIS site/application.
2. Extract the site directory to the IIS physical path.
3. Give the application pool identity read and execute access to the site directory.
4. Use an application pool with No Managed Code.
5. Create site\.xpscript-cache and grant the application pool identity Modify permission only on that cache directory. XPscript uses it for server-side compiled units, the private .NET CLI/NuGet profile and caches, and browser-WASM on-demand compilation.
6. Configure HTTP/HTTPS host bindings in IIS.
7. Start the site.

Web Deploy:
  deploy.cmd "Default Web Site/MyApp"

A request for / resolves to index.xps. index.xps may be a normal server-side XPscript file, a server-rendered UIForm file, or a [Platform:browser-wasm] application.
IIS bindings control the public hostnames. ASP.NET Core Module V2 starts XPscript and forwards requests to its private loopback Kestrel endpoint.
Normal server-side XPscript routes, server-rendered UIForm routes and browser-WASM routes can coexist in the same WebIIS application.
""";
}
