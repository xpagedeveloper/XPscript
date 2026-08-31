using System.Reflection;
using XPScript.Compiler;

var assembly = Assembly.GetExecutingAssembly();
var buildDate = assembly
    .GetCustomAttributes<AssemblyMetadataAttribute>()
    .FirstOrDefault(attribute => attribute.Key == "XPScriptBuildDate")?.Value
    ?? "unknown";

Console.WriteLine($"XPScript version 0.9 Beta - build {buildDate}");
Console.WriteLine("XPageDeveloper.com (c)");
Console.WriteLine();

return await XPScriptCompilerCommandLine.RunAsync(args);
