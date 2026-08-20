namespace XPScript.Compiler;

internal sealed class UIFormAdditionalFieldPreparePostProcessor
{
    private const string Sentinel = "private string __XpsExtendedFieldCompatibilityMarker()";
    private const string RenderMarker = "    private string RenderWebForm()";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains(Sentinel, StringComparison.Ordinal)) return generated;
        var renderIndex = generated.IndexOf(RenderMarker, StringComparison.Ordinal);
        if (renderIndex < 0) return generated;

        const string helper = """
    private string __XpsExtendedFieldCompatibilityMarker()
    {
        var html = new System.Text.StringBuilder();
        html.Append("<button type=\"submit\" name=\"__xps_uiform_submit\" value=\"1\">OK</button></form>");
        return html.ToString();
    }

""";
        return generated.Insert(renderIndex, helper);
    }
}
