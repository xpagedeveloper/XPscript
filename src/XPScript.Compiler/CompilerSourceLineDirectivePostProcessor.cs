using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class CompilerSourceLineDirectivePostProcessor
{
    private static readonly Regex MarkerPattern = new(
        @"(?m)^(?<indent>[ \t]*)XPSourceLineRuntime\.__XPSOURCE_(?<line>\d+)_(?<source>[0-9A-F]+)\(\);\s*$",
        RegexOptions.CultureInvariant);

    private const string RuntimeBoundary = "internal static class LSControlRuntime";

    public string Transform(string generated)
    {
        if (string.IsNullOrEmpty(generated)) return generated;

        var foundMarker = false;
        var rewritten = MarkerPattern.Replace(generated, match =>
        {
            foundMarker = true;
            var indent = match.Groups["indent"].Value;
            var line = match.Groups["line"].Value;
            var sourceId = DecodeSourceId(match.Groups["source"].Value);
            var directiveSource = EscapeDirectiveString(sourceId);
            return string.Join(Environment.NewLine,
                indent + "// XPSOURCE|" + sourceId + "|" + line,
                indent + "#line " + line + " \"" + directiveSource + "\"",
                indent + "XPSourceLineRuntime.Set(" + line + ");");
        });

        if (!foundMarker) return rewritten;

        var boundary = rewritten.IndexOf(RuntimeBoundary, StringComparison.Ordinal);
        if (boundary < 0)
            throw new CompilerException("Unable to restore generated source line mapping before runtime code.");

        return rewritten.Insert(boundary, "#line default" + Environment.NewLine);
    }

    private static string DecodeSourceId(string hex)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromHexString(hex));
        }
        catch (Exception ex)
        {
            throw new CompilerException("Invalid generated XPScript source mapping marker: " + ex.Message);
        }
    }

    private static string EscapeDirectiveString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
