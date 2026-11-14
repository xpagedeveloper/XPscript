using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

public static class DotNetVersionSelection
{
    private static readonly Regex SdkLine = new(@"^\s*(?<version>\d+\.\d+\.\d+)\s+\[(?<path>[^]]+)\]", RegexOptions.Compiled);
    private static readonly Regex RuntimeLine = new(@"^\s*Microsoft\.NETCore\.App\s+(?<version>\d+\.\d+\.\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string NormalizeTargetFramework(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "net" + SelectHighestSdk().Major + ".0";
        var text = value.Trim();
        if (text.StartsWith("net", StringComparison.OrdinalIgnoreCase)) text = text[3..];
        if (!int.TryParse(text.Split('.')[0], out var major) || major < 10)
            throw new CompilerException("--target-framework must be .NET 10 or higher, for example 10 or net11.0.", "", "configuration");
        return $"net{major}.0";
    }

    public static Version SelectSdk(string targetFramework)
    {
        var major = ParseMajor(targetFramework);
        var sdks = ListSdks().Where(version => version.Major == major).OrderByDescending(version => version).ToArray();
        if (sdks.Length == 0)
            throw new CompilerException($"No .NET SDK for {targetFramework} is installed. Install .NET {major} SDK or choose another target framework.", "", "configuration");
        return sdks[0];
    }

    public static void RequireRuntime(string targetFramework)
    {
        var major = ParseMajor(targetFramework);
        var installed = RunDotnet("--list-runtimes").Split('\n')
            .Select(line => RuntimeLine.Match(line))
            .Where(match => match.Success)
            .Select(match => Version.Parse(match.Groups["version"].Value))
            .Any(version => version.Major == major);
        if (!installed)
            throw new CompilerException($"Program requires .NET {major} runtime ({targetFramework}), but it is not installed. Install the .NET {major} Runtime and try again.", "", "configuration");
    }

    public static string CreateGlobalJson(string targetFramework)
    {
        var sdk = SelectSdk(targetFramework);
        return JsonSerializer.Serialize(new { sdk = new { version = sdk.ToString(), rollForward = "latestPatch", allowPrerelease = false } }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;
    }

    private static int ParseMajor(string targetFramework)
        => int.Parse(targetFramework.TrimStart('n', 'e', 't').Split('.')[0], System.Globalization.CultureInfo.InvariantCulture);

    private static Version[] ListSdks()
        => RunDotnet("--list-sdks").Split('\n').Select(line => SdkLine.Match(line)).Where(match => match.Success)
            .Select(match => Version.Parse(match.Groups["version"].Value)).ToArray();

    private static Version SelectHighestSdk()
    {
        var sdk = ListSdks().Where(version => version.Major >= 10).OrderByDescending(version => version).FirstOrDefault();
        if (sdk is null) throw new CompilerException(".NET SDK 10 or higher is required. Install a supported .NET SDK and try again.", "", "configuration");
        return sdk;
    }

    private static string RunDotnet(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo { FileName = "dotnet", Arguments = arguments, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true })
            ?? throw new CompilerException("Unable to start dotnet to inspect installed SDKs/runtimes.", "", "configuration");
        var output = process.StandardOutput.ReadToEnd() + Environment.NewLine + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
