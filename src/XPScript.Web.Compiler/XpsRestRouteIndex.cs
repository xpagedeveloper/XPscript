using System.Security.Cryptography;
using System.Text;
using XPScript.Web.Runtime;

namespace XPScript.Web.Compiler;

internal sealed record XpsExplicitRouteMatch(
    string ScriptPath,
    string ProcedureName,
    XpsWebRouteDescriptor Descriptor,
    IReadOnlyDictionary<string, string> RouteValues);

internal sealed class XpsRestRouteIndex
{
    private readonly string _root;
    private readonly object _gate = new();
    private string _fingerprint = string.Empty;
    private IReadOnlyList<Entry> _entries = Array.Empty<Entry>();

    public XpsRestRouteIndex(string root)
    {
        _root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));
        Refresh(force: true);
    }

    public XpsExplicitRouteMatch? Match(string path, string method)
    {
        Refresh(force: false);
        var normalizedPath = NormalizeRequestPath(path);
        Entry[] entries;
        lock (_gate) entries = _entries.ToArray();

        var pathMatches = new List<(Entry Entry, IReadOnlyDictionary<string, string> Values)>();
        foreach (var entry in entries)
        {
            if (TryMatchTemplate(entry.Descriptor.RouteTemplate!, normalizedPath, out var values))
                pathMatches.Add((entry, values));
        }

        if (pathMatches.Count == 0) return null;
        var methodMatch = pathMatches.FirstOrDefault(x =>
            x.Entry.Descriptor.Policy.Methods.Contains(method, StringComparer.OrdinalIgnoreCase) ||
            (method.Equals("HEAD", StringComparison.OrdinalIgnoreCase) && x.Entry.Descriptor.Policy.Methods.Contains("GET", StringComparer.OrdinalIgnoreCase)));
        var selected = methodMatch.Entry is not null ? methodMatch : pathMatches[0];
        return new XpsExplicitRouteMatch(
            selected.Entry.ScriptPath,
            selected.Entry.Descriptor.ProcedureName,
            selected.Entry.Descriptor,
            selected.Values);
    }

    private void Refresh(bool force)
    {
        var files = EnumerateSourceFiles().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var fingerprint = Fingerprint(files);
        lock (_gate)
        {
            if (!force && string.Equals(_fingerprint, fingerprint, StringComparison.Ordinal)) return;
        }

        var next = new List<Entry>();
        var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var parser = new XpsWebRouteMetadataParser();
        foreach (var file in files)
        {
            XpsWebRouteParseResult parsed;
            try
            {
                parsed = parser.Parse(File.ReadAllText(file));
            }
            catch (Exception ex)
            {
                throw new XpsWebRouteMetadataException($"Unable to index REST routes in '{Path.GetRelativePath(_root, file)}': {ex.Message}");
            }

            foreach (var descriptor in parsed.Routes.Values.Where(x => x.RouteTemplate is not null))
            {
                foreach (var method in descriptor.Policy.Methods)
                {
                    var key = method.ToUpperInvariant() + " " + CanonicalTemplate(descriptor.RouteTemplate!);
                    if (!duplicates.Add(key))
                        throw new XpsWebRouteMetadataException($"Duplicate REST route '{method} {descriptor.RouteTemplate}' exists across web source files.");
                }
                next.Add(new Entry(file, descriptor));
            }
        }

        lock (_gate)
        {
            _entries = next;
            _fingerprint = fingerprint;
        }
    }

    private IEnumerable<string> EnumerateSourceFiles()
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            MatchCasing = MatchCasing.PlatformDefault
        };
        return Directory.EnumerateFiles(_root, "*.xps", options)
            .Select(Path.GetFullPath)
            .Where(path => IsWithinRoot(path));
    }

    private bool IsWithinRoot(string path)
    {
        var relative = Path.GetRelativePath(_root, path);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string Fingerprint(IEnumerable<string> files)
    {
        var builder = new StringBuilder();
        foreach (var path in files)
        {
            var info = new FileInfo(path);
            builder.Append(path).Append('\0').Append(info.Length).Append('\0').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static string NormalizeRequestPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";
        var value = path.Replace('\\', '/');
        var query = value.IndexOf('?');
        if (query >= 0) value = value[..query];
        if (!value.StartsWith('/')) value = "/" + value;
        return value.Length > 1 ? value.TrimEnd('/') : value;
    }

    private static string CanonicalTemplate(string template)
    {
        var segments = template.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.StartsWith('{') && segment.EndsWith('}') ? "{}" : segment.ToLowerInvariant());
        return "/" + string.Join('/', segments);
    }

    private static bool TryMatchTemplate(string template, string path, out IReadOnlyDictionary<string, string> values)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var templateSegments = template.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (templateSegments.Length != pathSegments.Length)
        {
            values = result;
            return false;
        }

        for (var i = 0; i < templateSegments.Length; i++)
        {
            var templateSegment = templateSegments[i];
            string pathSegment;
            try { pathSegment = Uri.UnescapeDataString(pathSegments[i]); }
            catch (UriFormatException) { values = result; return false; }

            if (templateSegment.StartsWith('{') && templateSegment.EndsWith('}'))
            {
                var name = templateSegment[1..^1];
                result[name] = pathSegment;
                continue;
            }

            if (!templateSegment.Equals(pathSegment, StringComparison.OrdinalIgnoreCase))
            {
                values = result;
                return false;
            }
        }

        values = result;
        return true;
    }

    private sealed record Entry(string ScriptPath, XpsWebRouteDescriptor Descriptor);
}
