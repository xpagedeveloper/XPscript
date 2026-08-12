namespace XPScript.Compiler;

internal static class CompilerToolResolver
{
    public static string ResolveDotnetHost()
    {
        var executableName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";

        foreach (var candidate in PreferredDotnetCandidates(executableName))
        {
            if (IsUsableAbsoluteFile(candidate))
                return Path.GetFullPath(candidate);
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var rawDirectory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = rawDirectory.Trim().Trim('"');
            if (directory.Length == 0 || !Path.IsPathRooted(directory))
                continue;

            string candidate;
            try
            {
                candidate = Path.Combine(Path.GetFullPath(directory), executableName);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (IsUsableAbsoluteFile(candidate))
                return Path.GetFullPath(candidate);
        }

        throw new CompilerException(
            "Unable to locate a trusted absolute dotnet host path. Install the .NET 10 SDK or configure DOTNET_ROOT to an absolute .NET installation directory.");
    }

    private static IEnumerable<string> PreferredDotnetCandidates(string executableName)
    {
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrWhiteSpace(dotnetRoot) && Path.IsPathRooted(dotnetRoot))
            yield return Path.Combine(dotnetRoot, executableName);

        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
                yield return Path.Combine(programFiles, "dotnet", executableName);
            yield break;
        }

        yield return "/usr/bin/dotnet";
        yield return "/usr/local/bin/dotnet";
        yield return "/usr/local/share/dotnet/dotnet";
        yield return "/opt/homebrew/bin/dotnet";
        yield return "/opt/homebrew/share/dotnet/dotnet";
    }

    private static bool IsUsableAbsoluteFile(string path)
    {
        try
        {
            if (!Path.IsPathRooted(path)) return false;
            var full = Path.GetFullPath(path);
            if (!File.Exists(full)) return false;

            var info = new FileInfo(full);
            if (info.LinkTarget is null && (info.Attributes & FileAttributes.ReparsePoint) == 0)
                return true;

            var target = info.ResolveLinkTarget(returnFinalTarget: true);
            return target is not null && File.Exists(target.FullName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
