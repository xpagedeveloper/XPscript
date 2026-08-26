using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NotesRuntimePreprocessor
{
    private const string NotesTypePattern = "NotesSession|NotesDatabase|NotesView|NotesDocumentCollection|NotesDocument|NotesItem|NotesRichTextItem|NotesName|NotesDateTime|NotesAgentResult";

    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 16);
        var notesVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notesDocumentCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var replacementIndex = 0;

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNew = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+({NotesTypePattern})\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                var type = dimNew.Groups[2].Value;
                notesVariables.Add(name);
                if (type.Equals("NotesDocumentCollection", StringComparison.OrdinalIgnoreCase))
                    notesDocumentCollections.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                output.Add(indent + $"{name} = {CreateExpression(type, dimNew.Groups[3].Value)}");
                continue;
            }

            var dim = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+({NotesTypePattern})\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                var name = dim.Groups[1].Value;
                var type = dim.Groups[2].Value;
                notesVariables.Add(name);
                if (type.Equals("NotesDocumentCollection", StringComparison.OrdinalIgnoreCase))
                    notesDocumentCollections.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                continue;
            }

            var rewritten = Regex.Replace(
                line,
                @"\bNew\s+NotesSession\s*\((.*)\)",
                "XPScriptNotes.CreateSession($1)",
                RegexOptions.IgnoreCase);

            var unsupportedNew = Regex.Match(rewritten, $@"\bNew\s+(NotesDatabase|NotesView|NotesDocumentCollection|NotesDocument|NotesItem|NotesRichTextItem|NotesName|NotesDateTime|NotesAgentResult)\b", RegexOptions.IgnoreCase);
            if (unsupportedNew.Success)
                throw new CompilerException($"{unsupportedNew.Groups[1].Value} objects must be created from NotesSession, NotesDatabase, NotesView, or NotesDocument.");

            var recycle = Regex.Match(rewritten, @"^(?:Call\s+)?([A-Za-z_]\w*)\.Recycle\s*\(\s*\)\s*$", RegexOptions.IgnoreCase);
            if (recycle.Success && notesVariables.Contains(recycle.Groups[1].Value))
            {
                var name = recycle.Groups[1].Value;
                output.Add(indent + $"Call XPScriptNotes.RecycleValue({name})");
                output.Add(indent + $"{name} = Nothing");
                continue;
            }

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && notesVariables.Contains(set.Groups[1].Value))
            {
                var name = set.Groups[1].Value;
                var rhs = set.Groups[2].Value.Trim();
                var temp = "__notesReplacement" + (++replacementIndex).ToString(System.Globalization.CultureInfo.InvariantCulture);
                output.Add(indent + $"Dim {temp} As Variant");
                output.Add(indent + $"{temp} = {rhs}");
                output.Add(indent + $"Call XPScriptNotes.RecycleForReplacement({name}, {temp})");
                output.Add(indent + $"{name} = {temp}");
                continue;
            }

            foreach (var collectionName in notesDocumentCollections)
            {
                var escaped = Regex.Escape(collectionName);
                rewritten = Regex.Replace(
                    rewritten,
                    $@"\bUBound\s*\(\s*{escaped}\s*(?:,\s*1\s*)?\)",
                    $"({collectionName}.Count - 1)",
                    RegexOptions.IgnoreCase);
                rewritten = Regex.Replace(
                    rewritten,
                    $@"\bLBound\s*\(\s*{escaped}\s*(?:,\s*1\s*)?\)",
                    "0",
                    RegexOptions.IgnoreCase);
                rewritten = Regex.Replace(
                    rewritten,
                    $@"\b{escaped}\s*\(\s*([^()]*)\s*\)",
                    $"{collectionName}.GetNoteIdString($1)",
                    RegexOptions.IgnoreCase);
            }

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static string CreateExpression(string type, string rawArguments)
    {
        var args = rawArguments.Trim();
        if (!type.Equals("NotesSession", StringComparison.OrdinalIgnoreCase))
            throw new CompilerException($"{type} objects must be created from NotesSession, NotesDatabase, NotesView, or NotesDocument.");
        if (string.IsNullOrWhiteSpace(args))
            throw new CompilerException("NotesSession requires the Notes/Domino runtime directory argument.");
        return $"XPScriptNotes.CreateSession({args})";
    }
}
