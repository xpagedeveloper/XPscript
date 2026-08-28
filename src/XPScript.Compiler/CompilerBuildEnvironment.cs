using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class CompilerBuildEnvironment
{
    private const string AvaloniaVersion = "12.0.3";
    private const string MicrosoftDataSqliteVersion = "10.0.11";
    private const string MicrosoftDataSqlClientVersion = "7.0.2";
    private const string MySqlConnectorVersion = "2.6.2";

    public static void Configure(ProcessStartInfo startInfo, string workspace)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        var root = Path.GetFullPath(workspace);
        var usePersistentRunCache = IsTransientRunBuild(startInfo);
        var cacheRoot = usePersistentRunCache ? PersistentRunCacheRoot() : root;
        var environmentRoot = usePersistentRunCache
            ? CreatePrivateDirectory(cacheRoot, "environment")
            : root;

        // Process temp remains invocation-local. The dotnet user/profile state is safe to
        // reuse for transient run builds because the persistent cache is owner-only.
        // Keeping DOTNET_CLI_HOME and the profile stable avoids paying a fresh CLI/tooling
        // cold-start on every edit-run cycle while release compile remains one-shot isolated.
        var processTemp = CreatePrivateDirectory(root, "process-temp");
        var cliHome = CreatePrivateDirectory(environmentRoot, "dotnet-home");
        var profile = CreatePrivateDirectory(environmentRoot, "profile");
        var appData = CreatePrivateDirectory(profile, Path.Combine("AppData", "Roaming"));
        var localAppData = CreatePrivateDirectory(profile, Path.Combine("AppData", "Local"));
        _ = CreatePrivateDirectory(appData, "NuGet");

        var nugetPackages = CreatePrivateDirectory(cacheRoot, "nuget-packages");
        var nugetHttpCache = CreatePrivateDirectory(cacheRoot, "nuget-http-cache");
        var nugetPluginsCache = CreatePrivateDirectory(cacheRoot, "nuget-plugins-cache");

        ConfigureGeneratedDependencies(startInfo, root);
        if (usePersistentRunCache)
            ConfigurePersistentRunBuild(startInfo, root, cacheRoot);

        startInfo.FileName = CompilerToolResolver.ResolveDotnetHost();

        startInfo.Environment["TEMP"] = processTemp;
        startInfo.Environment["TMP"] = processTemp;
        startInfo.Environment["TMPDIR"] = processTemp;
        startInfo.Environment["DOTNET_CLI_HOME"] = cliHome;
        startInfo.Environment["NUGET_PACKAGES"] = nugetPackages;
        startInfo.Environment["NUGET_HTTP_CACHE_PATH"] = nugetHttpCache;
        startInfo.Environment["NUGET_PLUGINS_CACHE_PATH"] = nugetPluginsCache;
        startInfo.Environment["USERPROFILE"] = profile;
        startInfo.Environment["HOME"] = profile;
        startInfo.Environment["APPDATA"] = appData;
        startInfo.Environment["LOCALAPPDATA"] = localAppData;

        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        if (usePersistentRunCache)
            startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "1";

        startInfo.Environment.Remove("MSBuildProjectExtensionsPath");
        startInfo.Environment.Remove("MSBUILDPROJECTEXTENSIONSPATH");
        startInfo.Environment.Remove("MSBuildSDKsPath");
        startInfo.Environment.Remove("MSBUILDSDKSPATH");
        startInfo.Environment.Remove("MSBUILD_EXE_PATH");
    }

    private static bool IsTransientRunBuild(ProcessStartInfo startInfo)
    {
        if (startInfo.ArgumentList.Count == 0) return false;
        return string.Equals(startInfo.ArgumentList[0], "build", StringComparison.OrdinalIgnoreCase);
    }

    private static string PersistentRunCacheRoot()
    {
        var baseDirectory = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDirectory))
            baseDirectory = Path.Combine(Path.GetTempPath(), "XPScript-user-cache");

        var compilerIdentity = typeof(CompilerBuildEnvironment).Assembly.ManifestModule.ModuleVersionId.ToString("N");
        var root = Path.Combine(baseDirectory, "XPScript", "run-build-cache", compilerIdentity);
        Directory.CreateDirectory(root);
        CompilerPathSecurity.HardenTemporaryDirectory(root);
        return root;
    }

    private static void ConfigurePersistentRunBuild(ProcessStartInfo startInfo, string workspace, string cacheRoot)
    {
        if (startInfo.ArgumentList.Count < 2) return;

        var projectPath = startInfo.ArgumentList[1];
        if (string.IsNullOrWhiteSpace(projectPath)) return;
        projectPath = Path.GetFullPath(projectPath);
        if (!File.Exists(projectPath)) return;

        var projectText = File.ReadAllText(projectPath);
        if (projectText.Contains("<HintPath>", StringComparison.OrdinalIgnoreCase))
            return;

        var propsPath = Path.Combine(workspace, "Directory.Build.props");
        var propsText = File.Exists(propsPath) ? File.ReadAllText(propsPath) : string.Empty;
        var generatedPath = Path.Combine(workspace, "Program.cs");
        var generatedSource = File.Exists(generatedPath) ? File.ReadAllText(generatedPath) : string.Empty;
        var sourceIdentity = ExtractSourceIdentity(generatedSource);
        var rid = ReadRuntimeIdentifier(startInfo);
        var identity = sourceIdentity + "\0" + projectText + "\0" + propsText + "\0" + rid;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();

        var restoreBase = CreatePrivateDirectory(cacheRoot, "restore");
        var restoreRoot = CreatePrivateDirectory(restoreBase, hash[..32]);
        var assetsPath = Path.Combine(restoreRoot, "project.assets.json");
        var propsGeneratedPath = Path.Combine(restoreRoot, "Generated.csproj.nuget.g.props");
        var targetsGeneratedPath = Path.Combine(restoreRoot, "Generated.csproj.nuget.g.targets");

        startInfo.ArgumentList.Add("-p:MSBuildProjectExtensionsPath=" + EnsureTrailingSeparator(restoreRoot));
        if (File.Exists(assetsPath) && File.Exists(propsGeneratedPath) && File.Exists(targetsGeneratedPath))
            startInfo.ArgumentList.Add("--no-restore");

        // Reuse obj state only when this process can claim the cache entry. Concurrent
        // builds of the same script fall back to their invocation-local obj directory.
        // This preserves compiler isolation while allowing normal edit-run cycles to reuse
        // generated MSBuild/Roslyn state across separate xpscript invocations.
        var intermediateBase = CreatePrivateDirectory(cacheRoot, "intermediate");
        var intermediateRoot = CreatePrivateDirectory(intermediateBase, hash[..32]);
        if (TryClaimIntermediateCache(intermediateRoot))
            startInfo.ArgumentList.Add("-p:BaseIntermediateOutputPath=" + EnsureTrailingSeparator(Path.Combine(intermediateRoot, "obj")));
    }

    private static string ExtractSourceIdentity(string generatedSource)
    {
        if (generatedSource.Length == 0) return "generated-empty";

        var lineDirective = Regex.Match(
            generatedSource,
            "#line\\s+\\d+\\s+\"([^\"]+\\.xps)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (lineDirective.Success)
            return lineDirective.Groups[1].Value;

        var quotedSource = Regex.Match(
            generatedSource,
            "\"([^\"\\r\\n]+\\.xps)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (quotedSource.Success)
            return quotedSource.Groups[1].Value;

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(generatedSource))).ToLowerInvariant();
    }

    private static bool TryClaimIntermediateCache(string intermediateRoot)
    {
        var lockPath = Path.Combine(intermediateRoot, "active.lock");
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using (var stream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 1024, leaveOpen: false))
                    writer.Write(Environment.ProcessId);

                CompilerPathSecurity.HardenTemporaryFile(lockPath);
                AppDomain.CurrentDomain.ProcessExit += (_, _) =>
                {
                    try { File.Delete(lockPath); } catch { }
                };
                return true;
            }
            catch (IOException)
            {
                if (!TryRemoveStaleIntermediateLock(lockPath)) return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
        return false;
    }

    private static bool TryRemoveStaleIntermediateLock(string lockPath)
    {
        try
        {
            var text = File.ReadAllText(lockPath).Trim();
            if (!int.TryParse(text, out var processId) || processId <= 0)
                return false;

            try
            {
                using var process = Process.GetProcessById(processId);
                if (!process.HasExited) return false;
            }
            catch (ArgumentException)
            {
                // Process no longer exists.
            }

            File.Delete(lockPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;

    private static void ConfigureGeneratedDependencies(ProcessStartInfo startInfo, string root)
    {
        var generatedSource = Path.Combine(root, "Program.cs");
        if (!File.Exists(generatedSource)) return;

        var source = File.ReadAllText(generatedSource);
        var usesUiForm = source.Contains("XPScriptUI.CreateForm(", StringComparison.Ordinal);
        var usesUiListView = source.Contains("XPScriptUIList.CreateListView(", StringComparison.Ordinal);
        var usesDesktopDialog = source.Contains("XPScriptUIDialogRuntime.", StringComparison.Ordinal);
        var usesSqlite = source.Contains("internal sealed class XPScriptDbSqlite", StringComparison.Ordinal);
        var usesMsSql = source.Contains("internal sealed class XPScriptDbMsSql", StringComparison.Ordinal);
        var usesMySql = source.Contains("XPScriptDbMySql", StringComparison.Ordinal);
        var runtimeIdentifier = ReadRuntimeIdentifier(startInfo);
        var stagedIconName = StageApplicationIcon(source, root, runtimeIdentifier);

        if (usesMySql)
        {
            File.AppendAllText(generatedSource, Environment.NewLine + Environment.NewLine + MySqlDbRuntimeSource.Code + Environment.NewLine);
            CompilerPathSecurity.HardenTemporaryFile(generatedSource);
        }

        if (!usesUiForm && !usesUiListView && !usesDesktopDialog && !usesSqlite && !usesMsSql && !usesMySql && stagedIconName is null) return;

        string? escapedAssembly = null;
        if (usesUiForm || usesUiListView || usesDesktopDialog)
        {
            var desktopAssembly = typeof(XPScript.UI.Desktop.DesktopFormHost).Assembly.Location;
            if (string.IsNullOrWhiteSpace(desktopAssembly) || !File.Exists(desktopAssembly))
                throw new CompilerException("Desktop UI runtime assembly is unavailable for UI compilation.");

            escapedAssembly = SecurityElement.Escape(Path.GetFullPath(desktopAssembly))
                ?? throw new CompilerException("Desktop UI runtime assembly path could not be encoded.");
        }

        var propertyEntries = stagedIconName is null
            ? string.Empty
            : $"""
    <ApplicationIcon>{SecurityElement.Escape(stagedIconName)}</ApplicationIcon>
""";
        if (usesSqlite || usesMsSql)
        {
            propertyEntries += """
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
""";
        }

        var propertyGroup = propertyEntries.Length == 0
            ? string.Empty
            : $"""
  <PropertyGroup>
{propertyEntries}  </PropertyGroup>
""";

        var itemEntries = escapedAssembly is null
            ? string.Empty
            : $"""
    <Reference Include="XPScript.UI.Desktop">
      <HintPath>{escapedAssembly}</HintPath>
      <Private>true</Private>
    </Reference>
    <PackageReference Include="Avalonia" Version="{AvaloniaVersion}" />
    <PackageReference Include="Avalonia.Desktop" Version="{AvaloniaVersion}" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="{AvaloniaVersion}" />
""";
        if (usesSqlite)
        {
            itemEntries += $"""
    <PackageReference Include="Microsoft.Data.Sqlite" Version="{MicrosoftDataSqliteVersion}" />
""";
        }
        if (usesMsSql)
        {
            itemEntries += $"""
    <PackageReference Include="Microsoft.Data.SqlClient" Version="{MicrosoftDataSqlClientVersion}" />
""";
        }
        if (usesMySql)
        {
            itemEntries += $"""
    <PackageReference Include="MySqlConnector" Version="{MySqlConnectorVersion}" />
""";
        }

        var itemGroup = itemEntries.Length == 0
            ? string.Empty
            : $"""
  <ItemGroup>
{itemEntries}  </ItemGroup>
""";

        var propsPath = Path.Combine(root, "Directory.Build.props");
        var props = $"""
<Project>
{propertyGroup}{itemGroup}</Project>
""";
        File.WriteAllText(propsPath, props);
        CompilerPathSecurity.HardenTemporaryFile(propsPath);
    }

    private static string? StageApplicationIcon(string generatedSource, string root, string runtimeIdentifier)
    {
        if (!runtimeIdentifier.StartsWith("win-", StringComparison.OrdinalIgnoreCase)) return null;

        var markerIndex = generatedSource.IndexOf(ApplicationObjectPreprocessor.BuildIconMarker, StringComparison.Ordinal);
        if (markerIndex < 0) return null;
        var valueStart = markerIndex + ApplicationObjectPreprocessor.BuildIconMarker.Length;
        var valueEnd = generatedSource.IndexOfAny(['\r', '\n'], valueStart);
        var path = (valueEnd < 0 ? generatedSource[valueStart..] : generatedSource[valueStart..valueEnd]).Trim();
        path = path.TrimEnd('"', '\'', '/', '*', ' ', ';', ')');
        if (path.Length == 0) return null;
        if (!Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException("Application.Icon must reference an .ico file when building a Windows executable.");
        if (!File.Exists(path))
            throw new CompilerException("Application.Icon file was not found: " + Path.GetFileName(path));

        var staged = Path.Combine(root, "application.ico");
        CompilerSecureFileCopy.CopyValidatedRegularFile(path, staged, "Application.Icon");
        CompilerPathSecurity.HardenTemporaryFile(staged);
        return Path.GetFileName(staged);
    }

    private static string ReadRuntimeIdentifier(ProcessStartInfo startInfo)
    {
        for (var i = 0; i + 1 < startInfo.ArgumentList.Count; i++)
        {
            if (startInfo.ArgumentList[i] is "-r" or "--runtime")
                return startInfo.ArgumentList[i + 1] ?? string.Empty;
        }
        return string.Empty;
    }

    private static string CreatePrivateDirectory(string root, string name)
    {
        Directory.CreateDirectory(root);
        CompilerPathSecurity.HardenTemporaryDirectory(root);
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        CompilerPathSecurity.HardenTemporaryDirectory(path);
        return path;
    }
}
