using System.Net.Http.Headers;
using System.Text.Json;
using XPScript.Compiler;

namespace XPScript.Cli;

internal static class PackagePatchCommand
{
    private static readonly HttpClient Http = CreateHttpClient();

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            WriteHelp();
            return 0;
        }

        var action = args[0];
        var checkOnly = args.Skip(1).Any(a => a.Equals("--check", StringComparison.OrdinalIgnoreCase));
        var securityOnly = args.Skip(1).Any(a => a.Equals("--security-only", StringComparison.OrdinalIgnoreCase));
        if (action.Equals("status", StringComparison.OrdinalIgnoreCase) || action.Equals("list", StringComparison.OrdinalIgnoreCase))
            return WriteStatus();
        if (action.Equals("remove", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2 || !args[1].Equals("all", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Usage: xpscript patch remove all");
            ApplicationPackagePatchStore.Clear();
            Console.WriteLine($"Removed package patches for XPScript {ApplicationPackagePatchStore.CurrentXPScriptVersion}.");
            return 0;
        }

        var existing = ApplicationPackagePatchStore.GetPatches().ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var next = new Dictionary<string, ApplicationPackagePatch>(existing, StringComparer.OrdinalIgnoreCase);
        var changes = 0;
        Console.WriteLine($"XPScript package patch line: {ApplicationPackagePatchStore.CurrentXPScriptVersion}");
        Console.WriteLine(securityOnly ? "Patch policy: security fixes only." : "Patch policy: compatible security and maintenance patches.");

        if (action.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var groupedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in ApplicationDependencyCatalog.PatchGroups)
            {
                foreach (var name in group.Packages) groupedNames.Add(name);
                changes += await PatchGroupAsync(group, existing, next, checkOnly, securityOnly, cancellationToken).ConfigureAwait(false);
            }

            foreach (var target in ApplicationDependencyCatalog.Defaults.Where(package => !groupedNames.Contains(package.Name)))
                changes += await PatchPackageAsync(target, existing, next, checkOnly, securityOnly, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var match = ApplicationDependencyCatalog.Defaults.FirstOrDefault(p => p.Name.Equals(action, StringComparison.OrdinalIgnoreCase));
            if (match is null) throw new ArgumentException("Unknown XPScript package: " + action);

            var group = ApplicationDependencyCatalog.FindPatchGroup(match.Name);
            if (group is not null)
                changes += await PatchGroupAsync(group, existing, next, checkOnly, securityOnly, cancellationToken).ConfigureAwait(false);
            else
                changes += await PatchPackageAsync(match, existing, next, checkOnly, securityOnly, cancellationToken).ConfigureAwait(false);
        }

        if (checkOnly)
        {
            Console.WriteLine(changes == 0 ? "No eligible patch updates found." : $"{changes} eligible package patch update(s) found. No changes made.");
            return 0;
        }

        ApplicationPackagePatchStore.SavePatches(next.Values);
        Console.WriteLine(changes == 0 ? "Package patch set unchanged." : $"Applied {changes} package patch update(s).");
        Console.WriteLine("Patches are scoped to this exact XPScript release and will not be reused by a different XPScript version.");
        return 0;
    }

    private static async Task<int> PatchGroupAsync(
        ApplicationPackagePatchGroup group,
        IReadOnlyDictionary<string, ApplicationPackagePatch> existing,
        IDictionary<string, ApplicationPackagePatch> next,
        bool checkOnly,
        bool securityOnly,
        CancellationToken cancellationToken)
    {
        var packages = ApplicationDependencyCatalog.GetPatchGroupPackages(group);
        var available = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in packages)
            available[package.Name] = await FindAvailableVersionsAsync(package.Name, cancellationToken).ConfigureAwait(false);

        var candidate = ApplicationPackagePatchStore.SelectLatestCommonCompatiblePatch(packages, available);
        if (candidate is null)
        {
            var baselineSecurity = await GetAggregateSecurityAsync(packages.Select(p => (p.Name, p.Version)), cancellationToken).ConfigureAwait(false);
            var kind = ApplicationPackagePatchSecurity.Classify(baselineSecurity, baselineSecurity, hasNewerCandidate: false);
            Console.WriteLine($"{group.Name} group: no common compatible patch version found - {ApplicationPackagePatchSecurity.ToDisplayText(kind)}; group left unchanged.");
            return 0;
        }

        var baselineGroupSecurity = await GetAggregateSecurityAsync(packages.Select(p => (p.Name, p.Version)), cancellationToken).ConfigureAwait(false);
        var candidateGroupSecurity = await GetAggregateSecurityAsync(packages.Select(p => (p.Name, candidate)), cancellationToken).ConfigureAwait(false);
        var hasNewerCandidate = packages.Any(package => !candidate.Equals(package.Version, StringComparison.OrdinalIgnoreCase));
        var securityKind = ApplicationPackagePatchSecurity.Classify(baselineGroupSecurity, candidateGroupSecurity, hasNewerCandidate);
        var securityText = ApplicationPackagePatchSecurity.ToDisplayText(securityKind);

        var pending = packages.Where(package =>
            !candidate.Equals(package.Version, StringComparison.OrdinalIgnoreCase) &&
            (!existing.TryGetValue(package.Name, out var current) ||
             !current.BaselineVersion.Equals(package.Version, StringComparison.OrdinalIgnoreCase) ||
             !current.PatchedVersion.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (pending.Length == 0)
        {
            Console.WriteLine($"{group.Name} group: {candidate} - already active or baseline-current; {securityText}.");
            return 0;
        }

        if (securityOnly && securityKind != ApplicationPackagePatchSecurityKind.SecurityPatch)
        {
            Console.WriteLine($"{group.Name} group: {candidate} - {securityText}; skipped by --security-only.");
            return 0;
        }

        Console.WriteLine($"{group.Name} group: common patch {candidate} - {securityText}{(checkOnly ? " - available" : "")}");
        foreach (var package in packages)
            Console.WriteLine($"  {package.Name}: {package.Version} -> {candidate}");

        if (checkOnly) return pending.Length;

        Directory.CreateDirectory(ApplicationPackagePatchStore.PackageDirectory);
        var staged = new List<ApplicationPackagePatch>();
        foreach (var package in packages)
        {
            if (candidate.Equals(package.Version, StringComparison.OrdinalIgnoreCase))
            {
                next.Remove(package.Name);
                continue;
            }

            var packageFile = $"{package.Name.ToLowerInvariant()}.{candidate}.nupkg";
            var destination = Path.Combine(ApplicationPackagePatchStore.PackageDirectory, packageFile);
            await DownloadPackageAsync(package.Name, candidate, destination, cancellationToken).ConfigureAwait(false);
            var hash = ApplicationPackagePatchStore.ComputeSha256(destination);
            staged.Add(new(package.Name, package.Version, candidate, packageFile, hash));
        }
        foreach (var patch in staged) next[patch.Name] = patch;
        return pending.Length;
    }

    private static async Task<int> PatchPackageAsync(
        ApplicationPackageReference target,
        IReadOnlyDictionary<string, ApplicationPackagePatch> existing,
        IDictionary<string, ApplicationPackagePatch> next,
        bool checkOnly,
        bool securityOnly,
        CancellationToken cancellationToken)
    {
        var versions = await FindAvailableVersionsAsync(target.Name, cancellationToken).ConfigureAwait(false);
        var candidate = ApplicationPackagePatchStore.SelectLatestCompatiblePatch(target.Version, versions);
        var hasNewerCandidate = candidate is not null && !candidate.Equals(target.Version, StringComparison.OrdinalIgnoreCase);
        var baselineSecurity = await GetVersionSecurityAsync(target.Name, target.Version, cancellationToken).ConfigureAwait(false);
        var candidateSecurity = candidate is null
            ? baselineSecurity
            : await GetVersionSecurityAsync(target.Name, candidate, cancellationToken).ConfigureAwait(false);
        var securityKind = ApplicationPackagePatchSecurity.Classify(baselineSecurity, candidateSecurity, hasNewerCandidate);
        var securityText = ApplicationPackagePatchSecurity.ToDisplayText(securityKind);

        if (!hasNewerCandidate || candidate is null)
        {
            Console.WriteLine($"{target.Name}: {target.Version} - no newer compatible patch; {securityText}.");
            return 0;
        }

        if (existing.TryGetValue(target.Name, out var current) &&
            current.BaselineVersion.Equals(target.Version, StringComparison.OrdinalIgnoreCase) &&
            current.PatchedVersion.Equals(candidate, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{target.Name}: {target.Version} -> {candidate} - already active; {securityText}.");
            return 0;
        }

        if (securityOnly && securityKind != ApplicationPackagePatchSecurityKind.SecurityPatch)
        {
            Console.WriteLine($"{target.Name}: {target.Version} -> {candidate} - {securityText}; skipped by --security-only.");
            return 0;
        }

        Console.WriteLine($"{target.Name}: {target.Version} -> {candidate} - {securityText}{(checkOnly ? " - available" : "")}");
        if (checkOnly) return 1;

        Directory.CreateDirectory(ApplicationPackagePatchStore.PackageDirectory);
        var packageFile = $"{target.Name.ToLowerInvariant()}.{candidate}.nupkg";
        var destination = Path.Combine(ApplicationPackagePatchStore.PackageDirectory, packageFile);
        await DownloadPackageAsync(target.Name, candidate, destination, cancellationToken).ConfigureAwait(false);
        var hash = ApplicationPackagePatchStore.ComputeSha256(destination);
        next[target.Name] = new(target.Name, target.Version, candidate, packageFile, hash);
        return 1;
    }

    private static int WriteStatus()
    {
        var patches = ApplicationPackagePatchStore.GetPatches();
        Console.WriteLine($"XPScript version: {ApplicationPackagePatchStore.CurrentXPScriptVersion}");
        if (patches.Count == 0)
        {
            Console.WriteLine("No active package patches.");
            return 0;
        }
        foreach (var patch in patches)
            Console.WriteLine($"{patch.Name}: {patch.BaselineVersion} -> {patch.PatchedVersion}  sha256:{patch.Sha256[..Math.Min(12, patch.Sha256.Length)]}");
        Console.WriteLine("Package source: " + ApplicationPackagePatchStore.PackageDirectory);
        return 0;
    }

    private static async Task<IReadOnlyCollection<string>> FindAvailableVersionsAsync(string packageName, CancellationToken cancellationToken)
    {
        var lower = packageName.ToLowerInvariant();
        var url = $"https://api.nuget.org/v3-flatcontainer/{Uri.EscapeDataString(lower)}/index.json";
        using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!json.RootElement.TryGetProperty("versions", out var versions) || versions.ValueKind != JsonValueKind.Array)
            return [];

        return versions.EnumerateArray()
            .Select(element => element.GetString())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!)
            .ToArray();
    }

    private static async Task<ApplicationPackageVersionSecurity> GetAggregateSecurityAsync(
        IEnumerable<(string Name, string Version)> packages,
        CancellationToken cancellationToken)
    {
        var available = true;
        var count = 0;
        var highestSeverity = -1;
        foreach (var package in packages)
        {
            var security = await GetVersionSecurityAsync(package.Name, package.Version, cancellationToken).ConfigureAwait(false);
            available &= security.Available;
            count += security.VulnerabilityCount;
            highestSeverity = Math.Max(highestSeverity, SeverityRank(security.HighestSeverity));
        }
        return new ApplicationPackageVersionSecurity(available, count, SeverityText(highestSeverity));
    }

    private static async Task<ApplicationPackageVersionSecurity> GetVersionSecurityAsync(
        string packageName,
        string version,
        CancellationToken cancellationToken)
    {
        try
        {
            var lower = packageName.ToLowerInvariant();
            var url = $"https://api.nuget.org/v3/registration5-gz-semver2/{Uri.EscapeDataString(lower)}/index.json";
            using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var index = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!index.RootElement.TryGetProperty("items", out var pages) || pages.ValueKind != JsonValueKind.Array)
                return new(false, 0);

            foreach (var page in pages.EnumerateArray())
            {
                if (page.TryGetProperty("items", out var inlineItems) && inlineItems.ValueKind == JsonValueKind.Array)
                {
                    if (TryReadVersionSecurity(inlineItems, version, out var security)) return security;
                    continue;
                }

                if (!page.TryGetProperty("@id", out var idProperty)) continue;
                var pageUrl = idProperty.GetString();
                if (string.IsNullOrWhiteSpace(pageUrl)) continue;
                using var pageResponse = await Http.GetAsync(pageUrl, cancellationToken).ConfigureAwait(false);
                pageResponse.EnsureSuccessStatusCode();
                await using var pageStream = await pageResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var pageJson = await JsonDocument.ParseAsync(pageStream, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (pageJson.RootElement.TryGetProperty("items", out var pageItems) &&
                    pageItems.ValueKind == JsonValueKind.Array &&
                    TryReadVersionSecurity(pageItems, version, out var pageSecurity))
                    return pageSecurity;
            }

            return new(false, 0);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return new(false, 0);
        }
    }

    private static bool TryReadVersionSecurity(
        JsonElement items,
        string version,
        out ApplicationPackageVersionSecurity security)
    {
        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("catalogEntry", out var catalogEntry)) continue;
            if (!catalogEntry.TryGetProperty("version", out var versionProperty)) continue;
            if (!string.Equals(versionProperty.GetString(), version, StringComparison.OrdinalIgnoreCase)) continue;

            var count = 0;
            var highestSeverity = -1;
            if (catalogEntry.TryGetProperty("vulnerabilities", out var vulnerabilities) && vulnerabilities.ValueKind == JsonValueKind.Array)
            {
                foreach (var vulnerability in vulnerabilities.EnumerateArray())
                {
                    count++;
                    if (vulnerability.TryGetProperty("severity", out var severity))
                        highestSeverity = Math.Max(highestSeverity, ReadSeverityRank(severity));
                }
            }

            security = new(true, count, SeverityText(highestSeverity));
            return true;
        }

        security = new(false, 0);
        return false;
    }

    private static int ReadSeverityRank(JsonElement severity)
    {
        if (severity.ValueKind == JsonValueKind.Number && severity.TryGetInt32(out var number)) return number;
        if (severity.ValueKind != JsonValueKind.String) return -1;
        return SeverityRank(severity.GetString());
    }

    private static int SeverityRank(string? severity) => severity?.ToLowerInvariant() switch
    {
        "low" => 0,
        "moderate" or "medium" => 1,
        "high" => 2,
        "critical" => 3,
        _ => -1
    };

    private static string? SeverityText(int severity) => severity switch
    {
        0 => "low",
        1 => "moderate",
        2 => "high",
        3 => "critical",
        _ => null
    };

    private static async Task DownloadPackageAsync(string packageName, string version, string destination, CancellationToken cancellationToken)
    {
        var lower = packageName.ToLowerInvariant();
        var normalizedVersion = version.ToLowerInvariant();
        var url = $"https://api.nuget.org/v3-flatcontainer/{Uri.EscapeDataString(lower)}/{Uri.EscapeDataString(normalizedVersion)}/{Uri.EscapeDataString(lower)}.{Uri.EscapeDataString(normalizedVersion)}.nupkg";
        var temp = destination + ".download";
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var target = File.Create(temp))
            await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        File.Move(temp, destination, true);
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("XPScript", "package-patch"));
        return client;
    }

    private static void WriteHelp()
    {
        Console.WriteLine("XPScript package patch commands:");
        Console.WriteLine("  xpscript patch all [--check] [--security-only]");
        Console.WriteLine("  xpscript patch <package> [--check] [--security-only]");
        Console.WriteLine("  xpscript patch status");
        Console.WriteLine("  xpscript patch remove all");
        Console.WriteLine();
        Console.WriteLine("Only newer stable patch versions in the same major.minor line are eligible.");
        Console.WriteLine("Packages in a compatibility group, such as Avalonia, are patched atomically to one common version.");
        Console.WriteLine("NuGet package metadata is used to label updates as security or maintenance patches.");
        Console.WriteLine("--security-only applies only candidates that move a vulnerable baseline to a non-vulnerable compatible patch.");
    }
}
