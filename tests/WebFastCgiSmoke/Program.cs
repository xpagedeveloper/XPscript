using System.Buffers.Binary;
using System.Text;
using XPScript.Web.FastCgi;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-fastcgi-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var server = new XpsServerInfo("fastcgi-smoke", root, XpsWebHostingMode.FastCgi, DateTimeOffset.UtcNow, "test");
    var options = new XpsFastCgiOptions
    {
        MaxParamsBytes = 4096,
        MaxParamCount = 64,
        MaxParamNameBytes = 256,
        MaxParamValueBytes = 1024,
        MaxRequestBodyBytes = 1024,
        MaxHeaderCount = 32,
        MaxHeaderValueBytes = 1024
    };
    await using var adapter = new XpsFastCgiAdapter(options, server, new EchoHandler());

    var getInput = BuildRequest(
        1,
        new Dictionary<string, string>
        {
            ["REQUEST_METHOD"] = "GET",
            ["SCRIPT_NAME"] = "/index.xps",
            ["QUERY_STRING"] = "q=one&q=two",
            ["SERVER_NAME"] = "localhost",
            ["SERVER_PROTOCOL"] = "HTTP/1.1",
            ["REMOTE_ADDR"] = "127.0.0.1",
            ["HTTP_HOST"] = "localhost",
            ["HTTP_X_TEST"] = "present",
            ["HTTP_COOKIE"] = "client=abc",
            ["SCRIPT_FILENAME"] = Path.Combine(root, "index.xps")
        },
        []);
    var getStream = new FragmentedDuplexStream(getInput, 3);
    await adapter.ProcessConnectionAsync(getStream);
    var getOutput = ParseResponse(getStream.Written);
    if (!getOutput.Contains("Status: 201\r\n", StringComparison.Ordinal)) throw new Exception("FastCGI status was not serialized.");
    if (!getOutput.Contains("METHOD=GET", StringComparison.Ordinal)) throw new Exception("FastCGI method mapping failed.");
    if (!getOutput.Contains("PATH=/index.xps", StringComparison.Ordinal)) throw new Exception("FastCGI path mapping failed.");
    if (!getOutput.Contains("QUERY=q=one&q=two", StringComparison.Ordinal)) throw new Exception("FastCGI query mapping failed.");
    if (!getOutput.Contains("HEADER=present", StringComparison.Ordinal)) throw new Exception("FastCGI header mapping failed.");
    if (!getOutput.Contains("COOKIE=abc", StringComparison.Ordinal)) throw new Exception("FastCGI cookie mapping failed.");

    var postBody = Encoding.UTF8.GetBytes("hello=world");
    var postInput = BuildRequest(
        7,
        new Dictionary<string, string>
        {
            ["REQUEST_METHOD"] = "POST",
            ["SCRIPT_NAME"] = "/submit.xps",
            ["QUERY_STRING"] = "",
            ["CONTENT_TYPE"] = "application/x-www-form-urlencoded",
            ["CONTENT_LENGTH"] = postBody.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["SERVER_NAME"] = "localhost",
            ["SERVER_PROTOCOL"] = "HTTP/1.1",
            ["HTTPS"] = "on",
            ["SCRIPT_FILENAME"] = Path.Combine(root, "submit.xps")
        },
        postBody);
    var postStream = new FragmentedDuplexStream(postInput, 5);
    await adapter.ProcessConnectionAsync(postStream);
    var postOutput = ParseResponse(postStream.Written);
    if (!postOutput.Contains("METHOD=POST", StringComparison.Ordinal)) throw new Exception("FastCGI POST method mapping failed.");
    if (!postOutput.Contains("BODY=hello=world", StringComparison.Ordinal)) throw new Exception("FastCGI STDIN body mapping failed.");
    if (!postOutput.Contains("SCHEME=https", StringComparison.Ordinal)) throw new Exception("FastCGI HTTPS mapping failed.");

    var escapeInput = BuildRequest(
        9,
        new Dictionary<string, string>
        {
            ["REQUEST_METHOD"] = "GET",
            ["SCRIPT_NAME"] = "/index.xps",
            ["SCRIPT_FILENAME"] = Path.Combine(root, "..", "outside.xps")
        },
        []);
    try
    {
        await adapter.ProcessConnectionAsync(new FragmentedDuplexStream(escapeInput, 2));
        throw new Exception("FastCGI SCRIPT_FILENAME root escape was accepted.");
    }
    catch (XpsFastCgiProtocolException)
    {
    }

    var truncated = new byte[] { 1, 1, 0, 1, 0, 8, 0, 0, 0, 1 };
    try
    {
        await adapter.ProcessConnectionAsync(new FragmentedDuplexStream(truncated, 1));
        throw new Exception("Truncated FastCGI record was accepted.");
    }
    catch (XpsFastCgiProtocolException)
    {
    }

    var badVersion = BuildRequest(11, new Dictionary<string, string> { ["REQUEST_METHOD"] = "GET" }, []);
    badVersion[0] = 2;
    try
    {
        await adapter.ProcessConnectionAsync(new FragmentedDuplexStream(badVersion, 8));
        throw new Exception("Unsupported FastCGI version was accepted.");
    }
    catch (XpsFastCgiProtocolException)
    {
    }

    Console.WriteLine("WEB-FASTCGI-SMOKE=OK");
}
finally
{
    Directory.Delete(root, recursive: true);
}

static byte[] BuildRequest(ushort requestId, IReadOnlyDictionary<string, string> parameters, byte[] body)
{
    using var output = new MemoryStream();
    var begin = new byte[8];
    BinaryPrimitives.WriteUInt16BigEndian(begin.AsSpan(0, 2), 1);
    begin[2] = 0;
    WriteRecord(output, 1, requestId, begin);

    using var paramBody = new MemoryStream();
    foreach (var pair in parameters)
    {
        var name = Encoding.UTF8.GetBytes(pair.Key);
        var value = Encoding.UTF8.GetBytes(pair.Value);
        WriteLength(paramBody, name.Length);
        WriteLength(paramBody, value.Length);
        paramBody.Write(name);
        paramBody.Write(value);
    }
    WriteRecord(output, 4, requestId, paramBody.ToArray());
    WriteRecord(output, 4, requestId, []);
    if (body.Length > 0) WriteRecord(output, 5, requestId, body);
    WriteRecord(output, 5, requestId, []);
    return output.ToArray();
}

static void WriteRecord(Stream output, byte type, ushort requestId, byte[] content)
{
    if (content.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(content));
    Span<byte> header = stackalloc byte[8];
    header[0] = 1;
    header[1] = type;
    BinaryPrimitives.WriteUInt16BigEndian(header.Slice(2, 2), requestId);
    BinaryPrimitives.WriteUInt16BigEndian(header.Slice(4, 2), (ushort)content.Length);
    header[6] = 0;
    header[7] = 0;
    output.Write(header);
    output.Write(content);
}

static void WriteLength(Stream output, int length)
{
    if (length < 128)
    {
        output.WriteByte((byte)length);
        return;
    }
    Span<byte> encoded = stackalloc byte[4];
    BinaryPrimitives.WriteUInt32BigEndian(encoded, (uint)length | 0x80000000u);
    output.Write(encoded);
}

static string ParseResponse(byte[] raw)
{
    var offset = 0;
    using var stdout = new MemoryStream();
    var sawEnd = false;
    while (offset < raw.Length)
    {
        if (raw.Length - offset < 8) throw new Exception("Truncated FastCGI response header.");
        var version = raw[offset];
        var type = raw[offset + 1];
        var contentLength = BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(offset + 4, 2));
        var padding = raw[offset + 6];
        if (version != 1) throw new Exception("Unexpected FastCGI response version.");
        offset += 8;
        if (raw.Length - offset < contentLength + padding) throw new Exception("Truncated FastCGI response content.");
        if (type == 6 && contentLength > 0) stdout.Write(raw, offset, contentLength);
        if (type == 3) sawEnd = true;
        offset += contentLength + padding;
    }
    if (!sawEnd) throw new Exception("FastCGI response did not contain END_REQUEST.");
    return Encoding.UTF8.GetString(stdout.ToArray());
}

sealed class EchoHandler : IXpsWebRequestHandler
{
    public Task HandleAsync(XpsWebContext context)
    {
        context.Response.StatusCode = 201;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.SetHeader("X-FastCGI", "ok");
        context.Response.Write("METHOD=" + context.Request.Method + "\n");
        context.Response.Write("PATH=" + context.Request.Path + "\n");
        context.Response.Write("QUERY=" + context.Request.QueryString + "\n");
        context.Response.Write("BODY=" + Encoding.UTF8.GetString(context.Request.Body.Span) + "\n");
        context.Response.Write("HEADER=" + context.Request.HeaderFirst("X-Test") + "\n");
        context.Response.Write("COOKIE=" + context.Request.Cookie("client") + "\n");
        context.Response.Write("SCHEME=" + context.Request.Scheme + "\n");
        return Task.CompletedTask;
    }
}

sealed class FragmentedDuplexStream : Stream
{
    private readonly MemoryStream _input;
    private readonly MemoryStream _output = new();
    private readonly int _maxRead;

    public FragmentedDuplexStream(byte[] input, int maxRead)
    {
        _input = new MemoryStream(input, writable: false);
        _maxRead = maxRead;
    }

    public byte[] Written => _output.ToArray();
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => _output.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _output.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => _input.Read(buffer, offset, Math.Min(count, _maxRead));
    public override int Read(Span<byte> buffer) => _input.Read(buffer[..Math.Min(buffer.Length, _maxRead)]);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _input.ReadAsync(buffer[..Math.Min(buffer.Length, _maxRead)], cancellationToken);
    public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<byte> buffer) => _output.Write(buffer);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        _output.WriteAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
