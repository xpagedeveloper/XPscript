using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class CompilerSourceLineDirectivePostProcessor
{
    private static readonly Regex MarkerPattern = new(
        "(?m)^(?<indent>[ \\t]*)XPSourceLineRuntime\\.SetMapped\\((?<line>\\d+),\\s*\\\"(?<file>[^\\\"]+)\\\"\\);\\s*$",
        RegexOptions.CultureInvariant);

    private const string RuntimeBoundary = "internal static class LSControlRuntime";

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
            return string.Join(Environment.NewLine,
                indent + "// XPSOURCE|" + file + "|" + line,
                indent + "#line " + line + " \"" + file + "\"",
                indent + "XPSourceLineRuntime.Set(" + line + ");");
        });

        if (!foundMarker) return rewritten;

        var boundary = rewritten.IndexOf(RuntimeBoundary, StringComparison.Ordinal);
        if (boundary < 0)
            throw new CompilerException("Unable to restore generated source line mapping before runtime code.");

        return rewritten.Insert(boundary, "#line default" + Environment.NewLine);
    }
}
