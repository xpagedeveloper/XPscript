using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class CompilerSourceLineDirectivePostProcessor
{
    private static readonly Regex MarkerPattern = new(
        @"(?m)^(?<indent>[ \t]*)XPSourceLineRuntime\.Set\((?<line>\d+),\s*\"(?<file>(?:\\.|[^\"])*)\"\);\s*$",
        RegexOptions.CultureInvariant);

    public string Transform(string generated)
    {
        if (string.IsNullOrEmpty(generated)) return generated;

        var foundMarker = false;
        var rewritten = MarkerPattern.Replace(generated, match =>
        {
            foundMarker = true;
            var indent = match.Groups["indent"].Value;
            var line = match.Groups["line"].Value;
            var file = match.Groups["file"].Value;
            return $"{indent}#line {line} \"{file}\"{Environment.NewLine}{match.Value}";
        });

        if (!foundMarker) return rewritten;
        return rewritten + Environment.NewLine + "#line default" + Environment.NewLine;
    }
}
