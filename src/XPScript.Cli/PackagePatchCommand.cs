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

        var existing = ApplicationPackagePatchStore.GetPatches().ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        var next = new Dictionary<string, ApplicationPackagePatch>(existing, StringComparer.OrdinalIgnoreCase);
        var changes = 0;
        Console.WriteLine($"XPScript package patch line: {ApplicationPackagePatchStore.CurrentXPScriptVersion}");

        if (action.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var groupedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in ApplicationDependencyCatalog.PatchGroups)
            {
                foreach (var name in group.Packages) groupedNames.Add(name);
                changes += await PatchGroupAsync(group, existing, next, checkOnly, cancellationToken).ConfigureAwait(false);
            }

            foreach (var target in ApplicationDependencyCatalog.Defaults.Where(package => !groupedNames.Contains(package.Name)))
                changes += await PatchPackageAsync(target, existing, next, checkOnly, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var match = ApplicationDependencyCatalog.Defaults.FirstOrDefault(p => p.Name.Equals(action, StringComparison.OrdinalIgnoreCase));
            if (match is null) throw new ArgumentException("Unknown XPScript package: " + action);

            var group = ApplicationDependencyCatalog.FindPatchGroup(match.Name);
            if (group is not null)
                changes += await PatchGroupAsync(group, existing, next, checkOnly, cancellationToken).ConfigureAwait(false);
            else
                changes += await PatchPackageAsync(match, existing, next, checkOnly, cancellationToken).ConfigureAwait(false);
        }

        if (checkOnly)
        {
            Console.WriteLine(changes == 0 ? "No compatible patch updates found." : $"{changes} compatible package patch update(s) found. No changes made.");
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
        CancellationToken cancellationToken)
    {
        var packages = ApplicationDependencyCatalog.GetPatchGroupPackages(group);
        var available = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in packages)
            available[package.Name] = await FindAvailableVersionsAsync(package.Name, cancellationToken).ConfigureAwait(false);

        var candidate = ApplicationPackagePatchStore.SelectLatestCommonCompatiblePatch(packages, available);
        if (candidate is null)
        {
            Console.WriteLine($"{group.Name} group: no common compatible patch version found; group left unchanged.");
            return 0;
        }

        var pending = packages.Where(package =>
            !candidate.Equals(package.Version, StringComparison.OrdinalIgnoreCase) &&
            (!existing.TryGetValue(package.Name, out var current) ||
             !current.BaselineVersion.Equals(package.Version, StringComparison.OrdinalIgnoreCase) ||
             !current.PatchedVersion.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (pending.Length == 0)
        {
            Console.WriteLine($"{group.Name} group: {candidate} - already active or baseline-current.");
            return 0;
        }

        Console.WriteLine($"{group.Name} group: common patch {candidate}{(checkOnly ? " - available" : "")}");
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
        CancellationToken cancellationToken)
    {
        var versions = await FindAvailableVersionsAsync(target.Name, cancellationToken).ConfigureAwait(false);
        var candidate = ApplicationPackagePatchStore.SelectLatestCompatiblePatch(target.Version, versions);
        if (candidate is null || candidate.Equals(target.Version, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{target.Name}: {target.Version} - no newer compatible patch.");
            return 0;
        }

        if (existing.TryGetValue(target.Name, out var current) &&
            current.BaselineVersion.Equals(target.Version, StringComparison.OrdinalIgnoreCase) &&
            current.PatchedVersion.Equals(candidate, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{target.Name}: {target.Version} -> {candidate} - already active.");
            return 0;
        }

        Console.WriteLine($"{target.Name}: {target.Version} -> {candidate}{(checkOnly ? " - available" : "")}");
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
        Console.WriteLine("Packages in a compatibility group, such as Avalonia, are patched atomically to one common version.");
    }
}
