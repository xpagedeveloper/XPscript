using System.Reflection;
using XPScript.Compiler;

var buildDate = Assembly.GetExecutingAssembly()
    .GetCustomAttributes<AssemblyMetadataAttribute>()
    .FirstOrDefault(attribute => string.Equals(attribute.Key, "XPScriptBuildDate", StringComparison.Ordinal))
    ?.Value ?? "unknown";

Console.WriteLine($"XPScript version 0.9 Beta - build {buildDate} XPageDeveloper.com (c)");

return await XPScriptCompilerCommandLine.RunAsync(NormalizeArguments(args));

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
