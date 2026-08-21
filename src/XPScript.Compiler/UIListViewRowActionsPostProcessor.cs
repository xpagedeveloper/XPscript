namespace XPScript.Compiler;

internal sealed class UIListViewRowActionsPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        generated = ReplaceRequired(generated,
            "internal sealed class XPScriptUIListView\n{\n",
            """
internal sealed class XPScriptUIListRowAction
{
    public required string Name { get; init; }
    public required string Label { get; set; }
    public required string Kind { get; init; }
    public string Handler { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
}

internal sealed class XPScriptUIListView
{
""");

        generated = ReplaceRequired(generated,
            "    private string _onDoubleClickHandler = string.Empty;\n",
            """
    private string _onDoubleClickHandler = string.Empty;
    private readonly List<XPScriptUIListRowAction> _rowActions = [];
""");

        generated = ReplaceRequired(generated,
            """
    public void SetOnSelect(object? handlerName)
""",
            """
    public void AddRowButton(object? name, object? label, object? handlerName)
    {
        var actionName = NormalizeName(name, "row action");
        EnsureUniqueRowAction(actionName);
        _rowActions.Add(new XPScriptUIListRowAction
        {
            Name = actionName,
            Label = XPScriptRuntime.CStr(label),
            Kind = "Handler",
            Handler = NormalizeHandlerName(handlerName)
        });
    }

    public void AddRowNavigationButton(object? name, object? label, object? targetScript)
    {
        var actionName = NormalizeName(name, "row action");
        EnsureUniqueRowAction(actionName);
        _rowActions.Add(new XPScriptUIListRowAction
        {
            Name = actionName,
            Label = XPScriptRuntime.CStr(label),
            Kind = "Navigate",
            Target = NormalizeTarget(targetScript)
        });
    }

    public void ClearRowActions() => _rowActions.Clear();

    private void EnsureUniqueRowAction(string name)
    {
        if (_rowActions.Any(action => action.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new XPScriptRuntimeException(5, $"UIListView row action '{name}' already exists.");
    }

    public void SetOnSelect(object? handlerName)
""");

        generated = ReplaceRequired(generated,
            """
        var handlerName = eventName.Equals("select", StringComparison.OrdinalIgnoreCase)
            ? _onSelectHandler
            : eventName.Equals("doubleclick", StringComparison.OrdinalIgnoreCase)
                ? _onDoubleClickHandler
                : throw new XPScriptRuntimeException(5, "UIListView event type is unsupported.");

        if (handlerName.Length > 0)
            InvokeRegisteredHandler(handlerName);
        return SerializeLiveState();
""",
            """
        string handlerName;
        if (eventName.Equals("select", StringComparison.OrdinalIgnoreCase))
            handlerName = _onSelectHandler;
        else if (eventName.Equals("doubleclick", StringComparison.OrdinalIgnoreCase))
            handlerName = _onDoubleClickHandler;
        else if (eventName.StartsWith("action:", StringComparison.OrdinalIgnoreCase))
        {
            var actionName = eventName[7..];
            var action = _rowActions.FirstOrDefault(candidate => candidate.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase))
                ?? throw new XPScriptRuntimeException(5, $"UIListView row action '{actionName}' is not registered.");
            if (!action.Kind.Equals("Handler", StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(5, $"UIListView row action '{actionName}' is not a handler action.");
            handlerName = action.Handler;
        }
        else
            throw new XPScriptRuntimeException(5, "UIListView event type is unsupported.");

        if (handlerName.Length > 0)
            InvokeRegisteredHandler(handlerName);
        return SerializeLiveState();
""");

        generated = ReplaceRequired(generated,
            """
            hasOnDoubleClick = _onDoubleClickHandler.Length > 0,
            columns = visibleColumns.Select(column => new
""",
            """
            hasOnDoubleClick = _onDoubleClickHandler.Length > 0,
            rowActions = _rowActions.Select(action => new
            {
                name = action.Name,
                label = action.Label,
                kind = action.Kind
            }).ToArray(),
            columns = visibleColumns.Select(column => new
""");

        generated = ReplaceRequired(generated,
            """
            rows = Enumerable.Range(0, _data.Count).Select(index => new
            {
                index,
                href = BuildRowHref(index),
                values = visibleColumns.Select(column => GetRowValueString(index, column.Name)).ToArray()
            }).ToArray()
""",
            """
            rows = Enumerable.Range(0, _data.Count).Select(index => new
            {
                index,
                href = BuildRowHref(index),
                values = visibleColumns.Select(column => GetRowValueString(index, column.Name)).ToArray(),
                actions = _rowActions.Select(action => new
                {
                    name = action.Name,
                    label = action.Label,
                    kind = action.Kind,
                    href = action.Kind.Equals("Navigate", StringComparison.OrdinalIgnoreCase)
                        ? BuildActionHref(action, index)
                        : string.Empty
                }).ToArray()
            }).ToArray()
""");

        generated = ReplaceRequired(generated,
            """
        sb.Append("</tr></thead><tbody>");
""",
            """
        if (_rowActions.Count > 0) sb.Append("<th class=\"xps-list-actions-header\">Actions</th>");
        sb.Append("</tr></thead><tbody>");
""");

        generated = ReplaceRequired(generated,
            """
            foreach (var column in visibleColumns)
                sb.Append("<td>").Append(Html(GetRowValueString(rowIndex, column.Name))).Append("</td>");
            sb.Append("</tr>");
""",
            """
            foreach (var column in visibleColumns)
                sb.Append("<td>").Append(Html(GetRowValueString(rowIndex, column.Name))).Append("</td>");
            if (_rowActions.Count > 0)
            {
                sb.Append("<td class=\"xps-list-actions\">");
                foreach (var action in _rowActions)
                {
                    if (action.Kind.Equals("Navigate", StringComparison.OrdinalIgnoreCase))
                        sb.Append("<a class=\"xps-list-action\" data-action=\"").Append(HtmlAttribute(action.Name)).Append("\" href=\"").Append(HtmlAttribute(BuildActionHref(action, rowIndex))).Append("\">").Append(Html(action.Label)).Append("</a>");
                    else
                        sb.Append("<button type=\"button\" class=\"xps-list-action\" data-action=\"").Append(HtmlAttribute(action.Name)).Append("\">").Append(Html(action.Label)).Append("</button>");
                }
                sb.Append("</td>");
            }
            sb.Append("</tr>");
""");

        generated = ReplaceRequired(generated,
            """
        sb.Append("const go=r=>{const h=r?.dataset.href;if(h)location.assign(h);};");
""",
            """
        sb.Append("const go=r=>{const h=r?.dataset.href;if(h)location.assign(h);};const actionClick=async e=>{const a=e.target.closest('.xps-list-action');if(!a)return false;e.stopPropagation();const r=a.closest('tr[data-row-index]');if(!r)return true;if(a.tagName==='A')return true;e.preventDefault();await postEvent('action:'+String(a.dataset.action||''),r);return true;};body.addEventListener('click',actionClick);");
""");

        generated = ReplaceRequired(generated,
            """
const hr=root.querySelector('thead tr');if(hr){hr.innerHTML='';cols.forEach((c,i)=>{const th=document.createElement('th');if(Number(c.width)>0)th.style.width=Number(c.width)+'px';if(s.sortable){const b=document.createElement('button');b.type='button';b.className='xps-list-sort';b.dataset.column=String(i);b.textContent=c.label??c.name??'';th.appendChild(b);}else th.textContent=c.label??c.name??'';hr.appendChild(th);});}body.innerHTML='';rows.forEach(r=>{const tr=document.createElement('tr');tr.dataset.rowIndex=String(r.index);if(r.href){tr.dataset.href=r.href;tr.tabIndex=0;tr.setAttribute('role','link');}(Array.isArray(r.values)?r.values:[]).forEach(v=>{const td=document.createElement('td');td.textContent=v??'';tr.appendChild(td);});body.appendChild(tr);});
""",
            """
const hr=root.querySelector('thead tr');if(hr){hr.innerHTML='';cols.forEach((c,i)=>{const th=document.createElement('th');if(Number(c.width)>0)th.style.width=Number(c.width)+'px';if(s.sortable){const b=document.createElement('button');b.type='button';b.className='xps-list-sort';b.dataset.column=String(i);b.textContent=c.label??c.name??'';th.appendChild(b);}else th.textContent=c.label??c.name??'';hr.appendChild(th);});if(rows.some(r=>Array.isArray(r.actions)&&r.actions.length)){const th=document.createElement('th');th.className='xps-list-actions-header';th.textContent='Actions';hr.appendChild(th);}}body.innerHTML='';rows.forEach(r=>{const tr=document.createElement('tr');tr.dataset.rowIndex=String(r.index);if(r.href){tr.dataset.href=r.href;tr.tabIndex=0;tr.setAttribute('role','link');}(Array.isArray(r.values)?r.values:[]).forEach(v=>{const td=document.createElement('td');td.textContent=v??'';tr.appendChild(td);});if(Array.isArray(r.actions)&&r.actions.length){const td=document.createElement('td');td.className='xps-list-actions';r.actions.forEach(a=>{let el;if(String(a.kind).toLowerCase()==='navigate'){el=document.createElement('a');el.href=a.href||'#';}else{el=document.createElement('button');el.type='button';}el.className='xps-list-action';el.dataset.action=a.name||'';el.textContent=a.label||a.name||'';td.appendChild(el);});tr.appendChild(td);}body.appendChild(tr);});
""");

        generated = ReplaceRequired(generated,
            """
    private string BuildRowHref(int rowIndex)
""",
            """
    private string BuildActionHref(XPScriptUIListRowAction action, int rowIndex)
    {
        _ = rowIndex;
        return action.Kind.Equals("Navigate", StringComparison.OrdinalIgnoreCase) ? action.Target : string.Empty;
    }

    internal bool TryWriteDesktopRowActionNavigation(string actionName)
    {
        var action = _rowActions.FirstOrDefault(candidate => candidate.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase));
        if (action is null || !action.Kind.Equals("Navigate", StringComparison.OrdinalIgnoreCase) || _selectedIndex < 0)
            return false;
        var navigationFile = Environment.GetEnvironmentVariable("XPSCRIPT_NAVIGATION_FILE");
        if (string.IsNullOrWhiteSpace(navigationFile)) return false;
        File.WriteAllText(navigationFile, System.Text.Json.JsonSerializer.Serialize(new
        {
            target = action.Target
        }));
        return true;
    }

    private string BuildRowHref(int rowIndex)
""");

        generated = ReplaceRequired(generated,
            """
        if (result.Equals("Open", StringComparison.OrdinalIgnoreCase))
""",
            """
        if (result.StartsWith("RowAction:", StringComparison.OrdinalIgnoreCase))
        {
            var actionName = result[10..];
            if (TryWriteDesktopRowActionNavigation(actionName)) return "Navigate";
            return "Cancel";
        }

        if (result.Equals("Open", StringComparison.OrdinalIgnoreCase))
""");

        if (generated.Contains("ParameterName", StringComparison.Ordinal) ||
            generated.Contains("parameterName", StringComparison.Ordinal) ||
            generated.Contains("parameterValue", StringComparison.Ordinal))
            throw new CompilerException("UIListView row navigation parameter cleanup was incomplete.");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIListView row action runtime.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
