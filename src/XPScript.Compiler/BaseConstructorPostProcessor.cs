using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class BaseConstructorPostProcessor
{
    private static readonly Regex ExplicitBaseConstructor = new(
        @"(?m)^(?<indent>[ \t]*)public\s+(?<name>[A-Za-z_]\w*)\((?<parameters>[^\r\n]*)\)\s*\r?\n" +
        @"(?<open>[ \t]*\{\s*\r?\n)" +
        @"(?<marker>[ \t]*XPSourceLineRuntime\.__XPSOURCE_\d+_[0-9A-F]+\(\);\s*\r?\n)?" +
        @"(?<bodyIndent>[ \t]*)base\.New\((?<arguments>[^\r\n]*)\);\s*\r?\n",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        return ExplicitBaseConstructor.Replace(
            generated,
            m =>
                $"{m.Groups["indent"].Value}public {m.Groups["name"].Value}({m.Groups["parameters"].Value}) : base({m.Groups["arguments"].Value}){Environment.NewLine}" +
                m.Groups["open"].Value +
                m.Groups["marker"].Value,
            1);
    }
}
