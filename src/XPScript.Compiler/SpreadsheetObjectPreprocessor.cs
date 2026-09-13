using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class SpreadsheetObjectPreprocessor
{
    private static readonly string[] SpreadsheetMembers =
    [
        "Path", "WorksheetCount", "AddWorksheet", "Worksheet", "RemoveWorksheet", "RenameWorksheet",
        "Open", "Save", "SaveAs", "Close", "ToBytes"
    ];

    private static readonly string[] WorksheetMembers =
    [
        "Name", "Index", "UsedRowCount", "UsedColumnCount", "Cell", "Clear"
    ];

    private static readonly string[] CellMembers =
    [
        "Value", "Text", "Formula", "Address", "Row", "Column", "Clear"
    ];

    public string Transform(string source)
    {
        var codeOnly = PreprocessorFeatureGate.CodeOnly(source);
        if (!PreprocessorFeatureGate.ContainsTypeReference(codeOnly, "XPSpreadsheet", "XPWorksheet", "XPCell")) return source;

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 16);
        var spreadsheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var worksheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cells = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNew = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+XPSpreadsheet\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                spreadsheets.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                var args = dimNew.Groups[2].Value.Trim();
                output.Add(indent + (string.IsNullOrEmpty(args)
                    ? $"{name} = XPScriptSpreadsheetFactory.Create()"
                    : $"{name} = XPScriptSpreadsheetFactory.Create({args})"));
                continue;
            }

            var dim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+(XPSpreadsheet|XPWorksheet|XPCell)\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                var name = dim.Groups[1].Value;
                switch (dim.Groups[2].Value.ToUpperInvariant())
                {
                    case "XPSPREADSHEET": spreadsheets.Add(name); break;
                    case "XPWORKSHEET": worksheets.Add(name); break;
                    case "XPCELL": cells.Add(name); break;
                }
                output.Add(indent + $"Dim {name} As Variant");
                continue;
            }

            var rewritten = Regex.Replace(line, @"\bNew\s+XPSpreadsheet\s*(?:\(\s*\))?", "XPScriptSpreadsheetFactory.Create()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPSpreadsheet\s*\((.*)\)", m => $"XPScriptSpreadsheetFactory.Create({m.Groups[1].Value})", RegexOptions.IgnoreCase);

            TrackAssignments(rewritten, spreadsheets, worksheets, cells);
            NormalizeMembers(ref rewritten, spreadsheets, SpreadsheetMembers);
            NormalizeMembers(ref rewritten, worksheets, WorksheetMembers);
            NormalizeMembers(ref rewritten, cells, CellMembers);

            foreach (var spreadsheet in spreadsheets.OrderByDescending(x => x.Length))
            {
                var escaped = Regex.Escape(spreadsheet);
                rewritten = Regex.Replace(rewritten, $@"\b{escaped}\s*\.\s*(AddWorksheet|Worksheet)\s*\(", m =>
                {
                    var method = m.Groups[1].Value.Equals("AddWorksheet", StringComparison.OrdinalIgnoreCase) ? "AddWorksheet" : "Worksheet";
                    return spreadsheet + "." + method + "(";
                }, RegexOptions.IgnoreCase);
            }

            foreach (var worksheet in worksheets.OrderByDescending(x => x.Length))
            {
                var escaped = Regex.Escape(worksheet);
                rewritten = Regex.Replace(rewritten, $@"\b{escaped}\s*\.\s*Cell\s*\(", worksheet + ".Cell(", RegexOptions.IgnoreCase);
            }

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (spreadsheets.Contains(set.Groups[1].Value)
                || worksheets.Contains(set.Groups[1].Value)
                || cells.Contains(set.Groups[1].Value)
                || set.Groups[2].Value.Contains("XPScriptSpreadsheet", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static void TrackAssignments(string line, HashSet<string> spreadsheets, HashSet<string> worksheets, HashSet<string> cells)
    {
        var assignment = Regex.Match(line, @"^(?:Set\s+)?([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
        if (!assignment.Success) return;
        var target = assignment.Groups[1].Value;
        var rhs = assignment.Groups[2].Value;

        if (Regex.IsMatch(rhs, @"\bXPScriptSpreadsheetFactory\.Create\s*\(", RegexOptions.IgnoreCase))
            spreadsheets.Add(target);
        if (Regex.IsMatch(rhs, @"\.\s*(?:AddWorksheet|Worksheet)\s*\(", RegexOptions.IgnoreCase))
            worksheets.Add(target);
        if (Regex.IsMatch(rhs, @"\.\s*Cell\s*\(", RegexOptions.IgnoreCase))
            cells.Add(target);
    }

    private static void NormalizeMembers(ref string source, IEnumerable<string> variables, IEnumerable<string> members)
    {
        foreach (var variable in variables.OrderByDescending(x => x.Length))
        {
            var escaped = Regex.Escape(variable);
            foreach (var member in members)
                source = Regex.Replace(source, $@"\b{escaped}\s*\.\s*{Regex.Escape(member)}\b", variable + "." + member, RegexOptions.IgnoreCase);
        }
    }
}
