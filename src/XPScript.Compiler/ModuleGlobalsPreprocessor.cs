using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ModuleGlobalsPreprocessor
{
    private readonly List<string> _declarations = [];
    public IReadOnlyList<string> Declarations => _declarations;

    public string Transform(string source)
    {
        _declarations.Clear();
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length);
        var inClass = false;
        var inProcedure = false;

        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            if (Regex.IsMatch(line, @"^(?:(?:Public|Private)\s+)?Class\b", RegexOptions.IgnoreCase)) inClass = true;
            if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase)) { inClass = false; output.Add(raw); continue; }
            if (Regex.IsMatch(line, @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function|Property)\b", RegexOptions.IgnoreCase)) inProcedure = true;
            if (Regex.IsMatch(line, @"^End\s+(?:Sub|Function|Property)$", RegexOptions.IgnoreCase)) { inProcedure = false; output.Add(raw); continue; }

            if (!inClass && !inProcedure)
            {
                var match = Regex.Match(line, @"^(Public|Private)\s+([A-Za-z_]\w*)\s+As\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var visibility = match.Groups[1].Value.Equals("Public", StringComparison.OrdinalIgnoreCase) ? "public" : "private";
                    var name = match.Groups[2].Value;
                    var type = MapType(match.Groups[3].Value);
                    _declarations.Add($"    {visibility} static {type} {name} = {DefaultValue(type)};");
                    output.Add("");
                    continue;
                }
            }

            output.Add(raw);
        }

        return string.Join(Environment.NewLine, output);
    }

    public string Inject(string generated)
    {
        if (_declarations.Count == 0) return generated;
        var marker = "internal static class Script\n{";
        var index = generated.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0) throw new CompilerException("Unable to inject module-level variables into generated Script class.");
        var insertion = marker + Environment.NewLine + string.Join(Environment.NewLine, _declarations) + Environment.NewLine;
        return generated[..index] + insertion + generated[(index + marker.Length)..];
    }

    private static string MapType(string type) => type.Trim().ToLowerInvariant() switch
    {
        "string" => "string", "integer" or "int" => "int", "long" => "long", "double" => "double", "single" => "float",
        "boolean" or "bool" => "bool", "byte" => "byte", "currency" => "decimal", "date" => "DateTime", "variant" => "dynamic", "object" => "object",
        _ => "dynamic"
    };

    private static string DefaultValue(string type) => type switch
    {
        "string" => "\"\"", "bool" => "false", "byte" or "int" or "long" or "float" or "double" or "decimal" => "0", "DateTime" => "default", _ => "null"
    };

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = !inString;
            }
            else if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }
}
