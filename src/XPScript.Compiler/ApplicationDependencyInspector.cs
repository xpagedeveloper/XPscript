using System.Diagnostics;
using System.Security;
using System.Text;
using System.Text.Json;

namespace XPScript.Compiler;

public sealed record ApplicationDependencyVulnerability(string Severity, string AdvisoryUrl);

public sealed record ApplicationResolvedDependency(
    string Name,
    string Version,
    bool Direct,
    string? Reason,
    IReadOnlyList<ApplicationDependencyVulnerability> Vulnerabilities);

public sealed record ApplicationDependencyInspection(
    string SourceFile,
    string RuntimeIdentifier,
    IReadOnlyList<ApplicationResolvedDependency> Dependencies)
{
    public IReadOnlyList<ApplicationResolvedDependency> VulnerableDependencies =>
        Dependencies.Where(d => d.Vulnerabilities.Count > 0).ToArray();
}

public static class ApplicationDependencyInspector
{
    public static async Task<ApplicationDependencyInspection> InspectAsync(
        string sourcePath,
        string? runtimeIdentifier = null,
        bool vulnerabilitiesOnly = false,
        CancellationToken cancellationToken = default)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("Source file not found.", sourcePath);
        if (!Path.GetExtension(sourcePath).Equals(".xps", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("XPScript source files must use the .xps extension.", nameof(sourcePath));

        var rid = string.IsNullOrWhiteSpace(runtimeIdentifier)
            ? CompilerDriver.CurrentRuntimeIdentifier()
            : runtimeIdentifier.Trim().ToLowerInvariant();
        if (!CompilerDriver.SupportedRuntimes.Contains(rid, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Unsupported runtime identifier '" + rid + "'.");

        var originalSource = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var includeResult = new IncludeSourcePreprocessor().Transform(originalSource, sourcePath);
        var transpiler = new XPScriptTranspiler();
        string generatedSource;
        using (ExpandedSourceContext.Begin(includeResult.Source, sourcePath, includeResult.Map))
            generatedSource = transpiler.Transpile(includeResult.Source, sourcePath, rid);

        var direct = ApplicationDependencyCatalog.Detect(generatedSource);
        if (direct.Count == 0)
            return new(Path.GetFileName(sourcePath), rid, []);

        var directByName = direct.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var tempRoot = CompilerPathSecurity.CreateOwnedTemporaryDirectory("dependency-inspect-");
        try
        {
            var projectPath = Path.Combine(tempRoot, "DependencyInspection.csproj");
            await File.WriteAllTextAsync(projectPath, BuildInspectionProject(rid, direct), cancellationToken).ConfigureAwait(false);
            CompilerPathSecurity.HardenTemporaryFile(projectPath);

            var restore = await RunDotnetAsync(tempRoot, ["restore", projectPath, "--nologo"], cancellationToken).ConfigureAwait(false);
            if (restore.ExitCode != 0)
                throw new CompilerException("Unable to restore application dependencies." + Environment.NewLine + restore.Output);

            var args = new List<string>
            {
                "package", "list", projectPath, "--include-transitive", "--format", "json", "--no-restore"
            };
            if (vulnerabilitiesOnly) args.Add("--vulnerable");

            var list = await RunDotnetAsync(tempRoot, args, cancellationToken).ConfigureAwait(false);
            if (list.ExitCode != 0)
                throw new CompilerException("Unable to inspect application dependencies." + Environment.NewLine + list.Output);

            var dependencies = ParsePackageListJson(list.Stdout, directByName);
            return new(Path.GetFileName(sourcePath), rid, dependencies);
        }
        finally
        {
            try { CompilerPathSecurity.DeleteOwnedTemporaryDirectory(tempRoot); } catch { }
        }
    }

    private static string BuildInspectionProject(string rid, IReadOnlyList<ApplicationPackageReference> packages)
    {
        var items = new StringBuilder();
        foreach (var package in packages)
            items.Append("    <PackageReference Include=\"")
                .Append(SecurityElement.Escape(package.Name))
                .Append("\" Version=\"")
                .Append(SecurityElement.Escape(package.Version))
                .AppendLine("\" />");

        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RuntimeIdentifier>{SecurityElement.Escape(rid)}</RuntimeIdentifier>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
{items}  </ItemGroup>
</Project>
""";
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr, string Output)> RunDotnetAsync(
        string workingDirectory,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);
        CompilerBuildEnvironment.Configure(psi, workingDirectory);
        using var process = Process.Start(psi) ?? throw new CompilerException("Unable to start dotnet dependency inspection.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        return (process.ExitCode, stdout, stderr, stdout + Environment.NewLine + stderr);
    }

    private static IReadOnlyList<ApplicationResolvedDependency> ParsePackageListJson(
        string json,
        IReadOnlyDictionary<string, ApplicationPackageReference> directByName)
    {
        using var document = JsonDocument.Parse(json);
        var result = new Dictionary<string, ApplicationResolvedDependency>(StringComparer.OrdinalIgnoreCase);
        if (!document.RootElement.TryGetProperty("projects", out var projects) || projects.ValueKind != JsonValueKind.Array)
            return [];

        foreach (var project in projects.EnumerateArray())
        {
            if (!project.TryGetProperty("frameworks", out var frameworks) || frameworks.ValueKind != JsonValueKind.Array) continue;
            foreach (var framework in frameworks.EnumerateArray())
            {
                ReadPackages(framework, "topLevelPackages", true, directByName, result);
                ReadPackages(framework, "transitivePackages", false, directByName, result);
            }
        }

        return result.Values
            .OrderByDescending(x => x.Direct)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ReadPackages(
        JsonElement framework,
        string propertyName,
        bool direct,
        IReadOnlyDictionary<string, ApplicationPackageReference> directByName,
        IDictionary<string, ApplicationResolvedDependency> result)
    {
        if (!framework.TryGetProperty(propertyName, out var packages) || packages.ValueKind != JsonValueKind.Array) return;
        foreach (var package in packages.EnumerateArray())
        {
            var name = ReadString(package, "id") ?? ReadString(package, "name");
            var version = ReadString(package, "resolvedVersion") ?? ReadString(package, "version");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(version)) continue;
            directByName.TryGetValue(name, out var directInfo);
            var vulnerabilities = ReadVulnerabilities(package);
            var key = name + "\0" + version;
            result[key] = new(name, version, direct || directInfo is not null, directInfo?.Reason, vulnerabilities);
        }
    }

    private static IReadOnlyList<ApplicationDependencyVulnerability> ReadVulnerabilities(JsonElement package)
    {
        if (!package.TryGetProperty("vulnerabilities", out var vulnerabilities) || vulnerabilities.ValueKind != JsonValueKind.Array)
            return [];
        var result = new List<ApplicationDependencyVulnerability>();
        foreach (var vulnerability in vulnerabilities.EnumerateArray())
        {
            var severity = ReadString(vulnerability, "severity") ?? "unknown";
            var advisory = ReadString(vulnerability, "advisoryurl") ??
                           ReadString(vulnerability, "advisoryUrl") ??
                           ReadString(vulnerability, "url") ?? "";
            result.Add(new(severity.ToLowerInvariant(), advisory));
        }
        return result;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
