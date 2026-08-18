using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIExtensionPreprocessor
{
    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 8);
        var uiVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNewForm = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+UIForm\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNewForm.Success)
            {
                var name = dimNewForm.Groups[1].Value;
                uiVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                output.Add(indent + $"{name} = {CreateFormExpression(dimNewForm.Groups[2].Value)}");
                continue;
            }

            var dimNewList = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+UIListView\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNewList.Success)
            {
                var name = dimNewList.Groups[1].Value;
                uiVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                output.Add(indent + $"{name} = {CreateListViewExpression(dimNewList.Groups[2].Value)}");
                continue;
            }

            var dim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+(UIForm|UIListView)\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                uiVariables.Add(dim.Groups[1].Value);
                output.Add(indent + $"Dim {dim.Groups[1].Value} As Variant");
                continue;
            }

            var rewritten = Regex.Replace(line, @"\bNew\s+UIForm\s*(?:\(([^)]*)\))?", m => CreateFormExpression(m.Groups[1].Value), RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+UIListView\s*(?:\(([^)]*)\))?", m => CreateListViewExpression(m.Groups[1].Value), RegexOptions.IgnoreCase);
            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (uiVariables.Contains(set.Groups[1].Value) ||
                set.Groups[2].Value.Contains("XPScriptUI.CreateForm", StringComparison.Ordinal) ||
                set.Groups[2].Value.Contains("XPScriptUIList.CreateListView", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static string CreateFormExpression(string rawArguments)
    {
        var args = rawArguments.Trim();
        return string.IsNullOrWhiteSpace(args)
            ? "XPScriptUI.CreateForm()"
            : $"XPScriptUI.CreateForm({args})";
    }

    private static string CreateListViewExpression(string rawArguments)
    {
        var args = rawArguments.Trim();
        return string.IsNullOrWhiteSpace(args)
            ? "XPScriptUIList.CreateListView()"
            : $"XPScriptUIList.CreateListView({args})";
    }
}
