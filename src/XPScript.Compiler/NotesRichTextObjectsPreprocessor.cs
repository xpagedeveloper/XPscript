using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NotesRichTextObjectsPreprocessor
{
    private const string TypePattern = "NotesRichTextNavigator|NotesRichTextParagraphStyle|NotesRichTextRange|NotesRichTextSection|NotesRichTextStyle|NotesRichTextTab|NotesRichTextTable|NotesRichTextDocLink|NotesColorObject";

    private static readonly Dictionary<string, int> Constants = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RTELEM_TYPE_TABLE"] = 1,
        ["RTELEM_TYPE_TEXTRUN"] = 3,
        ["RTELEM_TYPE_TEXTPARAGRAPH"] = 4,
        ["RTELEM_TYPE_DOCLINK"] = 5,
        ["RTELEM_TYPE_SECTION"] = 6,
        ["RTELEM_TYPE_TABLECELL"] = 7,
        ["RTELEM_TYPE_FILEATTACHMENT"] = 8,
        ["RTELEM_TYPE_OLE"] = 9,
        ["RTELEM_TYPE_TEXTPOSITION"] = 10,
        ["RTELEM_TYPE_TEXTSTRING"] = 11,
        ["STYLE_NO_CHANGE"] = 255,
        ["RT_FIND_CASEINSENSITIVE"] = 1,
        ["RT_FIND_PITCHINSENSITIVE"] = 2,
        ["RT_FIND_ACCENTINSENSITIVE"] = 4,
        ["RT_REPL_PRESERVECASE"] = 8,
        ["RT_REPL_ALL"] = 16,
        ["TAB_LEFT"] = 0,
        ["TAB_RIGHT"] = 1,
        ["TAB_DECIMAL"] = 2,
        ["TAB_CENTER"] = 3,
        ["RULER_ONE_CENTIMETER"] = 567,
        ["RULER_ONE_INCH"] = 1440,
        ["TABLESTYLE_NONE"] = 0,
        ["TABLESTYLE_LEFTTOP"] = 1,
        ["TABLESTYLE_TOP"] = 2,
        ["TABLESTYLE_LEFT"] = 3,
        ["TABLESTYLE_ALTERNATINGCOLS"] = 4,
        ["TABLESTYLE_ALTERNATINGROWS"] = 5,
        ["TABLESTYLE_RIGHTTOP"] = 6,
        ["TABLESTYLE_RIGHT"] = 7,
        ["TABLESTYLE_SOLID"] = 8,
        ["FONT_ROMAN"] = 0,
        ["FONT_HELV"] = 1,
        ["FONT_COURIER"] = 4,
        ["EFFECTS_NONE"] = 0,
        ["EFFECTS_SUPER"] = 1,
        ["EFFECTS_SUB"] = 2,
        ["EFFECTS_SHADOW"] = 3,
        ["EFFECTS_EMBOSS"] = 4,
        ["EFFECTS_EXTRUDE"] = 5,
        ["PARA_ALIGN_LEFT"] = 0,
        ["PARA_ALIGN_RIGHT"] = 1,
        ["PARA_ALIGN_BLOCK"] = 2,
        ["PARA_ALIGN_CENTER"] = 3,
        ["PARA_ALIGN_NONE"] = 4,
        ["PAGINATE_DEFAULT"] = 0,
        ["PAGINATE_BEFORE"] = 1,
        ["PAGINATE_KEEP_WITH_NEXT"] = 2,
        ["PAGINATE_KEEP_TOGETHER"] = 4
    };

    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 16);
        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var replacementIndex = 0;

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNew = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+({TypePattern})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (dimNew.Success)
                throw new CompilerException(dimNew.Groups[2].Value + " must be created through NotesSession, NotesRichTextItem, NotesRichTextNavigator, or NotesRichTextParagraphStyle.");

            var dim = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+({TypePattern})\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (dim.Success)
            {
                var name = dim.Groups[1].Value;
                variables.Add(name);
                types[name] = dim.Groups[2].Value;
                output.Add(indent + $"Dim {name} As Variant");
                output.Add(indent + $"{name} = XPScriptNotes.NothingValue");
                continue;
            }

            var rewritten = ReplaceConstants(line);
            rewritten = RewriteNullableNavigatorMembers(rewritten, types);

            var recycle = Regex.Match(rewritten, @"^(?:Call\s+)?([A-Za-z_]\w*)\.Recycle\s*\(\s*\)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (recycle.Success && variables.Contains(recycle.Groups[1].Value))
            {
                var name = recycle.Groups[1].Value;
                output.Add(indent + $"Call XPScriptNotes.RecycleValue({name})");
                output.Add(indent + $"{name} = XPScriptNotes.NothingValue");
                continue;
            }

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (set.Success && variables.Contains(set.Groups[1].Value))
            {
                var name = set.Groups[1].Value;
                var rhs = set.Groups[2].Value.Trim();
                rhs = rhs.Equals("Nothing", StringComparison.OrdinalIgnoreCase)
                    ? "XPScriptNotes.NothingValue"
                    : $"XPScriptNotes.NormalizeObjectResult({rhs})";
                var temp = "__notesRichTextReplacement" + (++replacementIndex).ToString(System.Globalization.CultureInfo.InvariantCulture);
                output.Add(indent + $"Dim {temp} As Variant");
                output.Add(indent + $"{temp} = {rhs}");
                output.Add(indent + $"Call XPScriptNotes.RecycleForReplacement({name}, {temp})");
                output.Add(indent + $"{name} = {temp}");
                continue;
            }

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static string RewriteNullableNavigatorMembers(string line, IReadOnlyDictionary<string, string> types)
    {
        foreach (var pair in types)
        {
            if (!pair.Value.Equals("NotesRichTextNavigator", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var method in new[] { "GetElement", "GetFirstElement", "GetLastElement", "GetNextElement", "GetNthElement" })
                line = WrapMethodCalls(line, pair.Key, method);
        }
        return line;
    }

    private static string WrapMethodCalls(string line, string variableName, string methodName)
    {
        var pattern = new Regex($@"\b{Regex.Escape(variableName)}\.{Regex.Escape(methodName)}\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var offset = 0;
        while (offset < line.Length)
        {
            var match = pattern.Match(line, offset);
            if (!match.Success) break;
            if (IsInsideNormalizer(line, match.Index)) { offset = match.Index + match.Length; continue; }
            var open = line.IndexOf('(', match.Index);
            var close = FindMatchingParen(line, open);
            if (close < 0) break;
            var call = line[match.Index..(close + 1)];
            var replacement = "XPScriptNotes.NormalizeObjectResult(" + call + ")";
            line = line[..match.Index] + replacement + line[(close + 1)..];
            offset = match.Index + replacement.Length;
        }
        return line;
    }

    private static bool IsInsideNormalizer(string line, int index)
    {
        const string prefix = "XPScriptNotes.NormalizeObjectResult(";
        var start = Math.Max(0, index - prefix.Length);
        return line.AsSpan(start, index - start).EndsWith(prefix, StringComparison.Ordinal);
    }

    private static int FindMatchingParen(string text, int open)
    {
        var depth = 0;
        var inString = false;
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (inString && i + 1 < text.Length && text[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return i;
        }
        return -1;
    }

    private static string ReplaceConstants(string line)
    {
        if (line.Length == 0) return line;
        var output = new System.Text.StringBuilder(line.Length + 16);
        var inString = false;
        for (var i = 0; i < line.Length;)
        {
            var c = line[i];
            if (c == '"')
            {
                output.Append(c);
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { output.Append('"'); i += 2; continue; }
                inString = !inString;
                i++;
                continue;
            }
            if (!inString && (char.IsLetter(c) || c == '_'))
            {
                var start = i++;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                var token = line[start..i];
                output.Append(Constants.TryGetValue(token, out var value)
                    ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : token);
                continue;
            }
            output.Append(c);
            i++;
        }
        return output.ToString();
    }
}
