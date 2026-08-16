using System.Buffers.Binary;
using System.Text;

namespace XPScript.Web.FastCgi;

internal enum XpsFastCgiRecordType : byte
{
    BeginRequest = 1,
    AbortRequest = 2,
    EndRequest = 3,
    Params = 4,
    Stdin = 5,
    Stdout = 6,
    Stderr = 7,
    Data = 8,
    GetValues = 9,
    GetValuesResult = 10,
    UnknownType = 11
}

internal static class XpsFastCgiProtocol
{
    internal const byte Version1 = 1;
    internal const ushort ResponderRole = 1;
    internal const byte KeepConnectionFlag = 1;
    internal const byte RequestComplete = 0;
    internal const byte CantMultiplexConnection = 1;
    internal const byte UnknownRole = 3;

    internal readonly record struct Record(
        XpsFastCgiRecordType Type,
        ushort RequestId,
        byte[] Content,
        byte PaddingLength);

    internal static async ValueTask<Record?> ReadRecordAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[8];
        var headerRead = await ReadAtMostAsync(stream, header, cancellationToken).ConfigureAwait(false);
        if (headerRead == 0) return null;
        if (headerRead != header.Length)
            throw new XpsFastCgiProtocolException("Truncated FastCGI record header.");
        if (header[0] != Version1)
            throw new XpsFastCgiProtocolException("Unsupported FastCGI version.");

        var type = (XpsFastCgiRecordType)header[1];
        var requestId = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
        var contentLength = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
        var paddingLength = header[6];
        if (header[7] != 0)
            throw new XpsFastCgiProtocolException("FastCGI reserved header byte must be zero.");

        var content = new byte[contentLength];
        if (contentLength > 0)
            await ReadExactlyAsync(stream, content, cancellationToken).ConfigureAwait(false);
        if (paddingLength > 0)
        {
            Span<byte> padding = paddingLength <= 256 ? stackalloc byte[paddingLength] : new byte[paddingLength];
            await ReadExactlyAsync(stream, padding, cancellationToken).ConfigureAwait(false);
        }

        return new Record(type, requestId, content, paddingLength);
    }

    internal static void ParseParams(
        ReadOnlySpan<byte> content,
        IDictionary<string, string> destination,
        XpsFastCgiOptions options,
        ref int totalParamsBytes,
        ref int totalParamCount)
    {
        totalParamsBytes = checked(totalParamsBytes + content.Length);
        if (totalParamsBytes > options.MaxParamsBytes)
            throw new XpsFastCgiProtocolException("FastCGI PARAMS exceed the configured size limit.");

        var offset = 0;
        while (offset < content.Length)
        {
            var nameLength = ReadLength(content, ref offset);
            var valueLength = ReadLength(content, ref offset);
            if (nameLength > options.MaxParamNameBytes)
                throw new XpsFastCgiProtocolException("FastCGI parameter name exceeds the configured limit.");
            if (valueLength > options.MaxParamValueBytes)
                throw new XpsFastCgiProtocolException("FastCGI parameter value exceeds the configured limit.");
            var required = checked(nameLength + valueLength);
            if (required > content.Length - offset)
                throw new XpsFastCgiProtocolException("Truncated FastCGI PARAMS name/value pair.");

            totalParamCount = checked(totalParamCount + 1);
            if (totalParamCount > options.MaxParamCount)
                throw new XpsFastCgiProtocolException("FastCGI parameter count exceeds the configured limit.");

            var name = DecodeUtf8(content.Slice(offset, nameLength), "parameter name");
            offset += nameLength;
            var value = DecodeUtf8(content.Slice(offset, valueLength), "parameter value");
            offset += valueLength;
            if (string.IsNullOrEmpty(name))
                throw new XpsFastCgiProtocolException("FastCGI parameter name must not be empty.");
            if (destination.ContainsKey(name))
                throw new XpsFastCgiProtocolException("Duplicate FastCGI parameter names are not accepted.");
            destination.Add(name, value);
        }
    }

    internal static async Task WriteRecordAsync(
        Stream stream,
        XpsFastCgiRecordType type,
        ushort requestId,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        if (content.Length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(content));
        var header = new byte[8];
        header[0] = Version1;
        header[1] = (byte)type;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2, 2), requestId);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4, 2), checked((ushort)content.Length));
        header[6] = 0;
        header[7] = 0;
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        if (content.Length > 0) await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task WriteStreamAsync(
        Stream stream,
        XpsFastCgiRecordType type,
        ushort requestId,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < content.Length)
        {
            var count = Math.Min(ushort.MaxValue, content.Length - offset);
            await WriteRecordAsync(stream, type, requestId, content.Slice(offset, count), cancellationToken).ConfigureAwait(false);
            offset += count;
        }
        await WriteRecordAsync(stream, type, requestId, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task WriteEndRequestAsync(
        Stream stream,
        ushort requestId,
        uint appStatus,
        byte protocolStatus,
        CancellationToken cancellationToken)
    {
        var body = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(0, 4), appStatus);
        body[4] = protocolStatus;
        await WriteRecordAsync(stream, XpsFastCgiRecordType.EndRequest, requestId, body, cancellationToken).ConfigureAwait(false);
    }

    private static int ReadLength(ReadOnlySpan<byte> content, ref int offset)
    {
        if (offset >= content.Length)
            throw new XpsFastCgiProtocolException("Truncated FastCGI PARAMS length.");
        var first = content[offset++];
        if ((first & 0x80) == 0) return first;
        if (content.Length - offset < 3)
            throw new XpsFastCgiProtocolException("Truncated FastCGI four-byte PARAMS length.");
        var value = ((first & 0x7f) << 24) |
                    (content[offset] << 16) |
                    (content[offset + 1] << 8) |
                    content[offset + 2];
        offset += 3;
        if (value < 0)
            throw new XpsFastCgiProtocolException("Invalid FastCGI PARAMS length.");
        return value;
    }

    private static string DecodeUtf8(ReadOnlySpan<byte> value, string field)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(value);
        }
        catch (DecoderFallbackException ex)
        {
            throw new XpsFastCgiProtocolException($"FastCGI {field} contains invalid UTF-8.", ex);
        }
    }

    private static async ValueTask<int> ReadAtMostAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static async ValueTask ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await ReadAtMostAsync(stream, buffer, cancellationToken).ConfigureAwait(false);
        if (read != buffer.Length) throw new XpsFastCgiProtocolException("Truncated FastCGI record content.");
    }

    private static async ValueTask ReadExactlyAsync(Stream stream, Span<byte> buffer, CancellationToken cancellationToken)
    {
        var temporary = new byte[buffer.Length];
        await ReadExactlyAsync(stream, temporary, cancellationToken).ConfigureAwait(false);
        temporary.CopyTo(buffer);
    }
}

public sealed class XpsFastCgiProtocolException : Exception
{
    public XpsFastCgiProtocolException(string message) : base(message) { }
    public XpsFastCgiProtocolException(string message, Exception innerException) : base(message, innerException) { }
}
