using System.Reflection;
using System.Runtime.CompilerServices;

namespace XPScript.Cli;

internal static class XPScriptCliStartupBanner
{
    [ModuleInitializer]
    internal static void Write()
    {
        var arguments = Environment.GetCommandLineArgs();
        var commandArguments = arguments.Skip(1).ToArray();
        var assembly = Assembly.GetExecutingAssembly();
        var buildDate = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "XPScriptBuildDate", StringComparison.Ordinal))
            ?.Value ?? "unknown";

        if (commandArguments.Any(argument => argument.Equals("--debug", StringComparison.OrdinalIgnoreCase)))
            Environment.SetEnvironmentVariable("XPSCRIPT_RUNTIME_DEBUG", "1");

        if (ShouldWriteBanner(commandArguments))
            Console.WriteLine($"XPScript version 0.9 Beta - build {buildDate} - XPageDeveloper (c) 2026");

        if (commandArguments.Length == 1 && commandArguments[0] is "--version" or "--info" or "--debug")
            Environment.Exit(0);
    }

    private static bool ShouldWriteBanner(string[] arguments)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            if (!arguments[i].Equals("--result-format", StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 >= arguments.Length) return true;
            return !arguments[i + 1].Equals("json", StringComparison.OrdinalIgnoreCase) &&
                   !arguments[i + 1].Equals("xml", StringComparison.OrdinalIgnoreCase);
        }
        return true;
    }
}
