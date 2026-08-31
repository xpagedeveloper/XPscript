using System.Reflection;
using System.Runtime.CompilerServices;

namespace XPScript.Cli;

internal static class XPScriptCliStartupBanner
{
    [ModuleInitializer]
    internal static void Write()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var buildDate = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "XPScriptBuildDate", StringComparison.Ordinal))
            ?.Value ?? "unknown";

        if (Environment.GetCommandLineArgs().Any(argument => argument.Equals("--debug", StringComparison.OrdinalIgnoreCase)))
            Environment.SetEnvironmentVariable("XPSCRIPT_RUNTIME_DEBUG", "1");

        Console.WriteLine($"XPScript version 0.9 Beta - build {buildDate} - XPageDeveloper.com ©");
    }
}
