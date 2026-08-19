using System.Text;
using System.Text.RegularExpressions;
using XPScript.Web.Runtime;

namespace XPScript.Web.Compiler;

internal static partial class XpsWebLinkedPrecompiler
{
    private const int MaxScanBytes = 2 * 1024 * 1024;
    private const int MaxLinkedScriptsPerScan = 64;

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
        if (response.Body.IsEmpty || response.Body.Length > MaxScanBytes) return;
        if (!IsHtml(response.ContentType)) return;

        string html;
        try { html = Encoding.UTF8.GetString(response.Body.Span); }
        catch { return; }

        await PrecompileLinksFromTextAsync(
            html,
            resolver,
            sourceScriptPath,
            precompileAsync,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task PrecompileSourceLinksAsync(
        string sourceScriptPath,
        XpsWebPathResolver resolver,
        Func<string, CancellationToken, Task> precompileAsync,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceScriptPath);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(precompileAsync);

        FileInfo info;
        try { info = new FileInfo(sourceScriptPath); }
        catch { return; }
        if (!info.Exists || info.Length <= 0 || info.Length > MaxScanBytes) return;

        string source;
        try { source = await File.ReadAllTextAsync(sourceScriptPath, cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException) { return; }

        // XPscript string literals escape a quote as two double-quotes. Collapsing those
        // for discovery lets href=""page.xps"" be treated like href="page.xps".
        source = source.Replace("\"\"", "\"", StringComparison.Ordinal);

        await PrecompileLinksFromTextAsync(
            source,
            resolver,
            sourceScriptPath,
            precompileAsync,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task PrecompileLinksFromTextAsync(
        string text,
        XpsWebPathResolver resolver,
        string sourceScriptPath,
        Func<string, CancellationToken, Task> precompileAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceScriptPath);
        ArgumentNullException.ThrowIfNull(precompileAsync);
        if (string.IsNullOrEmpty(text)) return;

        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (Match match in XpsLinkRegex().Matches(text))
        {
            if (seen.Count >= MaxLinkedScriptsPerScan) break;
            cancellationToken.ThrowIfCancellationRequested();

            var raw = match.Groups["url"].Value.Trim();
            if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("//", StringComparison.Ordinal)) continue;

            var rootRelative = raw.StartsWith("/", StringComparison.Ordinal) || raw.StartsWith('\\');
            if (!rootRelative && Uri.TryCreate(raw, UriKind.Absolute, out _)) continue;

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
            if (link.StartsWith("/", StringComparison.Ordinal) || link.StartsWith('\\'))
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
