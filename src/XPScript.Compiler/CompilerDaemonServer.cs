using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace XPScript.Compiler;

/// <summary>Local loopback compiler host used by IDEs. The host is intentionally non-public-network facing.</summary>
public static class CompilerDaemonServer
{
    public const int ProtocolVersion = 1;
    private static readonly SemaphoreSlim CompileGate = new(1, 1);

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

        var authToken = Environment.GetEnvironmentVariable("XPSCRIPT_DAEMON_TOKEN");
        if (string.IsNullOrWhiteSpace(authToken))
        {
            Console.Error.WriteLine("Compiler daemon requires XPSCRIPT_DAEMON_TOKEN.");
            return 1;
        }

        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        Console.WriteLine(JsonSerializer.Serialize(new { type = "ready", protocol = ProtocolVersion, port = endpoint.Port, processId = Environment.ProcessId }));
        Console.Out.Flush();
        await CompilerDaemonClient.WriteStateAsync(endpoint.Port, authToken).ConfigureAwait(false);

        using var shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var lastActivityTicks = DateTimeOffset.UtcNow.UtcTicks;
        var activeRequests = 0;
        void Touch() => Interlocked.Exchange(ref lastActivityTicks, DateTimeOffset.UtcNow.UtcTicks);

        var idleMonitor = Task.Run(async () =>
        {
            while (!shutdown.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), shutdown.Token).ConfigureAwait(false);
                var lastActivity = new DateTimeOffset(Interlocked.Read(ref lastActivityTicks), TimeSpan.Zero);
                if (Volatile.Read(ref activeRequests) == 0 && DateTimeOffset.UtcNow - lastActivity >= idleTimeout)
                    shutdown.Cancel();
            }
        }, shutdown.Token);

        var clientTasks = new HashSet<Task>();
        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(shutdown.Token).ConfigureAwait(false);
                Touch();
                var clientTask = HandleClientAsync(client, Touch, () => Interlocked.Increment(ref activeRequests), () => Interlocked.Decrement(ref activeRequests), authToken, shutdown);
                lock (clientTasks) clientTasks.Add(clientTask);
                _ = clientTask.ContinueWith(completed =>
                {
                    lock (clientTasks) clientTasks.Remove(completed);
                    _ = completed.Exception;
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        try { await idleMonitor.ConfigureAwait(false); } catch (OperationCanceledException) { }
        Task[] pendingClients;
        lock (clientTasks) pendingClients = clientTasks.ToArray();
        try { await Task.WhenAll(pendingClients).ConfigureAwait(false); } catch (OperationCanceledException) { } catch { }
        finally { listener.Stop(); CompilerDaemonClient.DeleteStateIfOwned(endpoint.Port, authToken); }
        return 0;
    }

    private static async Task HandleClientAsync(TcpClient client, Action touch, Action beginRequest, Action endRequest, string authToken, CancellationTokenSource shutdown)
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
                var requestStarted = false;
                JsonElement id = default;
                try
                {
                    touch();
                    beginRequest();
                    requestStarted = true;
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    id = root.TryGetProperty("id", out var idElement) ? idElement.Clone() : default;
                    var method = root.TryGetProperty("method", out var methodElement) ? methodElement.GetString() : null;
                    var requestToken = root.TryGetProperty("token", out var tokenElement) ? tokenElement.GetString() : null;
                    if (!string.Equals(requestToken, authToken, StringComparison.Ordinal))
                    {
                        await WriteErrorAsync(writer, id, "Unauthorized daemon request.").ConfigureAwait(false);
                        continue;
                    }
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
                    if (method == "compileRun")
                    {
                        var parameters = root.GetProperty("params");
                        var source = parameters.GetProperty("source").GetString() ?? "";
                        var outputDirectory = parameters.GetProperty("outputDirectory").GetString() ?? "";
                        var runtimeIdentifier = parameters.GetProperty("runtimeIdentifier").GetString() ?? "";
                        var debug = parameters.TryGetProperty("debug", out var debugElement) && debugElement.GetBoolean();
                        var restricted = parameters.TryGetProperty("restricted", out var restrictedElement) && restrictedElement.GetBoolean();
                        var securityText = parameters.TryGetProperty("securityMode", out var securityElement) ? securityElement.GetString() : null;
                        var securityMode = Enum.TryParse<ApplicationSecurityMode>(securityText, true, out var parsedSecurity)
                            ? parsedSecurity
                            : ApplicationSecurityMode.Off;
                        var sourceRoots = parameters.TryGetProperty("sourceRoots", out var rootsElement)
                            ? rootsElement.EnumerateArray().Select(value => value.GetString() ?? "").Where(value => value.Length > 0).ToArray()
                            : [];
                        var sourcePreprocessors = parameters.TryGetProperty("sourcePreprocessors", out var preprocessorsElement)
                            ? preprocessorsElement.EnumerateArray().Select(value => value.GetString() ?? "").Where(value => value.Length > 0).ToArray()
                            : [];

                        // Compiler request configuration is AsyncLocal-scoped, but the complete
                        // run compiler pipeline has not yet been proven safe for concurrent writes,
                        // Roslyn/MSBuild execution, and dependency staging. Serialize compileRun
                        // requests so multiple IDE/CLI clients can safely share one daemon.
                        await CompileGate.WaitAsync(shutdown.Token).ConfigureAwait(false);
                        try
                        {
                            using var securityScope = ApplicationSecurityModeContext.Push(securityMode);
                            using var diagnosticMode = CompilerDiagnosticMode.Push(debug);
                            using var preprocessorScope = SourcePreprocessorConfigurationContext.Push(sourcePreprocessors);
                            using var includeScope = restricted ? IncludeSecurityContext.Push(sourceRoots) : null;
                            var result = await RunCompiler.CompileWithResultAsync(source, outputDirectory, runtimeIdentifier, debug, shutdown.Token).ConfigureAwait(false);
                            await WriteAsync(writer, id, result).ConfigureAwait(false);
                        }
                        finally
                        {
                            CompileGate.Release();
                        }
                        continue;
                    }
                    if (method == "validate")
                    {
                        var source = root.GetProperty("source").GetString() ?? "";
                        var result = await driver.ValidateWithResultAsync(source).ConfigureAwait(false);
                        await WriteAsync(writer, id, result).ConfigureAwait(false);
                        continue;
                    }
                    await WriteErrorAsync(writer, id, $"Unknown daemon method: {method}").ConfigureAwait(false);
                }
                catch (JsonException) { await WriteErrorAsync(writer, id, "Invalid daemon request.").ConfigureAwait(false); }
                catch (KeyNotFoundException) { await WriteErrorAsync(writer, id, "Invalid daemon request.").ConfigureAwait(false); }
                catch (InvalidOperationException) { await WriteErrorAsync(writer, id, "Invalid daemon request.").ConfigureAwait(false); }
                catch (Exception) { await WriteErrorAsync(writer, id, "Daemon request failed.").ConfigureAwait(false); }
                finally { if (requestStarted) endRequest(); }
            }
        }
    }

    private static Task WriteAsync(StreamWriter writer, JsonElement id, object result) =>
        writer.WriteLineAsync(JsonSerializer.Serialize(new { id = id.ValueKind == JsonValueKind.Undefined ? null : JsonSerializer.Deserialize<object>(id.GetRawText()), result }));

    private static Task WriteErrorAsync(StreamWriter writer, JsonElement id, string error) =>
        writer.WriteLineAsync(JsonSerializer.Serialize(new { id = id.ValueKind == JsonValueKind.Undefined ? null : JsonSerializer.Deserialize<object>(id.GetRawText()), error }));
}
