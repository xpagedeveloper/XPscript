namespace XPScript.Compiler;

internal sealed class UIListViewDesktopPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        generated = ReplaceRequired(
            generated,
            """
            title = _title,
            selectedIndex = _selectedIndex,
            columns = visibleColumns.Select(column => new
""",
            """
            title = _title,
            selectedIndex = _selectedIndex,
            sortable = _sortable,
            filterEnabled = _filterEnabled,
            hasRowAction = _rowActionTarget.Length > 0,
            columns = visibleColumns.Select(column => new
""");

        generated = ReplaceRequired(
            generated,
            """
        if (!result.Equals("OK", StringComparison.OrdinalIgnoreCase)) return "Cancel";
        if (root.TryGetProperty("selectedIndex", out var selectedElement) && selectedElement.TryGetInt32(out var selected))
            _selectedIndex = selected >= 0 && selected < _data.Count ? selected : -1;
        return "OK";
    }

    private string RenderWebList()
""",
            """
        if (root.TryGetProperty("selectedIndex", out var selectedElement) && selectedElement.TryGetInt32(out var selected))
            _selectedIndex = selected >= 0 && selected < _data.Count ? selected : -1;

        if (result.Equals("Open", StringComparison.OrdinalIgnoreCase))
        {
            if (_selectedIndex < 0 || _rowActionTarget.Length == 0)
                return "Cancel";
            WriteDesktopNavigationRequest();
            return "Navigate";
        }

        if (!result.Equals("OK", StringComparison.OrdinalIgnoreCase)) return "Cancel";
        return "OK";
    }

    private void WriteDesktopNavigationRequest()
    {
        var navigationFile = Environment.GetEnvironmentVariable("XPSCRIPT_NAVIGATION_FILE");
        if (string.IsNullOrWhiteSpace(navigationFile))
            return;

        var value = GetRowValueString(_selectedIndex, _rowActionValueField);
        var request = System.Text.Json.JsonSerializer.Serialize(new
        {
            target = _rowActionTarget,
            parameterName = _rowActionParameterName,
            parameterValue = value
        });
        File.WriteAllText(navigationFile, request);
    }

    private string RenderWebList()
""");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIListView desktop runtime bridge.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
