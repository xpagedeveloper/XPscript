using System.Diagnostics;

namespace XPScript.Compiler;

internal static class TargetPrerequisiteValidator
{
    public static void Validate(string runtimeIdentifier)
    {
        if (runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase))
        {
            RequireDotNetSdk("browser-wasm");
            RequireWorkload("wasm-tools", "dotnet workload install wasm-tools");
            return;
        }

        if (runtimeIdentifier is "android-arm64" or "android-x64")
        {
            RequireDotNetSdk("Android");
            RequireWorkload("android", "dotnet workload install android");
            ValidateAndroidSdk();
        }
    }

    private static void RequireDotNetSdk(string target)
    {
        var result = Run("dotnet", "--list-sdks");
        if (!result.Success || !result.Output.Split('\n').Any(line => line.TrimStart().StartsWith("10.", StringComparison.Ordinal)))
            throw new CompilerException($"{target} compilation requires the .NET 10 SDK. Install it manually and retry: https://dotnet.microsoft.com/download/dotnet/10.0", "", "configuration");
    }

    private static void RequireWorkload(string workload, string installCommand)
    {
        var result = Run("dotnet", "workload list");
        if (!result.Success || !result.Output.Split('\n').Any(line => line.Trim().StartsWith(workload + " ", StringComparison.OrdinalIgnoreCase) || line.Trim().Equals(workload, StringComparison.OrdinalIgnoreCase)))
            throw new CompilerException($"The '{workload}' .NET workload is required for this target. Install it manually with: {installCommand}", "", "configuration");
    }

    private static void ValidateAndroidSdk()
    {
        var sdkRoot = Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT") ?? Environment.GetEnvironmentVariable("ANDROID_HOME");
        if (string.IsNullOrWhiteSpace(sdkRoot) || !Directory.Exists(sdkRoot))
            throw new CompilerException("Android SDK was not found. Set ANDROID_SDK_ROOT or ANDROID_HOME and install Android SDK Platform 36 and Build Tools manually.", "", "configuration");

        var platformJar = Path.Combine(sdkRoot, "platforms", "android-36", "android.jar");
        if (!File.Exists(platformJar))
            throw new CompilerException($"Android SDK Platform 36 is required. Install it manually with: sdkmanager \"platforms;android-36\" (SDK root: {sdkRoot})", "", "configuration");

        var buildToolsRoot = Path.Combine(sdkRoot, "build-tools");
        var aapt2Name = OperatingSystem.IsWindows() ? "aapt2.exe" : "aapt2";
        var hasAapt2 = Directory.Exists(buildToolsRoot) && Directory.EnumerateFiles(buildToolsRoot, aapt2Name, SearchOption.AllDirectories).Any();
        if (!hasAapt2)
            throw new CompilerException($"Android SDK Build Tools with aapt2 are required. Install them manually with: sdkmanager \"build-tools;35.0.0\" (SDK root: {sdkRoot})", "", "configuration");

        RequireTool("java", "Install a JDK and ensure java is on PATH");
        RequireTool("adb", "Install Android SDK Platform Tools and ensure adb is on PATH");
    }

    private static void RequireTool(string executable, string instruction)
    {
        var result = Run(executable, "--version");
        if (!result.Success)
            throw new CompilerException($"Required Android tool '{executable}' was not found. {instruction}.", "", "configuration");
    }

    private static (bool Success, string Output) Run(string executable, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null) return (false, "");
            var output = process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode == 0, output);
        }
        catch
        {
            return (false, "");
        }
    }
}
