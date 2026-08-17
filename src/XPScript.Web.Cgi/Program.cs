using System.Collections;
using System.Reflection;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

namespace XPScript.Web.Cgi;

internal static class Program
{
    public static async Task<int> Main()
    {
        var stdout = Console.OpenStandardOutput();
        try
        {
            var environment = ReadEnvironment();
            var root = ResolveRoot(environment);
            var siteId = Value(environment, "XPSCRIPT_SITE_ID")
                         ?? Value(environment, "SERVER_NAME")
                         ?? "cgi-site";
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            var server = new XpsServerInfo(siteId, root, XpsWebHostingMode.Cgi, DateTimeOffset.UtcNow, version);

            await using var dispatcher = new XpsWebDispatcher(root);
            var adapter = new XpsCgiAdapter(new XpsCgiOptions(), server, dispatcher);
            await adapter.RunAsync(Console.OpenStandardInput(), stdout, environment).ConfigureAwait(false);
            return 0;
        }
        catch (XpsCgiException)
        {
            await XpsCgiAdapter.WriteErrorAsync(stdout, 400, "Bad Request").ConfigureAwait(false);
            return 1;
        }
        catch (Exception)
        {
            await XpsCgiAdapter.WriteErrorAsync(stdout, 500, "Internal Server Error").ConfigureAwait(false);
            return 1;
        }
    }

    private static Dictionary<string, string?> ReadEnvironment()
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key)
                result[key] = entry.Value?.ToString();
        }
        return result;
    }

    private static string ResolveRoot(IReadOnlyDictionary<string, string?> environment)
    {
        var configured = Value(environment, "XPSCRIPT_WEB_ROOT") ?? Value(environment, "DOCUMENT_ROOT");
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);

        var script = Value(environment, "SCRIPT_FILENAME");
        if (!string.IsNullOrWhiteSpace(script))
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(script));
            if (!string.IsNullOrWhiteSpace(directory)) return directory;
        }

        throw new XpsCgiException("CGI site root cannot be determined.");
    }

    private static string? Value(IReadOnlyDictionary<string, string?> environment, string name) =>
        environment.TryGetValue(name, out var value) ? value : null;
}
