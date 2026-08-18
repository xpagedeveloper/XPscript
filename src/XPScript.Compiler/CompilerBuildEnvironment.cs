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

        ConfigureDesktopUiDependencies(root);

        startInfo.FileName = CompilerToolResolver.ResolveDotnetHost();

        startInfo.Environment["TEMP"] = processTemp;
        startInfo.Environment["TMP"] = processTemp;
        startInfo.Environment["TMPDIR"] = processTemp;
        startInfo.Environment["DOTNET_CLI_HOME"] = cliHome;
        startInfo.Environment["NUGET_PACKAGES"] = nugetPackages;

        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        startInfo.Environment.Remove("MSBuildProjectExtensionsPath");
        startInfo.Environment.Remove("MSBUILDPROJECTEXTENSIONSPATH");
        startInfo.Environment.Remove("MSBuildSDKsPath");
        startInfo.Environment.Remove("MSBUILDSDKSPATH");
        startInfo.Environment.Remove("MSBUILD_EXE_PATH");
    }

    private static void ConfigureDesktopUiDependencies(string root)
    {
        var generatedSource = Path.Combine(root, "Program.cs");
        if (!File.Exists(generatedSource)) return;

        var source = File.ReadAllText(generatedSource);
        var usesUiForm = source.Contains("XPScriptUI.CreateForm(", StringComparison.Ordinal);
        var usesUiListView = source.Contains("XPScriptUIList.CreateListView(", StringComparison.Ordinal);
        var usesDesktopDialog = source.Contains("XPScriptUIDialogRuntime.", StringComparison.Ordinal);
        if (!usesUiForm && !usesUiListView && !usesDesktopDialog) return;

        var desktopAssembly = typeof(XPScript.UI.Desktop.DesktopFormHost).Assembly.Location;
        if (string.IsNullOrWhiteSpace(desktopAssembly) || !File.Exists(desktopAssembly))
            throw new CompilerException("Desktop UI runtime assembly is unavailable for UI compilation.");

        var escapedAssembly = SecurityElement.Escape(Path.GetFullPath(desktopAssembly))
            ?? throw new CompilerException("Desktop UI runtime assembly path could not be encoded.");
        var propsPath = Path.Combine(root, "Directory.Build.props");
        var props = $"""
<Project>
  <ItemGroup>
    <Reference Include="XPScript.UI.Desktop">
      <HintPath>{escapedAssembly}</HintPath>
      <Private>true</Private>
    </Reference>
    <PackageReference Include="Avalonia" Version="{AvaloniaVersion}" />
    <PackageReference Include="Avalonia.Desktop" Version="{AvaloniaVersion}" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="{AvaloniaVersion}" />
  </ItemGroup>
</Project>
""";
        File.WriteAllText(propsPath, props);
        CompilerPathSecurity.HardenTemporaryFile(propsPath);
    }

    private static string CreatePrivateDirectory(string root, string name)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        CompilerPathSecurity.HardenTemporaryDirectory(path);
        return path;
    }
}
