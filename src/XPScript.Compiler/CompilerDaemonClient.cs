using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace XPScript.Compiler;

public static class CompilerDaemonClient
{
    private static string StatePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XPScript", "daemon-v1.json");

    public static async Task<bool> EnsureRunningAsync(CancellationToken cancellationToken = default)
    {
        var hello = await SendAsync("hello").ConfigureAwait(false);
        if (IsCompatible(hello)) return true;

        TryDeleteState();
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)) return false;
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("daemon");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add("0");
        using var process = Process.Start(startInfo);
        if (process is null) return false;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            while (!timeout.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(timeout.Token).ConfigureAwait(false);
                if (line is null) break;
                using var ready = JsonDocument.Parse(line);
                if (ready.RootElement.TryGetProperty("type", out var type) && type.GetString() == "ready")
                {
                    for (var attempt = 0; attempt < 20; attempt++)
                    {
                        hello = await SendAsync("hello").ConfigureAwait(false);
                        if (IsCompatible(hello)) return true;
                        await Task.Delay(50, timeout.Token).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        return false;
    }

    public static async Task<CompileResult?> CompileForRunAsync(
        string sourcePath,
        string outputDirectory,
        string runtimeIdentifier,
        bool debug,
        ApplicationSecurityMode securityMode,
        bool restricted,
        IReadOnlyList<string> sourceRoots,
        IReadOnlyList<string> sourcePreprocessors)
    {
        var response = await SendAsync("compileRun", new
        {
            source = sourcePath,
            outputDirectory,
            runtimeIdentifier,
            debug,
            securityMode = securityMode.ToString(),
            restricted,
            sourceRoots,
            sourcePreprocessors
        }, TimeSpan.FromMinutes(5)).ConfigureAwait(false);
        return response is null
            ? null
            : response.Value.Deserialize<CompileResult>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static bool IsCompatible(JsonElement? hello) =>
        hello is { } value &&
        value.TryGetProperty("protocol", out var protocol) &&
        protocol.GetInt32() == CompilerDaemonServer.ProtocolVersion;

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

    private static Task<JsonElement?> SendAsync(string method) => SendAsync(method, null, TimeSpan.FromSeconds(2));

    private static Task<JsonElement?> SendAsync(string method, object? parameters) => SendAsync(method, parameters, TimeSpan.FromSeconds(2));

    private static async Task<JsonElement?> SendAsync(string method, object? parameters, TimeSpan timeoutDuration)
    {
        if (!File.Exists(StatePath)) return null;
        try
        {
            using var state = JsonDocument.Parse(await File.ReadAllTextAsync(StatePath).ConfigureAwait(false));
            var port = state.RootElement.GetProperty("port").GetInt32();
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(timeoutDuration);
            await client.ConnectAsync("127.0.0.1", port, timeout.Token).ConfigureAwait(false);
            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { id = 1, method, @params = parameters })).ConfigureAwait(false);
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
