namespace XPScript.Compiler;

internal sealed class UIListViewEventPostProcessor
{
    private const string ListViewClassToken = "internal sealed class XPScriptUIListView";
    private const string DesktopCallbackSentinel = "types: [typeof(string), typeof(Func<string, string, string>)]";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        var classStart = generated.IndexOf(ListViewClassToken, StringComparison.Ordinal);
        if (classStart < 0)
            throw new CompilerException("Unable to install UIListView event runtime (class).");

        var prefix = generated[..classStart];
        var listSource = generated[classStart..];

        if (!prefix.Contains("internal sealed class XPScriptUIListViewEvent", StringComparison.Ordinal))
        {
            prefix += """
internal sealed class XPScriptUIListViewEvent
{
    internal XPScriptUIListViewEvent(XPScriptUIListView listView, string eventType, int rowIndex, object? row, string key)
    {
        ListView = listView;
        EventType = eventType;
        RowIndex = rowIndex;
        Row = row;
        Key = key;
    }

    public XPScriptUIListView ListView { get; }
    public string EventType { get; }
    public int RowIndex { get; }
    public object? Row { get; }
    public string Key { get; }
}

""";
        }

        listSource = ReplaceRequired(listSource,
            "    private string _rowActionTarget = string.Empty;\n",
            """
    private string _rowActionTarget = string.Empty;
    private string _onSelectHandler = string.Empty;
    private string _onDoubleClickHandler = string.Empty;
    private object?[] _onSelectCallbackArguments = [];
    private object?[] _onDoubleClickCallbackArguments = [];
    private bool _onSelectUsesEventCallback;
    private bool _onDoubleClickUsesEventCallback;
""", "event-fields");

        listSource = ReplaceRequired(listSource,
            """
    public object? GetRow(object? index)
""",
            """
    public void SetOnSelect(object? handlerName)
    {
        _onSelectHandler = NormalizeHandlerName(handlerName);
        _onSelectCallbackArguments = [];
        _onSelectUsesEventCallback = false;
    }

    public void SetOnDoubleClick(object? handlerName)
    {
        _onDoubleClickHandler = NormalizeHandlerName(handlerName);
        _onDoubleClickCallbackArguments = [];
        _onDoubleClickUsesEventCallback = false;
    }

    public void SetOnSelectCallback(object? handlerName, params object?[] callbackArguments)
    {
        _onSelectHandler = NormalizeHandlerName(handlerName);
        _onSelectCallbackArguments = CopyCallbackArguments(callbackArguments);
        _onSelectUsesEventCallback = true;
    }

    public void SetOnDoubleClickCallback(object? handlerName, params object?[] callbackArguments)
    {
        _onDoubleClickHandler = NormalizeHandlerName(handlerName);
        _onDoubleClickCallbackArguments = CopyCallbackArguments(callbackArguments);
        _onDoubleClickUsesEventCallback = true;
    }

    internal string DispatchRegisteredEvent(string eventName, string rowIndexText)
    {
        int rowIndex;
        if (!int.TryParse(rowIndexText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out rowIndex))
            throw new XPScriptRuntimeException(13, "UIListView event row index must be an Integer value.");
        _selectedIndex = NormalizeRowIndex(rowIndex);

        string handlerName;
        object?[] callbackArguments;
        bool usesEventCallback;
        string normalizedEventType;
        if (eventName.Equals("select", StringComparison.OrdinalIgnoreCase))
        {
            handlerName = _onSelectHandler;
            callbackArguments = _onSelectCallbackArguments;
            usesEventCallback = _onSelectUsesEventCallback;
            normalizedEventType = "select";
        }
        else if (eventName.Equals("doubleclick", StringComparison.OrdinalIgnoreCase))
        {
            handlerName = _onDoubleClickHandler;
            callbackArguments = _onDoubleClickCallbackArguments;
            usesEventCallback = _onDoubleClickUsesEventCallback;
            normalizedEventType = "doubleclick";
        }
        else
        {
            throw new XPScriptRuntimeException(5, "UIListView event type is unsupported.");
        }

        if (handlerName.Length == 0) return string.Empty;
        if (usesEventCallback)
        {
            var evt = new XPScriptUIListViewEvent(this, normalizedEventType, rowIndex, GetRow(rowIndex), GetSelectedKey());
            XPScriptCallbackRuntime.Invoke(
                handlerName,
                "UIListView event",
                XPScriptCallbackRuntime.Prepend(evt, callbackArguments));
        }
        else
        {
            InvokeRegisteredHandler(handlerName);
        }
        return string.Empty;
    }

    private void InvokeRegisteredHandler(string handlerName)
    {
        var methods = typeof(Script)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(method => method.Name.Equals(handlerName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (methods.Any(method => method.GetParameters().Length == 0))
        {
            XPScriptCallbackRuntime.Invoke(handlerName, "UIListView event");
            return;
        }

        if (methods.Any(method => method.GetParameters().Length == 1))
        {
            XPScriptCallbackRuntime.Invoke(handlerName, "UIListView event", this);
            return;
        }

        throw new XPScriptRuntimeException(5, $"UIListView handler '{handlerName}' must accept zero parameters or the current UIListView as one parameter.");
    }

    private static object?[] CopyCallbackArguments(object?[]? callbackArguments)
    {
        callbackArguments ??= [];
        if (callbackArguments.Length > 63)
            throw new XPScriptRuntimeException(5, "UIListView callback context exceeds the 63-argument limit.");
        return callbackArguments.ToArray();
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

        if (!listSource.Contains(DesktopCallbackSentinel, StringComparison.Ordinal))
        {
            listSource = ReplaceBetweenRequired(
                listSource,
                "var method = hostType.GetMethod(",
                "?? throw new XPScriptRuntimeException(5, \"XPScript desktop UIListView bridge is incomplete.\");",
                """
var method = hostType.GetMethod(
                "ShowDialog",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                binder: null,
                types: [typeof(string), typeof(Func<string, string, string>)],
                modifiers: null)
            ?? throw new XPScriptRuntimeException(5, "XPScript desktop UIListView bridge is incomplete.");
""", "desktop-method");
        }

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
        if (_onSelectHandler.Length > 0 || _onDoubleClickHandler.Length > 0 || _rowActionTarget.Length > 0)
            sb.Append("const postEvent=async(k,r)=>{const p=new URLSearchParams();p.set('__xps_list_event',k);p.set('__xps_list_index',r?.dataset.rowIndex||'');const x=await fetch(location.href,{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded;charset=UTF-8'},credentials:'same-origin',body:p.toString()});if(!x.ok)throw new Error('UIListView event failed');};");
        else
            sb.Append("const postEvent=async()=>{};");

        if (_onDoubleClickHandler.Length > 0)
        {
            sb.Append("body.addEventListener('click',async e=>{const r=e.target.closest('tr[data-row-index]');if(!r)return;");
            if (_onSelectHandler.Length > 0 || _rowActionTarget.Length > 0) sb.Append("await postEvent('select',r);");
            sb.Append("});body.addEventListener('dblclick',async e=>{const r=e.target.closest('tr[data-row-index]');if(!r)return;await postEvent('doubleclick',r);go(r);});");
        }
        else
        {
            sb.Append("body.addEventListener('click',async e=>{const r=e.target.closest('tr[data-row-index]');if(!r)return;");
            if (_onSelectHandler.Length > 0 || _rowActionTarget.Length > 0) sb.Append("await postEvent('select',r);");
            sb.Append("go(r);});");
        }
        sb.Append("body.addEventListener('keydown',async e=>{if(e.key==='Enter'||e.key===' '){const r=e.target.closest('tr[data-row-index]');if(r){e.preventDefault();");
        if (_onSelectHandler.Length > 0 || _rowActionTarget.Length > 0) sb.Append("await postEvent('select',r);");
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
