namespace XPScript.Compiler;

internal sealed class UIListViewDesktopPostProcessor
{
    private const string ClassToken = "internal sealed class XPScriptUIListView";
    private const string MetadataSentinel = "hasRowAction = _rowActionTarget.Length > 0";
    private const string NavigationSentinel = "private void WriteDesktopNavigationRequest()";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        var classStart = generated.IndexOf(ClassToken, StringComparison.Ordinal);
        if (classStart < 0)
            throw new CompilerException("Unable to install UIListView desktop runtime bridge (class).");

        var prefix = generated[..classStart];
        var listSource = generated[classStart..];

        if (!listSource.Contains(MetadataSentinel, StringComparison.Ordinal))
        {
            const string token = "selectedIndex = _selectedIndex,";
            var index = listSource.IndexOf(token, StringComparison.Ordinal);
            if (index < 0)
                throw new CompilerException("Unable to install UIListView desktop runtime bridge (metadata).");
            index += token.Length;
            // Match the platform newline used by raw string templates in later postprocessors.
            var newline = Environment.NewLine;
            listSource = listSource[..index]
                + newline + "            sortable = _sortable,"
                + newline + "            filterEnabled = _filterEnabled,"
                + newline + "            hasRowAction = _rowActionTarget.Length > 0,"
                + listSource[index..];
        }

        if (!listSource.Contains(NavigationSentinel, StringComparison.Ordinal))
        {
            listSource = ReplaceBetweenRequired(
                listSource,
                "if (!result.Equals(\"OK\", StringComparison.OrdinalIgnoreCase)) return \"Cancel\";",
                "private string RenderWebList()",
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
""",
                "navigation");
        }

        return prefix + listSource;
    }

    private static string ReplaceBetweenRequired(
        string source,
        string startToken,
        string endToken,
        string replacement,
        string stage)
    {
        var start = source.IndexOf(startToken, StringComparison.Ordinal);
        if (start < 0)
            throw new CompilerException($"Unable to install UIListView desktop runtime bridge ({stage}:start).");

        var endStart = source.IndexOf(endToken, start, StringComparison.Ordinal);
        if (endStart < 0)
            throw new CompilerException($"Unable to install UIListView desktop runtime bridge ({stage}:end).");

        var lineStart = source.LastIndexOf('\n', Math.Max(0, start - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var indentation = source[lineStart..start];
        var formatted = string.Join("\n", replacement.Split('\n').Select(line => indentation + line));

        return source[..lineStart] + formatted + source[(endStart + endToken.Length)..];
    }
}
