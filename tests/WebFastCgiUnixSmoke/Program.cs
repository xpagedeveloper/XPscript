using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using XPScript.Web.FastCgi;
using XPScript.Web.Runtime;

if (OperatingSystem.IsWindows())
{
    Console.WriteLine("WEB-FASTCGI-UNIX-SMOKE=SKIPPED-WINDOWS");
    return;
}

var parent = Path.Combine(Path.GetTempPath(), "xps-fcgi-unix-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(parent);
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(root);
var socketPath = Path.Combine(parent, "xps.sock");
var scriptPath = Path.Combine(root, "index.xps");
await File.WriteAllTextAsync(scriptPath, "placeholder");

try
{
    var server = new XpsServerInfo("fcgi-unix-smoke", root, XpsWebHostingMode.FastCgi, DateTimeOffset.UtcNow, "test");
    await using var adapter = new XpsFastCgiAdapter(new XpsFastCgiOptions(), server, new EchoHandler());
    await using var listener = new XpsFastCgiUnixSocketListener(adapter, new XpsFastCgiUnixSocketOptions
    {
        SocketPath = socketPath,
        MaxConcurrentConnections = 4,
        Backlog = 4
    });

    await listener.StartAsync();
    if (!File.Exists(socketPath)) throw new Exception("FastCGI Unix socket file was not created.");

    using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeout.Token);
    await using var stream = new NetworkStream(socket, ownsSocket: false);

    var request = BuildRequest(1, new Dictionary<string, string>
    {
        ["REQUEST_METHOD"] = "GET",
        ["SCRIPT_NAME"] = "/index.xps",
        ["SCRIPT_FILENAME"] = scriptPath,
        ["QUERY_STRING"] = "unix=ok",
        ["SERVER_NAME"] = "localhost",
        ["SERVER_PROTOCOL"] = "HTTP/1.1",
        ["REMOTE_ADDR"] = "127.0.0.1"
    });
    await stream.WriteAsync(request, timeout.Token);
    await stream.FlushAsync(timeout.Token);
    socket.Shutdown(SocketShutdown.Send);

    var response = await ReadAllAsync(stream, timeout.Token);
    var stdout = ParseStdout(response);
    if (!stdout.Contains("UNIX=unix=ok", StringComparison.Ordinal))
        throw new Exception("FastCGI Unix socket listener returned unexpected response: " + stdout);

    await listener.StopAsync();
    if (File.Exists(socketPath)) throw new Exception("FastCGI Unix socket file was not removed on shutdown.");

    Console.WriteLine("WEB-FASTCGI-UNIX-SMOKE=OK");
}
finally
{
    if (File.Exists(socketPath)) File.Delete(socketPath);
    Directory.Delete(parent, recursive: true);
}

static byte[] BuildRequest(ushort requestId, IReadOnlyDictionary<string, string> parameters)
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
    WriteRecord(output, 4, requestId, encoded.ToArray());
    WriteRecord(output, 4, requestId, []);
    WriteRecord(output, 5, requestId, []);
    return output.ToArray();
}

static void WriteLength(Stream stream, int length)
{
    if (length < 128)
    {
        stream.WriteByte((byte)length);
        return;
    }
    Span<byte> bytes = stackalloc byte[4];
    BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)length | 0x80000000u);
    stream.Write(bytes);
}

static void WriteRecord(Stream stream, byte type, ushort requestId, byte[] content)
{
    Span<byte> header = stackalloc byte[8];
    header[0] = 1;
    header[1] = type;
    BinaryPrimitives.WriteUInt16BigEndian(header.Slice(2, 2), requestId);
    BinaryPrimitives.WriteUInt16BigEndian(header.Slice(4, 2), checked((ushort)content.Length));
    stream.Write(header);
    stream.Write(content);
}

static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
{
    using var output = new MemoryStream();
    var buffer = new byte[4096];
    while (true)
    {
        var read = await stream.ReadAsync(buffer, cancellationToken);
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

sealed class EchoHandler : IXpsWebRequestHandler
{
    public Task HandleAsync(XpsWebContext context)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.Write("UNIX=" + context.Request.QueryString);
        return Task.CompletedTask;
    }
}
