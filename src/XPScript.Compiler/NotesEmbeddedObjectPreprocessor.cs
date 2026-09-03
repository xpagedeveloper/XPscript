using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NotesEmbeddedObjectPreprocessor
{
    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.Contains("NotesEmbeddedObject", StringComparison.OrdinalIgnoreCase)) return source;

        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 8);
        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var replacementIndex = 0;

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+NotesEmbeddedObject\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                var name = dim.Groups[1].Value;
                variables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                output.Add(indent + $"{name} = XPScriptNotes.NothingValue");
                continue;
            }

            var recycle = Regex.Match(line, @"^(?:Call\s+)?([A-Za-z_]\w*)\.Recycle\s*\(\s*\)\s*$", RegexOptions.IgnoreCase);
            if (recycle.Success && variables.Contains(recycle.Groups[1].Value))
            {
                var name = recycle.Groups[1].Value;
                output.Add(indent + $"Call XPScriptNotes.RecycleValue({name})");
                output.Add(indent + $"{name} = XPScriptNotes.NothingValue");
                continue;
            }

            var set = Regex.Match(line, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && variables.Contains(set.Groups[1].Value))
            {
                var name = set.Groups[1].Value;
                var rhs = set.Groups[2].Value.Trim();
                rhs = rhs.Equals("Nothing", StringComparison.OrdinalIgnoreCase)
                    ? "XPScriptNotes.NothingValue"
                    : $"XPScriptNotes.NormalizeObjectResult({rhs})";
                var temp = "__notesEmbeddedReplacement" + (++replacementIndex).ToString(System.Globalization.CultureInfo.InvariantCulture);
                output.Add(indent + $"Dim {temp} As Variant");
                output.Add(indent + $"{temp} = {rhs}");
                output.Add(indent + $"Call XPScriptNotes.RecycleForReplacement({name}, {temp})");
                output.Add(indent + $"{name} = {temp}");
                continue;
            }

            output.Add(raw);
        }

        return string.Join(Environment.NewLine, output);
    }
}
