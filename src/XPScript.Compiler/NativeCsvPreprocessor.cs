using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NativeCsvPreprocessor
{
    private static readonly AsyncLocal<(int Line, string Source)?> CurrentDiagnosticLine = new();
    private const string NativeCsvTypePattern = "XPCsvDocument|XPCsvHeaderCollection|XPCsvRowCollection|XPCsvRow|XPCsvColumnCollection|XPCsvColumn";

    public string Transform(string source)
    {
        if (!source.Contains("Csv", StringComparison.OrdinalIgnoreCase)) return source;

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 16);
        var nativeVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documentVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rowVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var iteratorId = 0;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var raw = lines[lineIndex];
            CurrentDiagnosticLine.Value = (lineIndex + 1, raw);
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNew = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+({NativeCsvTypePattern})\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                var type = dimNew.Groups[2].Value;
                nativeVariables.Add(name);
                if (type.Equals("XPCsvDocument", StringComparison.OrdinalIgnoreCase)) documentVariables.Add(name);
                if (type.Equals("XPCsvRow", StringComparison.OrdinalIgnoreCase)) rowVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                output.Add(indent + $"{name} = {CreateExpression(type, dimNew.Groups[3].Value)}");
                continue;
            }

            var dim = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+({NativeCsvTypePattern})\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                var name = dim.Groups[1].Value;
                var type = dim.Groups[2].Value;
                nativeVariables.Add(name);
                if (type.Equals("XPCsvDocument", StringComparison.OrdinalIgnoreCase)) documentVariables.Add(name);
                if (type.Equals("XPCsvRow", StringComparison.OrdinalIgnoreCase)) rowVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                continue;
            }

            RejectRemovedFileWriteApis(line, documentVariables);

            if (TryRewriteFileWrite(line, documentVariables, out var fileWrite))
            {
                output.Add(indent + fileWrite);
                continue;
            }

            if (TryRewriteFromBytes(line, documentVariables, out var fromBytes))
            {
                output.Add(indent + fromBytes);
                continue;
            }

            var rewritten = line;
            rewritten = Regex.Replace(rewritten, @"\bXPCsvDocument\.Load\s*\(([^)]*)\)", m => RewriteLoad(m.Groups[1].Value), RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bXPCsvDocument\.ParseBytes\s*\(", "XPScriptNativeCsv.ParseBytes(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bXPCsvDocument\.Parse\s*\(", "XPScriptNativeCsv.Parse(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bCsvParseBytes\s*\(", "XPScriptNativeCsv.ParseBytes(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bCsvParse\s*\(", "XPScriptNativeCsv.Parse(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bCsvStringify\s*\(", "XPScriptNativeCsv.Stringify(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bCsvEscape\s*\(", "XPScriptNativeCsv.Escape(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPCsvDocument\s*(?:\(\s*\))?", "XPScriptNativeCsv.CreateDocument()", RegexOptions.IgnoreCase);

            foreach (var documentVariable in documentVariables)
            {
                var escaped = Regex.Escape(documentVariable);
                rewritten = Regex.Replace(rewritten, $@"\b{escaped}\.FileEncoding\b", documentVariable + ".Encoding", RegexOptions.IgnoreCase);
                rewritten = Regex.Replace(rewritten, $@"\b{escaped}\.Headers\.Add\s*\(", documentVariable + ".AddHeader(", RegexOptions.IgnoreCase);
                rewritten = Regex.Replace(rewritten, $@"\b{escaped}\.ToSpreadsheet\s*\(([^)]*)\)", m =>
                {
                    var args = m.Groups[1].Value.Trim();
                    return string.IsNullOrEmpty(args)
                        ? $"XPScriptSpreadsheetCsvInterop.ToSpreadsheet({documentVariable})"
                        : $"XPScriptSpreadsheetCsvInterop.ToSpreadsheet({documentVariable}, {args})";
                }, RegexOptions.IgnoreCase);
            }

            rewritten = Regex.Replace(rewritten, @"\.Headers\s*\[([^\]]+)\]", ".Headers.Get($1)", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\.Rows\s*\[([^\]]+)\]", ".Rows.Get($1)", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\.Columns\s*\[([^\]]+)\]", ".Columns.Get($1)", RegexOptions.IgnoreCase);
            foreach (var rowVariable in rowVariables)
                rewritten = Regex.Replace(rewritten, $@"\b{Regex.Escape(rowVariable)}\s*\[([^\]]+)\]", rowVariable + ".Get($1)", RegexOptions.IgnoreCase);

            var csvForAll = Regex.Match(rewritten, @"^ForAll\s+([A-Za-z_]\w*)\s+In\s+(.+\.(?:Headers|Rows|Columns))$", RegexOptions.IgnoreCase);
            if (csvForAll.Success)
            {
                var temp = "__xpsCsvIterator" + (++iteratorId).ToString(System.Globalization.CultureInfo.InvariantCulture);
                output.Add(indent + $"Dim {temp} As Variant");
                output.Add(indent + $"{temp} = {csvForAll.Groups[2].Value}");
                output.Add(indent + $"ForAll {csvForAll.Groups[1].Value} In {temp}");
                continue;
            }

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (nativeVariables.Contains(set.Groups[1].Value) || set.Groups[2].Value.Contains("XPScriptNativeCsv", StringComparison.Ordinal)
                || set.Groups[2].Value.Contains("XPScriptSpreadsheetCsvInterop", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static CompilerException CsvDiagnostic(
        string diagnosticCode,
        string message,
        params (string Name, string Value)[] properties)
    {
        var current = CurrentDiagnosticLine.Value;
        var safeSource = CompilerDiagnosticRedaction.MaskStringLiterals(current?.Source ?? string.Empty).TrimEnd();
        var position = Math.Max(1, (current?.Source ?? string.Empty).IndexOf(properties.FirstOrDefault(p => p.Name == "symbol").Value ?? string.Empty, StringComparison.OrdinalIgnoreCase) + 1);
        var diagnostic = new CompileDiagnostic
        {
            Line = current?.Line ?? 0,
            Position = position,
            EndLine = current?.Line ?? 0,
            EndColumn = position + 1,
            Description = message,
            DiagnosticCode = diagnosticCode,
            Category = "syntax",
            Properties = properties.Select(property => new CompileDiagnosticProperty { Name = property.Name, Value = property.Value }).ToList(),
            SourceCode = safeSource,
            MarkedCode = safeSource.Length == 0 ? string.Empty : safeSource + Environment.NewLine + new string(' ', Math.Max(0, position - 1)) + "^"
        };
        return new CompilerException(message, diagnosticCode, "syntax", [diagnostic]);
    }

    private static string RewriteLoad(string rawArguments)
    {
        var args = SplitTopLevelArguments(rawArguments);
        if (args.Count is < 1 or > 4)
            throw CsvDiagnostic(CompilerDiagnosticCodes.InvalidNativeArgumentList, "XPCsvDocument.Load requires path and optional encoding, delimiter, and hasHeaders arguments.", ("symbol", "XPCsvDocument.Load"), ("expectedArgumentCount", "1..4"), ("actualArgumentCount", args.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return "XPScriptSpreadsheetCsvInterop.LoadCsv(" + string.Join(", ", args) + ")";
    }

    private static void RejectRemovedFileWriteApis(string line, HashSet<string> documentVariables)
    {
        if (Regex.IsMatch(line, @"^(?:Call\s+)?(?:CsvSave|CsvWriteFile)\b", RegexOptions.IgnoreCase))
            throw CsvDiagnostic(CompilerDiagnosticCodes.RemovedNativeApi, "CSV file output is available only through XPCsvDocument.Save or XPCsvDocument.SaveFile.", ("symbol", "CsvSave/CsvWriteFile"), ("expectedConstruct", "XPCsvDocument.Save or XPCsvDocument.SaveFile"));
        foreach (var documentVariable in documentVariables)
            if (Regex.IsMatch(line, $@"^(?:Call\s+)?{Regex.Escape(documentVariable)}\.WriteFile\b", RegexOptions.IgnoreCase))
                throw CsvDiagnostic(CompilerDiagnosticCodes.RemovedNativeApi, "XPCsvDocument.WriteFile was removed. Use Save or SaveFile.", ("symbol", "XPCsvDocument.WriteFile"), ("expectedConstruct", "XPCsvDocument.Save or XPCsvDocument.SaveFile"));
    }

    private static bool TryRewriteFileWrite(string line, HashSet<string> documentVariables, out string rewritten)
    {
        rewritten = "";
        foreach (var documentVariable in documentVariables)
        {
            var method = Regex.Match(line, $@"^(?:Call\s+)?{Regex.Escape(documentVariable)}\.(Save|SaveFile)\s*\((.*)\)\s*$", RegexOptions.IgnoreCase);
            if (!method.Success) continue;
            var args = SplitTopLevelArguments(method.Groups[2].Value);
            if (args.Count is < 1 or > 2) throw CsvDiagnostic(CompilerDiagnosticCodes.InvalidNativeArgumentList, "XPCsvDocument.Save requires path and optional encoding arguments.", ("symbol", "XPCsvDocument.Save"), ("expectedArgumentCount", "1..2"), ("actualArgumentCount", args.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            var bytes = args.Count == 1 ? documentVariable + ".ToBytes()" : documentVariable + ".ToBytes(" + args[1] + ")";
            rewritten = "Call XPCrossPlatformRuntime.WriteBytes(" + args[0] + ", " + bytes + ")";
            return true;
        }
        return false;
    }

    private static bool TryRewriteFromBytes(string line, HashSet<string> documentVariables, out string rewritten)
    {
        rewritten = "";
        foreach (var documentVariable in documentVariables)
        {
            var method = Regex.Match(line, $@"^(?:Call\s+)?{Regex.Escape(documentVariable)}\.FromBytes\s*\((.*)\)\s*$", RegexOptions.IgnoreCase);
            if (!method.Success) continue;
            var args = SplitTopLevelArguments(method.Groups[1].Value);
            if (args.Count is < 1 or > 2) throw CsvDiagnostic(CompilerDiagnosticCodes.InvalidNativeArgumentList, "XPCsvDocument.FromBytes requires bytes and an optional encoding argument.", ("symbol", "XPCsvDocument.FromBytes"), ("expectedArgumentCount", "1..2"), ("actualArgumentCount", args.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            var encoding = args.Count == 1 ? documentVariable + ".Encoding" : args[1];
            rewritten = documentVariable + " = XPScriptNativeCsv.ParseBytes(" + args[0] + ", " + encoding + ", " + documentVariable + ".Delimiter, " + documentVariable + ".HasHeaders)";
            return true;
        }
        return false;
    }

    private static List<string> SplitTopLevelArguments(string text)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var depth = 0;
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '"')
            {
                current.Append(ch);
                if (quoted && i + 1 < text.Length && text[i + 1] == '"') { current.Append(text[++i]); continue; }
                quoted = !quoted;
                continue;
            }
            if (!quoted)
            {
                if (ch == '(') depth++;
                else if (ch == ')') depth--;
                else if (ch == ',' && depth == 0) { result.Add(current.ToString().Trim()); current.Clear(); continue; }
            }
            current.Append(ch);
        }
        if (quoted || depth != 0) throw CsvDiagnostic(CompilerDiagnosticCodes.InvalidNativeArgumentList, "Invalid CSV argument list.", ("expectedConstruct", "balanced CSV argument list"));
        if (current.Length > 0 || result.Count > 0) result.Add(current.ToString().Trim());
        if (result.Any(string.IsNullOrWhiteSpace)) throw CsvDiagnostic(CompilerDiagnosticCodes.InvalidNativeArgumentList, "CSV arguments cannot be empty.", ("expectedConstruct", "non-empty CSV arguments"));
        return result;
    }

    private static string CreateExpression(string type, string rawArguments)
    {
        var args = rawArguments.Trim();
        if (type.Equals("XPCsvDocument", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(args) ? "XPScriptNativeCsv.CreateDocument()" : $"XPScriptNativeCsv.Parse({args})";
        throw CsvDiagnostic(CompilerDiagnosticCodes.InvalidNativeConstructor, "Only XPCsvDocument can be created with New.", ("symbol", type), ("symbolKind", "type"), ("expectedConstruct", "New XPCsvDocument"));
    }
}
