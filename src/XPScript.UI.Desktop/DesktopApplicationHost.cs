using Avalonia;
using Avalonia.Threading;

namespace XPScript.UI.Desktop;

internal static class DesktopApplicationHost
{
    private static readonly object SyncRoot = new();
    private static readonly ManualResetEventSlim Started = new(false);
    private static Thread? _uiThread;
    private static Exception? _startupFailure;

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

        Thread.CurrentThread.IsBackground = !keepAlive;
    }

    private static void Run()
    {
        try
        {
            AppBuilder.Configure<XpsDesktopApplication>()
                .UsePlatformDetect()
                .SetupWithoutStarting();
            Started.Set();
            Dispatcher.UIThread.MainLoop(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _startupFailure = ex;
            Started.Set();
        }
    }
}
