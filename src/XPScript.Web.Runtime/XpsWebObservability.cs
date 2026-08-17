using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace XPScript.Web.Runtime;

public enum XpsWebHealthStatus
{
    Healthy,
    Stopping
}

public sealed record XpsWebHealthSnapshot(
    XpsWebHealthStatus Status,
    DateTimeOffset StartedUtc,
    TimeSpan Uptime,
    long ActiveRequests,
    long TotalRequests,
    long FailedRequests,
    long Responses2xx,
    long Responses3xx,
    long Responses4xx,
    long Responses5xx,
    long RequestBodyBytes,
    long ResponseBodyBytes);

public sealed record XpsWebRequestEvent(
    DateTimeOffset TimestampUtc,
    string Transport,
    string Method,
    int StatusCode,
    double DurationMilliseconds,
    long RequestBodyBytes,
    long ResponseBodyBytes,
    bool Failed);

public interface IXpsWebEventSink
{
    void Write(XpsWebRequestEvent requestEvent);
}

public sealed class XpsWebJsonLineEventSink : IXpsWebEventSink
{
    private readonly TextWriter _writer;
    private readonly object _gate = new();

    public XpsWebJsonLineEventSink(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public void Write(XpsWebRequestEvent requestEvent)
    {
        ArgumentNullException.ThrowIfNull(requestEvent);
        var json = JsonSerializer.Serialize(requestEvent);
        lock (_gate)
        {
            _writer.WriteLine(json);
            _writer.Flush();
        }
    }
}

public sealed class XpsWebTelemetry
{
    private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;
    private readonly IXpsWebEventSink? _eventSink;
    private long _stopping;
    private long _activeRequests;
    private long _totalRequests;
    private long _failedRequests;
    private long _responses2xx;
    private long _responses3xx;
    private long _responses4xx;
    private long _responses5xx;
    private long _requestBodyBytes;
    private long _responseBodyBytes;

    public XpsWebTelemetry(IXpsWebEventSink? eventSink = null)
    {
        _eventSink = eventSink;
    }

    public RequestScope BeginRequest(string transport, string method, long requestBodyBytes = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transport);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        if (requestBodyBytes < 0) requestBodyBytes = 0;
        Interlocked.Increment(ref _activeRequests);
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Add(ref _requestBodyBytes, requestBodyBytes);
        return new RequestScope(this, transport, method, requestBodyBytes);
    }

    public void MarkStopping() => Interlocked.Exchange(ref _stopping, 1);

    public XpsWebHealthSnapshot Snapshot()
    {
        var now = DateTimeOffset.UtcNow;
        return new XpsWebHealthSnapshot(
            Interlocked.Read(ref _stopping) == 0 ? XpsWebHealthStatus.Healthy : XpsWebHealthStatus.Stopping,
            _startedUtc,
            now - _startedUtc,
            Interlocked.Read(ref _activeRequests),
            Interlocked.Read(ref _totalRequests),
            Interlocked.Read(ref _failedRequests),
            Interlocked.Read(ref _responses2xx),
            Interlocked.Read(ref _responses3xx),
            Interlocked.Read(ref _responses4xx),
            Interlocked.Read(ref _responses5xx),
            Interlocked.Read(ref _requestBodyBytes),
            Interlocked.Read(ref _responseBodyBytes));
    }

    public string RenderPrometheus()
    {
        var snapshot = Snapshot();
        var builder = new StringBuilder();
        AppendGauge(builder, "xpscript_web_health", snapshot.Status == XpsWebHealthStatus.Healthy ? 1 : 0, "1 when the web runtime is healthy, otherwise 0.");
        AppendGauge(builder, "xpscript_web_active_requests", snapshot.ActiveRequests, "Current active web requests.");
        AppendCounter(builder, "xpscript_web_requests_total", snapshot.TotalRequests, "Total web requests accepted by the runtime.");
        AppendCounter(builder, "xpscript_web_failed_requests_total", snapshot.FailedRequests, "Total requests that failed with an exception or 5xx response.");
        AppendCounter(builder, "xpscript_web_responses_2xx_total", snapshot.Responses2xx, "Total 2xx responses.");
        AppendCounter(builder, "xpscript_web_responses_3xx_total", snapshot.Responses3xx, "Total 3xx responses.");
        AppendCounter(builder, "xpscript_web_responses_4xx_total", snapshot.Responses4xx, "Total 4xx responses.");
        AppendCounter(builder, "xpscript_web_responses_5xx_total", snapshot.Responses5xx, "Total 5xx responses.");
        AppendCounter(builder, "xpscript_web_request_body_bytes_total", snapshot.RequestBodyBytes, "Total request body bytes observed.");
        AppendCounter(builder, "xpscript_web_response_body_bytes_total", snapshot.ResponseBodyBytes, "Total response body bytes produced.");
        builder.Append("# TYPE xpscript_web_uptime_seconds gauge\n");
        builder.Append("# HELP xpscript_web_uptime_seconds Web runtime uptime in seconds.\n");
        builder.Append("xpscript_web_uptime_seconds ").Append(snapshot.Uptime.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)).Append('\n');
        return builder.ToString();
    }

    private void Complete(string transport, string method, int statusCode, long requestBodyBytes, long responseBodyBytes, long startedTimestamp, bool failed)
    {
        Interlocked.Decrement(ref _activeRequests);
        if (responseBodyBytes > 0) Interlocked.Add(ref _responseBodyBytes, responseBodyBytes);

        if (statusCode is >= 200 and <= 299) Interlocked.Increment(ref _responses2xx);
        else if (statusCode is >= 300 and <= 399) Interlocked.Increment(ref _responses3xx);
        else if (statusCode is >= 400 and <= 499) Interlocked.Increment(ref _responses4xx);
        else if (statusCode is >= 500 and <= 599) Interlocked.Increment(ref _responses5xx);

        var isFailure = failed || statusCode >= 500;
        if (isFailure) Interlocked.Increment(ref _failedRequests);

        if (_eventSink is not null)
        {
            var elapsed = Stopwatch.GetElapsedTime(startedTimestamp);
            _eventSink.Write(new XpsWebRequestEvent(
                DateTimeOffset.UtcNow,
                transport,
                method,
                statusCode,
                elapsed.TotalMilliseconds,
                requestBodyBytes,
                Math.Max(0, responseBodyBytes),
                isFailure));
        }
    }

    private static void AppendGauge(StringBuilder builder, string name, long value, string help)
    {
        builder.Append("# TYPE ").Append(name).Append(" gauge\n");
        builder.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
        builder.Append(name).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
    }

    private static void AppendCounter(StringBuilder builder, string name, long value, string help)
    {
        builder.Append("# TYPE ").Append(name).Append(" counter\n");
        builder.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
        builder.Append(name).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
    }

    public sealed class RequestScope : IDisposable
    {
        private readonly XpsWebTelemetry _owner;
        private readonly string _transport;
        private readonly string _method;
        private readonly long _requestBodyBytes;
        private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
        private int _completed;

        internal RequestScope(XpsWebTelemetry owner, string transport, string method, long requestBodyBytes)
        {
            _owner = owner;
            _transport = transport;
            _method = method;
            _requestBodyBytes = requestBodyBytes;
        }

        public void Complete(int statusCode, long responseBodyBytes, bool failed = false)
        {
            if (statusCode is < 100 or > 999) throw new ArgumentOutOfRangeException(nameof(statusCode));
            if (Interlocked.Exchange(ref _completed, 1) != 0) return;
            _owner.Complete(_transport, _method, statusCode, _requestBodyBytes, responseBodyBytes, _startedTimestamp, failed);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _completed, 1) != 0) return;
            _owner.Complete(_transport, _method, 500, _requestBodyBytes, 0, _startedTimestamp, failed: true);
        }
    }
}
