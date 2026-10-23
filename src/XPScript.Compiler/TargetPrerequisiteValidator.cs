using System.Diagnostics;

namespace XPScript.Compiler;

internal static class TargetPrerequisiteValidator
{
    private static readonly HashSet<string> VerifiedWorkloads = new(StringComparer.OrdinalIgnoreCase);

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
        if (VerifiedWorkloads.Contains(workload)) return;
        Console.Error.WriteLine($"Checking .NET workload '{workload}'...");
        var result = Run("dotnet", "workload list");
        if (result.Success && result.Output.Split('\n').Any(line => line.Trim().StartsWith(workload + " ", StringComparison.OrdinalIgnoreCase) || line.Trim().Equals(workload, StringComparison.OrdinalIgnoreCase)))
        {
            VerifiedWorkloads.Add(workload);
            Console.Error.WriteLine($".NET workload '{workload}' is installed.");
            return;
        }

        // workload list can fail on restricted Windows machines because the
        // SDK tries to query system services. Verify the installed SDK layout
        // directly before reporting the workload as missing.
        if (IsWorkloadPresentInSdk(workload))
        {
            VerifiedWorkloads.Add(workload);
            Console.Error.WriteLine($".NET workload '{workload}' is installed (verified from the SDK layout).");
            return;
        }

        if (!result.Success)
            throw new CompilerException($"Unable to verify the '{workload}' .NET workload. Run 'dotnet workload list' manually and install it if missing: {installCommand}", "", "configuration");

        if (!result.Success || !IsWorkloadPresentInSdk(workload))
            throw new CompilerException($"The '{workload}' .NET workload is required for this target. Install it manually with: {installCommand}", "", "configuration");
    }

    private static bool IsWorkloadPresentInSdk(string workload)
    {
        var dotnetRoot = ResolveDotNetRoot();
        if (dotnetRoot is null || !Directory.Exists(dotnetRoot)) return false;

        var patterns = workload.Equals("android", StringComparison.OrdinalIgnoreCase)
            ? new[] { "Microsoft.Android*", "Microsoft.NET.Sdk.Android*" }
            : new[] { "Microsoft.NETCore.App.Runtime.Mono.browser-wasm*", "Microsoft.NET.Runtime.MonoTargets.Sdk*", "Microsoft.NET.Sdk.BlazorWebAssembly*" };

        foreach (var root in new[] { Path.Combine(dotnetRoot, "packs"), Path.Combine(dotnetRoot, "sdk-manifests") })
        {
            if (!Directory.Exists(root)) continue;
            foreach (var pattern in patterns)
                if (Directory.EnumerateDirectories(root, pattern, SearchOption.AllDirectories).Any())
                    return true;
        }

        return false;
    }

    private static string? ResolveDotNetRoot()
    {
        foreach (var variable in new[] { "DOTNET_ROOT", "DOTNET_ROOT(x86)" })
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value)) return value;
        }

        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var root = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(Path.Combine(root, "packs"))) return root;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var standardRoot = Path.Combine(programFiles, "dotnet");
        return Directory.Exists(standardRoot) ? standardRoot : null;
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
