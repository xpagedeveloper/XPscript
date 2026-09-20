using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace XPScript.Web.Runtime;

public enum XpsWebLogKind
{
    Access,
    Application,
    Security,
    Error
}

public sealed class XpsWebLogOptions
{
    public string? DirectoryPath { get; init; }
    public long MaxFileBytes { get; init; } = 100L * 1024 * 1024;
    public TimeSpan Retention { get; init; } = TimeSpan.FromDays(14);
    public long MaxTotalBytes { get; init; } = 2L * 1024 * 1024 * 1024;
    public bool CompressRotatedFiles { get; init; } = true;

    public void Validate()
    {
        if (MaxFileBytes is < 1024 * 1024 or > 4L * 1024 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(MaxFileBytes), "Log files must be between 1 MiB and 4 GiB.");
        if (Retention < TimeSpan.FromDays(1) || Retention > TimeSpan.FromDays(3650))
            throw new ArgumentOutOfRangeException(nameof(Retention), "Log retention must be between 1 and 3650 days.");
        if (MaxTotalBytes < MaxFileBytes || MaxTotalBytes > 1024L * 1024 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(MaxTotalBytes), "Log quota must be at least one file and at most 1 TiB.");
    }
}

public sealed class XpsWebLogManager : IDisposable
{
    public const string SchemaVersion = "xpscript.web.log/1";
    private static readonly HashSet<string> SensitiveAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization", "cookie", "set-cookie", "password", "passwd", "secret", "token",
        "access_token", "refresh_token", "api_key", "apikey", "connection_string",
        "encryption_key", "private_key", "session", "session_id"
    };
    private static readonly HashSet<string> ReservedAttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "service.name", "service.version", "deployment.environment.name", "xpscript.site.id",
        "xpscript.hosting.mode", "http.request.method", "url.path", "url.scheme",
        "server.address", "network.protocol.version", "client.address", "user_agent.original",
        "http.response.status_code", "http.request.body.size", "http.response.body.size",
        "http.request.resend_count", "error.type", "event.category", "event.outcome",
        "request.id", "user.id", "duration.ms"
    };

    private readonly object _sync = new();
    private readonly XpsServerInfo _server;
    private readonly XpsWebLogOptions _options;
    private readonly Dictionary<XpsWebLogKind, LogFile> _files = [];
    private bool _disposed;

    public XpsWebLogManager(XpsServerInfo server, XpsWebLogOptions? options = null)
    {
        _server = server ?? throw new ArgumentNullException(nameof(server));
        _options = options ?? new XpsWebLogOptions();
        _options.Validate();
        DirectoryPath = ResolveAndValidateDirectory(server, _options.DirectoryPath);
        Directory.CreateDirectory(DirectoryPath);
        Cleanup();
    }

    public string DirectoryPath { get; }

    public static string DefaultDirectory(XpsServerInfo server)
    {
        ArgumentNullException.ThrowIfNull(server);
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = Path.Combine(AppContext.BaseDirectory, ".xpscript-state");
        return Path.Combine(basePath, "XPScript", "logs", SafeFilePart(server.SiteId));
    }

    public void WriteAccess(
        XpsWebRequest request,
        int statusCode,
        long responseBodyBytes,
        TimeSpan duration,
        string requestId,
        string transport,
        XpsWebPrincipal? principal = null,
        string? errorType = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.TryGetValue("User-Agent", out var agents);
        WriteAccess(
            request.Method, request.Path, request.Scheme, request.Host, request.Protocol,
            request.RemoteAddress, agents is { Count: > 0 } ? agents[0] : null,
            Math.Max(0, request.ContentLength ?? request.Body.Length), statusCode, responseBodyBytes,
            duration, requestId, transport, principal, errorType);
    }

    public void WriteAccess(
        string method,
        string path,
        string scheme,
        string host,
        string protocol,
        string? remoteAddress,
        string? userAgent,
        long requestBodyBytes,
        int statusCode,
        long responseBodyBytes,
        TimeSpan duration,
        string requestId,
        string transport,
        XpsWebPrincipal? principal = null,
        string? errorType = null)
    {
        var attributes = BaseAttributes();
        attributes["request.id"] = Clean(requestId, 128);
        attributes["xpscript.transport"] = Clean(transport, 32);
        attributes["http.request.method"] = Clean(method, 32);
        attributes["url.path"] = Clean(path, 4096);
        attributes["url.scheme"] = Clean(scheme, 32);
        attributes["server.address"] = Clean(host, 512);
        attributes["network.protocol.version"] = Clean(protocol, 64);
        attributes["http.response.status_code"] = statusCode;
        attributes["http.request.body.size"] = Math.Max(0, requestBodyBytes);
        attributes["http.response.body.size"] = Math.Max(0, responseBodyBytes);
        attributes["duration.ms"] = Math.Max(0, duration.TotalMilliseconds);
        if (!string.IsNullOrWhiteSpace(remoteAddress)) attributes["client.address"] = Clean(remoteAddress, 128);
        if (!string.IsNullOrWhiteSpace(userAgent)) attributes["user_agent.original"] = Clean(userAgent, 1024);
        if (principal?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(principal.UserId))
            attributes["user.id"] = Clean(principal.UserId, 256);
        if (!string.IsNullOrWhiteSpace(errorType)) attributes["error.type"] = Clean(errorType, 512);

        var severity = statusCode >= 500 ? "ERROR" : statusCode >= 400 ? "WARN" : "INFO";
        Write(XpsWebLogKind.Access, severity, "http.server.request",
            Clean(method, 32) + " " + Clean(path, 4096) + " " + statusCode, attributes);
        if (statusCode >= 500 || errorType is not null)
            Write(XpsWebLogKind.Error, "ERROR", "http.server.error",
                "Request failed with status " + statusCode, attributes);
    }

    public void WriteApplication(
        string severity,
        string eventName,
        string message,
        string? attributesJson,
        XpsWebContext context,
        bool audit)
    {
        ArgumentNullException.ThrowIfNull(context);
        var attributes = BaseAttributes();
        attributes["event.category"] = audit ? "audit" : "application";
        attributes["request.id"] = context.RequestId;
        attributes["http.request.method"] = context.Request.Method;
        attributes["url.path"] = context.Request.Path;
        if (context.Principal.IsAuthenticated && !string.IsNullOrWhiteSpace(context.Principal.UserId))
            attributes["user.id"] = Clean(context.Principal.UserId, 256);
        MergeApplicationAttributes(attributes, attributesJson);
        Write(audit ? XpsWebLogKind.Security : XpsWebLogKind.Application,
            NormalizeSeverity(severity), NormalizeEventName(eventName), Clean(message, 8192), attributes);
    }

    public void WriteSecurity(string eventName, string message, IReadOnlyDictionary<string, object?>? attributes = null)
    {
        var merged = BaseAttributes();
        merged["event.category"] = "security";
        if (attributes is not null)
            foreach (var pair in attributes)
                if (!IsSensitive(pair.Key) && !ReservedAttributeNames.Contains(pair.Key))
                    merged[CleanKey(pair.Key)] = pair.Value;
        Write(XpsWebLogKind.Security, "WARN", NormalizeEventName(eventName), Clean(message, 8192), merged);
    }

    private void Write(XpsWebLogKind kind, string severity, string eventName, string body, Dictionary<string, object?> attributes)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var line = Serialize(timestamp, severity, eventName, body, attributes);
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var file = GetFile(kind, timestamp, Encoding.UTF8.GetByteCount(line) + 1);
            file.Writer.WriteLine(line);
            file.Writer.Flush();
            file.Bytes += Encoding.UTF8.GetByteCount(line) + 1;
        }
    }

    private LogFile GetFile(XpsWebLogKind kind, DateTimeOffset timestamp, int incomingBytes)
    {
        var utcDate = DateOnly.FromDateTime(timestamp.UtcDateTime);
        if (_files.TryGetValue(kind, out var current) &&
            current.Date == utcDate && current.Bytes + incomingBytes <= _options.MaxFileBytes)
            return current;

        if (current is not null)
        {
            current.Writer.Dispose();
            _files.Remove(kind);
            if (_options.CompressRotatedFiles) Compress(current.Path);
            Cleanup();
        }

        var prefix = kind.ToString().ToLowerInvariant();
        var sequence = 1;
        string path;
        do
        {
            path = Path.Combine(DirectoryPath, $"{prefix}-{utcDate:yyyy-MM-dd}-{sequence:0000}.jsonl");
            sequence++;
        } while (File.Exists(path) && new FileInfo(path).Length >= _options.MaxFileBytes);

        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read,
            16 * 1024, FileOptions.WriteThrough);
        var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        var file = new LogFile(path, utcDate, stream.Length, writer);
        _files[kind] = file;
        return file;
    }

    private string Serialize(DateTimeOffset timestamp, string severity, string eventName, string body, Dictionary<string, object?> attributes)
    {
        using var output = new MemoryStream();
        using (var json = new Utf8JsonWriter(output))
        {
            json.WriteStartObject();
            json.WriteString("schema_version", SchemaVersion);
            json.WriteString("timestamp", timestamp);
            json.WriteString("observed_timestamp", DateTimeOffset.UtcNow);
            var activity = Activity.Current;
            if (activity is not null)
            {
                json.WriteString("trace_id", activity.TraceId.ToHexString());
                json.WriteString("span_id", activity.SpanId.ToHexString());
            }
            json.WriteString("severity_text", severity);
            json.WriteNumber("severity_number", SeverityNumber(severity));
            json.WriteString("event_name", eventName);
            json.WriteString("body", body);
            json.WritePropertyName("attributes");
            JsonSerializer.Serialize(json, attributes);
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private Dictionary<string, object?> BaseAttributes() => new(StringComparer.Ordinal)
    {
        ["service.name"] = "xpscript",
        ["service.version"] = _server.RuntimeVersion,
        ["deployment.environment.name"] = _server.Environment.ToString(),
        ["xpscript.site.id"] = _server.SiteId,
        ["xpscript.hosting.mode"] = _server.HostingMode.ToString().ToLowerInvariant()
    };

    private static void MergeApplicationAttributes(Dictionary<string, object?> target, string? jsonText)
    {
        if (string.IsNullOrWhiteSpace(jsonText)) return;
        if (Encoding.UTF8.GetByteCount(jsonText) > 64 * 1024)
            throw new ArgumentException("Log attributes cannot exceed 64 KiB.", nameof(jsonText));
        using var document = JsonDocument.Parse(jsonText, new JsonDocumentOptions { MaxDepth = 8 });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Log attributes must be a JSON object.", nameof(jsonText));
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var key = CleanKey(property.Name);
            if (ReservedAttributeNames.Contains(key))
                throw new ArgumentException("Log attribute is reserved by the runtime: " + key, nameof(jsonText));
            if (IsSensitive(key))
                throw new ArgumentException("Sensitive log attribute is prohibited: " + key, nameof(jsonText));
            target[key] = property.Value.Clone();
        }
    }

    private static string ResolveAndValidateDirectory(XpsServerInfo server, string? configured)
    {
        string root;
        string directory;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(server.RootPath));
            directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
                string.IsNullOrWhiteSpace(configured) ? DefaultDirectory(server) : configured));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new ArgumentException("The web log directory is invalid.", nameof(configured), ex);
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var separator = Path.DirectorySeparatorChar.ToString();
        if (directory.Equals(root, comparison) || directory.StartsWith(root + separator, comparison))
            throw new InvalidOperationException("The web log directory must be outside the web root and can never be served as web content.");

        Directory.CreateDirectory(directory);
        var resolvedRoot = ResolveLink(root);
        var resolvedDirectory = ResolveLink(directory);
        if (resolvedDirectory.Equals(resolvedRoot, comparison) || resolvedDirectory.StartsWith(resolvedRoot + separator, comparison))
            throw new InvalidOperationException("The resolved web log directory must be outside the web root.");
        return directory;
    }

    private static string ResolveLink(string path)
    {
        try
        {
            var info = new DirectoryInfo(path);
            if ((info.Attributes & FileAttributes.ReparsePoint) == 0) return info.FullName;
            return info.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? info.FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Unable to verify the web log directory boundary.", ex);
        }
    }

    private void Cleanup()
    {
        try
        {
            var active = _files.Values.Select(x => x.Path).ToHashSet(StringComparer.Ordinal);
            var files = new DirectoryInfo(DirectoryPath)
                .EnumerateFiles("*.jsonl*")
                .Where(x => !active.Contains(x.FullName))
                .OrderBy(x => x.LastWriteTimeUtc)
                .ToList();
            var cutoff = DateTime.UtcNow - _options.Retention;
            foreach (var file in files.Where(x => x.LastWriteTimeUtc < cutoff).ToArray())
            {
                file.Delete();
                files.Remove(file);
            }
            var total = files.Sum(x => x.Length) + _files.Values.Sum(x => x.Bytes);
            foreach (var file in files)
            {
                if (total <= _options.MaxTotalBytes) break;
                total -= file.Length;
                file.Delete();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("XPScript web log cleanup failed: " + ex.Message);
        }
    }

    private static void Compress(string path)
    {
        try
        {
            var destination = path + ".gz";
            using (var input = File.OpenRead(path))
            using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize))
                input.CopyTo(gzip);
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("XPScript web log compression failed: " + ex.Message);
        }
    }

    private static string NormalizeSeverity(string value) => (value ?? "").Trim().ToUpperInvariant() switch
    {
        "TRACE" => "TRACE", "DEBUG" => "DEBUG", "INFO" => "INFO", "WARN" or "WARNING" => "WARN",
        "ERROR" => "ERROR", "FATAL" or "CRITICAL" => "FATAL",
        _ => throw new ArgumentException("Unsupported log severity.", nameof(value))
    };

    private static int SeverityNumber(string severity) => severity switch
    {
        "TRACE" => 1, "DEBUG" => 5, "INFO" => 9, "WARN" => 13, "ERROR" => 17, "FATAL" => 21, _ => 0
    };

    private static string NormalizeEventName(string value)
    {
        var result = Clean(value, 128).Trim();
        if (result.Length == 0 || result.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')))
            throw new ArgumentException("Event names may contain letters, digits, dot, underscore and hyphen.", nameof(value));
        return result;
    }

    private static string CleanKey(string value)
    {
        var result = Clean(value, 128).Trim();
        if (result.Length == 0 || result.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')))
            throw new ArgumentException("Log attribute names contain invalid characters.", nameof(value));
        return result;
    }

    private static bool IsSensitive(string key)
    {
        var normalized = key.Replace('-', '_').Replace('.', '_');
        return SensitiveAttributeNames.Any(name =>
            normalized.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("_" + name, StringComparison.OrdinalIgnoreCase));
    }

    private static string Clean(string? value, int maxLength)
    {
        var text = value ?? string.Empty;
        if (text.Length > maxLength) text = text[..maxLength];
        return new string(text.Select(c => c is '\r' or '\n' or '\0' || char.IsControl(c) ? ' ' : c).ToArray());
    }

    private static string SafeFilePart(string? value)
    {
        var cleaned = new string((value ?? "site").Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_').ToArray());
        return cleaned.Length == 0 ? "site" : cleaned[..Math.Min(64, cleaned.Length)];
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var file in _files.Values) file.Writer.Dispose();
            _files.Clear();
        }
    }

    private sealed class LogFile(string path, DateOnly date, long bytes, StreamWriter writer)
    {
        public string Path { get; } = path;
        public DateOnly Date { get; } = date;
        public long Bytes { get; set; } = bytes;
        public StreamWriter Writer { get; } = writer;
    }
}
