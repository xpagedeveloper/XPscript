using System.Text.Json;
using XPScript.Compiler;

namespace XPScript.Cli;

internal static class ApplicationDependencyCommand
{
    public static Task<int> RunDependenciesAsync(string[] args) => RunAsync(args, securityOnly: false);
    public static Task<int> RunSecurityAsync(string[] args) => RunAsync(args, securityOnly: true);

    private static async Task<int> RunAsync(string[] args, bool securityOnly)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            WriteHelp(securityOnly);
            return 0;
        }

        var sourcePath = Path.GetFullPath(args[0]);
        string? runtimeIdentifier = null;
        var json = false;
        for (var i = 1; i < args.Length; i++)
        {
            if ((args[i] is "--runtime" or "--rid" or "--platform") && i + 1 < args.Length)
                runtimeIdentifier = args[++i];
            else if (args[i] == "--json")
                json = true;
            else
                throw new ArgumentException("Unknown argument: " + args[i]);
        }

        var inspection = await ApplicationDependencyInspector.InspectAsync(
            sourcePath,
            runtimeIdentifier,
            vulnerabilitiesOnly: securityOnly).ConfigureAwait(false);

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(inspection, new JsonSerializerOptions { WriteIndented = true }));
            return securityOnly && inspection.VulnerableDependencies.Count > 0 ? 2 : 0;
        }

        Console.WriteLine($"Application: {inspection.SourceFile}");
        Console.WriteLine($"Runtime: {inspection.RuntimeIdentifier}");

        if (securityOnly)
        {
            if (inspection.VulnerableDependencies.Count == 0)
            {
                Console.WriteLine("Security status: OK");
                Console.WriteLine("Known vulnerable dependencies: 0");
                return 0;
            }

            Console.WriteLine("Security status: VULNERABILITIES FOUND");
            foreach (var dependency in inspection.VulnerableDependencies)
            {
                Console.WriteLine($"  {dependency.Name} {dependency.Version}{(dependency.Direct ? " [direct]" : " [transitive]")}");
                foreach (var vulnerability in dependency.Vulnerabilities)
                    Console.WriteLine($"    {vulnerability.Severity.ToUpperInvariant()}: {vulnerability.AdvisoryUrl}");
            }
            return 2;
        }

        if (inspection.Dependencies.Count == 0)
        {
            Console.WriteLine("Application NuGet dependencies: none");
            return 0;
        }

        Console.WriteLine("Application dependencies:");
        foreach (var dependency in inspection.Dependencies)
        {
            var kind = dependency.Direct ? "direct" : "transitive";
            var reason = string.IsNullOrWhiteSpace(dependency.Reason) ? "" : " - " + dependency.Reason;
            Console.WriteLine($"  {dependency.Name} {dependency.Version} [{kind}]{reason}");
        }
        return 0;
    }

    private static void WriteHelp(bool securityOnly)
    {
        if (securityOnly)
        {
            Console.WriteLine("""
Usage:
  xpscript security <source.xps> [--runtime RID] [--json]

Checks known vulnerabilities only in the NuGet dependency graph resolved for this application.
Returns exit code 2 when one or more vulnerable dependencies are found.
""");
            return;
        }

        Console.WriteLine("""
Usage:
  xpscript dependencies <source.xps> [--runtime RID] [--json]

Lists direct and transitive NuGet dependencies resolved for this application.
Unused XPScript capabilities are not included.
""");
    }
}
