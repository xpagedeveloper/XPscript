namespace XPScript.Compiler;

internal sealed class UIFormDesktopLayoutMetadataPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        generated = ReplaceRequired(generated,
            "            resizable = form.Resizable,\n            fields = fields.Select(field => new\n",
            """
            resizable = form.Resizable,
            gridColumns = form.GridColumns,
            fields = fields.Select(field => new
""");

        generated = ReplaceRequired(generated,
            "                maximum = field.Maximum,\n                options = field.Options\n",
            """
                maximum = field.Maximum,
                options = field.Options,
                layoutRow = field.LayoutRow,
                layoutColumn = field.LayoutColumn,
                columnSpan = field.ColumnSpan,
                rowSpan = field.RowSpan,
                regionId = field.RegionId,
                refreshTargetRegion = field.RefreshTargetRegion,
                refreshHandler = field.RefreshHandler
""");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm desktop layout metadata bridge.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
