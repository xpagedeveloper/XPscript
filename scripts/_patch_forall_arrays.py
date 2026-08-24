from pathlib import Path
p=Path('src/XPScript.Compiler/AdvancedXPScriptTranspiler.cs')
s=p.read_text()
s=s.replace('private sealed record ForAllContext(string Alias, string ElementType);','private sealed record ForAllContext(string Alias, string ElementType, bool IsListAlias);')
old='''        var forAll = Regex.Match(line, @"^ForAll\\s+([A-Za-z_]\\w*)\\s+In\\s+([A-Za-z_]\\w*)$", RegexOptions.IgnoreCase);
        if (forAll.Success)
        {
            var alias = forAll.Groups[1].Value;
            var list = ResolveList(forAll.Groups[2].Value);
            if (list is null) throw new CompilerException($"ForAll currently requires a declared list. '{forAll.Groups[2].Value}' is not a list.");
            Write(sb, $"foreach (var {alias} in {list.Value.Expression}.Aliases())");
            Write(sb, "{");
            _indent++;
            _forAll.Push(new ForAllContext(alias, list.Value.ElementType));
            return;
        }
'''
new='''        var forAll = Regex.Match(line, @"^ForAll\\s+([A-Za-z_]\\w*)\\s+In\\s+([A-Za-z_]\\w*)$", RegexOptions.IgnoreCase);
        if (forAll.Success)
        {
            var alias = forAll.Groups[1].Value;
            var sourceName = forAll.Groups[2].Value;
            var list = ResolveList(sourceName);
            if (list is not null)
            {
                Write(sb, $"foreach (var {alias} in {list.Value.Expression}.Aliases())");
                Write(sb, "{");
                _indent++;
                _forAll.Push(new ForAllContext(alias, list.Value.ElementType, true));
                return;
            }

            if (!_variableTypes.ContainsKey(sourceName) && !_objectVariables.ContainsKey(sourceName))
                throw new CompilerException($"ForAll source '{sourceName}' is not a declared list, array, or enumerable variable.");

            Write(sb, $"foreach (var {alias} in LSForAllRuntime.Enumerate({TransformExpression(sourceName)}))");
            Write(sb, "{");
            _indent++;
            _forAll.Push(new ForAllContext(alias, "Variant", false));
            return;
        }
'''
if old not in s: raise SystemExit('ForAll block not found')
s=s.replace(old,new,1)
old2='''        foreach (var alias in _forAll)
        {
            text = Regex.Replace(text, $@"\\b{Regex.Escape(alias.Alias)}\\b", $"{alias.Alias}.Value", RegexOptions.IgnoreCase);
            text = text.Replace($"__LSLISTTAG_{alias.Alias}.Value__", $"{alias.Alias}.Tag", StringComparison.OrdinalIgnoreCase);
            text = text.Replace($"__LSLISTTAG_{alias.Alias}__", $"{alias.Alias}.Tag", StringComparison.OrdinalIgnoreCase);
        }
'''
new2='''        foreach (var alias in _forAll)
        {
            if (alias.IsListAlias)
            {
                text = Regex.Replace(text, $@"\\b{Regex.Escape(alias.Alias)}\\b", $"{alias.Alias}.Value", RegexOptions.IgnoreCase);
                text = text.Replace($"__LSLISTTAG_{alias.Alias}.Value__", $"{alias.Alias}.Tag", StringComparison.OrdinalIgnoreCase);
                text = text.Replace($"__LSLISTTAG_{alias.Alias}__", $"{alias.Alias}.Tag", StringComparison.OrdinalIgnoreCase);
            }
        }
'''
if old2 not in s: raise SystemExit('ForAll alias block not found')
s=s.replace(old2,new2,1)
marker='''{{XPScriptListRuntimeSource.Code}}

{{XPScriptRuntimeSource.Code}}
'''
replacement='''{{XPScriptListRuntimeSource.Code}}

internal static class LSForAllRuntime
{
    public static System.Collections.IEnumerable Enumerate(object? value)
    {
        if (value is null) yield break;
        if (value is string) throw new XPScriptRuntimeException(13, "ForAll requires a list, array, or enumerable value.");
        if (value is LSArray array)
        {
            if (!array.IsAllocated) yield break;
            if (array.Rank != 1) throw new XPScriptRuntimeException(13, "ForAll currently supports one-dimensional arrays.");
            for (var i = array.LBound(); i <= array.UBound(); i++) yield return array.Get(new object?[] { i });
            yield break;
        }
        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable) yield return item;
            yield break;
        }
        throw new XPScriptRuntimeException(13, "ForAll requires a list, array, or enumerable value.");
    }
}

{{XPScriptRuntimeSource.Code}}
'''
if marker not in s: raise SystemExit('runtime insertion marker not found')
s=s.replace(marker,replacement,1)
p.write_text(s)
print('patched ForAll arrays')
