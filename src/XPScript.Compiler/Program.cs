using System.Reflection;
using XPScript.Compiler;

var buildDate = Assembly.GetExecutingAssembly()
    .GetCustomAttributes<AssemblyMetadataAttribute>()
    .FirstOrDefault(attribute => string.Equals(attribute.Key, "XPScriptBuildDate", StringComparison.Ordinal))
    ?.Value ?? "unknown";

ConfigureRuntimeDiagnosticEnvironment(args);

if (ShouldWriteBanner(args))
    Console.WriteLine($"XPScript version 0.9.1 Beta - build {buildDate} - XPageDeveloper.com (c)");

return await XPScriptCompilerCommandLine.RunAsync(NormalizeArguments(args));

static void ConfigureRuntimeDiagnosticEnvironment(string[] arguments)
{
    if (arguments.Length == 0 || !arguments[0].Equals("run", StringComparison.OrdinalIgnoreCase))
        return;

    var separator = Array.IndexOf(arguments, "--");
    var optionCount = separator < 0 ? arguments.Length : separator;
    var explicitInfo = arguments.Take(optionCount).Any(value => value.Equals("--info", StringComparison.OrdinalIgnoreCase));
    Environment.SetEnvironmentVariable("XPSCRIPT_RUNTIME_INFO", explicitInfo ? "1" : null);
}

static bool ShouldWriteBanner(string[] arguments)
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

static string[] NormalizeArguments(string[] arguments)
{
    if (arguments.Length == 0 ||
        !arguments[0].Equals("run", StringComparison.OrdinalIgnoreCase) ||
        !arguments.Contains("--debug", StringComparer.OrdinalIgnoreCase) ||
        arguments.Contains("--info", StringComparer.OrdinalIgnoreCase))
        return arguments;

    var result = arguments.ToList();
    var separator = result.FindIndex(value => value == "--");
    if (separator < 0) result.Add("--info");
    else result.Insert(separator, "--info");
    return result.ToArray();
}
