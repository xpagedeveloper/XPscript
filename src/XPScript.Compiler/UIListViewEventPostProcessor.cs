namespace XPScript.Compiler;

internal sealed class UIListViewEventPostProcessor
{
    private const string ListViewClassToken = "internal sealed class XPScriptUIListView";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        var classStart = generated.IndexOf(ListViewClassToken, StringComparison.Ordinal);
        if (classStart < 0)
            throw new CompilerException("Unable to install UIListView event runtime (class).");

        var prefix = generated[..classStart];
        var listSource = generated[classStart..];

        listSource = ReplaceRequired(listSource,
            "    private string _rowActionParameterName = string.Empty;\n",
            """
    private string _rowActionParameterName = string.Empty;
    private string _onSelectHandler = string.Empty;
    private string _onDoubleClickHandler = string.Empty;
""", "event-fields");

        listSource = ReplaceRequired(listSource,
            """
    public object? GetRow(object? index)
""",
            """
    public void SetOnSelect(object? handlerName)
        => _onSelectHandler = NormalizeHandlerName(handlerName);

    public void SetOnDoubleClick(object? handlerName)
        => _onDoubleClickHandler = NormalizeHandlerName(handlerName);

    internal string DispatchRegisteredEvent(string eventName, string rowIndexText)
    {
        int rowIndex;
        if (!int.TryParse(rowIndexText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out rowIndex))
            throw new XPScriptRuntimeException(13, "UIListView event row index must be an Integer value.");
        _selectedIndex = NormalizeRowIndex(rowIndex);

        var handlerName = eventName.Equals("select", StringComparison.OrdinalIgnoreCase)
            ? _onSelectHandler
            : eventName.Equals("doubleclick", StringComparison.OrdinalIgnoreCase)
                ? _onDoubleClickHandler
                : throw new XPScriptRuntimeException(5, "UIListView event type is unsupported.");

        if (handlerName.Length == 0) return string.Empty;
        InvokeRegisteredHandler(handlerName);
        return string.Empty;
    }

    private void InvokeRegisteredHandler(string handlerName)
    {
        var method = typeof(Script).GetMethod(
            handlerName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase)
            ?? throw new XPScriptRuntimeException(5, $"UIListView handler '{handlerName}' does not exist.");

        var parameters = method.GetParameters();
        if (parameters.Length > 1)
            throw new XPScriptRuntimeException(5, $"UIListView handler '{handlerName}' must accept zero parameters or the current UIListView as one parameter.");
        if (parameters.Length == 1 && parameters[0].ParameterType != typeof(object) && !parameters[0].ParameterType.IsAssignableFrom(typeof(XPScriptUIListView)))
            throw new XPScriptRuntimeException(5, $"UIListView handler '{handlerName}' parameter must accept the current UIListView.");

        try
        {
            method.Invoke(null, parameters.Length == 0 ? null : [this]);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new XPScriptRuntimeException(5, "UIListView handler failed: " + ex.InnerException.Message);
        }
    }

    public object? GetRow(object? index)
""", "event-api");

        if (!listSource.Contains("__xps_list_event", StringComparison.Ordinal))
        {
            listSource = ReplaceBetweenRequired(
                listSource,
                "if (XPScriptUIWebAdapter.IsAvailable)",
                "var hostType = Type.GetType(DesktopHostTypeName",
                """
if (XPScriptUIWebAdapter.IsAvailable)
        {
            if (XPScriptUIWebAdapter.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                var eventName = XPScriptUIWebAdapter.FormFirst("__xps_list_event");
                if (eventName.Length > 0)
                {
                    DispatchRegisteredEvent(eventName, XPScriptUIWebAdapter.FormFirst("__xps_list_index"));
                    XPScriptUIWebAdapter.WriteHtml("OK");
                    return "Handled";
                }
            }
            XPScriptUIWebAdapter.WriteHtml(RenderWebList());
            return "Pending";
        }

        var hostType = Type.GetType(DesktopHostTypeName
""", "web-dispatch");
        }

        listSource = ReplaceRequired(listSource,
            """
        var method = hostType.GetMethod("ShowDialog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript desktop UIListView bridge is incomplete.");
""",
            """
        var method = hostType.GetMethod(
                "ShowDialog",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                binder: null,
                types: [typeof(string), typeof(Func<string, string, string>)],
                modifiers: null)
            ?? throw new XPScriptRuntimeException(5, "XPScript desktop UIListView bridge is incomplete.");
""", "desktop-method");

        listSource = ReplaceRequired(listSource,
            """
            hasRowAction = _rowActionTarget.Length > 0,
            columns = visibleColumns.Select(column => new
""",
            """
            hasRowAction = _rowActionTarget.Length > 0,
            hasOnSelect = _onSelectHandler.Length > 0,
            hasOnDoubleClick = _onDoubleClickHandler.Length > 0,
            columns = visibleColumns.Select(column => new
""", "desktop-metadata");

        listSource = ReplaceRequired(listSource,
            """
            resultJson = Convert.ToString(method.Invoke(null, [System.Text.Json.JsonSerializer.Serialize(request)]), System.Globalization.CultureInfo.InvariantCulture)
                ?? string.Empty;
""",
            """
            var callback = new Func<string, string, string>((eventName, rowIndex) => DispatchRegisteredEvent(eventName, rowIndex));
            resultJson = Convert.ToString(method.Invoke(null, [System.Text.Json.JsonSerializer.Serialize(request), callback]), System.Globalization.CultureInfo.InvariantCulture)
                ?? string.Empty;
""", "desktop-invoke");

        listSource = ReplaceRequired(listSource,
            """
        sb.Append("const go=r=>{const h=r?.dataset.href;if(h)location.assign(h);};body.addEventListener('click',e=>go(e.target.closest('tr[data-href]')));body.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){const r=e.target.closest('tr[data-href]');if(r){e.preventDefault();go(r);}}});})();</script>");
""",
            """
        sb.Append("const go=r=>{const h=r?.dataset.href;if(h)location.assign(h);};");
        if (_onSelectHandler.Length > 0 || _onDoubleClickHandler.Length > 0)
            sb.Append("const postEvent=async(k,r)=>{const p=new URLSearchParams();p.set('__xps_list_event',k);p.set('__xps_list_index',r?.dataset.rowIndex||'');const x=await fetch(location.href,{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded;charset=UTF-8'},credentials:'same-origin',body:p.toString()});if(!x.ok)throw new Error('UIListView event failed');};");
        else
            sb.Append("const postEvent=async()=>{};");

        if (_onDoubleClickHandler.Length > 0)
        {
            sb.Append("body.addEventListener('click',async e=>{const r=e.target.closest('tr[data-row-index]');if(!r)return;");
            if (_onSelectHandler.Length > 0) sb.Append("await postEvent('select',r);");
            sb.Append("});body.addEventListener('dblclick',async e=>{const r=e.target.closest('tr[data-row-index]');if(!r)return;await postEvent('doubleclick',r);go(r);});");
        }
        else
        {
            sb.Append("body.addEventListener('click',async e=>{const r=e.target.closest('tr[data-row-index]');if(!r)return;");
            if (_onSelectHandler.Length > 0) sb.Append("await postEvent('select',r);");
            sb.Append("go(r);});");
        }
        sb.Append("body.addEventListener('keydown',async e=>{if(e.key==='Enter'||e.key===' '){const r=e.target.closest('tr[data-row-index]');if(r){e.preventDefault();");
        if (_onSelectHandler.Length > 0) sb.Append("await postEvent('select',r);");
        sb.Append("go(r);}}});})();</script>");
""", "web-events");

        listSource = ReplaceRequired(listSource,
            """
    private static string NormalizeTarget(object? value)
""",
            """
    private static string NormalizeHandlerName(object? value)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length is < 1 or > 128 || (!char.IsLetter(name[0]) && name[0] != '_') || name.Any(ch => !(char.IsLetterOrDigit(ch) || ch == '_')))
            throw new XPScriptRuntimeException(5, "UIListView handler name is invalid.");
        return name;
    }

    private static string NormalizeTarget(object? value)
""", "handler-normalizer");

        return prefix + listSource;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to install UIListView event runtime ({stage}).");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }

    private static string ReplaceBetweenRequired(string source, string startToken, string endToken, string replacement, string stage)
    {
        var start = source.IndexOf(startToken, StringComparison.Ordinal);
        if (start < 0)
            throw new CompilerException($"Unable to install UIListView event runtime ({stage}:start).");
        var end = source.IndexOf(endToken, start, StringComparison.Ordinal);
        if (end < 0)
            throw new CompilerException($"Unable to install UIListView event runtime ({stage}:end).");
        end += endToken.Length;

        var lineStart = source.LastIndexOf('\n', Math.Max(0, start - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var indentation = source[lineStart..start];
        var formatted = string.Join("\n", replacement.Split('\n').Select(line => indentation + line));
        return source[..lineStart] + formatted + source[end..];
    }
}
