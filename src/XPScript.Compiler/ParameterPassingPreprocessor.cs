using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ParameterPassingPreprocessor
{
    private const string ByRefPrefix = "__xps_byref_";
    private const string ByValPrefix = "__xps_byval_";
    private const string ComputeWithFormHelper = "__XPSNotesComputeWithForm";

    private static readonly Regex ProcedureHeader = new(
        @"^(?<prefix>\s*(?:(?:Public|Private)\s+)?(?:Sub|Function)\s+[A-Za-z_]\w*\s*\()(?<args>.*)(?<suffix>\)\s*(?:As\s+[A-Za-z_]\w*)?\s*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ArgumentDeclaration = new(
        @"^(?<leading>\s*)(?:(?<modifier>ByVal|ByRef)\s+)?(?<name>[A-Za-z_]\w*)(?<rest>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string Transform(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;

        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Split('\n');
        Dictionary<string, string>? activeParameters = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (activeParameters is null)
            {
                var header = ProcedureHeader.Match(line);
                if (!header.Success) continue;

                activeParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var arguments = SplitArguments(header.Groups["args"].Value);
                for (var argIndex = 0; argIndex < arguments.Count; argIndex++)
                {
                    var declaration = ArgumentDeclaration.Match(arguments[argIndex]);
                    if (!declaration.Success) continue;

                    var originalName = declaration.Groups["name"].Value;
                    var modifier = declaration.Groups["modifier"].Value;
                    var marker = modifier.Equals("ByVal", StringComparison.OrdinalIgnoreCase) ? ByValPrefix : ByRefPrefix;
                    var generatedName = marker + originalName;
                    activeParameters[originalName] = generatedName;

                    var emittedModifier = modifier.Equals("ByVal", StringComparison.OrdinalIgnoreCase) ? "ByVal " : "";
                    arguments[argIndex] = declaration.Groups["leading"].Value + emittedModifier + generatedName + declaration.Groups["rest"].Value;
                }

                lines[i] = header.Groups["prefix"].Value + string.Join(",", arguments) + header.Groups["suffix"].Value;
                continue;
            }

            if (Regex.IsMatch(line, @"^\s*End\s+(Sub|Function)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                activeParameters = null;
                continue;
            }

            lines[i] = ReplaceIdentifiers(line, activeParameters);
        }

        var transformed = string.Join("\n", lines);
        transformed = RewriteNotesComputeWithFormCalls(transformed, out var usesComputeWithFormOutput);
        if (!usesComputeWithFormOutput) return transformed;

        return transformed + """

Function __XPSNotesComputeWithForm(doc As NotesDocument, doDataTypes As Variant, raiseError As Variant, __xps_byref_failedFields As Variant) As Boolean
    Dim __xps_cwf_success As Boolean
    __xps_cwf_success = doc.__ComputeWithFormCapture(doDataTypes, raiseError, True)
    __xps_byref_failedFields = doc.__ComputeWithFormCapturedFields()
    If CBool(raiseError) And Not __xps_cwf_success Then
        Call doc.__RaiseComputeWithFormCapturedError()
    End If
    __XPSNotesComputeWithForm = __xps_cwf_success
End Function
""";
    }

    private static string RewriteNotesComputeWithFormCalls(string source, out bool changed)
    {
        changed = false;
        var output = new StringBuilder(source.Length + 64);
        var cursor = 0;
        var marker = ".ComputeWithForm";

        while (cursor < source.Length)
        {
            var index = source.IndexOf(marker, cursor, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                output.Append(source.AsSpan(cursor));
                break;
            }

            var receiverEnd = index;
            var receiverStart = receiverEnd - 1;
            while (receiverStart >= cursor && (char.IsLetterOrDigit(source[receiverStart]) || source[receiverStart] is '_' or '.')) receiverStart--;
            receiverStart++;
            if (receiverStart >= receiverEnd || !IsReceiver(source[receiverStart..receiverEnd]))
            {
                output.Append(source.AsSpan(cursor, index + marker.Length - cursor));
                cursor = index + marker.Length;
                continue;
            }

            var open = index + marker.Length;
            while (open < source.Length && char.IsWhiteSpace(source[open])) open++;
            if (open >= source.Length || source[open] != '(')
            {
                output.Append(source.AsSpan(cursor, index + marker.Length - cursor));
                cursor = index + marker.Length;
                continue;
            }

            var close = FindMatchingParen(source, open);
            if (close < 0)
            {
                output.Append(source.AsSpan(cursor));
                break;
            }

            var args = SplitArguments(source[(open + 1)..close]);
            if (args.Count != 3 || !Regex.IsMatch(args[2].Trim(), @"^[A-Za-z_]\w*$", RegexOptions.CultureInvariant))
            {
                output.Append(source.AsSpan(cursor, close - cursor + 1));
                cursor = close + 1;
                continue;
            }

            output.Append(source.AsSpan(cursor, receiverStart - cursor));
            output.Append(ComputeWithFormHelper).Append('(')
                .Append(source.AsSpan(receiverStart, receiverEnd - receiverStart)).Append(", ")
                .Append(args[0].Trim()).Append(", ")
                .Append(args[1].Trim()).Append(", ")
                .Append(args[2].Trim()).Append(')');
            changed = true;
            cursor = close + 1;
        }

        return output.ToString();
    }

    private static bool IsReceiver(string value)
    {
        if (value.Length == 0 || !IsIdentifierStart(value[0])) return false;
        for (var i = 1; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '.')
            {
                if (i + 1 >= value.Length || !IsIdentifierStart(value[i + 1])) return false;
                continue;
            }
            if (!char.IsLetterOrDigit(c) && c != '_') return false;
        }
        return true;
    }

    private static int FindMatchingParen(string value, int openIndex)
    {
        var depth = 0;
        var inString = false;
        for (var i = openIndex; i < value.Length; i++)
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
            else if (c == ')' && --depth == 0) return i;
        }
        return -1;
    }

    private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';

    private static List<string> SplitArguments(string value)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(value)) return result;

        var current = new StringBuilder();
        var inString = false;
        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                current.Append(c);
                if (inString && i + 1 < value.Length && value[i + 1] == '"')
                {
                    current.Append(value[++i]);
                    continue;
                }
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    continue;
                }
            }
            current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }

    private static string ReplaceIdentifiers(string line, IReadOnlyDictionary<string, string> replacements)
    {
        if (replacements.Count == 0 || string.IsNullOrEmpty(line)) return line;

        var output = new StringBuilder(line.Length + 16);
        var inString = false;
        for (var i = 0; i < line.Length;)
        {
            var c = line[i];
            if (c == '"')
            {
                output.Append(c);
                i++;
                if (inString && i < line.Length && line[i] == '"')
                {
                    output.Append(line[i++]);
                    continue;
                }
                inString = !inString;
                continue;
            }

            if (!inString && c == '\'')
            {
                output.Append(line.AsSpan(i));
                break;
            }

            if (!inString && (char.IsLetter(c) || c == '_'))
            {
                var start = i++;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                var identifier = line[start..i];
                var previousNonWhitespace = PreviousNonWhitespace(line, start);
                if (previousNonWhitespace != '.' && replacements.TryGetValue(identifier, out var replacement))
                    output.Append(replacement);
                else
                    output.Append(identifier);
                continue;
            }

            output.Append(c);
            i++;
        }
        return output.ToString();
    }

    private static char PreviousNonWhitespace(string value, int index)
    {
        for (var i = index - 1; i >= 0; i--)
            if (!char.IsWhiteSpace(value[i])) return value[i];
        return '\0';
    }
}
