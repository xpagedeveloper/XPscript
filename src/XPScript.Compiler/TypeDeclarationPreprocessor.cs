using System.Globalization;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class TypeDeclarationPreprocessor
{
    private sealed record ArrayField(string Name, string ElementType, string Bounds);
    private sealed record NestedField(string Name, string Type);

    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        var optionBase = DetectOptionBase(lines);
        var typeNames = CollectTypeNames(lines);
        var output = new List<string>(lines.Count + 16);

        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var line = StripComment(raw).Trim();
            var start = Regex.Match(line, @"^(?:(Public|Private)\s+)?Type\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (!start.Success)
            {
                output.Add(raw);
                continue;
            }

            var visibility = start.Groups[1].Success ? start.Groups[1].Value + " " : "";
            var typeName = start.Groups[2].Value;
            output.Add(visibility + "Class " + typeName);

            var arrays = new List<ArrayField>();
            var nestedFields = new List<NestedField>();
            var foundEnd = false;
            for (i = i + 1; i < lines.Count; i++)
            {
                var memberRaw = lines[i];
                var memberLine = StripComment(memberRaw).Trim();
                if (Regex.IsMatch(memberLine, @"^End\s+Type$", RegexOptions.IgnoreCase))
                {
                    foundEnd = true;
                    break;
                }
                if (memberLine.Length == 0) continue;

                var field = Regex.Match(memberLine, @"^([A-Za-z_]\w*)\s*(\(([^)]*)\))?\s+As\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
                if (!field.Success)
                    throw new CompilerException("Unsupported Type member declaration: " + memberLine);

                var name = field.Groups[1].Value;
                var elementType = field.Groups[4].Value;
                if (!field.Groups[2].Success)
                {
                    output.Add("Public " + name + " As " + elementType);
                    if (typeNames.Contains(elementType)) nestedFields.Add(new NestedField(name, elementType));
                    continue;
                }

                arrays.Add(new ArrayField(name, elementType, field.Groups[3].Value.Trim()));
                output.Add("Public " + name + " As Variant");
            }

            if (!foundEnd)
                throw new CompilerException("Missing End Type for '" + typeName + "'.");

            if (arrays.Count > 0 || nestedFields.Count > 0)
            {
                output.Add("Sub New()");
                foreach (var nested in nestedFields)
                    output.Add("Set " + nested.Name + " = New " + nested.Type);

                foreach (var array in arrays)
                {
                    if (array.Bounds.Length == 0)
                    {
                        output.Add(array.Name + " = XPTypeArrayRuntime.Create(\"" + EscapeXPScriptString(array.ElementType) + "\", True)");
                    }
                    else
                    {
                        var bounds = ParseConstantBounds(array.Bounds, optionBase);
                        var args = new List<string>();
                        for (var dimension = 0; dimension < bounds.Lower.Length; dimension++)
                        {
                            args.Add(bounds.Lower[dimension].ToString(CultureInfo.InvariantCulture));
                            args.Add(bounds.Upper[dimension].ToString(CultureInfo.InvariantCulture));
                        }
                        output.Add(array.Name + " = XPTypeArrayRuntime.Create(\"" + EscapeXPScriptString(array.ElementType) + "\", False, " + string.Join(", ", args) + ")");
                    }
                }
                output.Add("End Sub");
            }
            output.Add("End Class");
        }

        if (typeNames.Count == 0) return source;

        for (var i = 0; i < output.Count; i++)
        {
            var line = output[i];
            foreach (var typeName in typeNames)
            {
                // Type variables are value containers and are initialized automatically.
                line = Regex.Replace(line,
                    $@"\bDim\s+([A-Za-z_]\w*)\s+As\s+(?!New\s+){Regex.Escape(typeName)}\b",
                    $"Dim $1 As New {typeName}", RegexOptions.IgnoreCase);
            }
            output[i] = line;
        }

        return string.Join(Environment.NewLine, output);
    }

    private static HashSet<string> CollectTypeNames(IEnumerable<string> lines)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var match = Regex.Match(StripComment(raw).Trim(), @"^(?:(?:Public|Private)\s+)?Type\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (match.Success) result.Add(match.Groups[1].Value);
        }
        return result;
    }

    private static int DetectOptionBase(IEnumerable<string> lines)
    {
        foreach (var raw in lines)
        {
            var match = Regex.Match(StripComment(raw).Trim(), @"^Option\s+Base\s+([01])$", RegexOptions.IgnoreCase);
            if (match.Success) return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }
        return 0;
    }

    private static (int[] Lower, int[] Upper) ParseConstantBounds(string raw, int optionBase)
    {
        var dimensions = SplitArguments(raw);
        if (dimensions.Count is < 1 or > 8)
            throw new CompilerException("Type array members must have between one and eight dimensions.");

        var lower = new int[dimensions.Count];
        var upper = new int[dimensions.Count];
        for (var i = 0; i < dimensions.Count; i++)
        {
            var range = Regex.Match(dimensions[i], @"^([+-]?\d+)\s+To\s+([+-]?\d+)$", RegexOptions.IgnoreCase);
            if (range.Success)
            {
                lower[i] = int.Parse(range.Groups[1].Value, CultureInfo.InvariantCulture);
                upper[i] = int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);
                continue;
            }
            if (!int.TryParse(dimensions[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var high))
                throw new CompilerException("Fixed Type array bounds must be integer constants. Use a dynamic Type array member with ReDim for runtime bounds.");
            lower[i] = optionBase;
            upper[i] = high;
        }
        return (lower, upper);
    }

    private static List<string> SplitArguments(string value)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '(') depth++;
            else if (value[i] == ')') depth--;
            else if (value[i] == ',' && depth == 0)
            {
                result.Add(value[start..i].Trim());
                start = i + 1;
            }
        }
        result.Add(value[start..].Trim());
        return result;
    }

    private static string EscapeXPScriptString(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);

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
