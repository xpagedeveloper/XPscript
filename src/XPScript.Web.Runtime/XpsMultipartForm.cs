using System.Collections.ObjectModel;
using System.Text;

namespace XPScript.Web.Runtime;

public sealed class XpsUploadedFile
{
    private readonly byte[] _content;

    internal XpsUploadedFile(string name, string fileName, string? contentType, ReadOnlySpan<byte> content)
    {
        Name = name;
        FileName = fileName;
        ContentType = contentType;
        _content = content.ToArray();
    }

    public string Name { get; }
    public string FileName { get; }
    public string? ContentType { get; }
    public long Length => _content.LongLength;
    public ReadOnlyMemory<byte> Content => _content;

    public byte[] Bytes() => _content.ToArray();

    public string Text(int maxBytes = 1_048_576)
    {
        if (maxBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        if (_content.Length > maxBytes)
            throw new InvalidOperationException($"Uploaded file exceeds the configured {maxBytes} byte text limit.");
        return new UTF8Encoding(false, true).GetString(_content);
    }
}

internal sealed class XpsMultipartFormData
{
    internal XpsMultipartFormData(
        IReadOnlyDictionary<string, IReadOnlyList<string>> fields,
        IReadOnlyList<XpsUploadedFile> files)
    {
        Fields = fields;
        Files = files;
    }

    internal IReadOnlyDictionary<string, IReadOnlyList<string>> Fields { get; }
    internal IReadOnlyList<XpsUploadedFile> Files { get; }
}

internal static class XpsMultipartFormParser
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal static XpsMultipartFormData Parse(
        string contentType,
        ReadOnlyMemory<byte> body,
        int maxBodyBytes,
        int maxFields,
        int maxFiles,
        int maxFileBytes,
        int maxHeaderBytes)
    {
        if (maxBodyBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxBodyBytes));
        if (maxFields is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(maxFields));
        if (maxFiles is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(maxFiles));
        if (maxFileBytes < 0 || maxFileBytes > maxBodyBytes) throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
        if (maxHeaderBytes is < 256 or > 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maxHeaderBytes));
        if (body.Length > maxBodyBytes)
            throw new InvalidOperationException($"Multipart request body exceeds the configured {maxBodyBytes} byte limit.");

        var boundary = ExtractBoundary(contentType);
        var marker = Encoding.ASCII.GetBytes("--" + boundary);
        var delimiter = Encoding.ASCII.GetBytes("\r\n--" + boundary);
        var span = body.Span;
        if (!span.StartsWith(marker)) throw new InvalidOperationException("Multipart body does not begin with the declared boundary.");

        var fields = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var files = new List<XpsUploadedFile>();
        var fieldCount = 0;
        var position = marker.Length;

        while (true)
        {
            if (HasBytes(span, position, "--"u8))
            {
                position += 2;
                if (position < span.Length)
                {
                    if (!HasBytes(span, position, "\r\n"u8) || position + 2 != span.Length)
                        throw new InvalidOperationException("Multipart body contains data after the final boundary.");
                }
                break;
            }

            if (!HasBytes(span, position, "\r\n"u8))
                throw new InvalidOperationException("Multipart boundary is not followed by CRLF.");
            position += 2;

            var remaining = span[position..];
            var headerEndOffset = remaining.IndexOf("\r\n\r\n"u8);
            if (headerEndOffset < 0) throw new InvalidOperationException("Multipart part headers are incomplete.");
            if (headerEndOffset > maxHeaderBytes)
                throw new InvalidOperationException($"Multipart part headers exceed the configured {maxHeaderBytes} byte limit.");

            var headers = ParseHeaders(remaining[..headerEndOffset]);
            position += headerEndOffset + 4;

            var contentRemaining = span[position..];
            var nextBoundaryOffset = contentRemaining.IndexOf(delimiter);
            if (nextBoundaryOffset < 0) throw new InvalidOperationException("Multipart part is missing a terminating boundary.");
            var partContent = contentRemaining[..nextBoundaryOffset];

            if (!headers.TryGetValue("Content-Disposition", out var disposition))
                throw new InvalidOperationException("Multipart part is missing Content-Disposition.");
            var parameters = ParseDisposition(disposition);
            if (!parameters.TryGetValue(string.Empty, out var dispositionType) ||
                !dispositionType.Equals("form-data", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Multipart Content-Disposition must be form-data.");
            if (!parameters.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Multipart form-data part is missing a field name.");
            ValidateFieldName(name);

            if (parameters.TryGetValue("filename", out var rawFileName) || parameters.TryGetValue("filename*", out rawFileName))
            {
                if (files.Count >= maxFiles)
                    throw new InvalidOperationException($"Multipart request exceeds the configured {maxFiles} file limit.");
                if (partContent.Length > maxFileBytes)
                    throw new InvalidOperationException($"Uploaded file exceeds the configured {maxFileBytes} byte limit.");
                var fileName = NormalizeFileName(rawFileName);
                if (fileName.Length > 0)
                {
                    headers.TryGetValue("Content-Type", out var fileContentType);
                    if (fileContentType is not null) XpsWebResponse.ValidateHeaderValue(fileContentType);
                    files.Add(new XpsUploadedFile(name, fileName, fileContentType, partContent));
                }
            }
            else
            {
                fieldCount++;
                if (fieldCount > maxFields)
                    throw new InvalidOperationException($"Multipart request exceeds the configured {maxFields} field limit.");
                string value;
                try { value = StrictUtf8.GetString(partContent); }
                catch (DecoderFallbackException ex) { throw new InvalidOperationException("Multipart text field is not valid UTF-8.", ex); }
                if (!fields.TryGetValue(name, out var values)) fields[name] = values = [];
                values.Add(value);
            }

            position += nextBoundaryOffset + delimiter.Length;
        }

        var frozenFields = new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            fields.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()),
                StringComparer.OrdinalIgnoreCase));
        return new XpsMultipartFormData(frozenFields, Array.AsReadOnly(files.ToArray()));
    }

    private static string ExtractBoundary(string contentType)
    {
        if (!contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Request is not multipart/form-data.");
        foreach (var part in SplitParameters(contentType))
        {
            var equals = part.IndexOf('=');
            if (equals <= 0) continue;
            var key = part[..equals].Trim();
            if (!key.Equals("boundary", StringComparison.OrdinalIgnoreCase)) continue;
            var value = Unquote(part[(equals + 1)..].Trim());
            if (value.Length is < 1 or > 70 || value.Any(c => c < 0x21 || c > 0x7e || c is '"' or '\\'))
                throw new InvalidOperationException("Multipart boundary is invalid.");
            return value;
        }
        throw new InvalidOperationException("Multipart boundary parameter is missing.");
    }

    private static Dictionary<string, string> ParseHeaders(ReadOnlySpan<byte> bytes)
    {
        string text;
        try { text = StrictUtf8.GetString(bytes); }
        catch (DecoderFallbackException ex) { throw new InvalidOperationException("Multipart headers are not valid UTF-8.", ex); }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = text.Split("\r\n", StringSplitOptions.None);
        if (lines.Length > 32) throw new InvalidOperationException("Multipart part contains too many headers.");
        foreach (var line in lines)
        {
            if (line.Length == 0) continue;
            var colon = line.IndexOf(':');
            if (colon <= 0) throw new InvalidOperationException("Multipart part contains a malformed header.");
            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            XpsWebResponse.ValidateHeaderName(name);
            XpsWebResponse.ValidateHeaderValue(value);
            if (!result.TryAdd(name, value))
                throw new InvalidOperationException("Multipart part contains a duplicate header.");
        }
        return result;
    }

    private static Dictionary<string, string> ParseDisposition(string value)
    {
        var segments = SplitParameters(value);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (segments.Count == 0) throw new InvalidOperationException("Multipart Content-Disposition is empty.");
        result[string.Empty] = segments[0].Trim();
        for (var i = 1; i < segments.Count; i++)
        {
            var equals = segments[i].IndexOf('=');
            if (equals <= 0) continue;
            var key = segments[i][..equals].Trim();
            var raw = segments[i][(equals + 1)..].Trim();
            if (key.Equals("filename*", StringComparison.OrdinalIgnoreCase))
            {
                var marker = raw.IndexOf("''", StringComparison.Ordinal);
                raw = marker >= 0 ? Uri.UnescapeDataString(raw[(marker + 2)..]) : Unquote(raw);
            }
            else
            {
                raw = Unquote(raw);
            }
            result[key] = raw;
        }
        return result;
    }

    private static List<string> SplitParameters(string value)
    {
        var result = new List<string>();
        var builder = new StringBuilder();
        var quoted = false;
        var escaped = false;
        foreach (var c in value)
        {
            if (escaped)
            {
                builder.Append(c);
                escaped = false;
                continue;
            }
            if (quoted && c == '\\')
            {
                builder.Append(c);
                escaped = true;
                continue;
            }
            if (c == '"') quoted = !quoted;
            if (c == ';' && !quoted)
            {
                result.Add(builder.ToString());
                builder.Clear();
                continue;
            }
            builder.Append(c);
        }
        if (quoted) throw new InvalidOperationException("Multipart header contains an unterminated quoted value.");
        result.Add(builder.ToString());
        return result;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
            value = value.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }
        return value;
    }

    private static string NormalizeFileName(string value)
    {
        if (value.IndexOfAny(['\r', '\n', '\0']) >= 0 || value.Any(char.IsControl))
            throw new InvalidOperationException("Uploaded file name contains a prohibited character.");
        var normalized = value.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        if (slash >= 0) normalized = normalized[(slash + 1)..];
        if (normalized.Length > 255) throw new InvalidOperationException("Uploaded file name is too long.");
        return normalized;
    }

    private static void ValidateFieldName(string name)
    {
        if (name.Length > 256 || name.Any(char.IsControl))
            throw new InvalidOperationException("Multipart field name is invalid.");
    }

    private static bool HasBytes(ReadOnlySpan<byte> source, int offset, ReadOnlySpan<byte> expected) =>
        offset >= 0 && offset + expected.Length <= source.Length && source.Slice(offset, expected.Length).SequenceEqual(expected);
}
