using System.Security.Cryptography;
using System.Text.Json;

namespace XPScript.Compiler;

public sealed record ApplicationPackagePatch(
    string Name,
    string BaselineVersion,
    string PatchedVersion,
    string PackageFile,
    string Sha256);

public static class ApplicationPackagePatchStore
{
    private const string ManifestFileName = "manifest.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string CurrentXPScriptVersion => ApplicationDependencyCatalog.XPScriptReleaseVersion;
    public static string RootDirectory => RootDirectoryForVersion(CurrentXPScriptVersion);
    public static string PackageDirectory => Path.Combine(RootDirectory, "packages");
    public static string ManifestPath => Path.Combine(RootDirectory, ManifestFileName);
    public static string? ActivePackageSource => GetPatches().Count == 0 ? null : PackageDirectory;

    public static string RootDirectoryForVersion(string xpscriptVersion) =>
        Path.Combine(GetUserDataRoot(), "XPScript", "package-patches", Sanitize(xpscriptVersion));

    public static IReadOnlyList<ApplicationPackagePatch> GetPatches()
    {
        if (!File.Exists(ManifestPath)) return [];
        try
        {
            var manifest = JsonSerializer.Deserialize<PatchManifest>(File.ReadAllText(ManifestPath), JsonOptions);
            if (manifest is null || !string.Equals(manifest.XPScriptVersion, CurrentXPScriptVersion, StringComparison.Ordinal))
                return [];
            return manifest.Patches ?? [];
        }
        catch (JsonException ex)
        {
            throw new CompilerException("XPScript package patch manifest is invalid: " + ex.Message);
        }
    }

    public static string ResolveVersion(string packageName, string baselineVersion)
    {
        var patch = GetPatches().FirstOrDefault(p =>
            p.Name.Equals(packageName, StringComparison.OrdinalIgnoreCase) &&
            p.BaselineVersion.Equals(baselineVersion, StringComparison.OrdinalIgnoreCase));
        if (patch is null) return baselineVersion;
        if (!IsCompatiblePatch(baselineVersion, patch.PatchedVersion))
            throw new CompilerException($"Package patch {packageName} {patch.PatchedVersion} is outside the allowed {MajorMinor(baselineVersion)} patch line.");

        var packagePath = Path.Combine(PackageDirectory, patch.PackageFile);
        if (!File.Exists(packagePath))
            throw new CompilerException("Patched package file is missing: " + patch.PackageFile);
        var actualHash = ComputeSha256(packagePath);
        if (!actualHash.Equals(patch.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new CompilerException("Patched package checksum does not match the XPScript patch manifest: " + patch.PackageFile);
        return patch.PatchedVersion;
    }

    public static void SavePatches(IEnumerable<ApplicationPackagePatch> patches)
    {
        var normalized = patches
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Last())
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Directory.CreateDirectory(PackageDirectory);
        var manifest = new PatchManifest(CurrentXPScriptVersion, DateTimeOffset.UtcNow, normalized);
        var temp = ManifestPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(manifest, JsonOptions));
        File.Move(temp, ManifestPath, true);
    }

    public static void Clear()
    {
        if (Directory.Exists(RootDirectory)) Directory.Delete(RootDirectory, recursive: true);
    }

    public static bool IsCompatiblePatch(string baselineVersion, string candidateVersion)
    {
        if (!TryParseStableVersion(baselineVersion, out var baseline) || !TryParseStableVersion(candidateVersion, out var candidate)) return false;
        return baseline.Major == candidate.Major && baseline.Minor == candidate.Minor && candidate >= baseline;
    }

    public static string? SelectLatestCompatiblePatch(string baselineVersion, IEnumerable<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates
            .Where(candidate => IsCompatiblePatch(baselineVersion, candidate))
            .Select(candidate => (Text: candidate, Version: Version.Parse(candidate)))
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => candidate.Text)
            .FirstOrDefault();
    }

    public static string? SelectLatestCommonCompatiblePatch(
        IReadOnlyList<ApplicationPackageReference> packages,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> availableVersions)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(availableVersions);
        if (packages.Count == 0) return null;

        HashSet<string>? common = null;
        foreach (var package in packages)
        {
            if (!availableVersions.TryGetValue(package.Name, out var versions)) return null;
            var compatible = versions
                .Where(version => IsCompatiblePatch(package.Version, version))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (common is null)
                common = compatible;
            else
                common.IntersectWith(compatible);
            if (common.Count == 0) return null;
        }

        return common
            .Select(version => (Text: version, Version: Version.Parse(version)))
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => candidate.Text)
            .FirstOrDefault();
    }

    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static bool TryParseStableVersion(string value, out Version version)
    {
        version = new Version();
        if (value.Contains('-', StringComparison.Ordinal)) return false;
        return Version.TryParse(value, out version!);
    }

    private static string MajorMinor(string value) =>
        Version.TryParse(value.Split('-', 2)[0], out var version) ? $"{version.Major}.{version.Minor}.x" : value;

    private static string GetUserDataRoot()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local)) return local;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home)) return Path.Combine(home, ".local", "share");
        return Path.GetTempPath();
    }

    private static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value;
    }

    private sealed record PatchManifest(string XPScriptVersion, DateTimeOffset UpdatedAtUtc, ApplicationPackagePatch[] Patches);
}
