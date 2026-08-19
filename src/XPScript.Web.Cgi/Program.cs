using System.Collections;
using System.Reflection;
using System.Text.Json;
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
            NormalizeInterpreterEnvironment(environment, root);
            var siteId = Value(environment, "XPSCRIPT_SITE_ID")
                         ?? Value(environment, "SERVER_NAME")
                         ?? "cgi-site";
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            var server = new XpsServerInfo(siteId, root, XpsWebHostingMode.Cgi, DateTimeOffset.UtcNow, version);

            await using var dispatcher = new XpsWebDispatcher(root);
            var stateRoot = ResolveSessionFolder(environment, root);
            if (!string.IsNullOrWhiteSpace(stateRoot))
            {
                await using var state = await XpsCgiPersistentState.OpenAsync(stateRoot, siteId).ConfigureAwait(false);
                var adapter = new XpsCgiAdapter(
                    new XpsCgiOptions(),
                    server,
                    dispatcher,
                    application: state.Application,
                    sessionFactory: state.BindSession);
                await adapter.RunAsync(Console.OpenStandardInput(), stdout, environment).ConfigureAwait(false);
            }
            else
            {
                var adapter = new XpsCgiAdapter(new XpsCgiOptions(), server, dispatcher);
                await adapter.RunAsync(Console.OpenStandardInput(), stdout, environment).ConfigureAwait(false);
            }
            return 0;
        }
        catch (XpsCgiException ex)
        {
            Console.Error.WriteLine(ex);
            await XpsCgiAdapter.WriteErrorAsync(stdout, 400, "Bad Request").ConfigureAwait(false);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            await XpsCgiAdapter.WriteErrorAsync(stdout, 500, "Internal Server Error").ConfigureAwait(false);
            return 1;
        }
    }


    private static string? ResolveSessionFolder(IReadOnlyDictionary<string,string?> environment, string root)
    {
        var env=Value(environment,"XPSCRIPT_SESSION_FOLDER");
        if(!string.IsNullOrWhiteSpace(env)) return Path.GetFullPath(env,root);
        var cfg=Value(environment,"XPSCRIPT_CONFIG");
        if(string.IsNullOrWhiteSpace(cfg)) cfg=Path.Combine(root,"web.cfg"); else cfg=Path.GetFullPath(cfg,root);
        if(!File.Exists(cfg)) return null;
        using var doc=JsonDocument.Parse(File.ReadAllText(cfg),new JsonDocumentOptions{AllowTrailingCommas=true,CommentHandling=JsonCommentHandling.Skip});
        if(!doc.RootElement.TryGetProperty("cgi",out var c)||c.ValueKind!=JsonValueKind.Object) return null;
        if(!c.TryGetProperty("sessionFolder",out var f)||f.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(f.GetString())) return null;
        return Path.GetFullPath(f.GetString()!,Path.GetDirectoryName(Path.GetFullPath(cfg))??root);
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

    private static void NormalizeInterpreterEnvironment(Dictionary<string, string?> environment, string root)
    {
        var pathInfo = Value(environment, "PATH_INFO");
        if (string.IsNullOrWhiteSpace(pathInfo) || pathInfo == "/") return;

        var requestPath = pathInfo.StartsWith("/", StringComparison.Ordinal) ? pathInfo : "/" + pathInfo;
        var relative = requestPath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        environment["SCRIPT_NAME"] = requestPath;
        environment["PATH_INFO"] = string.Empty;
        environment["SCRIPT_FILENAME"] = Path.Combine(root, relative);
    }

    private static string? Value(IReadOnlyDictionary<string, string?> environment, string name) =>
        environment.TryGetValue(name, out var value) ? value : null;
}
