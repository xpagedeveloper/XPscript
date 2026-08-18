namespace XPScript.Compiler;

internal sealed class UIFormWebPartialRefreshPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        generated = ReplaceRequired(generated,
            """
            if (XPScriptUIWebAdapter.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var field in _fields) ApplySubmittedValue(field, XPScriptUIWebAdapter.FormFirst(field.Name));
                return "OK";
            }
            XPScriptUIWebAdapter.WriteHtml(RenderWebForm());
            return "Pending";
""",
            """
            if (XPScriptUIWebAdapter.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var field in _fields) ApplySubmittedValue(field, XPScriptUIWebAdapter.FormFirst(field.Name));
                var partialRegion = XPScriptUIWebAdapter.FormFirst("__xps_uiform_partial").Trim();
                if (partialRegion.Length > 0)
                {
                    if (!_fields.Any(field => field.RegionId.Equals(partialRegion, StringComparison.Ordinal)))
                        throw new XPScriptRuntimeException(5, $"UIForm partial refresh region '{partialRegion}' does not exist.");
                    XPScriptUIWebAdapter.WriteHtml(RenderWebRegion(partialRegion));
                    return "Pending";
                }
                return "OK";
            }
            XPScriptUIWebAdapter.WriteHtml(RenderWebForm());
            return "Pending";
""");

        generated = ReplaceRequired(generated,
            """
        html.Append("<button style=\"grid-column:1/-1\" type=\"submit\" name=\"__xps_uiform_submit\" value=\"1\">OK</button></form>");
        return html.ToString();
    }

    private static string NormalizeFieldName(object? value)
""",
            """
        html.Append("<button style=\"grid-column:1/-1\" type=\"submit\" name=\"__xps_uiform_submit\" value=\"1\">OK</button>");
        html.Append(RenderReactiveScript());
        html.Append("</form>");
        return html.ToString();
    }

    private string RenderWebRegion(string regionId)
    {
        var html = RenderWebForm();
        var encoded = System.Net.WebUtility.HtmlEncode(regionId);
        var marker = " id=\"xps_region_" + encoded + "\"";
        var markerIndex = html.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new XPScriptRuntimeException(5, $"UIForm partial refresh region '{regionId}' could not be rendered.");
        var start = html.LastIndexOf("<div", markerIndex, StringComparison.Ordinal);
        var end = html.IndexOf("</div>", markerIndex, StringComparison.Ordinal);
        if (start < 0 || end < 0)
            throw new XPScriptRuntimeException(5, $"UIForm partial refresh region '{regionId}' produced invalid markup.");
        return html.Substring(start, end + 6 - start);
    }

    private string RenderReactiveScript()
    {
        var rules = _fields.Where(field => field.RefreshTargetRegion.Length > 0).ToArray();
        if (rules.Length == 0) return string.Empty;

        var script = new System.Text.StringBuilder();
        script.Append("<script>(function(){const f=document.currentScript.closest('form');if(!f)return;const r={");
        for (var i = 0; i < rules.Length; i++)
        {
            if (i > 0) script.Append(',');
            script.Append(System.Text.Json.JsonSerializer.Serialize(rules[i].Name));
            script.Append(':');
            script.Append(System.Text.Json.JsonSerializer.Serialize(rules[i].RefreshTargetRegion));
        }
        script.Append("};f.addEventListener('change',async function(e){const n=e.target&&e.target.name;if(!n||!r[n])return;const d=new FormData(f);d.set('__xps_uiform_partial',r[n]);const q=await fetch(window.location.href,{method:'POST',body:d,credentials:'same-origin',headers:{'X-XPScript-Partial':'1'}});if(!q.ok)return;const h=await q.text();const t=document.getElementById('xps_region_'+r[n]);if(t)t.outerHTML=h;});})();</script>");
        return script.ToString();
    }

    private static string NormalizeFieldName(object? value)
""");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm web partial refresh runtime.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
