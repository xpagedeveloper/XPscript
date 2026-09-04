using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NativeCsvPreprocessor
{
    private const string NativeCsvTypePattern = "CsvDocument|CsvHeaderCollection|CsvRowCollection|CsvRow|CsvColumnCollection|CsvColumn";

    public string Transform(string source)
    {
        if (!source.Contains("Csv", StringComparison.OrdinalIgnoreCase)) return source;

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 16);
        var nativeVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documentVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rowVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var iteratorId = 0;

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNew = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+({NativeCsvTypePattern})\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                var type = dimNew.Groups[2].Value;
                nativeVariables.Add(name);
                if (type.Equals("CsvDocument", StringComparison.OrdinalIgnoreCase)) documentVariables.Add(name);
                if (type.Equals("CsvRow", StringComparison.OrdinalIgnoreCase)) rowVariables.Add(name);
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
                if (type.Equals("CsvDocument", StringComparison.OrdinalIgnoreCase)) documentVariables.Add(name);
                if (type.Equals("CsvRow", StringComparison.OrdinalIgnoreCase)) rowVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                continue;
            }

            if (TryRewriteFileWrite(line, documentVariables, out var fileWrite))
            {
                output.Add(indent + fileWrite);
                continue;
            }

            var rewritten = line;
            rewritten = Regex.Replace(rewritten, @"\bCsvDocument\.ParseBytes\s*\(", "XPScriptNativeCsv.ParseBytes(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bCsvDocument\.Parse\s*\(", "XPScriptNativeCsv.Parse(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bCsvParseBytes\s*\(", "XPScriptNativeCsv.ParseBytes(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bCsvParse\s*\(", "XPScriptNativeCsv.Parse(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bCsvStringify\s*\(", "XPScriptNativeCsv.Stringify(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bCsvEscape\s*\(", "XPScriptNativeCsv.Escape(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+CsvDocument\s*(?:\(\s*\))?", "XPScriptNativeCsv.CreateDocument()", RegexOptions.IgnoreCase);

            foreach (var documentVariable in documentVariables)
            {
                rewritten = Regex.Replace(
                    rewritten,
                    $@"\b{Regex.Escape(documentVariable)}\.FileEncoding\b",
                    documentVariable + ".Encoding",
                    RegexOptions.IgnoreCase);
            }

            // XPscript does not otherwise use square-bracket member indexing. CSV keeps this
            // convenience surface by lowering collection/member index syntax to strict Get().
            rewritten = Regex.Replace(rewritten, @"\.Headers\s*\[([^\]]+)\]", ".Headers.Get($1)", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\.Rows\s*\[([^\]]+)\]", ".Rows.Get($1)", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\.Columns\s*\[([^\]]+)\]", ".Columns.Get($1)", RegexOptions.IgnoreCase);
            foreach (var rowVariable in rowVariables)
            {
                rewritten = Regex.Replace(
                    rewritten,
                    $@"\b{Regex.Escape(rowVariable)}\s*\[([^\]]+)\]",
                    rowVariable + ".Get($1)",
                    RegexOptions.IgnoreCase);
            }

            // The core ForAll grammar currently accepts an identifier after In. Preserve the
            // public CSV surface `ForAll x In doc.Headers/Rows/row.Columns` by lowering the
            // member expression to a temporary Variant before the core transpiler sees it.
            var csvForAll = Regex.Match(
                rewritten,
                @"^ForAll\s+([A-Za-z_]\w*)\s+In\s+(.+\.(?:Headers|Rows|Columns))$",
                RegexOptions.IgnoreCase);
            if (csvForAll.Success)
            {
                var temp = "__xpsCsvIterator" + (++iteratorId).ToString(System.Globalization.CultureInfo.InvariantCulture);
                output.Add(indent + $"Dim {temp} As Variant");
                output.Add(indent + $"{temp} = {csvForAll.Groups[2].Value}");
                output.Add(indent + $"ForAll {csvForAll.Groups[1].Value} In {temp}");
                continue;
            }

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (nativeVariables.Contains(set.Groups[1].Value) || set.Groups[2].Value.Contains("XPScriptNativeCsv", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static bool TryRewriteFileWrite(string line, HashSet<string> documentVariables, out string rewritten)
    {
        rewritten = "";

        var call = Regex.Match(line, @"^Call\s+(CsvSave|CsvWriteFile)\s*\((.*)\)\s*$", RegexOptions.IgnoreCase);
        if (call.Success)
            return TryBuildGlobalFileWrite(call.Groups[2].Value, out rewritten);

        var statement = Regex.Match(line, @"^(CsvSave|CsvWriteFile)\s+(.+)$", RegexOptions.IgnoreCase);
        if (statement.Success)
            return TryBuildGlobalFileWrite(statement.Groups[2].Value, out rewritten);

        foreach (var documentVariable in documentVariables)
        {
            var method = Regex.Match(
                line,
                $@"^(?:Call\s+)?{Regex.Escape(documentVariable)}\.(Save|SaveFile|WriteFile)\s*\((.*)\)\s*$",
                RegexOptions.IgnoreCase);
            if (!method.Success) continue;

            var args = SplitTopLevelArguments(method.Groups[2].Value);
            if (args.Count is < 1 or > 2)
                throw new CompilerException("CsvDocument.Save requires path and optional encoding arguments.");
            var bytes = args.Count == 1
                ? documentVariable + ".ToBytes()"
                : documentVariable + ".ToBytes(" + args[1] + ")";
            rewritten = "Call XPCrossPlatformRuntime.WriteBytes(" + args[0] + ", " + bytes + ")";
            return true;
        }

        return false;
    }

    private static bool TryBuildGlobalFileWrite(string rawArguments, out string rewritten)
    {
        rewritten = "";
        var args = SplitTopLevelArguments(rawArguments);
        if (args.Count is < 2 or > 3)
            throw new CompilerException("CsvSave requires document, path and optional encoding arguments.");
        var bytes = args.Count == 2
            ? args[0] + ".ToBytes()"
            : args[0] + ".ToBytes(" + args[2] + ")";
        rewritten = "Call XPCrossPlatformRuntime.WriteBytes(" + args[1] + ", " + bytes + ")";
        return true;
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
                if (quoted && i + 1 < text.Length && text[i + 1] == '"')
                {
                    current.Append(text[++i]);
                    continue;
                }
                quoted = !quoted;
                continue;
            }

            if (!quoted)
            {
                if (ch == '(') depth++;
                else if (ch == ')') depth--;
                else if (ch == ',' && depth == 0)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }
            }
            current.Append(ch);
        }

        if (quoted || depth != 0)
            throw new CompilerException("Invalid CSV file-write argument list.");
        result.Add(current.ToString().Trim());
        if (result.Any(string.IsNullOrWhiteSpace))
            throw new CompilerException("CSV file-write arguments cannot be empty.");
        return result;
    }

    private static string CreateExpression(string type, string rawArguments)
    {
        var args = rawArguments.Trim();
        if (type.Equals("CsvDocument", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(args) ? "XPScriptNativeCsv.CreateDocument()" : $"XPScriptNativeCsv.Parse({args})";
        throw new CompilerException("Only CsvDocument can be created with New.");
    }
}
