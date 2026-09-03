using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NotesSessionAutoDetectPreprocessor
{
    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.Contains("NotesSession", StringComparison.OrdinalIgnoreCase)) return source;

        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 8);

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNew = Regex.Match(
                line,
                @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+NotesSession\s*(?:\(\s*\))?\s*$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                output.Add(indent + "Dim " + name + " As NotesSession");
                output.Add(indent + "Set " + name + " = New NotesSession()");
                continue;
            }

            // LotusScript permits parameterless construction without parentheses.
            line = Regex.Replace(
                line,
                @"\bNew\s+NotesSession\b(?!\s*\()",
                "New NotesSession()",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            output.Add(indent + line);
        }

        return string.Join(Environment.NewLine, output);
    }
}
