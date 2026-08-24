using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class AttachmentCollectionPreprocessor
{
    private static readonly string[] Methods =
    [
        "List", "GetMetadata", "FindByName", "Save", "SaveAs", "Get", "SaveToDisk", "GetAll", "SendToBrowser", "Delete"
    ];

    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length);
        var attachmentVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();
            var rewritten = line;

            foreach (var variable in attachmentVariables.OrderByDescending(x => x.Length))
            {
                var escaped = Regex.Escape(variable);
                foreach (var method in Methods)
                {
                    rewritten = Regex.Replace(
                        rewritten,
                        $@"\b{escaped}\.{method}\s*\(",
                        $"XPScriptDatabaseAttachmentApi.{method}({variable}, ",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }

                rewritten = rewritten
                    .Replace($"XPScriptDatabaseAttachmentApi.List({variable}, )", $"XPScriptDatabaseAttachmentApi.List({variable})", StringComparison.OrdinalIgnoreCase)
                    .Replace($"XPScriptDatabaseAttachmentApi.GetMetadata({variable}, )", $"XPScriptDatabaseAttachmentApi.GetMetadata({variable})", StringComparison.OrdinalIgnoreCase);
            }

            var assignment = Regex.Match(
                rewritten,
                @"^(?:Set\s+)?([A-Za-z_]\w*)\s*=\s*(.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (assignment.Success)
            {
                var rhs = assignment.Groups[2].Value;
                if (rhs.Contains("XPScriptDatabaseAttachmentRuntime.For", StringComparison.Ordinal) ||
                    rhs.Contains("XPScriptDatabaseAttachmentApi.For", StringComparison.Ordinal))
                {
                    attachmentVariables.Add(assignment.Groups[1].Value);
                }
            }

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }
}
