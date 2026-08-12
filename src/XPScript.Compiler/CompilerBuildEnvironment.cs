using System.Diagnostics;

namespace XPScript.Compiler;

internal static class CompilerBuildEnvironment
{
    public static void Configure(ProcessStartInfo startInfo, string workspace)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        var root = Path.GetFullPath(workspace);
        var processTemp = CreatePrivateDirectory(root, "process-temp");
        var cliHome = CreatePrivateDirectory(root, "dotnet-home");
        var nugetPackages = CreatePrivateDirectory(root, "nuget-packages");

        // Resolve the SDK host to an absolute path so the generated build cannot be hijacked
        // by a relative/current-directory PATH entry such as a project-local dotnet.exe.
        startInfo.FileName = CompilerToolResolver.ResolveDotnetHost();

        // Redirect writable build/process state into this invocation's GUID workspace.
        startInfo.Environment["TEMP"] = processTemp;
        startInfo.Environment["TMP"] = processTemp;
        startInfo.Environment["TMPDIR"] = processTemp;
        startInfo.Environment["DOTNET_CLI_HOME"] = cliHome;
        startInfo.Environment["NUGET_PACKAGES"] = nugetPackages;

        // Avoid first-run state and telemetry side effects in a compiler-generated build.
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";

        // Generated projects are fully invocation-local. Do not let externally supplied
        // MSBuild extensions inject targets into them through these common environment hooks.
        startInfo.Environment.Remove("MSBuildProjectExtensionsPath");
        startInfo.Environment.Remove("MSBUILDPROJECTEXTENSIONSPATH");
        startInfo.Environment.Remove("MSBuildSDKsPath");
        startInfo.Environment.Remove("MSBUILDSDKSPATH");
        startInfo.Environment.Remove("MSBUILD_EXE_PATH");
    }

    private static string CreatePrivateDirectory(string root, string name)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(path);
        CompilerPathSecurity.HardenTemporaryDirectory(path);
        return path;
    }
}
