using System.Text;

internal static class ResponseBodyExtensions
{
    public static bool Contains(this ReadOnlyMemory<byte> body, string value, StringComparison comparison) =>
        Encoding.UTF8.GetString(body.Span).Contains(value, comparison);
}
