using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class ApplicationSecurityAudit
{
    private static readonly Regex NuGetAuditWarning = new(
        @"warning\s+(?<code>NU190[1-4]):\s*Package\s+'(?<package>[^']+)'\s+(?<version>[^\s]+)\s+has\s+a\s+known\s+(?<severity>low|moderate|high|critical)\s+severity\s+vulnerability,\s+(?<advisory>https?://\S+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void Report(string buildOutput)
    {
        var mode = ApplicationSecurityModeContext.Current;
        if (mode == ApplicationSecurityMode.Off || string.IsNullOrWhiteSpace(buildOutput)) return;

        var findings = Parse(buildOutput);
        if (findings.Count == 0) return;

        Console.Error.WriteLine("Application dependency security warnings:");
        foreach (var finding in findings)
        {
            Console.Error.WriteLine(
                $"  {finding.Severity.ToUpperInvariant()}: {finding.Package} {finding.Version} [{finding.Code}] {finding.Advisory}");
        }

        if (mode != ApplicationSecurityMode.Strict) return;
        var blocking = findings.Where(f => f.Severity is "high" or "critical").ToArray();
        if (blocking.Length == 0) return;

        throw new CompilerException(
            $"Application dependency security check failed: {blocking.Length} high or critical vulnerability/vulnerabilities detected in packages used by this application.");
    }

    internal static IReadOnlyList<Finding> Parse(string buildOutput)
    {
        var findings = new List<Finding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in NuGetAuditWarning.Matches(buildOutput))
        {
            var package = match.Groups["package"].Value.Trim();
            var version = match.Groups["version"].Value.Trim();
            var severity = match.Groups["severity"].Value.Trim().ToLowerInvariant();
            var advisory = match.Groups["advisory"].Value.Trim().TrimEnd('.', ',', ';', ')', ']');
            var code = match.Groups["code"].Value.Trim().ToUpperInvariant();
            var key = package + "\0" + version + "\0" + advisory;
            if (!seen.Add(key)) continue;
            findings.Add(new Finding(code, package, version, severity, advisory));
        }

        return findings
            .OrderByDescending(f => SeverityRank(f.Severity))
            .ThenBy(f => f.Package, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "critical" => 4,
        "high" => 3,
        "moderate" => 2,
        "low" => 1,
        _ => 0
    };

    internal sealed record Finding(string Code, string Package, string Version, string Severity, string Advisory);
}
