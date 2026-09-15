using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class SpreadsheetObjectPreprocessor
{
    private static readonly string[] SpreadsheetMembers =
    [
        "Path", "WorksheetCount", "CreatedByXPScript", "XPScriptFormatVersion", "CanUpdate",
        "AddWorksheet", "Worksheet", "RemoveWorksheet", "RenameWorksheet",
        "Open", "FromBytes", "Save", "SaveAs", "SaveAsSimple", "Close", "ToBytes"
    ];

    private static readonly string[] WorksheetMembers =
    [
        "Name", "Index", "UsedRowCount", "UsedColumnCount", "Cell", "Range",
        "AutoFilter", "ClearAutoFilter", "AutoFilterRange", "Clear", "ToCsv"
    ];

    private static readonly string[] CellMembers =
    [
        "Value", "Text", "Formula", "BackgroundColor", "Bold", "Italic", "FontColor", "FontSize", "FontName",
        "NumberFormat", "HorizontalAlignment", "VerticalAlignment", "WrapText",
        "BorderTop", "BorderBottom", "BorderLeft", "BorderRight", "BorderColor",
        "Address", "Row", "Column", "Clear"
    ];

    private static readonly string[] RangeMembers =
    [
        "Address", "RowCount", "ColumnCount", "Values", "BackgroundColor", "Bold", "Italic", "FontColor", "FontSize", "FontName",
        "NumberFormat", "HorizontalAlignment", "VerticalAlignment", "WrapText",
        "BorderTop", "BorderBottom", "BorderLeft", "BorderRight", "BorderColor",
        "ColumnWidth", "RowHeight", "AutoFit", "Clear"
    ];

    private static readonly string[] DirectSettableCellMembers =
    [
        "Value", "Formula", "BackgroundColor", "Bold", "Italic", "FontColor", "FontSize", "FontName", "NumberFormat",
        "HorizontalAlignment", "VerticalAlignment", "WrapText", "BorderTop", "BorderBottom", "BorderLeft", "BorderRight", "BorderColor"
    ];

    private static readonly string[] DirectSettableRangeMembers =
    [
        "BackgroundColor", "Bold", "Italic", "FontColor", "FontSize", "FontName", "NumberFormat",
        "HorizontalAlignment", "VerticalAlignment", "WrapText", "BorderTop", "BorderBottom", "BorderLeft", "BorderRight", "BorderColor",
        "ColumnWidth", "RowHeight"
    ];

    public string Transform(string source)
    {
        var codeOnly = PreprocessorFeatureGate.CodeOnly(source);
        if (!PreprocessorFeatureGate.ContainsTypeReference(codeOnly, "XPSpreadsheet", "XPWorksheet", "XPCell", "XPRange")) return source;

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 24);
        var spreadsheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var worksheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cells = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ranges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var temporaryCellId = 0;
        var temporaryRangeId = 0;

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
                output.Add(indent + (string.IsNullOrEmpty(args) ? $"{name} = XPScriptSpreadsheetFactory.Create()" : $"{name} = XPScriptSpreadsheetFactory.Create({args})"));
                continue;
            }

            var dim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+(XPSpreadsheet|XPWorksheet|XPCell|XPRange)\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                var name = dim.Groups[1].Value;
                switch (dim.Groups[2].Value.ToUpperInvariant())
                {
                    case "XPSPREADSHEET": spreadsheets.Add(name); break;
                    case "XPWORKSHEET": worksheets.Add(name); break;
                    case "XPCELL": cells.Add(name); break;
                    case "XPRANGE": ranges.Add(name); break;
                }
                output.Add(indent + $"Dim {name} As Variant");
                continue;
            }

            var rewritten = Regex.Replace(line, @"\bNew\s+XPSpreadsheet\s*(?:\(\s*\))?", "XPScriptSpreadsheetFactory.Create()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPSpreadsheet\s*\((.*)\)", m => $"XPScriptSpreadsheetFactory.Create({m.Groups[1].Value})", RegexOptions.IgnoreCase);

            TrackAssignments(rewritten, spreadsheets, worksheets, cells, ranges);
            NormalizeMembers(ref rewritten, spreadsheets, SpreadsheetMembers);
            NormalizeMembers(ref rewritten, worksheets, WorksheetMembers);
            NormalizeMembers(ref rewritten, cells, CellMembers);
            NormalizeMembers(ref rewritten, ranges, RangeMembers);

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
                rewritten = Regex.Replace(rewritten, $@"\b{escaped}\s*\.\s*(Cell|Range|AutoFilter|ClearAutoFilter)\b", m =>
                    worksheet + "." + WorksheetMembers.First(x => x.Equals(m.Groups[1].Value, StringComparison.OrdinalIgnoreCase)), RegexOptions.IgnoreCase);
                rewritten = Regex.Replace(rewritten, $@"\b{escaped}\s*\.\s*ToCsv\s*\(([^)]*)\)", m =>
                {
                    var args = m.Groups[1].Value.Trim();
                    return string.IsNullOrEmpty(args)
                        ? $"XPScriptSpreadsheetCsvInterop.FromWorksheet({worksheet})"
                        : $"XPScriptSpreadsheetCsvInterop.FromWorksheet({worksheet}, {args})";
                }, RegexOptions.IgnoreCase);
            }

            var directCellAssignment = Regex.Match(rewritten,
                @"^([A-Za-z_]\w*)\.Cell\((.*)\)\.([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (directCellAssignment.Success && worksheets.Contains(directCellAssignment.Groups[1].Value)
                && DirectSettableCellMembers.Any(x => x.Equals(directCellAssignment.Groups[3].Value, StringComparison.OrdinalIgnoreCase)))
            {
                var worksheet = directCellAssignment.Groups[1].Value;
                var arguments = directCellAssignment.Groups[2].Value;
                var member = DirectSettableCellMembers.First(x => x.Equals(directCellAssignment.Groups[3].Value, StringComparison.OrdinalIgnoreCase));
                var value = directCellAssignment.Groups[4].Value;
                var temporary = "__xpsSpreadsheetCell" + (++temporaryCellId).ToString(System.Globalization.CultureInfo.InvariantCulture);
                cells.Add(temporary);
                output.Add(indent + $"Dim {temporary} As Variant");
                output.Add(indent + $"{temporary} = {worksheet}.Cell({arguments})");
                output.Add(indent + $"{temporary}.{member} = {value}");
                continue;
            }

            var directRangeAssignment = Regex.Match(rewritten,
                @"^([A-Za-z_]\w*)\.Range\((.*)\)\.([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (directRangeAssignment.Success && worksheets.Contains(directRangeAssignment.Groups[1].Value)
                && DirectSettableRangeMembers.Any(x => x.Equals(directRangeAssignment.Groups[3].Value, StringComparison.OrdinalIgnoreCase)))
            {
                var worksheet = directRangeAssignment.Groups[1].Value;
                var arguments = directRangeAssignment.Groups[2].Value;
                var member = DirectSettableRangeMembers.First(x => x.Equals(directRangeAssignment.Groups[3].Value, StringComparison.OrdinalIgnoreCase));
                var value = directRangeAssignment.Groups[4].Value;
                var temporary = "__xpsSpreadsheetRange" + (++temporaryRangeId).ToString(System.Globalization.CultureInfo.InvariantCulture);
                ranges.Add(temporary);
                output.Add(indent + $"Dim {temporary} As Variant");
                output.Add(indent + $"{temporary} = {worksheet}.Range({arguments})");
                output.Add(indent + $"{temporary}.{member} = {value}");
                continue;
            }

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (spreadsheets.Contains(set.Groups[1].Value) || worksheets.Contains(set.Groups[1].Value)
                || cells.Contains(set.Groups[1].Value) || ranges.Contains(set.Groups[1].Value)
                || set.Groups[2].Value.Contains("XPScriptSpreadsheet", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static void TrackAssignments(string line, HashSet<string> spreadsheets, HashSet<string> worksheets, HashSet<string> cells, HashSet<string> ranges)
    {
        var assignment = Regex.Match(line, @"^(?:Set\s+)?([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
        if (!assignment.Success) return;
        var target = assignment.Groups[1].Value;
        var rhs = assignment.Groups[2].Value;
        if (Regex.IsMatch(rhs, @"\bXPScriptSpreadsheetFactory\.Create\s*\(", RegexOptions.IgnoreCase)) spreadsheets.Add(target);
        if (Regex.IsMatch(rhs, @"\.\s*(?:AddWorksheet|Worksheet)\s*\(", RegexOptions.IgnoreCase)) worksheets.Add(target);
        if (Regex.IsMatch(rhs, @"\.\s*Cell\s*\(", RegexOptions.IgnoreCase)) cells.Add(target);
        if (Regex.IsMatch(rhs, @"\.\s*Range\s*\(", RegexOptions.IgnoreCase)) ranges.Add(target);
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
