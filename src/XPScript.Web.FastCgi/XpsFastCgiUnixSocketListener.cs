using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;

namespace XPScript.Web.FastCgi;

public sealed class XpsFastCgiUnixSocketOptions
{
    public required string SocketPath { get; init; }
    public int Backlog { get; init; } = 128;
    public int MaxConcurrentConnections { get; init; } = 128;
    public UnixFileMode SocketMode { get; init; } =
        UnixFileMode.UserRead | UnixFileMode.UserWrite |
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite;

    public void Validate()
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("FastCGI Unix-domain sockets are supported only on Linux and macOS.");
        if (string.IsNullOrWhiteSpace(SocketPath)) throw new ArgumentException("SocketPath is required.", nameof(SocketPath));
        if (!Path.IsPathFullyQualified(SocketPath)) throw new ArgumentException("SocketPath must be absolute.", nameof(SocketPath));
        if (Encoding.UTF8.GetByteCount(SocketPath) > 100)
            throw new ArgumentException("SocketPath is too long for the supported Unix-domain socket path limit.", nameof(SocketPath));
        if (Backlog is < 1 or > 65_535) throw new ArgumentOutOfRangeException(nameof(Backlog));
        if (MaxConcurrentConnections is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(MaxConcurrentConnections));
    }
}

public sealed class XpsFastCgiUnixSocketListener : IAsyncDisposable
{
    private readonly XpsFastCgiAdapter _adapter;
    private readonly XpsFastCgiUnixSocketOptions _options;
    private readonly SemaphoreSlim _connections;
    private readonly ConcurrentDictionary<long, Task> _active = new();
    private Socket? _listener;
    private CancellationTokenSource? _shutdown;
    private Task? _acceptLoop;
    private long _connectionId;

    public XpsFastCgiUnixSocketListener(XpsFastCgiAdapter adapter, XpsFastCgiUnixSocketOptions options)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _connections = new SemaphoreSlim(_options.MaxConcurrentConnections, _options.MaxConcurrentConnections);
    }

    public string SocketPath => _options.SocketPath;
    public bool IsRunning => _listener is not null;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is not null) throw new InvalidOperationException("FastCGI Unix socket listener is already running.");
        _options.Validate();

        var directory = Path.GetDirectoryName(_options.SocketPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException("The FastCGI Unix socket directory does not exist.");
        if (File.Exists(_options.SocketPath))
            throw new IOException("The FastCGI Unix socket path already exists. Remove a stale socket explicitly before starting.");

        _shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            listener.Bind(new UnixDomainSocketEndPoint(_options.SocketPath));
            listener.Listen(_options.Backlog);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
                File.SetUnixFileMode(_options.SocketPath, _options.SocketMode);
            _listener = listener;
            _acceptLoop = AcceptLoopAsync(listener, _shutdown.Token);
            return Task.CompletedTask;
        }
        catch
        {
            listener.Dispose();
            TryDeleteSocketFile();
            _shutdown.Dispose();
            _shutdown = null;
            throw;
        }
    }

    public async Task StopAsync()
    {
        var listener = _listener;
        var shutdown = _shutdown;
        if (listener is null || shutdown is null) return;

        _listener = null;
        _shutdown = null;
        shutdown.Cancel();
        try { listener.Dispose(); } catch { }

        if (_acceptLoop is not null)
        {
            try { await _acceptLoop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) when (shutdown.IsCancellationRequested) { }
        }
        _acceptLoop = null;

        var active = _active.Values.ToArray();
        if (active.Length > 0)
        {
            try { await Task.WhenAll(active).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false); }
            catch (TimeoutException) { }
            catch (OperationCanceledException) { }
        }

        shutdown.Dispose();
        TryDeleteSocketFile();
    }

    private async Task AcceptLoopAsync(Socket listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket? client = null;
            var permitTaken = false;
            try
            {
                client = await listener.AcceptAsync(cancellationToken).ConfigureAwait(false);
                await _connections.WaitAsync(cancellationToken).ConfigureAwait(false);
                permitTaken = true;

                var id = Interlocked.Increment(ref _connectionId);
                var task = HandleClientAsync(client, id, cancellationToken);
                client = null;
                _active[id] = task;
                _ = task.ContinueWith(
                    completedTask =>
                    {
                        _active.TryRemove(id, out var removedTask);
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (permitTaken) _connections.Release();
                client?.Dispose();
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                if (permitTaken) _connections.Release();
                client?.Dispose();
                break;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                if (permitTaken) _connections.Release();
                client?.Dispose();
                break;
            }
            catch
            {
                if (permitTaken) _connections.Release();
                client?.Dispose();
                throw;
            }
        }
    }

    private async Task HandleClientAsync(Socket client, long id, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            await using (var stream = new NetworkStream(client, ownsSocket: false))
                await _adapter.ProcessConnectionAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (XpsFastCgiProtocolException) { }
        catch (IOException) { }
        catch (SocketException) { }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally
        {
            _connections.Release();
            _active.TryRemove(id, out var removedTask);
        }
    }

    private void TryDeleteSocketFile()
    {
        try
        {
            if (File.Exists(_options.SocketPath)) File.Delete(_options.SocketPath);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _connections.Dispose();
    }
}
