using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIFormWebPartialRefreshPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        if (!generated.Contains("public void SetRefreshOnChange(object? sourceField, object? targetRegion)\n", StringComparison.Ordinal))
        {
            generated = ReplaceRequiredRegex(
                generated,
                @"public\s+void\s+SetRefreshOnChange\s*\(\s*object\?\s+sourceField\s*,\s*object\?\s+targetRegion\s*,\s*object\?\s+handlerName\s*\)",
                """
    public void SetRefreshOnChange(object? sourceField, object? targetRegion)
        => SetRefreshOnChange(sourceField, targetRegion, string.Empty);

    public void SetRefreshOnChange(object? sourceField, object? targetRegion, object? handlerName)
""",
                "refresh-overload");
        }

        if (!generated.Contains("if (handler.Length > 0 &&", StringComparison.Ordinal))
        {
            generated = ReplaceRequiredRegex(
                generated,
                """var\s+handler\s*=\s*XPScriptRuntime\.CStr\(handlerName\)\.Trim\(\)\s*;\s*if\s*\(handler\.Length\s+is\s+<\s*1\s+or\s+>\s+128\s*\|\|.*?throw\s+new\s+XPScriptRuntimeException\(5,\s*"UIForm refresh handler name is invalid\."\)\s*;\s*field\.RefreshTargetRegion\s*=\s*region\s*;\s*field\.RefreshHandler\s*=\s*handler\s*;""",
                """
        var handler = XPScriptRuntime.CStr(handlerName).Trim();
        if (handler.Length > 0 && (handler.Length > 128 || !handler.All(ch => char.IsLetterOrDigit(ch) || ch == '_') || !char.IsLetter(handler[0]) && handler[0] != '_'))
            throw new XPScriptRuntimeException(5, "UIForm refresh handler name is invalid.");
        field.RefreshTargetRegion = region;
        field.RefreshHandler = handler;
""",
                "optional-handler",
                RegexOptions.CultureInvariant | RegexOptions.Singleline);
        }

        if (!generated.Contains("__xps_uiform_event", StringComparison.Ordinal))
        {
            generated = ReplaceRequiredRegex(
                generated,
                """if\s*\(XPScriptUIWebAdapter\.Method\.Equals\("POST",\s*StringComparison\.OrdinalIgnoreCase\)\)\s*\{\s*foreach\s*\(var\s+field\s+in\s+_fields\)\s*\{?\s*(?:if\s*\(field\.Type\s*==\s*"MultiListBox"\)\s*ApplySubmittedValues\(field,\s*XPScriptUIWebAdapter\.FormValues\(field\.Name\)\)\s*;\s*else\s*)?ApplySubmittedValue\(field,\s*XPScriptUIWebAdapter\.FormFirst\(field\.Name\)\)\s*;\s*\}?\s*return\s+"OK"\s*;\s*\}\s*XPScriptUIWebAdapter\.WriteHtml\(RenderWebForm\(\)\)\s*;\s*return\s+"Pending"\s*;""",
                """
            if (XPScriptUIWebAdapter.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var field in _fields)
                {
                    if (field.Type == "MultiListBox") ApplySubmittedValues(field, XPScriptUIWebAdapter.FormValues(field.Name));
                    else ApplySubmittedValue(field, XPScriptUIWebAdapter.FormFirst(field.Name));
                }

                var eventToken = XPScriptUIWebAdapter.FormFirst("__xps_uiform_event").Trim();
                if (eventToken.Length > 0)
                {
                    var eventValue = XPScriptUIWebAdapter.FormFirst("__xps_uiform_event_value");
                    XPScriptUIWebAdapter.WriteHtml(DispatchRegisteredEvent(eventToken, eventValue));
                    return "Pending";
                }

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
""",
                "web-post-dispatch",
                RegexOptions.CultureInvariant | RegexOptions.Singleline);
        }

        if (!generated.Contains("private string RenderReactiveScript()", StringComparison.Ordinal))
        {
            generated = ReplaceRequiredRegex(
                generated,
                """html\.Append\("<button style=\\"grid-column:1/-1\\" type=\\"submit\\" name=\\"__xps_uiform_submit\\" value=\\"1\\">OK</button></form>"\)\s*;""",
                """
        foreach (var button in _buttons.Where(button => button.Visible))
        {
            var buttonName = System.Net.WebUtility.HtmlEncode(button.Name);
            var buttonLabel = System.Net.WebUtility.HtmlEncode(button.Label);
            html.Append("<button type=\"button\" class=\"xpscript-uiform-action\" data-xps-event=\"button:").Append(buttonName).Append("\"");
            if (!button.Enabled) html.Append(" disabled");
            html.Append(" data-xps-button=\"").Append(buttonName).Append("\"");
            if (button.LayoutColumn > 0)
                html.Append(" style=\"grid-column:").Append(button.LayoutColumn).Append(" / span ").Append(button.ColumnSpan)
                    .Append(";grid-row:").Append(button.LayoutRow).Append(" / span ").Append(button.RowSpan).Append("\"");
            html.Append(">").Append(buttonLabel).Append("</button>");
        }
        html.Append("<button style=\"grid-column:1/-1\" type=\"submit\" name=\"__xps_uiform_submit\" value=\"1\">OK</button>");
        html.Append(RenderReactiveScript());
        html.Append("</form>");
""",
                "reactive-form-render");

            generated = ReplaceRequiredRegex(
                generated,
                @"private\s+static\s+string\s+NormalizeFieldName\s*\(\s*object\?\s+value\s*\)",
                """
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
        var eventFields = _fields.Where(field => field.OnChangeHandler.Length > 0 || field.RefreshHandler.Length > 0).ToArray();
        var partialFields = _fields.Where(field => field.RefreshTargetRegion.Length > 0 && field.OnChangeHandler.Length == 0 && field.RefreshHandler.Length == 0).ToArray();
        if (eventFields.Length == 0 && partialFields.Length == 0 && _buttons.Count == 0) return string.Empty;

        var script = new System.Text.StringBuilder();
        script.Append("<script>(function(){const f=document.currentScript.closest('form');if(!f)return;const ev={");
        for (var i = 0; i < eventFields.Length; i++)
        {
            if (i > 0) script.Append(',');
            script.Append(System.Text.Json.JsonSerializer.Serialize(eventFields[i].Name));
            script.Append(':');
            script.Append(System.Text.Json.JsonSerializer.Serialize("change:" + eventFields[i].Name));
        }
        script.Append("},pr={");
        for (var i = 0; i < partialFields.Length; i++)
        {
            if (i > 0) script.Append(',');
            script.Append(System.Text.Json.JsonSerializer.Serialize(partialFields[i].Name));
            script.Append(':');
            script.Append(System.Text.Json.JsonSerializer.Serialize(partialFields[i].RefreshTargetRegion));
        }
        script.Append("};");
        script.Append("async function postEvent(token,value){const d=new FormData(f);d.set('__xps_uiform_event',token);d.set('__xps_uiform_event_value',value||'');const q=await fetch(window.location.href,{method:'POST',body:d,credentials:'same-origin',headers:{'X-XPScript-Event':'1'}});if(!q.ok)return;const s=JSON.parse(await q.text());apply(s);}");
        script.Append("function apply(s){if(s.navigation&&s.navigation.target){let u=s.navigation.target;if(s.navigation.parameterName){u+=(u.includes('?')?'&':'?')+encodeURIComponent(s.navigation.parameterName)+'='+encodeURIComponent(s.navigation.parameterValue||'');}window.location.assign(u);return;}if(Array.isArray(s.fields))for(const x of s.fields){const e=f.elements.namedItem(x.name);if(!e)continue;const w=e.closest('.xpscript-uiform-field');if(w)w.hidden=x.visible===false;if('disabled'in e)e.disabled=x.enabled===false;if('readOnly'in e)e.readOnly=x.readOnly===true;if('required'in e)e.required=x.required===true;const l=f.querySelector('label[for=\"xps_'+CSS.escape(x.name)+'\"]');if(l)l.textContent=x.label||'';if(e.tagName==='SELECT'&&Array.isArray(x.options)){const old=e.multiple?Array.from(e.selectedOptions).map(o=>o.value):(x.value||'');e.replaceChildren();for(const o of x.options){const z=document.createElement('option');z.value=o;z.textContent=o;if(e.multiple&&Array.isArray(x.values))z.selected=x.values.includes(o);e.appendChild(z);}if(!e.multiple)e.value=x.value==null?old:x.value;}else if(e.type==='checkbox'){e.checked=String(x.value).toLowerCase()==='true';}else if(x.value!=null&&e.type!=='password'){e.value=x.value;}}if(Array.isArray(s.buttons))for(const b of s.buttons){const e=f.querySelector('[data-xps-button=\"'+CSS.escape(b.name)+'\"]');if(!e)continue;e.hidden=b.visible===false;e.disabled=b.enabled===false;e.textContent=b.label||'';}}");
        script.Append("f.addEventListener('click',function(e){const b=e.target.closest('[data-xps-event]');if(!b)return;e.preventDefault();postEvent(b.dataset.xpsEvent,'');});");
        script.Append("f.addEventListener('change',async function(e){const n=e.target&&e.target.name;if(!n)return;if(ev[n]){const v=e.target.multiple?Array.from(e.target.selectedOptions).map(o=>o.value).join('\\u001f'):(e.target.type==='checkbox'?(e.target.checked?'true':'false'):(e.target.value||''));postEvent(ev[n],v);return;}if(!pr[n])return;const d=new FormData(f);d.set('__xps_uiform_partial',pr[n]);const q=await fetch(window.location.href,{method:'POST',body:d,credentials:'same-origin',headers:{'X-XPScript-Partial':'1'}});if(!q.ok)return;const h=await q.text();const t=document.getElementById('xps_region_'+pr[n]);if(t)t.outerHTML=h;});})();</script>");
        return script.ToString();
    }

    private static string NormalizeFieldName(object? value)
""",
                "reactive-helpers");
        }

        return generated;
    }

    private static string ReplaceRequiredRegex(
        string source,
        string pattern,
        string replacement,
        string stage,
        RegexOptions options = RegexOptions.CultureInvariant)
    {
        var regex = new Regex(pattern, options);
        if (!regex.IsMatch(source))
            throw new CompilerException($"Unable to install UIForm web partial refresh runtime ({stage}).");
        return regex.Replace(source, replacement, 1);
    }
}
