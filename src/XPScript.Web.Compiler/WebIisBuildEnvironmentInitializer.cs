using System.Runtime.CompilerServices;

namespace XPScript.Web.Compiler;

internal static class WebIisBuildEnvironmentInitializer
{
    private const string CacheEnvironmentVariable = "XPSCRIPT_WEB_CACHE_DIRECTORY";

    [ModuleInitializer]
    internal static void Initialize()
    {
        var configuredCache = Environment.GetEnvironmentVariable(CacheEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredCache)) return;

        var applicationRoot = Directory.GetParent(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))?.FullName
            ?? Environment.CurrentDirectory;
        var cacheRoot = Path.IsPathRooted(configuredCache)
            ? Path.GetFullPath(configuredCache)
            : Path.GetFullPath(configuredCache, applicationRoot);

        Directory.CreateDirectory(cacheRoot);

        var dotnetHome = EnsureDirectory(Path.Combine(cacheRoot, "dotnet-home"));
        var nugetPackages = EnsureDirectory(Path.Combine(cacheRoot, "nuget-packages"));
        var profile = EnsureDirectory(Path.Combine(cacheRoot, "profile"));
        var appData = EnsureDirectory(Path.Combine(profile, "AppData", "Roaming"));
        var localAppData = EnsureDirectory(Path.Combine(profile, "AppData", "Local"));
        var nugetHttpCache = EnsureDirectory(Path.Combine(cacheRoot, "nuget-http-cache"));
        var nugetPluginsCache = EnsureDirectory(Path.Combine(cacheRoot, "nuget-plugins-cache"));
        _ = EnsureDirectory(Path.Combine(appData, "NuGet"));

        Environment.SetEnvironmentVariable(CacheEnvironmentVariable, cacheRoot);
        Environment.SetEnvironmentVariable("DOTNET_CLI_HOME", dotnetHome);
        Environment.SetEnvironmentVariable("NUGET_PACKAGES", nugetPackages);
        Environment.SetEnvironmentVariable("NUGET_HTTP_CACHE_PATH", nugetHttpCache);
        Environment.SetEnvironmentVariable("NUGET_PLUGINS_CACHE_PATH", nugetPluginsCache);
        Environment.SetEnvironmentVariable("USERPROFILE", profile);
        Environment.SetEnvironmentVariable("HOME", profile);
        Environment.SetEnvironmentVariable("APPDATA", appData);
        Environment.SetEnvironmentVariable("LOCALAPPDATA", localAppData);
        Environment.SetEnvironmentVariable("DOTNET_SKIP_FIRST_TIME_EXPERIENCE", "1");
        Environment.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "1");
        Environment.SetEnvironmentVariable("DOTNET_NOLOGO", "1");
    }

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return Path.GetFullPath(path);
    }
}
