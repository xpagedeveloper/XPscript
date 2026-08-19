namespace XPScript.Compiler;

internal sealed class UIListViewLiveUpdatePostProcessor
{
    private const string ClassStart = "internal sealed class XPScriptUIListView";
    private const string ClassEndAnchor = "    private static string HtmlAttribute(string value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        var (prefix, listView, suffix) = SplitListViewClass(generated);

        listView = ReplaceRequired(listView,
            """
        if (handlerName.Length == 0) return string.Empty;
        InvokeRegisteredHandler(handlerName);
        return string.Empty;
    }

    private void InvokeRegisteredHandler(string handlerName)
""",
            """
        if (handlerName.Length > 0)
            InvokeRegisteredHandler(handlerName);
        return SerializeLiveState();
    }

    private string SerializeLiveState()
    {
        var visibleColumns = _columns.Where(column => column.Visible).ToArray();
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            selectedIndex = _selectedIndex,
            sortable = _sortable,
            filterEnabled = _filterEnabled,
            columns = visibleColumns.Select(column => new
            {
                name = column.Name,
                label = column.Label,
                width = column.Width
            }).ToArray(),
            rows = Enumerable.Range(0, _data.Count).Select(index => new
            {
                index,
                href = BuildRowHref(index),
                values = visibleColumns.Select(column => GetRowValueString(index, column.Name)).ToArray()
            }).ToArray()
        });
    }

    private void InvokeRegisteredHandler(string handlerName)
""", "event-state");

        listView = ReplaceRequired(listView,
            """
                    DispatchRegisteredEvent(eventName, XPScriptUIWebAdapter.FormFirst("__xps_list_index"));
                    XPScriptUIWebAdapter.WriteHtml("OK");
                    return "Handled";
""",
            """
                    var stateJson = DispatchRegisteredEvent(eventName, XPScriptUIWebAdapter.FormFirst("__xps_list_index"));
                    XPScriptUIWebAdapter.WriteHtml(stateJson);
                    return "Handled";
""", "web-response");

        listView = ReplaceRequired(listView,
            """
        if (_filterEnabled)
            sb.Append("<label>Filter <input type=\"search\" class=\"xps-list-filter\" autocomplete=\"off\"></label>");
""",
            """
        sb.Append("<label class=\"xps-list-filter-wrap\"");
        if (!_filterEnabled) sb.Append(" hidden");
        sb.Append(">Filter <input type=\"search\" class=\"xps-list-filter\" autocomplete=\"off\"></label>");
""", "filter-wrapper");

        listView = ReplaceRequired(listView,
            """
            sb.Append("const postEvent=async(k,r)=>{const p=new URLSearchParams();p.set('__xps_list_event',k);p.set('__xps_list_index',r?.dataset.rowIndex||'');const x=await fetch(location.href,{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded;charset=UTF-8'},credentials:'same-origin',body:p.toString()});if(!x.ok)throw new Error('UIListView event failed');};");
""",
            """
            sb.Append("const bindSort=()=>root.querySelectorAll('.xps-list-sort').forEach(b=>{if(b.dataset.bound==='1')return;b.dataset.bound='1';b.addEventListener('click',()=>{const c=Number(b.dataset.column);const asc=b.dataset.order!=='asc';root.querySelectorAll('.xps-list-sort').forEach(x=>delete x.dataset.order);b.dataset.order=asc?'asc':'desc';const rows=[...body.rows];rows.sort((a,z)=>{const x=valueOf(a.cells[c]?.textContent||'');const y=valueOf(z.cells[c]?.textContent||'');let r=x[0]-y[0];if(!r)r=x[1]<y[1]?-1:x[1]>y[1]?1:0;return asc?r:-r;});rows.forEach(r=>body.appendChild(r));});});const applyState=s=>{if(!s)return;const cols=Array.isArray(s.columns)?s.columns:[];const rows=Array.isArray(s.rows)?s.rows:[];const fw=root.querySelector('.xps-list-filter-wrap');if(fw)fw.hidden=!s.filterEnabled;const hr=root.querySelector('thead tr');if(hr){hr.innerHTML='';cols.forEach((c,i)=>{const th=document.createElement('th');if(Number(c.width)>0)th.style.width=Number(c.width)+'px';if(s.sortable){const b=document.createElement('button');b.type='button';b.className='xps-list-sort';b.dataset.column=String(i);b.textContent=c.label??c.name??'';th.appendChild(b);}else th.textContent=c.label??c.name??'';hr.appendChild(th);});}body.innerHTML='';rows.forEach(r=>{const tr=document.createElement('tr');tr.dataset.rowIndex=String(r.index);if(r.href){tr.dataset.href=r.href;tr.tabIndex=0;tr.setAttribute('role','link');}(Array.isArray(r.values)?r.values:[]).forEach(v=>{const td=document.createElement('td');td.textContent=v??'';tr.appendChild(td);});body.appendChild(tr);});bindSort();};const postEvent=async(k,r)=>{const p=new URLSearchParams();p.set('__xps_list_event',k);p.set('__xps_list_index',r?.dataset.rowIndex||'');const x=await fetch(location.href,{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded;charset=UTF-8'},credentials:'same-origin',body:p.toString()});if(!x.ok)throw new Error('UIListView event failed');const text=await x.text();if(text){const state=JSON.parse(text);applyState(state);}return true;};");
""", "web-live-state");

        listView = ReplaceRequired(listView,
            """
            sb.Append("root.querySelectorAll('.xps-list-sort').forEach(b=>b.addEventListener('click',()=>{const c=Number(b.dataset.column);const asc=b.dataset.order!=='asc';root.querySelectorAll('.xps-list-sort').forEach(x=>delete x.dataset.order);b.dataset.order=asc?'asc':'desc';const rows=[...body.rows];rows.sort((a,z)=>{const x=valueOf(a.cells[c]?.textContent||'');const y=valueOf(z.cells[c]?.textContent||'');let r=x[0]-y[0];if(!r)r=x[1]<y[1]?-1:x[1]>y[1]?1:0;return asc?r:-r;});rows.forEach(r=>body.appendChild(r));}));");
""",
            """
            sb.Append("bindSort();");
""", "sort-rebind");

        return prefix + listView + suffix;
    }

    private static (string Prefix, string ListView, string Suffix) SplitListViewClass(string source)
    {
        var start = source.IndexOf(ClassStart, StringComparison.Ordinal);
        if (start < 0)
            throw new CompilerException("Unable to install UIListView live update runtime (class:start).");

        var anchor = source.IndexOf(ClassEndAnchor, start, StringComparison.Ordinal);
        if (anchor < 0)
            throw new CompilerException("Unable to install UIListView live update runtime (class:end-anchor).");

        var close = source.IndexOf("\n}", anchor + ClassEndAnchor.Length, StringComparison.Ordinal);
        if (close < 0)
            throw new CompilerException("Unable to install UIListView live update runtime (class:end).");
        close += 2;

        return (source[..start], source[start..close], source[close..]);
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to install UIListView live update runtime ({stage}).");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
