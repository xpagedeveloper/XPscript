using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIFormDirtyTrackingPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        if (!generated.Contains("private XPScriptJsonObject _dirtyBaseline = XPScriptNativeJson.CreateObject();", StringComparison.Ordinal))
        {
            generated = ReplaceRequired(
                generated,
                @"private\s+XPScriptJsonObject\s+_data\s*=\s*XPScriptNativeJson\.CreateObject\(\)\s*;",
                """
    private XPScriptJsonObject _data = XPScriptNativeJson.CreateObject();
    private XPScriptJsonObject _dirtyBaseline = XPScriptNativeJson.CreateObject();
""",
                "baseline-state");
        }

        if (!generated.Contains("public bool IsDirty =>", StringComparison.Ordinal))
        {
            generated = ReplaceRequired(
                generated,
                @"public\s+int\s+FieldCount\s*=>\s*_fields\.Count\s*;",
                """
    public int FieldCount => _fields.Count;
    public bool IsDirty => !System.Text.Json.Nodes.JsonNode.DeepEquals(_data.Node, _dirtyBaseline.Node);

    public XPScriptJsonArray DirtyFields
    {
        get
        {
            var result = XPScriptNativeJson.CreateArray();
            foreach (var uiField in _fields)
            {
                if (IsFieldDirty(uiField.Name)) result.Add(uiField.Name);
            }
            return result;
        }
    }

    public void MarkClean()
    {
        var document = XPScriptNativeJson.Parse(_data.Stringify());
        _dirtyBaseline = document.Root.AsObject()
            ?? throw new XPScriptRuntimeException(13, "UIForm dirty tracking requires object data.");
    }

    private bool IsFieldDirty(string fieldName)
    {
        var currentExists = _data.Contains(fieldName);
        var baselineExists = _dirtyBaseline.Contains(fieldName);
        if (currentExists != baselineExists) return true;
        if (!currentExists) return false;

        var current = XPScriptNativeJson.ToNode(_data.Get(fieldName));
        var baseline = XPScriptNativeJson.ToNode(_dirtyBaseline.Get(fieldName));
        return !System.Text.Json.Nodes.JsonNode.DeepEquals(current, baseline);
    }
""",
                "dirty-api");
        }

        if (!generated.Contains("_dirtyBaseline = document.Root.AsObject()", StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm dirty tracking API.");

        return ReplaceBindData(generated);
    }

    private static string ReplaceBindData(string generated)
    {
        const string marker = "public void BindData(object? value)";
        var start = generated.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0) throw new CompilerException("Unable to install UIForm dirty tracking baseline (BindData not found).");

        var brace = generated.IndexOf('{', start);
        if (brace < 0) throw new CompilerException("Unable to install UIForm dirty tracking baseline (BindData body not found).");

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = brace; i < generated.Length; i++)
        {
            var c = generated[i];
            if (escaped) { escaped = false; continue; }
            if (inString && c == '\\') { escaped = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    var body = generated[(brace + 1)..i];
                    if (!body.Contains("MarkClean();", StringComparison.Ordinal))
                        body = body.TrimEnd() + "\n        MarkClean();\n    ";
                    return generated[..(brace + 1)] + body + generated[i..];
                }
            }
        }

        throw new CompilerException("Unable to install UIForm dirty tracking baseline (unterminated BindData body).");
    }

    private static string ReplaceRequired(string source, string pattern, string replacement, string stage)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        if (!regex.IsMatch(source))
            throw new CompilerException($"Unable to install UIForm dirty tracking runtime extension ({stage}).");
        return regex.Replace(source, replacement, 1);
    }
}
