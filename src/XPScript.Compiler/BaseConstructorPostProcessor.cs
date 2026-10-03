using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class BaseConstructorPostProcessor
{
    private static readonly Regex ExplicitBaseConstructor = new(
        @"(?m)^(?<indent>[ 	]*)publics+(?<name>[A-Za-z_]w*)((?<parameters>[^
]*))s*?
" +
        @"(?<open>[ 	]*{s*?
)" +
        @"(?<bodyIndent>[ 	]*)base.New((?<arguments>[^
]*));s*?
",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        return ExplicitBaseConstructor.Replace(
            generated,
            m =>
                $"{m.Groups["indent"].Value}public {m.Groups["name"].Value}({m.Groups["parameters"].Value}) : base({m.Groups["arguments"].Value}){Environment.NewLine}" +
                m.Groups["open"].Value,
            1);
    }
}
