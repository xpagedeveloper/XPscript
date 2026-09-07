using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NotesMimeTypePreprocessor
{
    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!PreprocessorFeatureGate.ContainsAny(source, "NotesMIMEEntity", "NotesMIMEHeader")) return source;

        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length);
        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();
            var dim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+(NotesMIMEEntity|NotesMIMEHeader)\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                variables.Add(dim.Groups[1].Value);
                output.Add(indent + "Dim " + dim.Groups[1].Value + " As Variant");
                output.Add(indent + dim.Groups[1].Value + " = XPScriptNotes.NothingValue");
                continue;
            }

            var set = Regex.Match(line, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && variables.Contains(set.Groups[1].Value))
            {
                var rhs = set.Groups[2].Value.Trim();
                rhs = rhs.Equals("Nothing", StringComparison.OrdinalIgnoreCase) ? "XPScriptNotes.NothingValue" : "XPScriptNotes.NormalizeObjectResult(" + rhs + ")";
                output.Add(indent + set.Groups[1].Value + " = " + rhs);
                continue;
            }

            output.Add(raw);
        }
        return string.Join(Environment.NewLine, output);
    }
}
