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
                refreshHandler = field.RefreshHandler,
                onChangeHandler = field.OnChangeHandler,
                visible = field.Visible,
                enabled = field.Enabled,
                readOnly = field.ReadOnly
""");

        generated = ReplaceRequired(generated,
            "                options = field.Options,\n                layoutRow = field.LayoutRow,",
            """
                options = field.Options,
                layoutRow = field.LayoutRow,
""");

        generated = ReplaceRequired(generated,
            "            }).ToArray()\n        };\n",
            """
            }).ToArray(),
            buttons = form.Buttons.Select(button => new
            {
                name = button.Name,
                label = button.Label,
                style = button.Style,
                layoutRow = button.LayoutRow,
                layoutColumn = button.LayoutColumn,
                columnSpan = button.ColumnSpan,
                rowSpan = button.RowSpan,
                visible = button.Visible,
                enabled = button.Enabled
            }).ToArray()
        };
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
