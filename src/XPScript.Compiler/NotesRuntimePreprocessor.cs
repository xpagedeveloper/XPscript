using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NotesRuntimePreprocessor
{
    private const string NotesTypePattern = "NotesSession|NotesDatabase|NotesView|NotesDocumentCollection|NotesDocument|NotesItem|NotesRichTextItem|NotesName|NotesDateTime|NotesAgentResult|NotesDXLImporter|NotesDXLExporter";

    private static readonly Dictionary<string, string[]> NothingReturningMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NotesDatabase"] = ["OpenView", "GetDocumentByNoteId", "OpenDocumentByNoteId", "GetDocumentByUNID", "OpenDocumentByUNID", "Search", "FTSearch", "RunAgent"],
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

            var rewritten = Regex.Replace(
                line,
                @"\bNew\s+NotesSession\s*\((.*)\)",
                "XPScriptNotes.CreateSession($1)",
                RegexOptions.IgnoreCase);

            if (Regex.IsMatch(rewritten, @"\.GetFirstDocumentByKey\s*\(", RegexOptions.IgnoreCase))
                throw new CompilerException("NotesView.GetFirstDocumentByKey has been renamed to NotesView.GetDocumentByKey.");

            rewritten = Regex.Replace(
                rewritten,
                @"\.GetDocumentByKey\s*\(",
                ".GetFirstDocumentByKey(",
                RegexOptions.IgnoreCase);

            var unsupportedNew = Regex.Match(rewritten, $@"\bNew\s+(NotesDatabase|NotesView|NotesDocumentCollection|NotesDocument|NotesItem|NotesRichTextItem|NotesName|NotesDateTime|NotesAgentResult|NotesDXLImporter|NotesDXLExporter)\b", RegexOptions.IgnoreCase);
            if (unsupportedNew.Success)
                throw new CompilerException($"{unsupportedNew.Groups[1].Value} objects must be created from NotesSession, NotesDatabase, NotesView, or NotesDocument.");

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
                foreach (var documentName in notesDocuments.OrderByDescending(value => value.Length))
                    rhs = RewriteDocumentItemValues(rhs, documentName);
                foreach (var itemName in notesItems.OrderByDescending(value => value.Length))
                    rhs = RewriteNotesItemValues(rhs, itemName);
                rhs = RewriteNothingReturningMembers(rhs, notesVariableTypes);
                rhs = rhs.Equals("Nothing", StringComparison.OrdinalIgnoreCase)
                    ? "XPScriptNotes.NothingValue"
                    : $"XPScriptNotes.NormalizeObjectResult({rhs})";

                var temp = "__notesReplacement" + (++replacementIndex).ToString(System.Globalization.CultureInfo.InvariantCulture);
                output.Add(indent + $"Dim {temp} As Variant");
                output.Add(indent + $"{temp} = {rhs}");
                output.Add(indent + $"Call XPScriptNotes.RecycleForReplacement({name}, {temp})");
                output.Add(indent + $"{name} = {temp}");
                continue;
            }

            foreach (var documentName in notesDocuments.OrderByDescending(value => value.Length))
                rewritten = RewriteDocumentItemValues(rewritten, documentName);

            foreach (var itemName in notesItems.OrderByDescending(value => value.Length))
                rewritten = RewriteNotesItemValues(rewritten, itemName);

            rewritten = RewriteNothingReturningMembers(rewritten, notesVariableTypes);

            foreach (var collectionName in notesDocumentCollections)
            {
                var escaped = Regex.Escape(collectionName);
                if (Regex.IsMatch(rewritten, $@"\b(?:LBound|UBound)\s*\(\s*{escaped}\s*(?:,\s*1\s*)?\)", RegexOptions.IgnoreCase))
                    throw new CompilerException("LBound/UBound are no longer supported for NotesDocumentCollection. Use Count and document navigation methods.");
                if (Regex.IsMatch(rewritten, $@"\b{escaped}\s*\(", RegexOptions.IgnoreCase))
                    throw new CompilerException("NotesDocumentCollection index syntax is no longer supported. Use GetDocument, GetFirstDocument, or GetNextDocument.");
                if (Regex.IsMatch(rewritten, $@"\b{escaped}\.Get\s*\(", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(rewritten, $@"\b{escaped}\.GetNoteIdString\s*\(", RegexOptions.IgnoreCase))
                    throw new CompilerException("NotesDocumentCollection.Get/GetNoteIdString are no longer supported. Use GetDocument instead.");
            }

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static void RegisterNotesVariable(
        string name,
        string type,
        ISet<string> notesVariables,
        IDictionary<string, string> notesVariableTypes,
        ISet<string> documentCollections,
        ISet<string> documents,
        ISet<string> items)
    {
        notesVariables.Add(name);
        notesVariableTypes[name] = type;
        if (type.Equals("NotesDocumentCollection", StringComparison.OrdinalIgnoreCase)) documentCollections.Add(name);
        if (type.Equals("NotesDocument", StringComparison.OrdinalIgnoreCase)) documents.Add(name);
        if (type.Equals("NotesItem", StringComparison.OrdinalIgnoreCase) || type.Equals("NotesRichTextItem", StringComparison.OrdinalIgnoreCase)) items.Add(name);
    }

    private static string RewriteNothingReturningMembers(string line, IReadOnlyDictionary<string, string> notesVariableTypes)
    {
        foreach (var pair in notesVariableTypes.OrderByDescending(value => value.Key.Length))
        {
            if (NothingReturningMethods.TryGetValue(pair.Value, out var methods))
            {
                foreach (var method in methods)
                    line = WrapMethodCalls(line, pair.Key, method);
            }

            if (NothingReturningProperties.TryGetValue(pair.Value, out var properties))
            {
                foreach (var property in properties)
                    line = WrapPropertyRead(line, pair.Key, property);
            }
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
            if (IsInsideNothingNormalizer(line, match.Index))
            {
                offset = match.Index + match.Length;
                continue;
            }

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

    private static string WrapPropertyRead(string line, string variableName, string propertyName)
    {
        var pattern = new Regex($@"\b{Regex.Escape(variableName)}\.{Regex.Escape(propertyName)}\b(?!\s*=)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return pattern.Replace(line, match => IsInsideNothingNormalizer(line, match.Index)
            ? match.Value
            : "XPScriptNotes.NormalizeObjectResult(" + match.Value + ")");
    }

    private static bool IsInsideNothingNormalizer(string line, int memberIndex)
    {
        const string prefix = "XPScriptNotes.NormalizeObjectResult(";
        var start = Math.Max(0, memberIndex - prefix.Length);
        return line.AsSpan(start, memberIndex - start).EndsWith(prefix, StringComparison.Ordinal);
    }

    private static string RewriteDocumentItemValues(string line, string documentName)
    {
        var pattern = new Regex($@"\b{Regex.Escape(documentName)}\.GetItemValue\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var offset = 0;
        while (offset < line.Length)
        {
            var match = pattern.Match(line, offset);
            if (!match.Success) break;
            var open = line.IndexOf('(', match.Index);
            var close = FindMatchingParen(line, open);
            if (close < 0) break;

            var args = line[(open + 1)..close].Trim();
            var next = close + 1;
            while (next < line.Length && char.IsWhiteSpace(line[next])) next++;

            string replacement;
            int consumedThrough;
            if (next < line.Length && line[next] == '(')
            {
                var indexClose = FindMatchingParen(line, next);
                if (indexClose < 0) break;
                var index = line[(next + 1)..indexClose].Trim();
                replacement = $"XPScriptNotesValueApi.GetDocumentItemValueAt({documentName}, {args}, {index})";
                consumedThrough = indexClose;
            }
            else
            {
                replacement = $"XPScriptNotesValueApi.GetDocumentItemValues({documentName}, {args})";
                consumedThrough = close;
            }

            line = line[..match.Index] + replacement + line[(consumedThrough + 1)..];
            offset = match.Index + replacement.Length;
        }
        return line;
    }

    private static string RewriteNotesItemValues(string line, string itemName)
    {
        var pattern = new Regex($@"\b{Regex.Escape(itemName)}\.Values\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var offset = 0;
        while (offset < line.Length)
        {
            var match = pattern.Match(line, offset);
            if (!match.Success) break;
            var next = match.Index + match.Length;
            while (next < line.Length && char.IsWhiteSpace(line[next])) next++;

            if (next < line.Length && line[next] == '=')
            {
                offset = next + 1;
                continue;
            }

            string replacement;
            int consumedThrough = match.Index + match.Length - 1;
            if (next < line.Length && line[next] == '(')
            {
                var close = FindMatchingParen(line, next);
                if (close < 0) break;
                var index = line[(next + 1)..close].Trim();
                replacement = $"XPScriptNotesValueApi.GetItemValueAt({itemName}, {index})";
                consumedThrough = close;
            }
            else
            {
                replacement = $"XPScriptNotesValueApi.GetItemValues({itemName})";
            }

            line = line[..match.Index] + replacement + line[(consumedThrough + 1)..];
            offset = match.Index + replacement.Length;
        }
        return line;
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
