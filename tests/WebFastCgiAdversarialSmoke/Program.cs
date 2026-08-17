using System.Buffers.Binary;
using System.Text;
using XPScript.Web.FastCgi;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-fcgi-adversarial-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var scriptPath = Path.Combine(root, "index.xps");
    await File.WriteAllTextAsync(scriptPath, "placeholder");
    var server = new XpsServerInfo("fcgi-adversarial", root, XpsWebHostingMode.FastCgi, DateTimeOffset.UtcNow, "test");
    await using var adapter = new XpsFastCgiAdapter(new XpsFastCgiOptions
    {
        MaxParamsBytes = 16 * 1024,
        MaxParamCount = 128,
        MaxParamNameBytes = 512,
        MaxParamValueBytes = 4096,
        MaxRequestBodyBytes = 64 * 1024,
        MaxHeaderCount = 64,
        MaxHeaderValueBytes = 4096
    }, server, new NoopHandler());

    var valid = BuildValidRequest(scriptPath);
    var corpus = BuildFixedCorpus(valid, root);
    var random = new Random(0x585053);
    for (var i = 0; i < 1000; i++) corpus.Add(Mutate(valid, random));

    var accepted = 0;
    var rejected = 0;
    foreach (var payload in corpus)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var stream = new DuplexStream(payload, maxRead: 7);
        try
        {
            await adapter.ProcessConnectionAsync(stream, timeout.Token).WaitAsync(timeout.Token);
            accepted++;
        }
        catch (XpsFastCgiProtocolException)
        {
            rejected++;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new Exception("FastCGI adversarial input caused parser timeout/hang.");
        }
        catch (Exception ex)
        {
            throw new Exception($"Unexpected exception type {ex.GetType().FullName} for bounded adversarial input.", ex);
        }

        if (stream.Written.Length > 256 * 1024)
            throw new Exception("FastCGI adversarial input caused unexpectedly large response allocation.");
    }

    if (rejected == 0) throw new Exception("Adversarial corpus did not exercise rejection paths.");
    if (accepted == 0) throw new Exception("Adversarial corpus did not retain any valid/benign paths.");

    Console.WriteLine($"WEB-FASTCGI-ADVERSARIAL-SMOKE=OK accepted={accepted} rejected={rejected} total={corpus.Count}");
}
finally
{
    Directory.Delete(root, recursive: true);
}

static List<byte[]> BuildFixedCorpus(byte[] valid, string root)
{
    var result = new List<byte[]> { valid, Array.Empty<byte>(), new byte[] { 1 }, valid[..Math.Min(7, valid.Length)] };

    var badVersion = valid.ToArray();
    badVersion[0] = 2;
    result.Add(badVersion);

    var reservedHeader = valid.ToArray();
    reservedHeader[7] = 1;
    result.Add(reservedHeader);

    var hugeFirstContent = valid.ToArray();
    BinaryPrimitives.WriteUInt16BigEndian(hugeFirstContent.AsSpan(4, 2), ushort.MaxValue);
    result.Add(hugeFirstContent);

    result.Add(BuildRecord(5, 1, Encoding.UTF8.GetBytes("body-before-begin")));
    result.Add(BuildRecord(4, 1, [0x80]));
    result.Add(BuildRecord(4, 1, [0x01, 0x80, 0x00]));

    result.Add(BuildRequestWithParams(new Dictionary<string, string>
    {
        ["REQUEST_METHOD"] = "GET",
        ["SCRIPT_NAME"] = "/index.xps",
        ["SCRIPT_FILENAME"] = Path.Combine(root, "..", "escape.xps"),
        ["QUERY_STRING"] = ""
    }));

    result.Add(BuildRequestWithDuplicateParam("REQUEST_METHOD", "GET", "POST"));
    result.Add(BuildRequestWithRawParamBytes([0x01, 0x01, 0xff, 0x61]));
    result.Add(BuildRequestWithRawParamBytes(BuildOversizedParamName()));

    var bodyTooLarge = new byte[70 * 1024];
    result.Add(BuildRequestWithBody(bodyTooLarge, root));
    return result;
}

static byte[] Mutate(byte[] source, Random random)
{
    var copy = source.ToArray();
    var operations = random.Next(1, 6);
    for (var op = 0; op < operations; op++)
    {
        if (copy.Length == 0) return [unchecked((byte)random.Next(256))];
        switch (random.Next(5))
        {
            case 0:
                copy[random.Next(copy.Length)] ^= unchecked((byte)(1 << random.Next(8)));
                break;
            case 1:
                copy[random.Next(copy.Length)] = unchecked((byte)random.Next(256));
                break;
            case 2:
                copy = copy[..random.Next(copy.Length + 1)];
                break;
            case 3 when copy.Length < 8192:
            {
                var at = random.Next(copy.Length + 1);
                var extra = new byte[random.Next(1, 17)];
                random.NextBytes(extra);
                var expanded = new byte[copy.Length + extra.Length];
                copy.AsSpan(0, at).CopyTo(expanded);
                extra.CopyTo(expanded, at);
                copy.AsSpan(at).CopyTo(expanded.AsSpan(at + extra.Length));
                copy = expanded;
                break;
            }
            default:
                Array.Reverse(copy, random.Next(copy.Length), Math.Min(random.Next(1, 9), copy.Length));
                break;
        }
    }
    return copy;
}

static byte[] BuildValidRequest(string scriptPath) => BuildRequestWithParams(new Dictionary<string, string>
{
    ["REQUEST_METHOD"] = "GET",
    ["SCRIPT_NAME"] = "/index.xps",
    ["SCRIPT_FILENAME"] = scriptPath,
    ["QUERY_STRING"] = "a=1",
    ["SERVER_NAME"] = "localhost",
    ["SERVER_PROTOCOL"] = "HTTP/1.1",
    ["REMOTE_ADDR"] = "127.0.0.1"
});

static byte[] BuildRequestWithParams(IReadOnlyDictionary<string, string> parameters)
{
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
    return BuildRequestWithRawParamBytes(encoded.ToArray());
}

static byte[] BuildRequestWithDuplicateParam(string name, string first, string second)
{
    using var encoded = new MemoryStream();
    foreach (var value in new[] { first, second })
    {
        var n = Encoding.UTF8.GetBytes(name);
        var v = Encoding.UTF8.GetBytes(value);
        WriteLength(encoded, n.Length);
        WriteLength(encoded, v.Length);
        encoded.Write(n);
        encoded.Write(v);
    }
    return BuildRequestWithRawParamBytes(encoded.ToArray());
}

static byte[] BuildRequestWithRawParamBytes(byte[] paramBytes)
{
    using var output = new MemoryStream();
    var begin = new byte[8];
    BinaryPrimitives.WriteUInt16BigEndian(begin.AsSpan(0, 2), 1);
    output.Write(BuildRecord(1, 1, begin));
    output.Write(BuildRecord(4, 1, paramBytes));
    output.Write(BuildRecord(4, 1, []));
    output.Write(BuildRecord(5, 1, []));
    return output.ToArray();
}

static byte[] BuildRequestWithBody(byte[] body, string root)
{
    using var output = new MemoryStream();
    var begin = new byte[8];
    BinaryPrimitives.WriteUInt16BigEndian(begin.AsSpan(0, 2), 1);
    output.Write(BuildRecord(1, 1, begin));

    using var encoded = new MemoryStream();
    var parameters = new Dictionary<string, string>
    {
        ["REQUEST_METHOD"] = "POST",
        ["SCRIPT_NAME"] = "/index.xps",
        ["SCRIPT_FILENAME"] = Path.Combine(root, "index.xps"),
        ["CONTENT_LENGTH"] = body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
    };
    foreach (var pair in parameters)
    {
        var name = Encoding.UTF8.GetBytes(pair.Key);
        var value = Encoding.UTF8.GetBytes(pair.Value);
        WriteLength(encoded, name.Length);
        WriteLength(encoded, value.Length);
        encoded.Write(name);
        encoded.Write(value);
    }
    output.Write(BuildRecord(4, 1, encoded.ToArray()));
    output.Write(BuildRecord(4, 1, []));
    for (var offset = 0; offset < body.Length; offset += 32000)
    {
        var count = Math.Min(32000, body.Length - offset);
        output.Write(BuildRecord(5, 1, body.AsSpan(offset, count).ToArray()));
    }
    output.Write(BuildRecord(5, 1, []));
    return output.ToArray();
}

static byte[] BuildOversizedParamName()
{
    using var output = new MemoryStream();
    WriteLength(output, 600);
    WriteLength(output, 1);
    output.Write(new byte[600]);
    output.WriteByte((byte)'x');
    return output.ToArray();
}

static byte[] BuildRecord(byte type, ushort requestId, byte[] content)
{
    if (content.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(content));
    var result = new byte[8 + content.Length];
    result[0] = 1;
    result[1] = type;
    BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(2, 2), requestId);
    BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(4, 2), checked((ushort)content.Length));
    content.CopyTo(result, 8);
    return result;
}

static void WriteLength(Stream output, int length)
{
    if (length < 128)
    {
        output.WriteByte((byte)length);
        return;
    }
    Span<byte> bytes = stackalloc byte[4];
    BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)length | 0x80000000u);
    output.Write(bytes);
}

sealed class NoopHandler : IXpsWebRequestHandler
{
    public Task HandleAsync(XpsWebContext context)
    {
        context.Response.ContentType = "text/plain";
        context.Response.Write("ok");
        return Task.CompletedTask;
    }
}

sealed class DuplexStream(byte[] input, int maxRead) : Stream
{
    private readonly MemoryStream _input = new(input, writable: false);
    private readonly MemoryStream _output = new();
    public byte[] Written => _output.ToArray();
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => _output.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _output.FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => _input.Read(buffer, offset, Math.Min(count, maxRead));
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _input.ReadAsync(buffer[..Math.Min(buffer.Length, maxRead)], cancellationToken);
    public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        _output.WriteAsync(buffer, cancellationToken);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
