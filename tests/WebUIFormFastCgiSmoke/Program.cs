using System.Buffers.Binary;
using System.Text;
using XPScript.Web.Compiler;
using XPScript.Web.FastCgi;
using XPScript.Web.Runtime;

var parent = Path.Combine(Path.GetTempPath(), "xps-uiform-fastcgi-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(root);
var scriptPath = Path.Combine(root, "form.xps");
await File.WriteAllTextAsync(scriptPath, """
[Anonymous]
[Get]
[Post]
Sub Index()
    Dim data As New JsonObject
    Dim form As New UIForm("Contact form")
    Dim result As String

    Call data.Set("existing", "Loaded from JSON")
    Call form.BindData(data)
    Call form.AddTextField("existing", "Existing")
    Call form.AddTextField("missing", "Missing")

    result = form.ShowDialog()
    If result = "OK" Then
        Response.ContentType = "application/json; charset=utf-8"
        Response.Write(data.Stringify())
    End If
End Sub
""");

await using var cache = new XpsWebCompilationCache(new XpsWebCompiler());
await using var dispatcher = new XpsWebDispatcher(root, cache);
var server = new XpsServerInfo("uiform-fastcgi-smoke", root, XpsWebHostingMode.FastCgi, DateTimeOffset.UtcNow, "test");
var options = new XpsFastCgiOptions
{
    MaxParamsBytes = 8192,
    MaxParamCount = 64,
    MaxParamNameBytes = 256,
    MaxParamValueBytes = 2048,
    MaxRequestBodyBytes = 1024 * 1024,
    MaxHeaderCount = 64,
    MaxHeaderValueBytes = 4096
};
await using var adapter = new XpsFastCgiAdapter(options, server, dispatcher);

try
{
    var get = await RunAsync(adapter, BuildRequest(1, BaseParams("GET", root, scriptPath, null), []));
    if (!get.Contains("Content-Type: text/html; charset=utf-8\r\n", StringComparison.Ordinal))
        throw new Exception("UIForm FastCGI GET did not return HTML: " + get);
    if (!get.Contains(">Contact form</h1>", StringComparison.Ordinal))
        throw new Exception("UIForm FastCGI GET did not render title: " + get);
    if (!get.Contains("name=\"existing\" value=\"Loaded from JSON\"", StringComparison.Ordinal))
        throw new Exception("UIForm FastCGI GET did not load existing JSON value: " + get);
    if (!get.Contains("name=\"missing\" value=\"\"", StringComparison.Ordinal))
        throw new Exception("UIForm FastCGI GET did not render missing JSON field as empty: " + get);

    var body = Encoding.UTF8.GetBytes("existing=Changed+value&missing=Created+by+user");
    var post = await RunAsync(adapter, BuildRequest(2, BaseParams("POST", root, scriptPath, body.Length), body));
    if (!post.Contains("Content-Type: application/json; charset=utf-8\r\n", StringComparison.Ordinal))
        throw new Exception("UIForm FastCGI POST did not return JSON: " + post);
    if (!post.Contains("\"existing\":\"Changed value\"", StringComparison.Ordinal))
        throw new Exception("UIForm FastCGI POST did not save existing field: " + post);
    if (!post.Contains("\"missing\":\"Created by user\"", StringComparison.Ordinal))
        throw new Exception("UIForm FastCGI POST did not create missing JSON key: " + post);

    Console.WriteLine("WEB-UIFORM-FASTCGI=OK");
}
finally
{
    if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
}

static Dictionary<string, string> BaseParams(string method, string root, string scriptPath, int? bodyLength)
{
    var values = new Dictionary<string, string>
    {
        ["REQUEST_METHOD"] = method,
        ["SCRIPT_NAME"] = "/form.xps",
        ["SCRIPT_FILENAME"] = scriptPath,
        ["QUERY_STRING"] = "",
        ["SERVER_NAME"] = "localhost",
        ["SERVER_PROTOCOL"] = "HTTP/1.1",
        ["REMOTE_ADDR"] = "127.0.0.1",
        ["HTTP_HOST"] = "localhost",
        ["XPSCRIPT_WEB_ROOT"] = root
    };
    if (bodyLength.HasValue)
    {
        values["CONTENT_TYPE"] = "application/x-www-form-urlencoded";
        values["CONTENT_LENGTH"] = bodyLength.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    return values;
}

static async Task<string> RunAsync(XpsFastCgiAdapter adapter, byte[] request)
{
    var stream = new DuplexStream(request);
    await adapter.ProcessConnectionAsync(stream);
    return ParseResponse(stream.Written);
}

static byte[] BuildRequest(ushort requestId, IReadOnlyDictionary<string, string> parameters, byte[] body)
{
    using var output = new MemoryStream();
    var begin = new byte[8];
    BinaryPrimitives.WriteUInt16BigEndian(begin.AsSpan(0, 2), 1);
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
    Span<byte> header = stackalloc byte[8];
    header[0] = 1;
    header[1] = type;
    BinaryPrimitives.WriteUInt16BigEndian(header.Slice(2, 2), requestId);
    BinaryPrimitives.WriteUInt16BigEndian(header.Slice(4, 2), checked((ushort)content.Length));
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

sealed class DuplexStream : Stream
{
    private readonly MemoryStream _input;
    private readonly MemoryStream _output = new();
    public DuplexStream(byte[] input) => _input = new MemoryStream(input, writable: false);
    public byte[] Written => _output.ToArray();
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => _output.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _output.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => _input.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => _input.Read(buffer);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _input.ReadAsync(buffer, cancellationToken);
    public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<byte> buffer) => _output.Write(buffer);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _output.WriteAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
