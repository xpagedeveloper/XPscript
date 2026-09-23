using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class JsonNameMetadataPreprocessor
{
    internal sealed record Mapping(string ClassName, string MemberName, string JsonName);
    public List<Mapping> Mappings { get; } = [];

    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length);
        string? currentClass = null;
        string? pendingName = null;
        foreach (var raw in lines)
        {
            var code = StripComment(raw).Trim();
            var classMatch = Regex.Match(code, @"^(?:(?:Public|Private)\s+)?Class\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
            if (classMatch.Success) currentClass = classMatch.Groups[1].Value;
            if (Regex.IsMatch(code, @"^End\s+Class$", RegexOptions.IgnoreCase)) currentClass = null;
            var metadata = Regex.Match(code, "^\\[JsonName\\(\"((?:\"\"|[^\"])*)\"\\)\\]$", RegexOptions.IgnoreCase);
            if (metadata.Success)
            {
                if (currentClass is null) throw new CompilerException("[JsonName] is only valid on class fields.");
                pendingName = metadata.Groups[1].Value.Replace("\"\"", "\"", StringComparison.Ordinal);
                if (pendingName.Length == 0) throw new CompilerException("[JsonName] requires a non-empty JSON property name.");
                output.Add(""); continue;
            }
            if (pendingName is not null)
            {
                var field = Regex.Match(code, @"^(?:(?:Public|Private)\s+)?([A-Za-z_]\w*)\s*(?:\([^)]*\))?\s+(?:List\s+)?As\s+[A-Za-z_]\w*\s*$", RegexOptions.IgnoreCase);
                if (!field.Success) throw new CompilerException("[JsonName] must be followed by a class field declaration.");
                Mappings.Add(new Mapping(currentClass!, field.Groups[1].Value, pendingName)); pendingName = null;
            }
            output.Add(raw);
        }
        if (pendingName is not null) throw new CompilerException("[JsonName] must be followed by a class field declaration.");
        return string.Join(Environment.NewLine, output);
    }

    public string ApplyToGeneratedCode(string generated)
    {
        foreach (var mapping in Mappings)
        {
            var escaped = mapping.JsonName.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
            var classPattern = $@"\bclass\s+{Regex.Escape(mapping.ClassName)}\b[^{{]*\{{";
            var classMatch = Regex.Match(generated, classPattern, RegexOptions.CultureInvariant);
            if (!classMatch.Success) throw new CompilerException($"[JsonName] generated class '{mapping.ClassName}' was not found.");
            var openBrace = generated.IndexOf('{', classMatch.Index);
            var closeBrace = FindMatchingBrace(generated, openBrace);
            if (closeBrace < 0) throw new CompilerException($"[JsonName] generated class '{mapping.ClassName}' is malformed.");
            var body = generated.Substring(openBrace + 1, closeBrace - openBrace - 1);
            var fieldPattern = $@"(?m)(?<indent>^[ \t]*)(?<decl>(?:public|private|protected|internal)\s+[^\r\n;=]+\s+{Regex.Escape(mapping.MemberName)}\s*(?:=[^;]*)?;)";
            var fieldRegex = new Regex(fieldPattern, RegexOptions.CultureInvariant);
            var fieldMatch = fieldRegex.Match(body);
            if (!fieldMatch.Success) throw new CompilerException($"[JsonName] generated field '{mapping.ClassName}.{mapping.MemberName}' was not found.");
            var replacement = $"{fieldMatch.Groups["indent"].Value}[System.Text.Json.Serialization.JsonPropertyName(\"{escaped}\")]\n{fieldMatch.Groups["indent"].Value}{fieldMatch.Groups["decl"].Value}";
            body = fieldRegex.Replace(body, replacement, 1);
            generated = generated[..(openBrace + 1)] + body + generated[closeBrace..];
        }
        return generated;
    }

    private static int FindMatchingBrace(string text, int openBrace)
    {
        var depth = 0;
        for (var i = openBrace; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++) { if (line[i] == '\"') { if (inString && i + 1 < line.Length && line[i + 1] == '\"') { i++; continue; } inString = !inString; } else if (!inString && line[i] == '\'') return line[..i]; }
        return line;
    }
}
