using System.Text.Json;
using System.Text.Json.Serialization;
using XPScript.Web.Runtime;

internal static class XpsHostConfig
{
    private const string DefaultFileName = "web.cfg";

    public static string[] Apply(string command, string[] commandArgs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(commandArgs);

        var (configPath, explicitConfig, remainingArgs) = ExtractConfigPath(commandArgs);
        if (command.Equals("web", StringComparison.OrdinalIgnoreCase))
            remainingArgs = NormalizeWebPositionalRoot(remainingArgs);

        XpsHostConfigFile? config = null;
        string configDirectory = AppContext.BaseDirectory;
        if (configPath is null)
        {
            var automaticPath = Path.Combine(AppContext.BaseDirectory, DefaultFileName);
            if (File.Exists(automaticPath)) configPath = automaticPath;
        }
        else if (!File.Exists(configPath))
            throw new FileNotFoundException("Web host config file does not exist: " + configPath, configPath);

        if (configPath is not null)
        {
            var fullPath = Path.GetFullPath(configPath);
            configDirectory = Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory;
            config = Load(fullPath);
            var configArgs = command.ToLowerInvariant() switch
            {
                "web" => BuildWebArgs(config.Web, configDirectory, remainingArgs),
                "fastcgi" => BuildFastCgiArgs(config.FastCgi, configDirectory, remainingArgs),
                _ => throw new ArgumentException("Unsupported host config command: " + command)
            };
            if (configArgs.Count == 0 && explicitConfig)
                throw new InvalidOperationException($"Config file '{fullPath}' does not contain a '{command}' section.");
            remainingArgs = [.. configArgs, .. remainingArgs];
        }

        if (command.Equals("web", StringComparison.OrdinalIgnoreCase))
            remainingArgs = ApplyWebEnvironment(remainingArgs);
        remainingArgs = NormalizeRootArgument(remainingArgs);
        return command.Equals("web", StringComparison.OrdinalIgnoreCase)
            ? ApplyApiDocs(remainingArgs)
            : remainingArgs;
    }

    private static string[] ApplyApiDocs(string[] args)
    {
        var remaining = new List<string>(args.Length + 1);
        bool? cli = null;
        string? root = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--api-docs", StringComparison.OrdinalIgnoreCase)) { cli = true; continue; }
            if (args[i].Equals("--no-api-docs", StringComparison.OrdinalIgnoreCase)) { cli = false; continue; }
            remaining.Add(args[i]);
            if (args[i].Equals("--root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length) root = args[i + 1];
        }
        if (root is null) return remaining.ToArray();

        var configured = ReadProjectApiDocs(root);
        var enabled = cli ?? configured ?? false;
        if (!enabled) return remaining.ToArray();

        XpsApiDocGenerator.Generate(root);
        if (!HasAny(remaining.ToArray(), "--static-files")) remaining.Add("--static-files");
        Console.WriteLine("API documentation: " + Path.Combine(root, "apidoc", "index.html"));
        return remaining.ToArray();
    }

    private static bool? ReadProjectApiDocs(string root)
    {
        var path = Path.Combine(root, "xpscript.json");
        if (!File.Exists(path)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (!doc.RootElement.TryGetProperty("web", out var web) || web.ValueKind != JsonValueKind.Object) return null;
            if (!web.TryGetProperty("apiDocs", out var value)) return null;
            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new InvalidOperationException("xpscript.json web.apiDocs must be true or false.");
            return value.GetBoolean();
        }
        catch (JsonException ex) { throw new InvalidOperationException($"Invalid project config '{path}': {ex.Message}", ex); }
    }

    private static string[] NormalizeWebPositionalRoot(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("-", StringComparison.Ordinal)) return args;
        if (HasAny(args, "--root")) throw new ArgumentException("Specify the web root either as 'xpscript web PATH' or with --root, not both.");
        return ["--root", args[0], .. args[1..]];
    }

    private static string[] NormalizeRootArgument(string[] args)
    {
        var normalized = (string[])args.Clone();
        for (var i = 0; i < normalized.Length; i++)
        {
            if (!normalized[i].Equals("--root", StringComparison.OrdinalIgnoreCase)) continue;
            if (i + 1 >= normalized.Length) throw new ArgumentException("--root requires a directory path.");
            var supplied = normalized[++i].Trim();
            if (supplied.Length >= 2 && ((supplied[0] == '"' && supplied[^1] == '"') || (supplied[0] == '\'' && supplied[^1] == '\''))) supplied = supplied[1..^1].Trim();
            if (supplied.Length == 0) throw new ArgumentException("--root requires a directory path.");
            var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(supplied));
            if (!Directory.Exists(fullPath))
            {
                var suggestions = FindRootSuggestions(fullPath);
                var message = "Web root does not exist: " + fullPath;
                if (suggestions.Count > 0) message += Environment.NewLine + "Did you mean: " + string.Join(", ", suggestions);
                throw new DirectoryNotFoundException(message);
            }
            normalized[i] = fullPath;
        }
        return normalized;
    }

    private static IReadOnlyList<string> FindRootSuggestions(string missingRoot)
    {
        try
        {
            var parent = Directory.GetParent(missingRoot);
            if (parent is null || !parent.Exists) return Array.Empty<string>();
            var requestedName = Path.GetFileName(missingRoot);
            if (string.IsNullOrWhiteSpace(requestedName)) return Array.Empty<string>();
            return parent.EnumerateDirectories().Where(d => d.Name.StartsWith(requestedName, StringComparison.OrdinalIgnoreCase) || requestedName.StartsWith(d.Name, StringComparison.OrdinalIgnoreCase)).OrderBy(d => Math.Abs(d.Name.Length - requestedName.Length)).ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase).Take(3).Select(d => d.FullName).ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return Array.Empty<string>(); }
    }

    private static string[] ApplyWebEnvironment(string[] args)
    {
        XpsWebEnvironmentDefaults.Current = XpsWebEnvironment.Production;
        var remaining = new List<string>(args.Length);
        string? selected = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--environment", StringComparison.OrdinalIgnoreCase)) { remaining.Add(args[i]); continue; }
            if (selected is not null) throw new ArgumentException("--environment can be specified only once.");
            if (++i >= args.Length) throw new ArgumentException("--environment requires Production or Development.");
            selected = args[i];
        }
        if (selected is not null) XpsWebEnvironmentDefaults.Current = selected.Trim().ToLowerInvariant() switch
        {
            "production" => XpsWebEnvironment.Production,
            "development" => XpsWebEnvironment.Development,
            _ => throw new ArgumentException("--environment must be Production or Development.")
        };
        return remaining.ToArray();
    }

    private static XpsHostConfigFile Load(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize<XpsHostConfigFile>(stream, JsonOptions) ?? throw new InvalidOperationException("Web host config file is empty.");
        }
        catch (JsonException ex) { throw new InvalidOperationException($"Invalid web host config '{path}': {ex.Message}", ex); }
    }

    private static (string? Path, bool Explicit, string[] RemainingArgs) ExtractConfigPath(string[] args)
    {
        string? path = null; var remaining = new List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].Equals("--config", StringComparison.OrdinalIgnoreCase)) { remaining.Add(args[i]); continue; }
            if (path is not null) throw new ArgumentException("--config can be specified only once.");
            if (++i >= args.Length) throw new ArgumentException("--config requires a file path.");
            path = Path.GetFullPath(args[i]);
        }
        return (path, path is not null, remaining.ToArray());
    }

    private static List<string> BuildWebArgs(WebConfig? config, string baseDirectory, string[] cliArgs)
    {
        var result = new List<string>(); if (config is null) return result;
        AddValue(result, cliArgs, "--root", config.Root is null ? null : ResolvePath(baseDirectory, config.Root));
        AddValue(result, cliArgs, "--default-document", config.DefaultDocument); AddValue(result, cliArgs, "--environment", config.Environment);
        AddValue(result, cliArgs, ["--address", "--bind"], "--address", config.Address); AddValue(result, cliArgs, "--port", config.Port);
        if (!HasAny(cliArgs, "--host", "--allowed-host") && config.AllowedHosts is not null) foreach (var host in config.AllowedHosts) { if (string.IsNullOrWhiteSpace(host)) throw new InvalidOperationException("web.allowedHosts cannot contain empty values."); result.Add("--host"); result.Add(host); }
        AddValue(result, cliArgs, "--protocols", config.Protocols); AddValue(result, cliArgs, "--https-cert", config.HttpsCertificate is null ? null : ResolvePath(baseDirectory, config.HttpsCertificate)); AddValue(result, cliArgs, "--https-cert-password-env", config.HttpsCertificatePasswordEnvironment);
        AddFlag(result, cliArgs, "--health", config.Health); AddFlag(result, cliArgs, "--metrics", config.Metrics); AddFlag(result, cliArgs, "--sessions", config.Sessions); AddValue(result, cliArgs, "--session-cookie", config.SessionCookie); AddValue(result, cliArgs, "--session-timeout-seconds", config.SessionTimeoutSeconds); AddValue(result, cliArgs, "--session-same-site", config.SessionSameSite); AddFlag(result, cliArgs, "--session-secure", config.SessionSecure); AddFlag(result, cliArgs, "--operational-external", config.OperationalExternal); AddValue(result, cliArgs, "--structured-log", config.StructuredLog is null ? null : ResolvePath(baseDirectory, config.StructuredLog)); AddFlag(result, cliArgs, "--static-files", config.StaticFiles); AddValue(result, cliArgs, "--static-max-bytes", config.StaticMaxBytes);
        return result;
    }

    private static List<string> BuildFastCgiArgs(FastCgiConfig? config, string baseDirectory, string[] cliArgs)
    {
        var result = new List<string>(); if (config is null) return result;
        if (config.Listen is not null && (config.Address is not null || config.Port is not null)) throw new InvalidOperationException("fastCgi.listen cannot be combined with fastCgi.address or fastCgi.port.");
        if (config.UnixSocket is not null && (config.Listen is not null || config.Address is not null || config.Port is not null)) throw new InvalidOperationException("fastCgi.unixSocket cannot be combined with TCP listener settings.");
        AddValue(result, cliArgs, "--root", config.Root is null ? null : ResolvePath(baseDirectory, config.Root)); AddValue(result, cliArgs, "--default-document", config.DefaultDocument); AddValue(result, cliArgs, "--listen", config.Listen); AddValue(result, cliArgs, ["--address", "--bind"], "--address", config.Address); AddValue(result, cliArgs, "--port", config.Port); AddValue(result, cliArgs, "--unix-socket", config.UnixSocket is null ? null : ResolvePath(baseDirectory, config.UnixSocket)); return result;
    }

    private static string ResolvePath(string baseDirectory, string value) { if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Config paths cannot be empty."); return Path.GetFullPath(value, baseDirectory); }
    private static void AddFlag(List<string> result, string[] cliArgs, string option, bool? enabled) { if (enabled == true && !HasAny(cliArgs, option)) result.Add(option); }
    private static void AddValue(List<string> result, string[] cliArgs, string option, object? value) => AddValue(result, cliArgs, [option], option, value);
    private static void AddValue(List<string> result, string[] cliArgs, string[] aliases, string outputOption, object? value) { if (value is null || HasAny(cliArgs, aliases)) return; var text = value is IFormattable f ? f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) : value.ToString(); if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException($"Config value for {outputOption} cannot be empty."); result.Add(outputOption); result.Add(text); }
    private static bool HasAny(string[] args, params string[] options) => args.Any(arg => options.Any(option => arg.Equals(option, StringComparison.OrdinalIgnoreCase)));

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public sealed class XpsHostConfigFile { public WebConfig? Web { get; init; } public FastCgiConfig? FastCgi { get; init; } }
    public sealed class WebConfig
    {
        public string? Root { get; init; } public string? DefaultDocument { get; init; } public string? Environment { get; init; } public string? Address { get; init; } public int? Port { get; init; } public string[]? AllowedHosts { get; init; } public string? Protocols { get; init; } public string? HttpsCertificate { get; init; } public string? HttpsCertificatePasswordEnvironment { get; init; } public bool? Health { get; init; } public bool? Metrics { get; init; } public bool? Sessions { get; init; } public string? SessionCookie { get; init; } public int? SessionTimeoutSeconds { get; init; } public string? SessionSameSite { get; init; } public bool? SessionSecure { get; init; } public bool? OperationalExternal { get; init; } public string? StructuredLog { get; init; } public bool? StaticFiles { get; init; } public long? StaticMaxBytes { get; init; }
    }
    public sealed class FastCgiConfig { public string? Root { get; init; } public string? DefaultDocument { get; init; } public string? Listen { get; init; } public string? Address { get; init; } public int? Port { get; init; } public string? UnixSocket { get; init; } }
}
