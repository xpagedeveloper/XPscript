using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace XPScript.Compiler;

public static class CompilerDaemonClient
{
    private static string StatePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XPScript", "daemon-v1.json");

    public static async Task<int> StatusAsync()
    {
        var hello = await SendAsync("hello").ConfigureAwait(false);
        if (hello is null) { Console.WriteLine("XPScript daemon is not running."); return 0; }
        Console.WriteLine($"XPScript daemon is running. PID {hello.Value.GetProperty("processId").GetInt32()}, protocol {hello.Value.GetProperty("protocol").GetInt32()}.");
        return 0;
    }

    public static async Task<int> QuitAsync()
    {
        var response = await SendAsync("shutdown").ConfigureAwait(false);
        if (response is null) { Console.WriteLine("XPScript daemon is not running."); return 0; }
        Console.WriteLine("XPScript daemon shutdown requested.");
        return 0;
    }

    private static async Task<JsonElement?> SendAsync(string method)
    {
        if (!File.Exists(StatePath)) return null;
        try
        {
            using var state = JsonDocument.Parse(await File.ReadAllTextAsync(StatePath).ConfigureAwait(false));
            var port = state.RootElement.GetProperty("port").GetInt32();
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync("127.0.0.1", port, timeout.Token).ConfigureAwait(false);
            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { id = 1, method })).ConfigureAwait(false);
            var line = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
            if (line is null) return null;
            using var response = JsonDocument.Parse(line);
            if (!response.RootElement.TryGetProperty("result", out var result)) return null;
            return result.Clone();
        }
        catch { TryDeleteState(); return null; }
    }

    internal static async Task WriteStateAsync(int port)
    {
        var directory = Path.GetDirectoryName(StatePath)!;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(StatePath, JsonSerializer.Serialize(new { protocol = CompilerDaemonServer.ProtocolVersion, port, processId = Environment.ProcessId })).ConfigureAwait(false);
    }

    internal static void DeleteState() => TryDeleteState();

    private static void TryDeleteState()
    {
        try { if (File.Exists(StatePath)) File.Delete(StatePath); } catch { }
    }
}
