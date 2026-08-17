using System.Collections.Concurrent;
using System.Text;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

const int RequestCount = 2000;
const int DegreeOfParallelism = 32;

var parent = Path.Combine(Path.GetTempPath(), "xps-web-soak-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(root);
var scriptPath = Path.Combine(root, "index.xps");
await File.WriteAllTextAsync(scriptPath, """
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write(Request.QueryFirst("id"))
End Sub
""");

var failures = new ConcurrentQueue<string>();
var cache = new XpsWebCompilationCache(new XpsWebCompiler(), new XpsWebCompilationCacheOptions
{
    MaxEntries = 8,
    MaxSourceBytes = 1024 * 1024,
    IdleTtl = TimeSpan.FromMinutes(5),
    FailureBackoff = TimeSpan.FromSeconds(1),
    ConfigurationIdentity = "web-runtime-soak-v1"
});

try
{
    await using var dispatcher = new XpsWebDispatcher(root, cache);

    var warm = await SendAsync(dispatcher, root, "warm");
    ValidateResponse(warm, "warm");
    if (cache.CompilationStarts != 1)
        throw new Exception("Warm-up should compile exactly once. CompilationStarts=" + cache.CompilationStarts);

    await Parallel.ForEachAsync(
        Enumerable.Range(0, RequestCount),
        new ParallelOptions { MaxDegreeOfParallelism = DegreeOfParallelism },
        async (id, cancellationToken) =>
        {
            var expected = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            try
            {
                var response = await SendAsync(dispatcher, root, expected, cancellationToken);
                ValidateResponse(response, expected);
            }
            catch (Exception ex)
            {
                failures.Enqueue($"request={id} type={ex.GetType().Name} message={ex.Message}");
            }
        });

    if (!failures.IsEmpty)
    {
        var sample = string.Join(Environment.NewLine, failures.Take(10));
        throw new Exception($"{failures.Count} soak requests failed.{Environment.NewLine}{sample}");
    }

    if (cache.CompilationStarts != 1)
        throw new Exception("Stable source was recompiled during concurrent soak. CompilationStarts=" + cache.CompilationStarts);

    if (cache.Count != 1)
        throw new Exception("Stable single-route soak should retain exactly one cache entry. Count=" + cache.Count);

    Console.WriteLine($"WEB-RUNTIME-SOAK=OK requests={RequestCount} concurrency={DegreeOfParallelism} compilations={cache.CompilationStarts}");
}
finally
{
    await cache.DisposeAsync();
    Directory.Delete(parent, recursive: true);
}

static async Task<XpsWebResponse> SendAsync(
    IXpsWebRequestHandler handler,
    string root,
    string id,
    CancellationToken cancellationToken = default)
{
    var request = new XpsWebRequest(
        "GET",
        "/",
        string.Empty,
        "id=" + Uri.EscapeDataString(id),
        new Dictionary<string, IReadOnlyList<string>>(),
        null,
        0,
        ReadOnlyMemory<byte>.Empty,
        "localhost",
        "http",
        "127.0.0.1",
        "HTTP/1.1",
        new Dictionary<string, string>(),
        cancellationToken);
    var response = new XpsWebResponse();
    var context = new XpsWebContext(
        request,
        response,
        new XpsServerInfo("web-runtime-soak", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        new XpsWebPrincipal(false),
        new XpsApplicationState());
    await handler.HandleAsync(context);
    return response;
}

static void ValidateResponse(XpsWebResponse response, string expected)
{
    if (response.StatusCode != 200)
        throw new Exception($"Expected HTTP 200, got {response.StatusCode}.");
    if (!response.Completed)
        throw new Exception("Response was not completed.");
    var body = Encoding.UTF8.GetString(response.Body.Span);
    if (!string.Equals(body, expected, StringComparison.Ordinal))
        throw new Exception($"Expected body '{expected}', got '{body}'.");
}
