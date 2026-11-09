using Avalonia;
using Avalonia.Threading;
using System.Runtime.ExceptionServices;

namespace XPScript.UI.Desktop;

internal static class DesktopApplicationHost
{
    private static readonly object SyncRoot = new();
    private static readonly ManualResetEventSlim Started = new(false);
    private static Thread? _uiThread;
    private static Exception? _startupFailure;
    private static CancellationTokenSource? _macLoopCancellation;
    private static bool _macScriptCompleted;
    private static bool _macKeepAlive;

    // Test projects can supply Avalonia.Headless configuration without adding
    // the test package to the production desktop runtime.
    internal static Func<AppBuilder, AppBuilder>? TestConfigurator { get; set; }

    public static void RunMacOS(Action script)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("The macOS desktop loop requires macOS.");

        lock (SyncRoot)
        {
            if (_uiThread is not null)
                throw new InvalidOperationException("The desktop UI runtime has already started.");
            _uiThread = Thread.CurrentThread;
        }

        InitializeOnCurrentThread();
        using var cancellation = new CancellationTokenSource();
        _macLoopCancellation = cancellation;
        Started.Set();
        Exception? scriptFailure = null;
        var worker = new Thread(() =>
        {
            try { script(); }
            catch (Exception ex) { scriptFailure = ex; }
            finally
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _macScriptCompleted = true;
                    if (scriptFailure is not null || !_macKeepAlive) cancellation.Cancel();
                });
            }
        }) { IsBackground = true, Name = "XPScript script" };
        worker.Start();
        Dispatcher.UIThread.MainLoop(cancellation.Token);
        worker.Join();
        if (scriptFailure is not null) ExceptionDispatchInfo.Capture(scriptFailure).Throw();
    }

    public static void EnsureStarted()
    {
        lock (SyncRoot)
        {
            if (_uiThread is null)
            {
                _uiThread = new Thread(Run)
                {
                    IsBackground = true,
                    Name = "XPScript Desktop UI"
                };

                if (OperatingSystem.IsWindows())
                    _uiThread.SetApartmentState(ApartmentState.STA);

                _uiThread.Start();
            }
        }

        Started.Wait();
        if (_startupFailure is not null)
            throw new InvalidOperationException(
                "Unable to initialize the XPScript desktop UI runtime. " +
                "Startup failure: " + FormatExceptionChain(_startupFailure),
                _startupFailure);
    }

    private static string FormatExceptionChain(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var message = string.IsNullOrWhiteSpace(current.Message) ? "<no message>" : current.Message;
            messages.Add(current.GetType().FullName + ": " + message);
        }

        return string.Join(" -> ", messages);
    }

    public static void SetProcessKeepAlive(bool keepAlive)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Invoke(() => SetProcessKeepAlive(keepAlive));
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            _macKeepAlive = keepAlive;
            if (!keepAlive && _macScriptCompleted) _macLoopCancellation?.Cancel();
            return;
        }
        Thread.CurrentThread.IsBackground = !keepAlive;
    }

    private static void Run()
    {
        try
        {
            InitializeOnCurrentThread();
            Started.Set();
            Dispatcher.UIThread.MainLoop(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _startupFailure = ex;
            Started.Set();
        }
    }

    // macOS entry points will call this on the process main thread before
    // dispatching the XPscript body to a worker thread.
    internal static void InitializeOnCurrentThread()
    {
        var builder = AppBuilder.Configure<XpsDesktopApplication>();
        builder = TestConfigurator?.Invoke(builder) ?? builder.UsePlatformDetect();
        builder.SetupWithoutStarting();
    }
}
