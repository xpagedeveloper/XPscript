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

        IReadOnlyList<ApplicationPackageReference> targets;
        if (action.Equals("all", StringComparison.OrdinalIgnoreCase))
            targets = ApplicationDependencyCatalog.Defaults;
        else
        {
            var match = ApplicationDependencyCatalog.Defaults.FirstOrDefault(p => p.Name.Equals(action, StringComparison.OrdinalIgnoreCase));
            if (match is null) throw new ArgumentException("Unknown XPScript package: " + action);
            targets = [match];
        }

        var existing = ApplicationPackagePatchStore.GetPatches().ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var next = new Dictionary<string, ApplicationPackagePatch>(existing, StringComparer.OrdinalIgnoreCase);
        var changes = 0;
        Console.WriteLine($"XPScript package patch line: {ApplicationPackagePatchStore.CurrentXPScriptVersion}");

        foreach (var target in targets)
        {
            var candidate = await FindLatestCompatiblePatchAsync(target.Name, target.Version, cancellationToken).ConfigureAwait(false);
            if (candidate is null || candidate.Equals(target.Version, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"{target.Name}: {target.Version} - no newer compatible patch.");
                continue;
            }

            if (existing.TryGetValue(target.Name, out var current) &&
                current.BaselineVersion.Equals(target.Version, StringComparison.OrdinalIgnoreCase) &&
                current.PatchedVersion.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"{target.Name}: {target.Version} -> {candidate} - already active.");
                continue;
            }

            Console.WriteLine($"{target.Name}: {target.Version} -> {candidate}{(checkOnly ? " - available" : "")}");
            changes++;
            if (checkOnly) continue;

            Directory.CreateDirectory(ApplicationPackagePatchStore.PackageDirectory);
            var packageFile = $"{target.Name.ToLowerInvariant()}.{candidate}.nupkg";
            var destination = Path.Combine(ApplicationPackagePatchStore.PackageDirectory, packageFile);
            await DownloadPackageAsync(target.Name, candidate, destination, cancellationToken).ConfigureAwait(false);
            var hash = ApplicationPackagePatchStore.ComputeSha256(destination);
            next[target.Name] = new(target.Name, target.Version, candidate, packageFile, hash);
        }

        if (checkOnly)
        {
            Console.WriteLine(changes == 0 ? "No compatible patch updates found." : $"{changes} compatible patch update(s) found. No changes made.");
            return 0;
        }

        ApplicationPackagePatchStore.SavePatches(next.Values);
        Console.WriteLine(changes == 0 ? "Package patch set unchanged." : $"Applied {changes} package patch update(s).");
        Console.WriteLine("Patches are scoped to this exact XPScript release and will not be reused by a different XPScript version.");
        return 0;
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

    private static async Task<string?> FindLatestCompatiblePatchAsync(string packageName, string baselineVersion, CancellationToken cancellationToken)
    {
        var lower = packageName.ToLowerInvariant();
        var url = $"https://api.nuget.org/v3-flatcontainer/{Uri.EscapeDataString(lower)}/index.json";
        using var response = await Http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!json.RootElement.TryGetProperty("versions", out var versions) || versions.ValueKind != JsonValueKind.Array)
            return null;

        if (!Version.TryParse(baselineVersion.Split('-', 2)[0], out var baseline)) return null;
        Version? best = null;
        string? bestText = null;
        foreach (var element in versions.EnumerateArray())
        {
            var text = element.GetString();
            if (string.IsNullOrWhiteSpace(text) || text.Contains('-', StringComparison.Ordinal)) continue;
            if (!Version.TryParse(text, out var version)) continue;
            if (version.Major != baseline.Major || version.Minor != baseline.Minor || version < baseline) continue;
            if (best is null || version > best)
            {
                best = version;
                bestText = text;
            }
        }
        return bestText;
    }

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
        Console.WriteLine("  xpscript patch all [--check]");
        Console.WriteLine("  xpscript patch <package> [--check]");
        Console.WriteLine("  xpscript patch status");
        Console.WriteLine("  xpscript patch remove all");
        Console.WriteLine();
        Console.WriteLine("Only newer stable patch versions in the same major.minor line are eligible.");
    }
}
