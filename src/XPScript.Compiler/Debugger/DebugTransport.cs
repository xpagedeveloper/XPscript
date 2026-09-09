namespace XPScript.Compiler.Debugger;

public enum DebugTargetKind
{
    Cli,
    Desktop,
    WebServer,
    WebAssembly
}

public sealed record DebugTarget(
    DebugTargetKind Kind,
    string Name,
    string? ProcessId = null,
    Uri? Endpoint = null);

public interface IDebugTransport : IAsyncDisposable
{
    DebugTarget Target { get; }
    Task StartAsync(DebugSession session, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public sealed record DebugAttachOptions(
    string? Host = null,
    int? Port = null,
    string? Token = null,
    bool UseTls = false);
