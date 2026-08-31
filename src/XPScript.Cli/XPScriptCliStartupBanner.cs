using System.Reflection;
using System.Runtime.CompilerServices;
using XPScript.Compiler;

namespace XPScript.Cli;

internal static class XPScriptCliStartupBanner
{
    [ModuleInitializer]
    internal static void Write()
    {
        var compilerAssembly = typeof(XPScriptCompilerCommandLine).Assembly;
        var buildDate = compilerAssembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "XPScriptBuildDate", StringComparison.Ordinal))
            ?.Value ?? "unknown";

        Console.WriteLine($"XPScript version 0.9 Beta - build {buildDate}");
        Console.WriteLine("XPageDeveloper.com (c)");
        Console.WriteLine();
    }
}
