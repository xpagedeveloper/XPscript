using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIFormDesktopLayoutMetadataPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        // Replace the complete desktop request object as one scoped unit.  The old
        // implementation patched a generic "}).ToArray() };" marker which could
        // match another anonymous object in generated code and inject form.Buttons
        // where `form` was not in scope.
        if (generated.Contains("gridColumns = form.GridColumns", StringComparison.Ordinal) &&
            generated.Contains("buttons = form.Buttons.Select", StringComparison.Ordinal))
            return generated;

        const string pattern = """
            var\s+request\s*=\s*new\s*\{\s*
            title\s*=\s*form\.Title\s*,\s*
            width\s*=\s*form\.Width\s*>\s*0\s*\?\s*form\.Width\s*:\s*\(int\?\)null\s*,\s*
            height\s*=\s*form\.Height\s*>\s*0\s*\?\s*form\.Height\s*:\s*\(int\?\)null\s*,\s*
            resizable\s*=\s*form\.Resizable\s*,\s*
            fields\s*=\s*fields\.Select\(field\s*=>\s*new\s*\{.*?\}\)\.ToArray\(\)\s*
            \}\s*;
            """;

        const string replacement = """
        var request = new
        {
            title = form.Title,
            width = form.Width > 0 ? form.Width : (int?)null,
            height = form.Height > 0 ? form.Height : (int?)null,
            resizable = form.Resizable,
            gridColumns = form.GridColumns,
            fields = fields.Select(field => new
            {
                name = field.Name,
                label = field.Label,
                type = field.Type,
                required = field.Required,
                value = field.Type == "PasswordField"
                    ? null
                    : (data.Contains(field.Name) ? form.GetFieldValueString(field.Name) : null),
                minLength = field.MinLength,
                maxLength = field.MaxLength,
                minimum = field.Minimum,
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
        """;

        var regex = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
        if (!regex.IsMatch(generated))
            throw new CompilerException("Unable to install UIForm desktop layout metadata bridge (request-object).");
        return regex.Replace(generated, replacement, 1);
    }
}
