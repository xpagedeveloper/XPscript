using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using XPScript.Web.FastCgi;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-fastcgi-boundary-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var options = new XpsFastCgiOptions { Port = 0, MaxParamsBytes = 8192, MaxParamValueBytes = 4096 };
    var server = new XpsServerInfo("fcgi-boundary", root, XpsWebHostingMode.FastCgi, DateTimeOffset.UtcNow, "test");
    await using var adapter = new XpsFastCgiAdapter(options, server, new BoundaryHandler());

    var parameters = new Dictionary<string, string>
    {
        ["REQUEST_METHOD"] = "GET",
        ["SCRIPT_NAME"] = "/boundary.xps",
        ["QUERY_STRING"] = "name=åäö",
        ["HTTP_X_LONG"] = new string('x', 140),
        ["SCRIPT_FILENAME"] = Path.Combine(root, "boundary.xps")
    };
    var input = BuildSplitParamsRequest(3, parameters, splitSize: 5);
    var duplex = new DuplexStream(input, 2);
    await adapter.ProcessConnectionAsync(duplex);
    var text = ParseStdout(duplex.Written);
    if (!text.Contains("QUERY=name=åäö", StringComparison.Ordinal)) throw new Exception("Split FastCGI PARAMS lost UTF-8/query data.");
    if (!text.Contains("LONG=140", StringComparison.Ordinal)) throw new Exception("Split four-byte PARAMS length was not decoded.");

    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    await adapter.StartAsync(timeout.Token);
    var endpoint = adapter.LocalEndpoint ?? throw new Exception("FastCGI listener did not expose a local endpoint.");
    using var client = new TcpClient();
    await client.ConnectAsync(endpoint.Address, endpoint.Port, timeout.Token);
    await using var network = client.GetStream();
    var tcpRequest = BuildSplitParamsRequest(5, new Dictionary<string, string>
    {
        ["REQUEST_METHOD"] = "GET",
        ["SCRIPT_NAME"] = "/tcp.xps",
        ["QUERY_STRING"] = "tcp=ok",
        ["SCRIPT_FILENAME"] = Path.Combine(root, "tcp.xps")
    }, 7);
    await network.WriteAsync(tcpRequest, timeout.Token);
    await network.FlushAsync(timeout.Token);
    client.Client.Shutdown(SocketShutdown.Send);
    var tcpResponse = await ReadAllAsync(network, timeout.Token);
    var tcpText = ParseStdout(tcpResponse);
    if (!tcpText.Contains("QUERY=tcp=ok", StringComparison.Ordinal)) throw new Exception("FastCGI TCP listener did not process the request.");
    await adapter.StopAsync();

    Console.WriteLine("WEB-FASTCGI-BOUNDARY-SMOKE=OK");
}
finally
{
    Directory.Delete(root, recursive: true);
}

static byte[] BuildSplitParamsRequest(ushort requestId, IReadOnlyDictionary<string, string> parameters, int splitSize)
{
    using var output = new MemoryStream();
    var begin = new byte[8];
    BinaryPrimitives.WriteUInt16BigEndian(begin.AsSpan(0, 2), 1);
    WriteRecord(output, 1, requestId, begin);

    using var encoded = new MemoryStream();
    foreach (var pair in parameters)
    {
        var name = Encoding.UTF8.GetBytes(pair.Key);
        var value = Encoding.UTF8.GetBytes(pair.Value);
        WriteLength(encoded, name.Length);
        WriteLength(encoded, value.Length);
        encoded.Write(name);
        encoded.Write(value);
    }
    var data = encoded.ToArray();
    for (var offset = 0; offset < data.Length; offset += splitSize)
    {
        var count = Math.Min(splitSize, data.Length - offset);
        WriteRecord(output, 4, requestId, data.AsSpan(offset, count).ToArray());
    }
    WriteRecord(output, 4, requestId, []);
    WriteRecord(output, 5, requestId, []);
    return output.ToArray();
}

static void WriteLength(Stream output, int length)
{
    if (length < 128) { output.WriteByte((byte)length); return; }
    Span<byte> value = stackalloc byte[4];
    BinaryPrimitives.WriteUInt32BigEndian(value, (uint)length | 0x80000000u);
    output.Write(value);
}

static void WriteRecord(Stream output, byte type, ushort requestId, byte[] content)
{
    Span<byte> header = stackalloc byte[8];
    header[0] = 1;
    header[1] = type;
    BinaryPrimitives.WriteUInt16BigEndian(header.Slice(2, 2), requestId);
    BinaryPrimitives.WriteUInt16BigEndian(header.Slice(4, 2), (ushort)content.Length);
    output.Write(header);
    output.Write(content);
}

static async Task<byte[]> ReadAllAsync(Stream input, CancellationToken cancellationToken)
{
    using var output = new MemoryStream();
    var buffer = new byte[4096];
    while (true)
    {
        var read = await input.ReadAsync(buffer, cancellationToken);
        if (read == 0) break;
        output.Write(buffer, 0, read);
    }
    return output.ToArray();
}

static string ParseStdout(byte[] raw)
{
    var offset = 0;
    using var stdout = new MemoryStream();
    var end = false;
    while (offset < raw.Length)
    {
        if (raw.Length - offset < 8) throw new Exception("Truncated FastCGI response header.");
        var type = raw[offset + 1];
        var length = BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(offset + 4, 2));
        var padding = raw[offset + 6];
        offset += 8;
        if (raw.Length - offset < length + padding) throw new Exception("Truncated FastCGI response body.");
        if (type == 6 && length > 0) stdout.Write(raw, offset, length);
        if (type == 3) end = true;
        offset += length + padding;
    }
    if (!end) throw new Exception("FastCGI END_REQUEST missing.");
    return Encoding.UTF8.GetString(stdout.ToArray());
}

sealed class BoundaryHandler : IXpsWebRequestHandler
{
    public Task HandleAsync(XpsWebContext context)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.Write("QUERY=" + context.Request.QueryString + "\n");
        context.Response.Write("LONG=" + (context.Request.HeaderFirst("X-Long")?.Length ?? 0));
        return Task.CompletedTask;
    }
}

sealed class DuplexStream : Stream
{
    private readonly MemoryStream _input;
    private readonly MemoryStream _output = new();
    private readonly int _maxRead;
    public DuplexStream(byte[] input, int maxRead) { _input = new MemoryStream(input, false); _maxRead = maxRead; }
    public byte[] Written => _output.ToArray();
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => _output.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _output.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => _input.Read(buffer, offset, Math.Min(count, _maxRead));
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _input.ReadAsync(buffer[..Math.Min(buffer.Length, _maxRead)], cancellationToken);
    public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _output.WriteAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
