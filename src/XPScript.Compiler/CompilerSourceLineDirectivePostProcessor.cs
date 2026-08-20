using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class CompilerSourceLineDirectivePostProcessor
{
    private static readonly Regex MarkerPattern = new(
        @"XPSourceLineRuntime\.__XPSOURCE_(?<line>\d+)_(?<source>[0-9A-F]+)\(\)",
        RegexOptions.CultureInvariant);

    private const string RuntimeBoundary = "internal static class LSControlRuntime";

    public string Transform(string generated)
    {
        if (string.IsNullOrEmpty(generated)) return generated;

        var lines = generated.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 32);
        var foundMarker = false;
        var runtimeBoundaryInserted = false;

        foreach (var rawLine in lines)
        {
            if (!runtimeBoundaryInserted && rawLine.Contains(RuntimeBoundary, StringComparison.Ordinal))
            {
                if (foundMarker) output.Add("#line default");
                runtimeBoundaryInserted = true;
            }

            var match = MarkerPattern.Match(rawLine);
            if (!match.Success)
            {
                output.Add(rawLine);
                continue;
            }

            foundMarker = true;
            var indent = Regex.Match(rawLine, @"^\s*").Value;
            var sourceLine = match.Groups["line"].Value;
            var sourceId = DecodeSourceId(match.Groups["source"].Value);
            var directiveSource = EscapeDirectiveString(sourceId);

            output.Add(indent + "// XPSOURCE|" + sourceId + "|" + sourceLine);
            output.Add(indent + "#line " + sourceLine + " \"" + directiveSource + "\"");
            output.Add(MarkerPattern.Replace(rawLine, "XPSourceLineRuntime.Set(" + sourceLine + ")", 1));
        }

        if (foundMarker && !runtimeBoundaryInserted)
            throw new CompilerException("Unable to restore generated source line mapping before runtime code.");

        return string.Join(Environment.NewLine, output);
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
