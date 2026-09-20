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
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[++i], out var parsed)) port = parsed;
            else { Console.Error.WriteLine($"Unknown daemon argument: {args[i]}"); return 1; }
        }

        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        Console.WriteLine(JsonSerializer.Serialize(new { type = "ready", protocol = ProtocolVersion, port = endpoint.Port, processId = Environment.ProcessId }));
        Console.Out.Flush();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { listener.Stop(); }
        return 0;
    }

    private static async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, leaveOpen: true))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true })
        {
            var driver = new CompilerDriver();
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null) return;
                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    var id = root.TryGetProperty("id", out var idElement) ? idElement.Clone() : default;
                    var method = root.TryGetProperty("method", out var methodElement) ? methodElement.GetString() : null;
                    if (method == "hello")
                    {
                        await WriteAsync(writer, id, new { protocol = ProtocolVersion, processId = Environment.ProcessId }).ConfigureAwait(false);
                        continue;
                    }
                    if (method == "validate")
                    {
                        var source = root.GetProperty("source").GetString() ?? "";
                        var result = await driver.ValidateWithResultAsync(source, cancellationToken: cancellationToken).ConfigureAwait(false);
                        await WriteAsync(writer, id, result).ConfigureAwait(false);
                        continue;
                    }
                    await WriteErrorAsync(writer, id, $"Unknown daemon method: {method}").ConfigureAwait(false);
                }
                catch (Exception ex) { await writer.WriteLineAsync(JsonSerializer.Serialize(new { error = ex.Message })).ConfigureAwait(false); }
            }
        }
    }

    private static Task WriteAsync(StreamWriter writer, JsonElement id, object result) =>
        writer.WriteLineAsync(JsonSerializer.Serialize(new { id = id.ValueKind == JsonValueKind.Undefined ? null : JsonSerializer.Deserialize<object>(id.GetRawText()), result }));

    private static Task WriteErrorAsync(StreamWriter writer, JsonElement id, string error) =>
        writer.WriteLineAsync(JsonSerializer.Serialize(new { id = id.ValueKind == JsonValueKind.Undefined ? null : JsonSerializer.Deserialize<object>(id.GetRawText()), error }));
}
