using System.Globalization;
using System.Net;
using System.Net.Sockets;
using XPScript.Web.Compiler;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

var parent = Path.Combine(Path.GetTempPath(), "xps-cache-metrics-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(parent);
await File.WriteAllTextAsync(Path.Combine(parent, "index.xps"), """
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write("INDEX")
End Sub
""");
await File.WriteAllTextAsync(Path.Combine(parent, "second.xps"), """
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write("SECOND")
End Sub
""");

var port = GetFreePort();
await using var dispatcher = new XpsWebDispatcher(parent, new XpsWebCompilationCacheOptions
{
    MaxEntries = 1,
    IdleTtl = TimeSpan.FromMinutes(5)
});
var options = new XpsKestrelOptions
{
    Address = IPAddress.Loopback,
    Port = port,
    EnableMetricsEndpoint = true
};
var server = new XpsServerInfo(
    "cache-metrics-test",
    parent,
    XpsWebHostingMode.Kestrel,
    DateTimeOffset.UtcNow,
    "test",
    IPAddress.Loopback.ToString(),
    port);
var app = XpsKestrelAdapter.Build(options, server, dispatcher);

try
{
    await app.StartAsync();
    using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

    await AssertBodyAsync(client, "/", "INDEX");
    await AssertBodyAsync(client, "/", "INDEX");
    await AssertBodyAsync(client, "/second", "SECOND");

    using var response = await client.GetAsync(options.MetricsPath);
    response.EnsureSuccessStatusCode();
    var metrics = await response.Content.ReadAsStringAsync();

    AssertMetric(metrics, "xpscript_web_cache_entries", 1);
    AssertMetric(metrics, "xpscript_web_cache_hits_total", 1);
    AssertMetric(metrics, "xpscript_web_cache_misses_total", 2);
    AssertMetric(metrics, "xpscript_web_compilations_total", 2);
    AssertMetric(metrics, "xpscript_web_compilation_failures_total", 0);
    AssertMetricAtLeast(metrics, "xpscript_web_cache_evictions_total", 1);
    AssertMetricAtLeast(metrics, "xpscript_web_compilation_duration_seconds_total", 0);

    Console.WriteLine("WEB-CACHE-METRICS-SMOKE=OK");
}
finally
{
    await app.StopAsync();
    await app.DisposeAsync();
    Directory.Delete(parent, recursive: true);
}

static async Task AssertBodyAsync(HttpClient client, string path, string expected)
{
    using var response = await client.GetAsync(path);
    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadAsStringAsync();
    if (body != expected) throw new Exception($"Unexpected body for {path}: {body}");
}

static void AssertMetric(string metrics, string name, long expected)
{
    var actual = ReadMetric(metrics, name);
    if (actual != expected) throw new Exception($"Metric {name} expected {expected} but was {actual}. Metrics={Escape(metrics)}");
}

static void AssertMetricAtLeast(string metrics, string name, double minimum)
{
    var actual = ReadMetric(metrics, name);
    if (actual < minimum) throw new Exception($"Metric {name} expected at least {minimum} but was {actual}. Metrics={Escape(metrics)}");
}

static double ReadMetric(string metrics, string name)
{
    foreach (var line in metrics.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (!line.StartsWith(name + " ", StringComparison.Ordinal)) continue;
        var value = line[(name.Length + 1)..];
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        throw new Exception("Invalid metric value for " + name + ": " + value);
    }
    throw new Exception("Metric not found: " + name + ". Metrics=" + Escape(metrics));
}

static string Escape(string value) => value.Replace("\n", "\\n", StringComparison.Ordinal);

static int GetFreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}
