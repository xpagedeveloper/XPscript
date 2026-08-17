using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using XPScript.Web.FastCgi;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

await VerifyKestrelAsync();
await VerifyFastCgiTcpAsync();
if (!OperatingSystem.IsWindows()) await VerifyFastCgiUnixAsync();
Console.WriteLine("WEB-GRACEFUL-SHUTDOWN=OK");

static async Task VerifyKestrelAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "xps-shutdown-kestrel-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var handler = new BlockingHandler();
    var options = new XpsKestrelOptions
    {
        Port = 0,
        AllowedHosts = ["localhost", "127.0.0.1", "::1"]
    };
    var serverInfo = new XpsServerInfo("shutdown-kestrel", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");
    var app = XpsKestrelAdapter.Build(options, serverInfo, handler);
    try
    {
        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
            ?? throw new Exception("Kestrel did not expose an address.");
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        var requestTask = client.GetAsync("/wait");
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        using var shutdownTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var stopTask = app.StopAsync(shutdownTimeout.Token);
        await Task.Delay(200);
        if (stopTask.IsCompleted)
            throw new Exception("Kestrel shutdown completed before the in-flight request was released.");

        handler.Release.TrySetResult();
        using var response = await requestTask.WaitAsync(TimeSpan.FromSeconds(10));
        if (response.StatusCode != HttpStatusCode.OK)
            throw new Exception("In-flight Kestrel request did not complete successfully during graceful shutdown.");
        await stopTask.WaitAsync(TimeSpan.FromSeconds(10));
    }
    finally
    {
        handler.Release.TrySetResult();
        await app.DisposeAsync();
        Directory.Delete(root, recursive: true);
    }
}

static async Task VerifyFastCgiTcpAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "xps-shutdown-fcgi-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var serverInfo = new XpsServerInfo("shutdown-fcgi", root, XpsWebHostingMode.FastCgi, DateTimeOffset.UtcNow, "test");
    await using var adapter = new XpsFastCgiAdapter(new XpsFastCgiOptions { Address = IPAddress.Loopback, Port = 0 }, serverInfo, new NoOpHandler());
    try
    {
        await adapter.StartAsync();
        var endpoint = adapter.LocalEndpoint ?? throw new Exception("FastCGI TCP listener did not expose an endpoint.");
        using var client = new TcpClient();
        await client.ConnectAsync(endpoint.Address, endpoint.Port);
        await Task.Delay(100);
        await adapter.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await adapter.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));

        using var probe = new TcpClient();
        try
        {
            await probe.ConnectAsync(endpoint.Address, endpoint.Port).WaitAsync(TimeSpan.FromSeconds(2));
            throw new Exception("FastCGI TCP listener still accepted connections after shutdown.");
        }
        catch (SocketException) { }
        catch (TimeoutException) { }
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static async Task VerifyFastCgiUnixAsync()
{
    var root = Path.Combine(Path.GetTempPath(), "xps-shutdown-unix-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    var socketPath = Path.Combine("/tmp", "xps-" + Guid.NewGuid().ToString("N")[..12] + ".sock");
    var serverInfo = new XpsServerInfo("shutdown-unix", root, XpsWebHostingMode.FastCgi, DateTimeOffset.UtcNow, "test");
    await using var adapter = new XpsFastCgiAdapter(new XpsFastCgiOptions { Address = IPAddress.Loopback, Port = 0 }, serverInfo, new NoOpHandler());
    await using var listener = new XpsFastCgiUnixSocketListener(adapter, new XpsFastCgiUnixSocketOptions { SocketPath = socketPath });
    try
    {
        await listener.StartAsync();
        if (!File.Exists(socketPath)) throw new Exception("FastCGI Unix socket file was not created.");

        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await client.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
        await Task.Delay(100);
        await listener.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await listener.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
        if (File.Exists(socketPath)) throw new Exception("FastCGI Unix socket file remained after shutdown.");
    }
    finally
    {
        try { if (File.Exists(socketPath)) File.Delete(socketPath); } catch { }
        Directory.Delete(root, recursive: true);
    }
}

sealed class BlockingHandler : IXpsWebRequestHandler
{
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task HandleAsync(XpsWebContext context)
    {
        Started.TrySetResult();
        await Release.Task;
        context.Response.StatusCode = 200;
        context.Response.Write("ok");
    }
}

sealed class NoOpHandler : IXpsWebRequestHandler
{
    public Task HandleAsync(XpsWebContext context)
    {
        context.Response.StatusCode = 200;
        return Task.CompletedTask;
    }
}
