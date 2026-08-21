using System.Diagnostics;
using System.Security;

namespace XPScript.Compiler;

internal static class CompilerBuildEnvironment
{
    private const string AvaloniaVersion = "12.0.3";

    public static void Configure(ProcessStartInfo startInfo, string workspace)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        var root = Path.GetFullPath(workspace);
        var processTemp = CreatePrivateDirectory(root, "process-temp");
        var cliHome = CreatePrivateDirectory(root, "dotnet-home");
        var nugetPackages = CreatePrivateDirectory(root, "nuget-packages");
        var nugetHttpCache = CreatePrivateDirectory(root, "nuget-http-cache");
        var nugetPluginsCache = CreatePrivateDirectory(root, "nuget-plugins-cache");
        var profile = CreatePrivateDirectory(root, "profile");
        var appData = CreatePrivateDirectory(profile, Path.Combine("AppData", "Roaming"));
        var localAppData = CreatePrivateDirectory(profile, Path.Combine("AppData", "Local"));
        _ = CreatePrivateDirectory(appData, "NuGet");

        ConfigureDesktopUiDependencies(startInfo, root);

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

        startInfo.Environment.Remove("MSBuildProjectExtensionsPath");
        startInfo.Environment.Remove("MSBUILDPROJECTEXTENSIONSPATH");
        startInfo.Environment.Remove("MSBuildSDKsPath");
        startInfo.Environment.Remove("MSBUILDSDKSPATH");
        startInfo.Environment.Remove("MSBUILD_EXE_PATH");
    }

    private static void ConfigureDesktopUiDependencies(ProcessStartInfo startInfo, string root)
    {
        var generatedSource = Path.Combine(root, "Program.cs");
        if (!File.Exists(generatedSource)) return;

        var source = File.ReadAllText(generatedSource);
        var usesUiForm = source.Contains("XPScriptUI.CreateForm(", StringComparison.Ordinal);
        var usesUiListView = source.Contains("XPScriptUIList.CreateListView(", StringComparison.Ordinal);
        var usesDesktopDialog = source.Contains("XPScriptUIDialogRuntime.", StringComparison.Ordinal);
        var runtimeIdentifier = ReadRuntimeIdentifier(startInfo);
        var stagedIconName = StageApplicationIcon(source, root, runtimeIdentifier);

        if (!usesUiForm && !usesUiListView && !usesDesktopDialog && stagedIconName is null) return;

        string? escapedAssembly = null;
        if (usesUiForm || usesUiListView || usesDesktopDialog)
        {
            var desktopAssembly = typeof(XPScript.UI.Desktop.DesktopFormHost).Assembly.Location;
            if (string.IsNullOrWhiteSpace(desktopAssembly) || !File.Exists(desktopAssembly))
                throw new CompilerException("Desktop UI runtime assembly is unavailable for UI compilation.");

            escapedAssembly = SecurityElement.Escape(Path.GetFullPath(desktopAssembly))
                ?? throw new CompilerException("Desktop UI runtime assembly path could not be encoded.");
        }

        var propsPath = Path.Combine(root, "Directory.Build.props");
        var propertyGroup = stagedIconName is null
            ? string.Empty
            : $"""
  <PropertyGroup>
    <ApplicationIcon>{SecurityElement.Escape(stagedIconName)}</ApplicationIcon>
  </PropertyGroup>
""";

        var itemGroup = escapedAssembly is null
            ? string.Empty
            : $"""
  <ItemGroup>
    <Reference Include="XPScript.UI.Desktop">
      <HintPath>{escapedAssembly}</HintPath>
      <Private>true</Private>
    </Reference>
    <PackageReference Include="Avalonia" Version="{AvaloniaVersion}" />
    <PackageReference Include="Avalonia.Desktop" Version="{AvaloniaVersion}" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="{AvaloniaVersion}" />
  </ItemGroup>
""";

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
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        CompilerPathSecurity.HardenTemporaryDirectory(path);
        return path;
    }
}
