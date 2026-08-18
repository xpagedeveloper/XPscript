using System.Text;
using System.Text.RegularExpressions;
using XPScript.Web.Runtime;

namespace XPScript.Web.Compiler;

internal static partial class XpsWebLinkedPrecompiler
{
    private const int MaxResponseScanBytes = 2 * 1024 * 1024;
    private const int MaxLinkedScriptsPerResponse = 64;

    [GeneratedRegex("(?is)(?:href|src)\\s*=\\s*[\\\"'](?<url>[^\\\"']+\\.xps(?:[?#][^\\\"']*)?)[\\\"']", RegexOptions.CultureInvariant)]
    private static partial Regex XpsLinkRegex();

    public static async Task PrecompileResponseLinksAsync(
        XpsWebResponse response,
        XpsWebPathResolver resolver,
        string sourceScriptPath,
        Func<string, CancellationToken, Task> precompileAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceScriptPath);
        ArgumentNullException.ThrowIfNull(precompileAsync);

        if (response.Body.IsEmpty || response.Body.Length > MaxResponseScanBytes) return;
        if (!IsHtml(response.ContentType)) return;

        string html;
        try { html = Encoding.UTF8.GetString(response.Body.Span); }
        catch { return; }

        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (Match match in XpsLinkRegex().Matches(html))
        {
            if (seen.Count >= MaxLinkedScriptsPerResponse) break;
            cancellationToken.ThrowIfCancellationRequested();

            var raw = match.Groups["url"].Value.Trim();
            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("//", StringComparison.Ordinal)) continue;
            if (Uri.TryCreate(raw, UriKind.Absolute, out _)) continue;

            var clean = raw;
            var cut = clean.IndexOfAny(['?', '#']);
            if (cut >= 0) clean = clean[..cut];
            if (!clean.EndsWith(".xps", StringComparison.OrdinalIgnoreCase)) continue;

            var scriptPath = ResolveLinkedScript(resolver, sourceScriptPath, clean);
            if (scriptPath is null || !seen.Add(scriptPath)) continue;

            await precompileAsync(scriptPath, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string? ResolveLinkedScript(XpsWebPathResolver resolver, string sourceScriptPath, string link)
    {
        try
        {
            if (link.StartsWith('/', StringComparison.Ordinal) || link.StartsWith('\\'))
            {
                var route = resolver.Resolve('/' + link.TrimStart('/', '\\').Replace('\\', '/'));
                return route.Found ? route.ScriptPath : null;
            }

            var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceScriptPath));
            if (sourceDirectory is null) return null;
            var normalized = link.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(sourceDirectory, normalized));
            var relative = Path.GetRelativePath(resolver.Root, candidate);
            if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return null;
            if (!Path.GetExtension(candidate).Equals(".xps", StringComparison.OrdinalIgnoreCase)) return null;
            return File.Exists(candidate) ? candidate : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or XpsWebPathException)
        {
            return null;
        }
    }

    private static bool IsHtml(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        return contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);
    }
}
