namespace XPScript.Web.Runtime;

public sealed class XpsWebPathResolver
{
    private readonly string _root;
    private readonly string _rootWithSeparator;
    private readonly StringComparison _pathComparison;
    private readonly string _defaultDocumentName;

    public XpsWebPathResolver(string root, string defaultDocumentName = "index.xps")
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Web root is required.", nameof(root));
        _defaultDocumentName = ValidateDefaultDocumentName(defaultDocumentName);
        _root = Path.GetFullPath(root);
        _rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        _pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    public string Root => _root;
    public string DefaultDocumentName => _defaultDocumentName;

    public XpsRouteResolution Resolve(string requestPath, Func<string, bool>? fileExists = null, Func<string, bool>? directoryExists = null)
    {
        fileExists ??= File.Exists;
        directoryExists ??= Directory.Exists;

        var segments = NormalizeUrlPath(requestPath);
        if (segments.Count == 0)
            return ResolveCandidate(_defaultDocumentName, null, fileExists);

        var relative = string.Join(Path.DirectorySeparatorChar, segments);
        var directoryCandidate = MapInsideRoot(relative);

        if (directoryExists(directoryCandidate))
        {
            var index = MapInsideRoot(Path.Combine(relative, _defaultDocumentName));
            if (fileExists(index)) return new XpsRouteResolution(index, null, true);
            return XpsRouteResolution.NotFound;
        }

        if (segments[^1].EndsWith(".xps", StringComparison.OrdinalIgnoreCase))
            return ResolveCandidate(relative, null, fileExists);

        var directScript = ResolveCandidate(relative + ".xps", null, fileExists);
        if (directScript.Found) return directScript;

        if (segments.Count >= 2)
        {
            var routeFunction = segments[^1];
            ValidateRouteFunction(routeFunction);
            var scriptSegments = segments.Take(segments.Count - 1).ToArray();
            var scriptRelative = string.Join(Path.DirectorySeparatorChar, scriptSegments) + ".xps";
            var functionRoute = ResolveCandidate(scriptRelative, routeFunction, fileExists);
            if (functionRoute.Found) return functionRoute;
        }

        return XpsRouteResolution.NotFound;
    }

    public string MapPath(string relativePath)
    {
        if (relativePath is null) throw new ArgumentNullException(nameof(relativePath));
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments) ValidateSegment(segment);
        return MapInsideRoot(Path.Combine(segments));
    }

    private XpsRouteResolution ResolveCandidate(string relativePath, string? routeFunction, Func<string, bool> fileExists)
    {
        var fullPath = MapInsideRoot(relativePath);
        return fileExists(fullPath)
            ? new XpsRouteResolution(fullPath, routeFunction, true)
            : XpsRouteResolution.NotFound;
    }

    private string MapInsideRoot(string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) throw new XpsWebPathException("Absolute request paths are not permitted.");
        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        if (!IsInsideRoot(full))
            throw new XpsWebPathException("Resolved request path escapes the configured web root.");

        EnsureLinkTargetsStayInsideRoot(full);
        return full;
    }

    private void EnsureLinkTargetsStayInsideRoot(string fullPath)
    {
        var relative = Path.GetRelativePath(_root, fullPath);
        if (relative == ".") return;

        var current = _root;
        foreach (var segment in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);

            FileSystemInfo? info = null;
            if (Directory.Exists(current)) info = new DirectoryInfo(current);
            else if (File.Exists(current)) info = new FileInfo(current);
            if (info is null || info.LinkTarget is null) continue;

            FileSystemInfo? target;
            try
            {
                target = info.ResolveLinkTarget(returnFinalTarget: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                throw new XpsWebPathException("Unable to safely resolve a symbolic link or reparse point in the request path.", ex);
            }

            if (target is null || !IsInsideRoot(Path.GetFullPath(target.FullName)))
                throw new XpsWebPathException("Resolved request path escapes the configured web root through a symbolic link or reparse point.");
        }
    }

    private bool IsInsideRoot(string path) =>
        path.Equals(_root, _pathComparison) || path.StartsWith(_rootWithSeparator, _pathComparison);

    private static List<string> NormalizeUrlPath(string requestPath)
    {
        if (requestPath is null) throw new ArgumentNullException(nameof(requestPath));
        if (requestPath.IndexOf('\0') >= 0 || requestPath.Any(char.IsControl))
            throw new XpsWebPathException("Request path contains a prohibited control character.");

        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(requestPath);
        }
        catch (UriFormatException ex)
        {
            throw new XpsWebPathException("Request path contains malformed percent encoding.", ex);
        }

        if (decoded.Contains('%'))
        {
            for (var i = 0; i + 2 < decoded.Length; i++)
            {
                if (decoded[i] == '%' && IsHex(decoded[i + 1]) && IsHex(decoded[i + 2]))
                    throw new XpsWebPathException("Double-encoded request paths are not permitted.");
            }
        }

        decoded = decoded.Replace('\\', '/');
        var segments = decoded.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var segment in segments) ValidateSegment(segment);
        return segments;
    }

    private static string ValidateDefaultDocumentName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Default document name is required.", nameof(value));
        var normalized = value.Trim();
        if (!normalized.EndsWith(".xps", StringComparison.OrdinalIgnoreCase) ||
            normalized.Length > 255 ||
            normalized.IndexOfAny(['/', '\\', '\0', ':']) >= 0 ||
            normalized.Any(char.IsControl) ||
            normalized is "." or "..")
            throw new ArgumentException("Default document must be a single .xps filename inside the configured root.", nameof(value));
        return normalized;
    }

    private static void ValidateSegment(string segment)
    {
        if (segment is "." or "..") throw new XpsWebPathException("Path traversal segments are not permitted.");
        if (segment.Length == 0) throw new XpsWebPathException("Empty path segment is not permitted.");
        if (segment.IndexOfAny(['\0', ':']) >= 0) throw new XpsWebPathException("Path segment contains a prohibited character.");
        if (segment.Any(char.IsControl)) throw new XpsWebPathException("Path segment contains a control character.");
    }

    private static void ValidateRouteFunction(string value)
    {
        if (value.Length is 0 or > 128) throw new XpsWebPathException("Route function name is invalid.");
        if (!(char.IsLetter(value[0]) || value[0] == '_')) throw new XpsWebPathException("Route function name is invalid.");
        if (value.Skip(1).Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
            throw new XpsWebPathException("Route function name is invalid.");
    }

    private static bool IsHex(char c) =>
        c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
}

public sealed record XpsRouteResolution(string? ScriptPath, string? RouteFunction, bool Found)
{
    public static XpsRouteResolution NotFound { get; } = new(null, null, false);
}

public sealed class XpsWebPathException : Exception
{
    public XpsWebPathException(string message) : base(message) { }
    public XpsWebPathException(string message, Exception innerException) : base(message, innerException) { }
}
