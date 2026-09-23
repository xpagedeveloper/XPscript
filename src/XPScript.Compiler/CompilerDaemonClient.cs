using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace XPScript.Compiler;

public static class CompilerDaemonClient
{
    private static readonly SemaphoreSlim StartupGate = new(1, 1);
    private static string StateDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XPScript");
    private static string StatePath => Path.Combine(StateDirectory, "daemon-v1.json");
    private static string StartupLockPath => Path.Combine(StateDirectory, "daemon-v1.lock");

    public static async Task<bool> EnsureRunningAsync(CancellationToken cancellationToken = default)
    {
        var hello = await SendAsync("hello").ConfigureAwait(false);
        if (IsCompatible(hello)) return true;

        await StartupGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller in this process may have started the daemon while we waited.
            hello = await SendAsync("hello").ConfigureAwait(false);
            if (IsCompatible(hello)) return true;

            Directory.CreateDirectory(StateDirectory);
            await using var startupLock = await AcquireStartupLockAsync(cancellationToken).ConfigureAwait(false);

            // A separate xpscript process may have started the daemon while this process
            // was waiting for the cross-process lock.
            hello = await SendAsync("hello").ConfigureAwait(false);
            if (IsCompatible(hello)) return true;

            TryDeleteState();
            var authToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)) return false;
        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var entryAssembly = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(entryAssembly)) return false;
            startInfo.ArgumentList.Add(entryAssembly);
        }
        startInfo.Environment["XPSCRIPT_DAEMON_TOKEN"] = authToken;
        startInfo.ArgumentList.Add("daemon");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add("0");
        using var process = Process.Start(startInfo);
        if (process is null) return false;

        // The daemon is long-lived while this Process wrapper is intentionally short-lived.
        // Drain stderr for the daemon lifetime so a redirected pipe can never fill and block it.
        _ = DrainAsync(process.StandardError);

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
                    // Ready is the daemon's only stdout protocol frame. Continue draining stdout
                    // after startup before returning control to the foreground CLI.
                    _ = DrainAsync(process.StandardOutput);
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
        finally
        {
            StartupGate.Release();
        }
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

    public static async Task<int> RestartAsync()
    {
        await SendAsync("shutdown").ConfigureAwait(false);

        // Shutdown is asynchronous. Wait until the old daemon has released its
        // discovery state before starting the replacement.
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (await SendAsync("hello").ConfigureAwait(false) is null) break;
            await Task.Delay(50).ConfigureAwait(false);
        }

        if (!await EnsureRunningAsync().ConfigureAwait(false))
        {
            Console.Error.WriteLine("Unable to restart the XPScript daemon.");
            return 1;
        }

        var hello = await SendAsync("hello").ConfigureAwait(false);
        if (!IsCompatible(hello))
        {
            Console.Error.WriteLine("XPScript daemon restarted but did not respond with a compatible protocol.");
            return 1;
        }

        Console.WriteLine($"XPScript daemon restarted. PID {hello!.Value.GetProperty("processId").GetInt32()}, protocol {hello.Value.GetProperty("protocol").GetInt32()}.");
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
            var token = state.RootElement.TryGetProperty("token", out var tokenElement) ? tokenElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(token)) { TryDeleteState(); return null; }
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(timeoutDuration);
            await client.ConnectAsync("127.0.0.1", port, timeout.Token).ConfigureAwait(false);
            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { id = 1, method, token, @params = parameters })).ConfigureAwait(false);
            var line = await reader.ReadLineAsync(timeout.Token).ConfigureAwait(false);
            if (line is null) return null;
            using var response = JsonDocument.Parse(line);
            if (response.RootElement.TryGetProperty("error", out var error))
                throw new CompilerDaemonException(error.GetString() ?? "Compiler daemon request failed.");
            if (!response.RootElement.TryGetProperty("result", out var result)) return null;
            return result.Clone();
        }
        catch (JsonException)
        {
            TryDeleteState();
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (SocketException)
        {
            // A concurrent cold-start client may observe the newly written state file
            // before the winning daemon is accepting connections. Only remove state
            // when its recorded owner is definitely gone; otherwise another process
            // owns it and may become reachable momentarily.
            TryDeleteStateIfOwnerExited();
            return null;
        }
        catch (OperationCanceledException)
        {
            // A request timeout does not prove the daemon is stale. In particular,
            // compilation can legitimately outlive a client-side timeout.
            return null;
        }
        catch (CompilerDaemonException)
        {
            throw;
        }
        catch (IOException)
        {
            // The daemon may still be alive after a transient stream failure.
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        try
        {
            var buffer = new char[4096];
            while (await reader.ReadAsync(buffer.AsMemory()).ConfigureAwait(false) > 0) { }
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
    }

    private static async Task<FileStream> AcquireStartupLockAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(StartupLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException)
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    internal static async Task WriteStateAsync(int port, string token)
    {
        var directory = Path.GetDirectoryName(StatePath)!;
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(StatePath, JsonSerializer.Serialize(new { protocol = CompilerDaemonServer.ProtocolVersion, port, processId = Environment.ProcessId, token })).ConfigureAwait(false);
        RestrictStateFilePermissions();
    }

    private static void RestrictStateFilePermissions()
    {
        if (OperatingSystem.IsWindows()) return;
        try
        {
            File.SetUnixFileMode(StatePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (PlatformNotSupportedException) { }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    internal static void DeleteStateIfOwned(int port, string token)
    {
        try
        {
            if (!File.Exists(StatePath)) return;
            using var state = JsonDocument.Parse(File.ReadAllText(StatePath));
            var root = state.RootElement;
            if (root.GetProperty("processId").GetInt32() != Environment.ProcessId) return;
            if (root.GetProperty("port").GetInt32() != port) return;
            if (!root.TryGetProperty("token", out var tokenElement) ||
                !string.Equals(tokenElement.GetString(), token, StringComparison.Ordinal)) return;
            File.Delete(StatePath);
        }
        catch { }
    }

    private static void TryDeleteStateIfOwnerExited()
    {
        try
        {
            if (!File.Exists(StatePath)) return;
            using var state = JsonDocument.Parse(File.ReadAllText(StatePath));
            if (!state.RootElement.TryGetProperty("processId", out var processIdElement)) return;
            var processId = processIdElement.GetInt32();
            try
            {
                using var owner = Process.GetProcessById(processId);
                if (!owner.HasExited) return;
            }
            catch (ArgumentException)
            {
                // No process with the recorded id exists.
            }
            TryDeleteState();
        }
        catch (JsonException) { TryDeleteState(); }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (InvalidOperationException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDeleteState()
    {
        try { if (File.Exists(StatePath)) File.Delete(StatePath); } catch { }
    }
}


public sealed class CompilerDaemonException : Exception
{
    public CompilerDaemonException(string message) : base(message) { }
}
