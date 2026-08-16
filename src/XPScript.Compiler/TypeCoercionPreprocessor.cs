using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class TypeCoercionPreprocessor
{
    private static readonly HashSet<string> NumericTypes = new(StringComparer.OrdinalIgnoreCase)
    { "Byte", "Integer", "Long", "Single", "Double", "Currency" };

    public string Transform(string source)
    {
        source = new IncrementCompoundAssignmentPreprocessor().Transform(source);

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var output = new string[lines.Length];

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = StripComment(line).Trim();

            if (Regex.IsMatch(trimmed, @"^(?:Sub|Function|Property)\b", RegexOptions.IgnoreCase)) variables.Clear();

            var dim = Regex.Match(trimmed, @"^Dim\s+([A-Za-z_]\w*)\s*(?:\([^)]*\))?\s+As\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
            if (dim.Success) variables[dim.Groups[1].Value] = dim.Groups[2].Value;

            var assign = Regex.Match(trimmed, @"^(?:Let\s+)?([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (assign.Success && variables.TryGetValue(assign.Groups[1].Value, out var targetType))
            {
                var rhs = assign.Groups[2].Value.Trim();
                var plus = FindTopLevelPlus(rhs);
                if (plus > 0 && (NumericTypes.Contains(targetType) || targetType.Equals("Variant", StringComparison.OrdinalIgnoreCase)))
                {
                    var left = rhs[..plus].Trim();
                    var right = rhs[(plus + 1)..].Trim();
                    var method = targetType.ToLowerInvariant() switch
                    {
                        "byte" => "AddByte",
                        "integer" => "AddInteger",
                        "long" => "AddLong",
                        "single" => "AddSingle",
                        "double" => "AddDouble",
                        "currency" => "AddCurrency",
                        "variant" => "AddVariant",
                        _ => "AddVariant"
                    };
                    var indent = line[..(line.Length - line.TrimStart().Length)];
                    output[i] = RewriteNullSemantics($"{indent}{assign.Groups[1].Value} = XPScriptCoercion.{method}({left}, {right})");
                    continue;
                }
            }

            output[i] = RewriteNullSemantics(line);
        }
        return string.Join("\n", output);
    }

    private static string RewriteNullSemantics(string line)
    {
        var output = new StringBuilder(line.Length + 32);
        var code = new StringBuilder();
        var inString = false;

        void FlushCode()
        {
            if (code.Length == 0) return;
            var text = code.ToString();
            text = Regex.Replace(text, @"(?<![\w.])IsNull\s*\(", "XPScriptNullRuntime.IsNull(", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<![\w.])IsEmpty\s*\(", "XPScriptNullRuntime.IsEmpty(", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<![\w.])DataType\s*\(", "XPScriptNullRuntime.DataType(", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<![\w.])TypeName\s*\(", "XPScriptNullRuntime.TypeName(", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<![\w.])Null\b", "XPScriptNullRuntime.NullValue", RegexOptions.IgnoreCase);
            output.Append(text);
            code.Clear();
        }

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (!inString && c == '\'')
            {
                FlushCode();
                output.Append(line.AsSpan(i));
                return output.ToString();
            }

            if (c != '"')
            {
                if (inString) output.Append(c); else code.Append(c);
                continue;
            }

            if (!inString)
            {
                FlushCode();
                inString = true;
                output.Append(c);
                continue;
            }

            output.Append(c);
            if (i + 1 < line.Length && line[i + 1] == '"')
            {
                output.Append(line[++i]);
                continue;
            }
            inString = false;
        }

        FlushCode();
        return output.ToString();
    }

    private static int FindTopLevelPlus(string value)
    {
        var inString = false; var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString; continue;
            }
            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == '+' && depth == 0) return i;
        }
        return -1;
    }

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
