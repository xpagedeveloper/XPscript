using System.Text.RegularExpressions;

namespace XPScript.Compiler;

/// <summary>
/// Resolves NotesConst members to numeric XPscript literals before the normal parser sees them.
/// This prevents NotesConst members from being treated as unresolved Variant/member expressions.
/// It also rejects statically-known invalid GetModifiedDocuments note-class values at compile time.
/// </summary>
internal sealed class NotesConstSourcePreprocessor
{
    private static readonly IReadOnlyDictionary<string, int> Values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["DBMOD_DOC_DATA"] = 0x0001,
        ["FORM"] = 0x0004,
        ["VIEW"] = 0x0008,
        ["ICON"] = 0x0010,
        ["ACL"] = 0x0040,
        ["HELP"] = 0x0100,
        ["AGENT"] = 0x0200,
        ["SHAREDFIELD"] = 0x0400,
        ["REPLFORMULA"] = 0x0800,
        ["DBMOD_DOC_ALL"] = 0x7FFF,
        ["Data"] = 0x0001,
        ["Form"] = 0x0004,
        ["View"] = 0x0008,
        ["Icon"] = 0x0010,
        ["Acl"] = 0x0040,
        ["Help"] = 0x0100,
        ["Agent"] = 0x0200,
        ["SharedField"] = 0x0400,
        ["ReplFormula"] = 0x0800,
        ["All"] = 0x7FFF,
        ["RTELEM_TYPE_TABLE"] = 1,
        ["RTELEM_TYPE_TEXTRUN"] = 3,
        ["RTELEM_TYPE_TEXTPARAGRAPH"] = 4,
        ["RTELEM_TYPE_DOCLINK"] = 5,
        ["RTELEM_TYPE_SECTION"] = 6,
        ["RTELEM_TYPE_TABLECELL"] = 7,
        ["RTELEM_TYPE_FILEATTACHMENT"] = 8,
        ["RTELEM_TYPE_OLE"] = 9,
        ["Table"] = 1,
        ["TextRun"] = 3,
        ["TextParagraph"] = 4,
        ["DocLink"] = 5,
        ["Section"] = 6,
        ["TableCell"] = 7,
        ["FileAttachment"] = 8,
        ["Ole"] = 9,
        ["RT_FIND_CASEINSENSITIVE"] = 0,
        ["RT_FIND_CASESENSITIVE"] = 1,
        ["RT_FIND_ACCENTINSENSITIVE"] = 2,
        ["RT_FIND_PITCHINSENSITIVE"] = 4,
        ["FindCaseInsensitive"] = 0,
        ["FindCaseSensitive"] = 1,
        ["FindAccentInsensitive"] = 2,
        ["FindPitchInsensitive"] = 4
    };

    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.Contains("NotesConst", StringComparison.OrdinalIgnoreCase) &&
            !source.Contains("GetModifiedDocuments", StringComparison.OrdinalIgnoreCase))
            return source;

        var resolved = Regex.Replace(
            source,
            @"\bNotesConst\s*\.\s*([A-Za-z_]\w*)",
            match => Resolve(match.Groups[1].Value),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        ValidateGetModifiedDocuments(resolved);
        return resolved;
    }

    private static string Resolve(string member)
    {
        if (!Values.TryGetValue(member, out var value))
            throw new CompilerException($"Unknown NotesConst member '{member}'.");
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void ValidateGetModifiedDocuments(string source)
    {
        var callPattern = new Regex(@"\.GetModifiedDocuments\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        for (var match = callPattern.Match(source); match.Success; match = match.NextMatch())
        {
            var open = source.IndexOf('(', match.Index);
            var close = FindMatchingParen(source, open);
            if (close < 0) continue;

            var args = SplitTopLevelArguments(source[(open + 1)..close]);
            if (args.Count < 2) continue;

            var noteClass = args[1].Trim();
            if (IsZeroLiteral(noteClass))
                throw new CompilerException("NotesDatabase.GetModifiedDocuments noteClass cannot be 0. Use NotesConst.Data, NotesConst.Form, NotesConst.View, NotesConst.All, or a supported bitwise combination.");
        }
    }

    private static bool IsZeroLiteral(string value)
    {
        if (value.Equals("0", StringComparison.OrdinalIgnoreCase)) return true;
        if (Regex.IsMatch(value, @"^&H0+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return true;
        return false;
    }

    private static List<string> SplitTopLevelArguments(string text)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        var inString = false;
        for (var i = 0; i < text.Length; i++)
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
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(text[start..i]);
                start = i + 1;
            }
        }
        result.Add(text[start..]);
        return result;
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
}