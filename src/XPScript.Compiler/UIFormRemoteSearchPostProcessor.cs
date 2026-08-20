namespace XPScript.Compiler;

internal sealed class UIFormRemoteSearchPostProcessor
{
    private const string Sentinel = "public bool RemoteSearch { get; set; }";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (!generated.Contains("AddAutoCompleteField", StringComparison.Ordinal)) return generated;
        if (generated.Contains(Sentinel, StringComparison.Ordinal)) return generated;

        generated = InjectMetadata(generated);
        generated = InjectMethods(generated);
        generated = PatchValidation(generated);
        generated = PatchWebRendering(generated);
        generated += "\n" + Runtime + "\n";
        return generated;
    }

    private static string InjectMetadata(string generated)
    {
        const string marker = "    public string LabelMember { get; set; } = \"label\";";
        const string replacement = """
    public string LabelMember { get; set; } = "label";
    public bool RemoteSearch { get; set; }
    public string SearchParameter { get; set; } = "q";
    public string ValueParameter { get; set; } = "value";
    public int RemoteSearchMinChars { get; set; } = 2;
    public int RemoteSearchMaxResults { get; set; } = 25;
""";
        if (!generated.Contains(marker, StringComparison.Ordinal))
            throw new CompilerException("Unable to extend UIForm remote-search metadata.");
        return generated.Replace(marker, replacement, StringComparison.Ordinal);
    }

    private static string InjectMethods(string generated)
    {
        const string marker = "    public void SetFileOptions(object? name, object? accept, object? maxBytes, object? multiple)";
        const string methods = """
    public XPScriptUIField AddLookupField(object? name, object? label, object? url, object? serverSideSearch)
        => XPScriptUIRemoteSearchRuntime.ToBoolean(serverSideSearch)
            ? AddRemoteSearchField(name, label, "LookupField", url, "value", "label")
            : AddRemoteField(name, label, "LookupField", url, "value", "label");

    public XPScriptUIField AddLookupField(object? name, object? label, object? url, object? valueMember, object? labelMember, object? serverSideSearch)
        => XPScriptUIRemoteSearchRuntime.ToBoolean(serverSideSearch)
            ? AddRemoteSearchField(name, label, "LookupField", url, valueMember, labelMember)
            : AddRemoteField(name, label, "LookupField", url, valueMember, labelMember);

    public XPScriptUIField AddAutoCompleteField(object? name, object? label, object? url, object? serverSideSearch)
        => XPScriptUIRemoteSearchRuntime.ToBoolean(serverSideSearch)
            ? AddRemoteSearchField(name, label, "AutoCompleteField", url, "value", "label")
            : AddRemoteField(name, label, "AutoCompleteField", url, "value", "label");

    public XPScriptUIField AddAutoCompleteField(object? name, object? label, object? url, object? valueMember, object? labelMember, object? serverSideSearch)
        => XPScriptUIRemoteSearchRuntime.ToBoolean(serverSideSearch)
            ? AddRemoteSearchField(name, label, "AutoCompleteField", url, valueMember, labelMember)
            : AddRemoteField(name, label, "AutoCompleteField", url, valueMember, labelMember);

    public void SetRemoteSearchOptions(object? name, object? searchParameter, object? valueParameter, object? minChars, object? maxResults)
    {
        var field = FindField(name);
        if (field.Type is not ("LookupField" or "AutoCompleteField"))
            throw new XPScriptRuntimeException(5, "UIForm remote search options require a LookupField or AutoCompleteField.");
        field.SearchParameter = XPScriptUIRemoteSearchRuntime.ParameterName(searchParameter, "search parameter");
        field.ValueParameter = XPScriptUIRemoteSearchRuntime.ParameterName(valueParameter, "value parameter");
        field.RemoteSearchMinChars = XPScriptUIRemoteSearchRuntime.BoundedInt(minChars, 0, 20, "minimum search length");
        field.RemoteSearchMaxResults = XPScriptUIRemoteSearchRuntime.BoundedInt(maxResults, 1, 200, "maximum search results");
    }

    private XPScriptUIField AddRemoteSearchField(object? name, object? label, string type, object? url, object? valueMember, object? labelMember)
    {
        var field = AddField(name, label, type);
        field.DataSourceUrl = XPScriptUIAdditionalFieldRuntime.AbsoluteHttpUrl(url);
        field.ValueMember = XPScriptUIAdditionalFieldRuntime.MemberName(valueMember, "value member");
        field.LabelMember = XPScriptUIAdditionalFieldRuntime.MemberName(labelMember, "label member");
        field.RemoteSearch = true;
        return field;
    }

""";
        if (!generated.Contains(marker, StringComparison.Ordinal))
            throw new CompilerException("Unable to inject UIForm remote-search methods.");
        return generated.Replace(marker, methods + marker, StringComparison.Ordinal);
    }

    private static string PatchValidation(string generated)
    {
        const string oldCode = """
            case "LookupField":
            case "AutoCompleteField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!field.Options.Contains(submitted, StringComparer.Ordinal)) throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' contains an unsupported lookup value.");
                _data.Set(field.Name, submitted);
                return;
""";
        const string newCode = """
            case "LookupField":
            case "AutoCompleteField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (field.RemoteSearch)
                {
                    if (!XPScriptUIRemoteSearchRuntime.ValidateOption(field.DataSourceUrl, field.ValueMember, field.LabelMember, field.ValueParameter, submitted))
                        throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' contains an unsupported lookup value.");
                }
                else if (!field.Options.Contains(submitted, StringComparer.Ordinal))
                    throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' contains an unsupported lookup value.");
                _data.Set(field.Name, submitted);
                return;
""";
        if (!generated.Contains(oldCode, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm remote-search validation.");
        return generated.Replace(oldCode, newCode, StringComparison.Ordinal);
    }

    private static string PatchWebRendering(string generated)
    {
        const string oldLookup = """
                case "LookupField":
                    html.Append("<select id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\"").Append(required).Append(">"); if (!field.Required) html.Append("<option value=\"\"></option>"); foreach (var option in field.RemoteOptions) { var ev = System.Net.WebUtility.HtmlEncode(option.Value); var el = System.Net.WebUtility.HtmlEncode(option.Label); html.Append("<option value=\"").Append(ev).Append("\""); if (option.Value == GetFieldValueString(field.Name)) html.Append(" selected"); html.Append(">").Append(el).Append("</option>"); } html.Append("</select>"); break;
""";
        const string newLookup = """
                case "LookupField":
                    if (!field.RemoteSearch)
                    {
                        html.Append("<select id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\"").Append(required).Append(">"); if (!field.Required) html.Append("<option value=\"\"></option>"); foreach (var option in field.RemoteOptions) { var ev = System.Net.WebUtility.HtmlEncode(option.Value); var el = System.Net.WebUtility.HtmlEncode(option.Label); html.Append("<option value=\"").Append(ev).Append("\""); if (option.Value == GetFieldValueString(field.Name)) html.Append(" selected"); html.Append(">").Append(el).Append("</option>"); } html.Append("</select>");
                        break;
                    }
                    var lookupCurrent = GetFieldValueString(field.Name);
                    html.Append("<input type=\"search\" autocomplete=\"off\" class=\"xpscript-remote-search\" id=\"xps_search_").Append(name).Append("\" placeholder=\"Search...\">");
                    html.Append("<select id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\"").Append(required).Append(">");
                    if (!field.Required) html.Append("<option value=\"\"></option>");
                    if (lookupCurrent.Length > 0) { var ec = System.Net.WebUtility.HtmlEncode(lookupCurrent); html.Append("<option value=\"").Append(ec).Append("\" selected>").Append(ec).Append("</option>"); }
                    html.Append("</select>");
                    html.Append(XPScriptUIRemoteSearchRuntime.LookupScript(field));
                    break;
""";
        if (!generated.Contains(oldLookup, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm LookupField server search renderer.");
        generated = generated.Replace(oldLookup, newLookup, StringComparison.Ordinal);

        const string oldAuto = """
                case "AutoCompleteField":
                    html.Append("<input list=\"xps_list_").Append(name).Append("\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append("><datalist id=\"xps_list_").Append(name).Append("\">"); foreach (var option in field.RemoteOptions) { html.Append("<option value=\"").Append(System.Net.WebUtility.HtmlEncode(option.Value)).Append("\" label=\"").Append(System.Net.WebUtility.HtmlEncode(option.Label)).Append("\"></option>"); } html.Append("</datalist>"); break;
""";
        const string newAuto = """
                case "AutoCompleteField":
                    if (!field.RemoteSearch)
                    {
                        html.Append("<input list=\"xps_list_").Append(name).Append("\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append("><datalist id=\"xps_list_").Append(name).Append("\">"); foreach (var option in field.RemoteOptions) { html.Append("<option value=\"").Append(System.Net.WebUtility.HtmlEncode(option.Value)).Append("\" label=\"").Append(System.Net.WebUtility.HtmlEncode(option.Label)).Append("\"></option>"); } html.Append("</datalist>");
                        break;
                    }
                    var autoCurrent = GetFieldValueString(field.Name);
                    html.Append("<input type=\"hidden\" id=\"xps_value_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(System.Net.WebUtility.HtmlEncode(autoCurrent)).Append("\">");
                    html.Append("<input type=\"search\" autocomplete=\"off\" class=\"xpscript-remote-search\" id=\"xps_").Append(name).Append("\" value=\"").Append(System.Net.WebUtility.HtmlEncode(autoCurrent)).Append("\"").Append(required).Append("><div class=\"xpscript-autocomplete-results\" id=\"xps_results_").Append(name).Append("\"></div>");
                    html.Append(XPScriptUIRemoteSearchRuntime.AutoCompleteScript(field));
                    break;
""";
        if (!generated.Contains(oldAuto, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm AutoCompleteField server search renderer.");
        return generated.Replace(oldAuto, newAuto, StringComparison.Ordinal);
    }

    private const string Runtime = """
internal static class XPScriptUIRemoteSearchRuntime
{
    public static bool ToBoolean(object? value)
    {
        if (value is bool boolean) return boolean;
        var text = XPScriptRuntime.CStr(value).Trim();
        if (bool.TryParse(text, out var parsed)) return parsed;
        if (text == "1") return true;
        if (text == "0") return false;
        throw new XPScriptRuntimeException(13, "UIForm server-side search flag must be True or False.");
    }

    public static string ParameterName(object? value, string label)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length is < 1 or > 64 || name.Any(c => !(char.IsLetterOrDigit(c) || c is '_' or '-')))
            throw new XPScriptRuntimeException(5, "UIForm " + label + " is invalid.");
        return name;
    }

    public static int BoundedInt(object? value, int minimum, int maximum, string label)
    {
        var number = XPScriptRuntime.CInt(value);
        if (number < minimum || number > maximum)
            throw new XPScriptRuntimeException(5, "UIForm " + label + " must be between " + minimum + " and " + maximum + ".");
        return number;
    }

    public static bool ValidateOption(string url, string valueMember, string labelMember, string valueParameter, string submitted)
    {
        var validationUrl = AddQuery(url, valueParameter, submitted, 2);
        foreach (var option in XPScriptUIAdditionalFieldRuntime.LoadOptions(validationUrl, valueMember, labelMember))
            if (option.Value.Equals(submitted, StringComparison.Ordinal)) return true;
        return false;
    }

    public static string LookupScript(XPScriptUIField field)
    {
        var id = Js("xps_" + field.Name);
        var searchId = Js("xps_search_" + field.Name);
        var url = Js(field.DataSourceUrl);
        var searchParameter = Js(field.SearchParameter);
        var valueMember = Js(field.ValueMember);
        var labelMember = Js(field.LabelMember);
        return "<script>(()=>{const s=document.getElementById(" + searchId + "),e=document.getElementById(" + id + ");let t=0,a=null;const load=async()=>{const q=s.value.trim();if(q.length<" + field.RemoteSearchMinChars + ")return;if(a)a.abort();a=new AbortController();const u=new URL(" + url + ",window.location.href);u.searchParams.set(" + searchParameter + ",q);u.searchParams.set('limit','" + field.RemoteSearchMaxResults + "');try{const r=await fetch(u,{headers:{Accept:'application/json'},credentials:'same-origin',signal:a.signal});if(!r.ok)return;const rows=await r.json();const keep=e.value;e.replaceChildren();if(!e.required)e.add(new Option('',''));for(const row of Array.isArray(rows)?rows.slice(0," + field.RemoteSearchMaxResults + "):[]){const v=String(row[" + valueMember + "]??'');if(!v)continue;const l=String(row[" + labelMember + "]??v);e.add(new Option(l,v,v===keep,v===keep));}}catch(x){if(x.name!=='AbortError')console.error('XPScript lookup search failed',x);}};s.addEventListener('input',()=>{clearTimeout(t);t=setTimeout(load,250);});})();</script>";
    }

    public static string AutoCompleteScript(XPScriptUIField field)
    {
        var inputId = Js("xps_" + field.Name);
        var valueId = Js("xps_value_" + field.Name);
        var resultsId = Js("xps_results_" + field.Name);
        var url = Js(field.DataSourceUrl);
        var searchParameter = Js(field.SearchParameter);
        var valueMember = Js(field.ValueMember);
        var labelMember = Js(field.LabelMember);
        return "<script>(()=>{const i=document.getElementById(" + inputId + "),h=document.getElementById(" + valueId + "),d=document.getElementById(" + resultsId + ");let t=0,a=null;i.addEventListener('input',()=>{h.value='';clearTimeout(t);const q=i.value.trim();d.replaceChildren();if(q.length<" + field.RemoteSearchMinChars + ")return;t=setTimeout(async()=>{if(a)a.abort();a=new AbortController();const u=new URL(" + url + ",window.location.href);u.searchParams.set(" + searchParameter + ",q);u.searchParams.set('limit','" + field.RemoteSearchMaxResults + "');try{const r=await fetch(u,{headers:{Accept:'application/json'},credentials:'same-origin',signal:a.signal});if(!r.ok)return;const rows=await r.json();d.replaceChildren();for(const row of Array.isArray(rows)?rows.slice(0," + field.RemoteSearchMaxResults + "):[]){const v=String(row[" + valueMember + "]??'');if(!v)continue;const l=String(row[" + labelMember + "]??v);const b=document.createElement('button');b.type='button';b.className='xpscript-autocomplete-option';b.textContent=l;b.addEventListener('click',()=>{i.value=l;h.value=v;d.replaceChildren();});d.appendChild(b);}}catch(x){if(x.name!=='AbortError')console.error('XPScript autocomplete search failed',x);}},250);});})();</script>";
    }

    private static string AddQuery(string url, string name, string value, int limit)
    {
        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return url + separator + Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(value) + "&limit=" + limit.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Js(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
""";
}
