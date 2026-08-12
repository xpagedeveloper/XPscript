using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ModuleGlobalsPreprocessor
{
    private sealed record ModuleArray(string Name, string ElementType, bool Dynamic);

    private readonly List<string> _declarations = [];
    private readonly Dictionary<string, ModuleArray> _arrays = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _udtTypes;
    private int _optionBase;

    public ModuleGlobalsPreprocessor(IEnumerable<string>? udtTypes = null)
    {
        _udtTypes = udtTypes is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(udtTypes, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> Declarations => _declarations;

    public string Transform(string source)
    {
        _declarations.Clear();
        _arrays.Clear();
        _optionBase = 0;

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length);
        var inClass = false;
        var inProcedure = false;

        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            var optionBase = Regex.Match(line, @"^Option\s+Base\s+([01])$", RegexOptions.IgnoreCase);
            if (optionBase.Success) _optionBase = int.Parse(optionBase.Groups[1].Value);

            if (Regex.IsMatch(line, @"^(?:(?:Public|Private)\s+)?Class\b", RegexOptions.IgnoreCase)) inClass = true;
            if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase)) { inClass = false; output.Add(raw); continue; }
            if (Regex.IsMatch(line, @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function|Property)\b", RegexOptions.IgnoreCase)) inProcedure = true;
            if (Regex.IsMatch(line, @"^End\s+(?:Sub|Function|Property)$", RegexOptions.IgnoreCase)) { inProcedure = false; output.Add(raw); continue; }

            if (!inClass && !inProcedure)
            {
                var array = Regex.Match(line, @"^(Public|Private)\s+([A-Za-z_]\w*)\s*\((.*)\)\s+As\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
                if (array.Success)
                {
                    var visibility = array.Groups[1].Value.Equals("Public", StringComparison.OrdinalIgnoreCase) ? "public" : "private";
                    var name = array.Groups[2].Value;
                    var bounds = array.Groups[3].Value.Trim();
                    var elementType = array.Groups[4].Value;
                    var dynamic = bounds.Length == 0;
                    _arrays[name] = new ModuleArray(name, elementType, dynamic);

                    if (dynamic)
                    {
                        _declarations.Add($"    {visibility} static dynamic {name} = LSArrayRuntime.Dynamic(\"{EscapeCSharp(elementType)}\");");
                    }
                    else
                    {
                        var fixedBounds = ParseConstantBounds(bounds);
                        _declarations.Add($"    {visibility} static dynamic {name} = LSArrayRuntime.Fixed(\"{EscapeCSharp(elementType)}\", new int[] {{ {string.Join(", ", fixedBounds.Lower)} }}, new int[] {{ {string.Join(", ", fixedBounds.Upper)} }});");
                    }
                    output.Add("");
                    continue;
                }

                var match = Regex.Match(line, @"^(Public|Private)\s+([A-Za-z_]\w*)\s+As\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var visibility = match.Groups[1].Value.Equals("Public", StringComparison.OrdinalIgnoreCase) ? "public" : "private";
                    var name = match.Groups[2].Value;
                    var sourceType = match.Groups[3].Value;

                    if (_udtTypes.Contains(sourceType))
                    {
                        // XPScript Public/Private controls source-level module visibility. The generated
                        // Script container is an internal implementation detail, so UDT storage stays
                        // private to avoid exposing a CLR field whose generated type is less accessible.
                        _declarations.Add($"    private static {sourceType} {name} = new {sourceType}();");
                    }
                    else
                    {
                        var type = MapType(sourceType);
                        _declarations.Add($"    {visibility} static {type} {name} = {DefaultValue(type)};");
                    }
                    output.Add("");
                    continue;
                }
            }

            output.Add(raw);
        }

        if (_arrays.Count == 0)
            return string.Join(Environment.NewLine, output);

        for (var i = 0; i < output.Count; i++)
            output[i] = RewriteArrayUses(output[i]);

        return string.Join(Environment.NewLine, output);
    }

    public string Inject(string generated)
    {
        if (_declarations.Count == 0) return generated;

        var marker = Regex.Match(
            generated,
            @"internal\s+static\s+class\s+Script\s*\r?\n\s*\{",
            RegexOptions.CultureInvariant);
        if (!marker.Success)
            throw new CompilerException("Unable to inject module-level variables into generated Script class.");

        var insertion = marker.Value + Environment.NewLine + string.Join(Environment.NewLine, _declarations) + Environment.NewLine;
        return generated[..marker.Index] + insertion + generated[(marker.Index + marker.Length)..];
    }

    private string RewriteArrayUses(string raw)
    {
        var (code, comment) = SplitComment(raw);
        if (string.IsNullOrWhiteSpace(code)) return raw;
        var indent = Regex.Match(code, @"^\s*").Value;
        var trimmed = code.Trim();

        foreach (var array in _arrays.Values.OrderByDescending(x => x.Name.Length))
        {
            var name = Regex.Escape(array.Name);

            var redim = Regex.Match(trimmed, $@"^ReDim\s+(Preserve\s+)?{name}\s*\((.*)\)\s*(?:As\s+([A-Za-z_]\w*))?\s*$", RegexOptions.IgnoreCase);
            if (redim.Success)
            {
                var elementType = redim.Groups[3].Success ? redim.Groups[3].Value : array.ElementType;
                var pairs = BuildRuntimeBoundArguments(redim.Groups[2].Value);
                return indent + array.Name + " = XPModuleArrayRuntime.ReDim(" + array.Name + ", \"" + EscapeXPScriptString(elementType) + "\", " +
                       (!string.IsNullOrWhiteSpace(redim.Groups[1].Value) ? "True" : "False") + ", " + string.Join(", ", pairs) + ")" + comment;
            }

            if (Regex.IsMatch(trimmed, $@"^Erase\s+{name}$", RegexOptions.IgnoreCase))
                return indent + "Call LSArrayRuntime.Erase(" + array.Name + ")" + comment;

            var setter = Regex.Match(trimmed, $@"^{name}\s*\((.*)\)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (setter.Success)
            {
                var indexes = SplitArguments(setter.Groups[1].Value);
                return indent + "Call LSArrayRuntime.Set(" + array.Name + ", " + setter.Groups[2].Value.Trim() +
                       (indexes.Count > 0 ? ", " + string.Join(", ", indexes) : "") + ")" + comment;
            }

            code = ReplaceOutsideStrings(code,
                $@"\bLBound\s*\(\s*{name}\s*(?:,\s*([^()]+))?\)",
                m => "LSArrayRuntime.LBound(" + array.Name + (m.Groups[1].Success ? ", " + m.Groups[1].Value.Trim() : "") + ")");
            code = ReplaceOutsideStrings(code,
                $@"\bUBound\s*\(\s*{name}\s*(?:,\s*([^()]+))?\)",
                m => "LSArrayRuntime.UBound(" + array.Name + (m.Groups[1].Success ? ", " + m.Groups[1].Value.Trim() : "") + ")");
            code = ReplaceOutsideStrings(code,
                $@"(?<![\w.]){name}\s*\(([^()]*)\)",
                m => "LSArrayRuntime.Get(" + array.Name + (string.IsNullOrWhiteSpace(m.Groups[1].Value) ? "" : ", " + m.Groups[1].Value) + ")");
        }

        return code + comment;
    }

    private (int[] Lower, int[] Upper) ParseConstantBounds(string raw)
    {
        var dimensions = SplitArguments(raw);
        if (dimensions.Count is < 1 or > 8)
            throw new CompilerException("Module-level arrays must have between one and eight dimensions.");

        var lower = new int[dimensions.Count];
        var upper = new int[dimensions.Count];
        for (var i = 0; i < dimensions.Count; i++)
        {
            var range = Regex.Match(dimensions[i], @"^([+-]?\d+)\s+To\s+([+-]?\d+)$", RegexOptions.IgnoreCase);
            if (range.Success)
            {
                lower[i] = int.Parse(range.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                upper[i] = int.Parse(range.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                continue;
            }

            if (!int.TryParse(dimensions[i].Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var high))
                throw new CompilerException("Fixed module-level array bounds must be integer constants. Use a dynamic array with ReDim for runtime expressions.");
            lower[i] = _optionBase;
            upper[i] = high;
        }
        return (lower, upper);
    }

    private List<string> BuildRuntimeBoundArguments(string raw)
    {
        var dimensions = SplitArguments(raw);
        if (dimensions.Count is < 1 or > 8)
            throw new CompilerException("ReDim requires between one and eight dimensions.");

        var result = new List<string>(dimensions.Count * 2);
        foreach (var dimension in dimensions)
        {
            var range = Regex.Match(dimension, @"^(.+?)\s+To\s+(.+)$", RegexOptions.IgnoreCase);
            if (range.Success)
            {
                result.Add(range.Groups[1].Value.Trim());
                result.Add(range.Groups[2].Value.Trim());
            }
            else
            {
                result.Add(_optionBase.ToString(System.Globalization.CultureInfo.InvariantCulture));
                result.Add(dimension.Trim());
            }
        }
        return result;
    }

    private static List<string> SplitArguments(string value)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(value)) return result;
        var start = 0;
        var depth = 0;
        var inString = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(value[start..i].Trim());
                start = i + 1;
            }
        }
        result.Add(value[start..].Trim());
        return result;
    }

    private static string ReplaceOutsideStrings(string input, string pattern, MatchEvaluator evaluator)
    {
        var parts = Regex.Split(input, "(\"(?:\"\"|[^\"])*\")");
        for (var i = 0; i < parts.Length; i += 2)
            parts[i] = Regex.Replace(parts[i], pattern, evaluator, RegexOptions.IgnoreCase);
        return string.Concat(parts);
    }

    private static (string Code, string Comment) SplitComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = !inString;
            }
            else if (!inString && line[i] == '\'') return (line[..i], line[i..]);
        }
        return (line, "");
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

    private static string EscapeCSharp(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    private static string EscapeXPScriptString(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);
    private static string StripComment(string line) => SplitComment(line).Code;
}
