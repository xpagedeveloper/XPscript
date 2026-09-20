using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace XPScript.Compiler;

/// <summary>Local loopback compiler host used by IDEs. The host is intentionally non-public-network facing.</summary>
public static class CompilerDaemonServer
{
    public const int ProtocolVersion = 1;

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var port = 0;
        var idleTimeout = TimeSpan.FromMinutes(10);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[++i], out var parsed)) port = parsed;
            else if (args[i] == "--idle-minutes" && i + 1 < args.Length && double.TryParse(args[++i], out var minutes) && minutes > 0) idleTimeout = TimeSpan.FromMinutes(minutes);
            else { Console.Error.WriteLine($"Unknown daemon argument: {args[i]}"); return 1; }
        }

        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        Console.WriteLine(JsonSerializer.Serialize(new { type = "ready", protocol = ProtocolVersion, port = endpoint.Port, processId = Environment.ProcessId }));
        Console.Out.Flush();

        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var lastActivity = DateTimeOffset.UtcNow;
        var activeRequests = 0;
        void Touch() => lastActivity = DateTimeOffset.UtcNow;

        var idleMonitor = Task.Run(async () =>
        {
            while (!shutdown.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), shutdown.Token).ConfigureAwait(false);
                if (Volatile.Read(ref activeRequests) == 0 && DateTimeOffset.UtcNow - lastActivity >= idleTimeout)
                    shutdown.Cancel();
            }
        }, shutdown.Token);

        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(shutdown.Token).ConfigureAwait(false);
                Touch();
                _ = HandleClientAsync(client, Touch, () => Interlocked.Increment(ref activeRequests), () => Interlocked.Decrement(ref activeRequests), shutdown);
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        try { await idleMonitor.ConfigureAwait(false); } catch (OperationCanceledException) { }
        finally { listener.Stop(); }
        return 0;
    }

    private static async Task HandleClientAsync(TcpClient client, Action touch, Action beginRequest, Action endRequest, CancellationTokenSource shutdown)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, leaveOpen: true))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true })
        {
            var driver = new CompilerDriver();
            while (!shutdown.Token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(shutdown.Token).ConfigureAwait(false);
                if (line is null) return;
                try
                {
                    touch();
                    beginRequest();
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    var id = root.TryGetProperty("id", out var idElement) ? idElement.Clone() : default;
                    var method = root.TryGetProperty("method", out var methodElement) ? methodElement.GetString() : null;
                    if (method == "shutdown")
                    {
                        await WriteAsync(writer, id, new { shuttingDown = true }).ConfigureAwait(false);
                        shutdown.Cancel();
                        return;
                    }
                    if (method == "hello")
                    {
                        await WriteAsync(writer, id, new { protocol = ProtocolVersion, processId = Environment.ProcessId }).ConfigureAwait(false);
                        continue;
                    }
                    if (method == "validate")
                    {
                        var source = root.GetProperty("source").GetString() ?? "";
                        var result = await driver.ValidateWithResultAsync(source, cancellationToken: shutdown.Token).ConfigureAwait(false);
                        await WriteAsync(writer, id, result).ConfigureAwait(false);
                        continue;
                    }
                    await WriteErrorAsync(writer, id, $"Unknown daemon method: {method}").ConfigureAwait(false);
                }
                catch (Exception ex) { await writer.WriteLineAsync(JsonSerializer.Serialize(new { error = ex.Message })).ConfigureAwait(false); }
                finally { endRequest(); }
            }
        }
    }

    private static Task WriteAsync(StreamWriter writer, JsonElement id, object result) =>
        writer.WriteLineAsync(JsonSerializer.Serialize(new { id = id.ValueKind == JsonValueKind.Undefined ? null : JsonSerializer.Deserialize<object>(id.GetRawText()), result }));

    private static Task WriteErrorAsync(StreamWriter writer, JsonElement id, string error) =>
        writer.WriteLineAsync(JsonSerializer.Serialize(new { id = id.ValueKind == JsonValueKind.Undefined ? null : JsonSerializer.Deserialize<object>(id.GetRawText()), error }));
}
