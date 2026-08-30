namespace XPScript.Compiler;

internal sealed class UIFormWebWindowLifecyclePostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        const string signature = "    private string RenderWebForm()\n    {";
        var signatureIndex = generated.IndexOf(signature, StringComparison.Ordinal);
        if (signatureIndex < 0)
            throw new CompilerException("Unable to install UIForm web window lifecycle (renderer signature).");
        generated = generated.Replace(signature,
            "    private string RenderWebForm() => RenderWebForm(true);\n\n    private string RenderWebForm(bool modal)\n    {",
            StringComparison.Ordinal);

        var methodStart = generated.IndexOf("    private string RenderWebForm(bool modal)\n    {", StringComparison.Ordinal);
        var builderMarker = "        var html = new System.Text.StringBuilder();\n";
        var builderIndex = generated.IndexOf(builderMarker, methodStart, StringComparison.Ordinal);
        if (builderIndex < 0)
            throw new CompilerException("Unable to install UIForm web window lifecycle (renderer builder).");

        var prefix = """
        var id = System.Net.WebUtility.HtmlEncode(_instanceId);
        if (modal)
        {
            html.Append("<div class=\"modal fade xpscript-uiform-modal\" id=\"xps_uiform_").Append(id)
                .Append("\" tabindex=\"-1\" aria-hidden=\"true\"><div class=\"modal-dialog modal-dialog-scrollable\"><div class=\"modal-content\"><div class=\"modal-body\">");
        }
        else
        {
            html.Append("<div class=\"xpscript-uiform-window card shadow-sm mb-3\" id=\"xps_uiform_").Append(id)
                .Append("\"><div class=\"card-body\">");
        }
""";
        generated = generated.Insert(builderIndex + builderMarker.Length, prefix);

        methodStart = generated.IndexOf("    private string RenderWebForm(bool modal)\n    {", StringComparison.Ordinal);
        var returnMarker = "        return html.ToString();";
        var returnIndex = generated.IndexOf(returnMarker, methodStart, StringComparison.Ordinal);
        if (returnIndex < 0)
            throw new CompilerException("Unable to install UIForm web window lifecycle (renderer return).");

        var suffix = """
        html.Append("</div>");
        if (modal)
        {
            html.Append("</div></div></div><script>(function(){var e=document.getElementById('xps_uiform_").Append(id)
                .Append("');if(!e)return;if(window.bootstrap&&bootstrap.Modal){bootstrap.Modal.getOrCreateInstance(e,{backdrop:true,keyboard:true,focus:true}).show();}else{e.classList.add('show');e.style.display='block';e.removeAttribute('aria-hidden');}})();</script>");
        }
        else
        {
            html.Append("</div>");
        }
""";
        generated = generated.Insert(returnIndex, suffix);
        return generated;
    }
}
