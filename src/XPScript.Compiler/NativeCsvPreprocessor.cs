using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NativeCsvPreprocessor
{
    private const string NativeCsvTypePattern = "CsvDocument|CsvHeaderCollection|CsvRowCollection|CsvRow|CsvColumnCollection|CsvColumn";

    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 8);
        var nativeVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rowVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                if (type.Equals("CsvRow", StringComparison.OrdinalIgnoreCase)) rowVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                output.Add(indent + $"{name} = {CreateExpression(type, dimNew.Groups[3].Value)}");
                continue;
            }

            var dim = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+({NativeCsvTypePattern})\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                var name = dim.Groups[1].Value;
                nativeVariables.Add(name);
                if (dim.Groups[2].Value.Equals("CsvRow", StringComparison.OrdinalIgnoreCase)) rowVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
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

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (nativeVariables.Contains(set.Groups[1].Value) || set.Groups[2].Value.Contains("XPScriptNativeCsv", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static string CreateExpression(string type, string rawArguments)
    {
        var args = rawArguments.Trim();
        if (type.Equals("CsvDocument", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(args) ? "XPScriptNativeCsv.CreateDocument()" : $"XPScriptNativeCsv.Parse({args})";
        throw new CompilerException("Only CsvDocument can be created with New.");
    }
}
