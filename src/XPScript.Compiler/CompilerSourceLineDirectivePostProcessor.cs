using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class CompilerSourceLineDirectivePostProcessor
{
    private static readonly Regex MarkerPattern = new(
        @"XPSourceLineRuntime\.__XPSOURCE_(?<line>\d+)_(?<source>[0-9A-F]+)\(\)",
        RegexOptions.CultureInvariant);

    private static readonly Regex ScriptProcedurePattern = new(
        @"^\s*(?:public|private)\s+(?:static\s+)?(?:override\s+)?(?:[A-Za-z_]\w*(?:<[^>]+>)?(?:\[\])?\??\s+)?[A-Za-z_]\w*\s*\(",
        RegexOptions.CultureInvariant);

    private const string RuntimeBoundary = "internal static class LSControlRuntime";
    private const string ScriptBoundary = "internal static class Script";
    private const string NoInliningAttribute = "[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]";

    public string Transform(string generated)
    {
        if (string.IsNullOrEmpty(generated)) return generated;

        var lines = generated.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 64);
        var foundMarker = false;
        var runtimeBoundaryInserted = false;
        var scriptDeclarationSeen = false;
        var inScript = false;

        foreach (var rawLine in lines)
        {
            if (!runtimeBoundaryInserted && rawLine.Contains(RuntimeBoundary, StringComparison.Ordinal))
            {
                if (foundMarker) output.Add("#line default");
                runtimeBoundaryInserted = true;
            }

            if (!scriptDeclarationSeen && rawLine.Trim().Equals(ScriptBoundary, StringComparison.Ordinal))
            {
                scriptDeclarationSeen = true;
                output.Add(rawLine);
                continue;
            }

            if (scriptDeclarationSeen && !inScript && rawLine.Trim().Equals("{", StringComparison.Ordinal))
            {
                inScript = true;
                output.Add(rawLine);
                continue;
            }

            if (inScript && rawLine.Equals("}", StringComparison.Ordinal))
                inScript = false;

            var match = MarkerPattern.Match(rawLine);
            if (!match.Success)
            {
                if (inScript && ScriptProcedurePattern.IsMatch(rawLine))
                {
                    var indent = Regex.Match(rawLine, @"^\s*").Value;
                    output.Add(indent + NoInliningAttribute);
                }
                output.Add(rawLine);
                continue;
            }

            foundMarker = true;
            var markerIndent = Regex.Match(rawLine, @"^\s*").Value;
            var sourceLine = match.Groups["line"].Value;
            var sourceId = DecodeSourceId(match.Groups["source"].Value);
            var directiveSource = EscapeDirectiveString(sourceId);

            output.Add(markerIndent + "// XPSOURCE|" + sourceId + "|" + sourceLine);
            output.Add(markerIndent + "#line " + sourceLine + " \"" + directiveSource + "\"");
            output.Add(MarkerPattern.Replace(rawLine, "XPSourceLineRuntime.Set(" + sourceLine + ")", 1));
        }

        if (foundMarker && !runtimeBoundaryInserted)
            throw new CompilerException("Unable to restore generated source line mapping before runtime code.");

        return string.Join("\n", output);
    }

    private static string DecodeSourceId(string hex)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromHexString(hex));
        }
        catch (Exception)
        {
            throw new CompilerException("Invalid generated XPScript source mapping marker.");
        }
    }

    private static string EscapeDirectiveString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
