using System.Net;

namespace XPScript.Web.Runtime;

public sealed class XpsWebServer
{
    private readonly XpsServerInfo _info;
    private readonly XpsWebPathResolver _resolver;
    private readonly string _canonicalRoot;
    private readonly StringComparison _pathComparison;

    public XpsWebServer(XpsServerInfo info)
    {
        _info = info ?? throw new ArgumentNullException(nameof(info));
        _resolver = new XpsWebPathResolver(info.RootPath);
        _canonicalRoot = CanonicalizeExistingSegments(Path.GetFullPath(info.RootPath));
        _pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    public string SiteId => _info.SiteId;
    public string RootPath => _info.RootPath;
    public string HostingMode => _info.HostingMode.ToString();
    public DateTimeOffset StartTimeUtc => _info.StartTimeUtc;
    public string RuntimeVersion => _info.RuntimeVersion;
    public string? Address => _info.Address;
    public int? Port => _info.Port;

    public string MapPath(string relativePath)
    {
        var mapped = _resolver.MapPath(relativePath);
        var canonical = CanonicalizeExistingSegments(mapped);
        var prefix = _canonicalRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _canonicalRoot
            : _canonicalRoot + Path.DirectorySeparatorChar;
        if (!canonical.Equals(_canonicalRoot, _pathComparison) && !canonical.StartsWith(prefix, _pathComparison))
            throw new XpsWebPathException("Mapped path escapes the configured web root.");
        return canonical;
    }

    public static string HtmlEncode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    public static string UrlEncode(string? value) => WebUtility.UrlEncode(value ?? string.Empty);

    private static string CanonicalizeExistingSegments(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (string.IsNullOrWhiteSpace(root)) return full;

        var current = root;
        var relative = Path.GetRelativePath(root, full);
        foreach (var segment in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current)) continue;

            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is null && (info.Attributes & FileAttributes.ReparsePoint) == 0) continue;

            string? target;
            try
            {
                target = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
            }
            catch (IOException)
            {
                throw new XpsWebPathException("Unable to safely resolve a symbolic link or reparse point.");
            }

            if (string.IsNullOrWhiteSpace(target))
                throw new XpsWebPathException("Unable to safely resolve a symbolic link or reparse point.");
            current = Path.GetFullPath(target);
        }
        return Path.GetFullPath(current);
    }
}
