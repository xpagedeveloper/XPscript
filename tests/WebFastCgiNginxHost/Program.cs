using System.Net;
using XPScript.Web.Compiler;
using XPScript.Web.FastCgi;
using XPScript.Web.Runtime;

if (args.Length != 2 || !int.TryParse(args[1], out var port))
{
    Console.Error.WriteLine("Usage: WebFastCgiNginxHost <root> <port>");
    return 2;
}

var root = Path.GetFullPath(args[0]);
Directory.CreateDirectory(root);
await using var dispatcher = new XpsWebDispatcher(root, new XpsWebCompilationCacheOptions
{
    MaxEntries = 16,
    MaxSourceBytes = 1024 * 1024,
    IdleTtl = TimeSpan.FromMinutes(5),
    FailureBackoff = TimeSpan.FromSeconds(1),
    ConfigurationIdentity = "nginx-integration-v1"
});
var server = new XpsServerInfo("nginx-integration", root, XpsWebHostingMode.FastCgi, DateTimeOffset.UtcNow, "test", "127.0.0.1", port);
await using var adapter = new XpsFastCgiAdapter(new XpsFastCgiOptions
{
    Address = IPAddress.Loopback,
    Port = port,
    MaxConcurrentConnections = 32,
    MaxRequestBodyBytes = 1024 * 1024
}, server, dispatcher);

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

await adapter.StartAsync(shutdown.Token);
Console.WriteLine("XPS-FASTCGI-NGINX-HOST=READY");
Console.Out.Flush();

try
{
    await Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token);
}
catch (OperationCanceledException)
{
}

await adapter.StopAsync();
return 0;
