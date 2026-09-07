using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NotesRuntimePreprocessor
{
    private const string NotesTypePattern = "NotesSession|NotesDBDirectory|NotesDatabase|NotesView|NotesDocumentCollection|NotesNoteCollection|NotesDocument|NotesItem|NotesRichTextItem|NotesRichTextNavigator|NotesRichTextParagraphStyle|NotesRichTextRange|NotesRichTextSection|NotesRichTextStyle|NotesRichTextTab|NotesRichTextTable|NotesRichTextDocLink|NotesName|NotesDateTime|NotesAgent|NotesStream|NotesDXLImporter|NotesDXLExporter";

    private static readonly Dictionary<string, string[]> NothingReturningMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NotesDBDirectory"] = ["GetFirstDatabase", "GetNextDatabase"],
        ["NotesDatabase"] = ["OpenView", "GetDocumentByNoteId", "OpenDocumentByNoteId", "GetDocumentByUNID", "OpenDocumentByUNID", "Search", "FTSearch", "GetAgent"],
        ["NotesView"] = ["GetFirstDocumentByKey", "GetFirstDocument", "GetNextDocument"],
        ["NotesDocumentCollection"] = ["GetFirstDocument", "GetNextDocument", "GetDocument"],
        ["NotesDocument"] = ["GetFirstItem"]
    };

    private static readonly Dictionary<string, string[]> NothingReturningProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NotesItem"] = ["DateTimeValue"],
        ["NotesRichTextItem"] = ["DateTimeValue"]
    };

    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.Contains("Notes", StringComparison.OrdinalIgnoreCase)) return source;

        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 32);
        var notesVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notesVariableTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var notesDocumentCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notesDocuments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var notesItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var replacementIndex = 0;

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            if (Regex.IsMatch(line, @"\bNotesAgentResult\b", RegexOptions.IgnoreCase))
                throw new CompilerException("NotesAgentResult has been removed. Use NotesDatabase.GetAgent and NotesAgent.Run, RunWithDocumentContext, or RunOnServer.");
            if (Regex.IsMatch(line, @"\.RunAgent\s*\(", RegexOptions.IgnoreCase))
                throw new CompilerException("NotesDatabase.RunAgent has been removed. Use NotesDatabase.GetAgent and NotesAgent.Run or RunWithDocumentContext.");

            var dimNew = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+({NotesTypePattern})\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                var type = dimNew.Groups[2].Value;
                RegisterNotesVariable(name, type, notesVariables, notesVariableTypes, notesDocumentCollections, notesDocuments, notesItems);
                output.Add(indent + $"Dim {name} As Variant");
                output.Add(indent + $"{name} = {CreateExpression(type, dimNew.Groups[3].Value)}");
                continue;
            }

            var dim = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+({NotesTypePattern})\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                var name = dim.Groups[1].Value;
                var type = dim.Groups[2].Value;
                RegisterNotesVariable(name, type, notesVariables, notesVariableTypes, notesDocumentCollections, notesDocuments, notesItems);
                output.Add(indent + $"Dim {name} As Variant");
                output.Add(indent + $"{name} = XPScriptNotes.NothingValue");
                continue;
            }

            var rewritten = Regex.Replace(line, @"\bNotesConst\s*\.", "XPScriptNotesConst.", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+NotesSession\s*\((.*)\)", "XPScriptNotes.CreateSession($1)", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+NotesDBDirectory\s*\((.*)\)", "XPScriptNotes.CreateDbDirectory($1)", RegexOptions.IgnoreCase);

            if (Regex.IsMatch(rewritten, @"\.GetFirstDocumentByKey\s*\(", RegexOptions.IgnoreCase))
                throw new CompilerException("NotesView.GetFirstDocumentByKey has been renamed to NotesView.GetDocumentByKey.");

            rewritten = Regex.Replace(rewritten, @"\.GetDocumentByKey\s*\(", ".GetFirstDocumentByKey(", RegexOptions.IgnoreCase);

            var recycle = Regex.Match(rewritten, @"^(?:Call\s+)?([A-Za-z_]\w*)\.Recycle\s*\(\s*\)\s*$", RegexOptions.IgnoreCase);
            if (recycle.Success && notesVariables.Contains(recycle.Groups[1].Value))
            {
                var name = recycle.Groups[1].Value;
                output.Add(indent + $"Call XPScriptNotes.RecycleValue({name})");
                output.Add(indent + $"{name} = XPScriptNotes.NothingValue");
                continue;
            }

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && notesVariables.Contains(set.Groups[1].Value))
            {
                var name = set.Groups[1].Value;
                var rhs = set.Groups[2].Value.Trim();
                foreach (var documentName in notesDocuments.OrderByDescending(value => value.Length)) rhs = RewriteDocumentItemValues(rhs, documentName);
                foreach (var itemName in notesItems.OrderByDescending(value => value.Length)) rhs = RewriteNotesItemValues(rhs, itemName);
                rhs = RewriteNothingReturningMembers(rhs, notesVariableTypes);
                rhs = rhs.Equals("Nothing", StringComparison.OrdinalIgnoreCase) ? "XPScriptNotes.NothingValue" : $"XPScriptNotes.NormalizeObjectResult({rhs})";
                var temp = "__notesReplacement" + (++replacementIndex).ToString(System.Globalization.CultureInfo.InvariantCulture);
                output.Add(indent + $"Dim {temp} As Variant");
                output.Add(indent + $"{temp} = {rhs}");
                output.Add(indent + $"Call XPScriptNotes.RecycleForReplacement({name}, {temp})");
                output.Add(indent + $"{name} = {temp}");
                continue;
            }

            foreach (var documentName in notesDocuments.OrderByDescending(value => value.Length)) rewritten = RewriteDocumentItemValues(rewritten, documentName);
            foreach (var itemName in notesItems.OrderByDescending(value => value.Length)) rewritten = RewriteNotesItemValues(rewritten, itemName);
            rewritten = RewriteNothingReturningMembers(rewritten, notesVariableTypes);
            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static void RegisterNotesVariable(string name, string type, HashSet<string> notesVariables, Dictionary<string, string> notesVariableTypes, HashSet<string> notesDocumentCollections, HashSet<string> notesDocuments, HashSet<string> notesItems)
    {
        notesVariables.Add(name);
        notesVariableTypes[name] = type;
        if (type.Equals("NotesDocumentCollection", StringComparison.OrdinalIgnoreCase)) notesDocumentCollections.Add(name);
        if (type.Equals("NotesDocument", StringComparison.OrdinalIgnoreCase)) notesDocuments.Add(name);
        if (type.Equals("NotesItem", StringComparison.OrdinalIgnoreCase) || type.Equals("NotesRichTextItem", StringComparison.OrdinalIgnoreCase)) notesItems.Add(name);
    }

    private static string RewriteNothingReturningMembers(string line, IReadOnlyDictionary<string, string> notesVariableTypes)
    {
        foreach (var pair in notesVariableTypes.OrderByDescending(pair => pair.Key.Length))
        {
            if (NothingReturningMethods.TryGetValue(pair.Value, out var methods))
                foreach (var method in methods)
                    line = Regex.Replace(line, $@"\b{Regex.Escape(pair.Key)}\s*\.\s*{Regex.Escape(method)}\s*\(([^)]*)\)", $"XPScriptNotes.NormalizeObjectResult({pair.Key}.{method}($1))", RegexOptions.IgnoreCase);
            if (NothingReturningProperties.TryGetValue(pair.Value, out var properties))
                foreach (var property in properties)
                    line = Regex.Replace(line, $@"\b{Regex.Escape(pair.Key)}\s*\.\s*{Regex.Escape(property)}\b", $"XPScriptNotes.NormalizeObjectResult({pair.Key}.{property})", RegexOptions.IgnoreCase);
        }
        return line;
    }

    private static string RewriteDocumentItemValues(string line, string documentName)
    {
        var pattern = new Regex($@"\b{Regex.Escape(documentName)}\.([A-Za-z_]\w*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant); var offset = 0;
        while (offset < line.Length)
        {
            var match = pattern.Match(line, offset); if (!match.Success) break; var next = match.Index + match.Length; while (next < line.Length && char.IsWhiteSpace(line[next])) next++;
            if (next < line.Length && (line[next] == '(' || line[next] == '=')) { offset = next + 1; continue; }
            var itemName = match.Groups[1].Value;
            var replacement = $"XPScriptNotesValueApi.GetDocumentItemValues({documentName}, \"{itemName}\")";
            line = line[..match.Index] + replacement + line[(match.Index + match.Length)..]; offset = match.Index + replacement.Length;
        }
        return line;
    }

    private static string RewriteNotesItemValues(string line, string itemName)
    {
        var pattern = new Regex($@"\b{Regex.Escape(itemName)}\.Values\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant); var offset = 0;
        while (offset < line.Length)
        {
            var match = pattern.Match(line, offset); if (!match.Success) break; var next = match.Index + match.Length; while (next < line.Length && char.IsWhiteSpace(line[next])) next++;
            if (next < line.Length && line[next] == '=') { offset = next + 1; continue; }
            string replacement; int consumedThrough = match.Index + match.Length - 1;
            if (next < line.Length && line[next] == '(') { var close = FindMatchingParen(line, next); if (close < 0) break; var index = line[(next + 1)..close].Trim(); replacement = $"XPScriptNotesValueApi.GetItemValueAt({itemName}, {index})"; consumedThrough = close; }
            else replacement = $"XPScriptNotesValueApi.GetItemValues({itemName})";
            line = line[..match.Index] + replacement + line[(consumedThrough + 1)..]; offset = match.Index + replacement.Length;
        }
        return line;
    }

    private static int FindMatchingParen(string text, int open)
    {
        var depth = 0; var inString = false;
        for (var i = open; i < text.Length; i++) { var c = text[i]; if (c == '"') { if (inString && i + 1 < text.Length && text[i + 1] == '"') { i++; continue; } inString = !inString; continue; } if (inString) continue; if (c == '(') depth++; else if (c == ')' && --depth == 0) return i; }
        return -1;
    }

    private static string CreateExpression(string type, string rawArguments)
    {
        var args = rawArguments.Trim();
        if (type.Equals("NotesSession", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(args)) throw new CompilerException("NotesSession requires the Notes/Domino runtime directory argument.");
            return $"XPScriptNotes.CreateSession({args})";
        }
        if (type.Equals("NotesDBDirectory", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(args)) throw new CompilerException("NotesDBDirectory requires a server name argument. Use an empty string for the current computer.");
            return $"XPScriptNotes.CreateDbDirectory({args})";
        }
        throw new CompilerException($"{type} objects must be created from NotesSession, NotesDatabase, NotesView, or NotesDocument.");
    }
}
