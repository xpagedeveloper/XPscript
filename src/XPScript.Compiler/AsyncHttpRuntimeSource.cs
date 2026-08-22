namespace XPScript.Compiler;

internal static class AsyncHttpRuntimeSource
{
    public const string Code = """
internal static class XPScriptAsyncHttp
{
    public static XPScriptHttpAsyncRequest GetAsync(
        XPScriptHttpClient client,
        object? url,
        object? callbackName,
        params object?[] callbackArguments)
        => Start(() => client.Get(url), callbackName, callbackArguments);

    public static XPScriptHttpAsyncRequest DeleteAsync(
        XPScriptHttpClient client,
        object? url,
        object? callbackName,
        params object?[] callbackArguments)
        => Start(() => client.Delete(url), callbackName, callbackArguments);

    public static XPScriptHttpAsyncRequest PostAsync(
        XPScriptHttpClient client,
        object? url,
        object? body,
        object? callbackName,
        params object?[] callbackArguments)
        => Start(() => client.Post(url, body), callbackName, callbackArguments);

    public static XPScriptHttpAsyncRequest PutAsync(
        XPScriptHttpClient client,
        object? url,
        object? body,
        object? callbackName,
        params object?[] callbackArguments)
        => Start(() => client.Put(url, body), callbackName, callbackArguments);

    public static XPScriptHttpAsyncRequest PatchAsync(
        XPScriptHttpClient client,
        object? url,
        object? body,
        object? callbackName,
        params object?[] callbackArguments)
        => Start(() => client.Patch(url, body), callbackName, callbackArguments);

    private static XPScriptHttpAsyncRequest Start(
        Func<XPScriptHttpResponse> request,
        object? callbackName,
        object?[]? callbackArguments)
    {
        ArgumentNullException.ThrowIfNull(request);
        callbackArguments ??= [];
        var handle = new XPScriptHttpAsyncRequest();
        handle.Start(Task.Run(() =>
        {
            try
            {
                var response = request();
                handle.Complete(response);
                XPScriptCallbackRuntime.Invoke(
                    callbackName,
                    "HTTP async",
                    XPScriptCallbackRuntime.Prepend(response, callbackArguments));
            }
            catch (XPScriptRuntimeException ex)
            {
                handle.Fail(ex.Message);
            }
            catch (Exception)
            {
                handle.Fail("HTTP async request failed.");
            }
        }));
        return handle;
    }
}

internal sealed class XPScriptHttpAsyncRequest
{
    private readonly object _sync = new();
    private Task? _task;
    private XPScriptHttpResponse? _response;
    private string _error = string.Empty;
    private bool _completed;

    public bool IsCompleted
    {
        get { lock (_sync) return _completed; }
    }

    public bool IsSuccess
    {
        get
        {
            lock (_sync)
                return _completed && _error.Length == 0 && _response is not null && _response.IsSuccess;
        }
    }

    public string Error
    {
        get { lock (_sync) return _error; }
    }

    public XPScriptHttpResponse? Response
    {
        get { lock (_sync) return _response; }
    }

    public bool Wait() => Wait(-1);

    public bool Wait(object? millisecondsValue)
    {
        Task? task;
        lock (_sync) task = _task;
        if (task is null) return true;

        var milliseconds = XPScriptRuntime.CInt(millisecondsValue);
        if (milliseconds < -1)
            throw new XPScriptRuntimeException(5, "HTTP async wait timeout must be -1 or greater.");
        try
        {
            return milliseconds == -1 ? WaitInfinite(task) : task.Wait(milliseconds);
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(item => item is not XPScriptRuntimeException))
        {
            throw new XPScriptRuntimeException(5, "HTTP async wait failed.");
        }
    }

    internal void Start(Task task)
    {
        lock (_sync)
        {
            if (_task is not null)
                throw new InvalidOperationException("HTTP async request was already started.");
            _task = task;
        }
    }

    internal void Complete(XPScriptHttpResponse response)
    {
        lock (_sync)
        {
            _response = response;
            _completed = true;
        }
    }

    internal void Fail(string error)
    {
        lock (_sync)
        {
            _error = string.IsNullOrWhiteSpace(error) ? "HTTP async request failed." : error;
            _completed = true;
        }
    }

    private static bool WaitInfinite(Task task)
    {
        task.Wait();
        return true;
    }
}
""";
}
