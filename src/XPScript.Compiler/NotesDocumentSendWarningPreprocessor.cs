using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NotesDocumentSendWarningPreprocessor
{
    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.Contains("NotesDocument", StringComparison.OrdinalIgnoreCase) ||
            !source.Contains(".Send", StringComparison.OrdinalIgnoreCase))
            return source;

        var documentVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var declaration = Regex.Match(
                line,
                @"^Dim\s+([A-Za-z_]\w*)\s+As\s+(?:New\s+)?NotesDocument\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (declaration.Success)
                documentVariables.Add(declaration.Groups[1].Value);

            foreach (var variable in documentVariables)
            {
                if (!Regex.IsMatch(
                        line,
                        $@"\b{Regex.Escape(variable)}\.Send\s*\(\s*True\b",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    continue;

                Console.Error.WriteLine(
                    "warning: NotesDocument.Send attachForm=True is not supported. " +
                    "The document will not be sent with an attached form; use Send(False) or omit attachForm.");
                return source;
            }
        }

        return source;
    }
}
