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
        Func<string, CancellationToken, Task> precompileAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(resolver);
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

            if (!clean.StartsWith('/', StringComparison.Ordinal)) clean = "/" + clean.TrimStart('/');

            XpsRouteResolution resolution;
            try { resolution = resolver.Resolve(clean); }
            catch (XpsWebPathException) { continue; }
            if (!resolution.Found || resolution.ScriptPath is null) continue;
            if (!seen.Add(resolution.ScriptPath)) continue;

            await precompileAsync(resolution.ScriptPath, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsHtml(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        return contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ||
               contentType.StartsWith("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);
    }
}
