namespace XPScript.Web.Runtime;

public sealed class XpsWebPathResolver
{
    private readonly string _root;
    private readonly string _rootWithSeparator;
    private readonly StringComparison _pathComparison;

    public XpsWebPathResolver(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Web root is required.", nameof(root));
        _root = Path.GetFullPath(root);
        _rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        _pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    public string Root => _root;

    public XpsRouteResolution Resolve(string requestPath, Func<string, bool>? fileExists = null, Func<string, bool>? directoryExists = null)
    {
        fileExists ??= File.Exists;
        directoryExists ??= Directory.Exists;

        var segments = NormalizeUrlPath(requestPath);
        if (segments.Count == 0)
            return ResolveCandidate("index.xps", null, fileExists);

        var relative = string.Join(Path.DirectorySeparatorChar, segments);
        var directoryCandidate = MapInsideRoot(relative);

        if (directoryExists(directoryCandidate))
        {
            var index = MapInsideRoot(Path.Combine(relative, "index.xps"));
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
        if (!full.Equals(_root, _pathComparison) && !full.StartsWith(_rootWithSeparator, _pathComparison))
            throw new XpsWebPathException("Resolved request path escapes the configured web root.");
        return full;
    }

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
