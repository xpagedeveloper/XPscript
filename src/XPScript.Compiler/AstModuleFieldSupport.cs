using System.Text.RegularExpressions;
using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler;

/// <summary>One storage description shared by module symbol registration and emission.</summary>
internal static class AstModuleFieldSupport
{
    internal sealed record Field(VariableSymbol Symbol)
    {
        internal string Declaration => Symbol.HasTypedStorage
            ? $"    public static {BoundMethodEmitter.CSharpType(Symbol.Type)} {Symbol.Name} = {(Symbol.Type == typeof(string) ? "string.Empty" : "default")};"
            : $"    public static dynamic {Symbol.Name} = null;";
    }

    internal static IReadOnlyList<Field> Find(string source)
    {
        // Do not mistake fields or declaration-like text inside procedure bodies for globals.
        var text = source.ToCharArray();
        var unit = new DeclarationParser(source).ParseCompilationUnit();
        foreach (var declaration in unit.Declarations.Where(d => d is ClassDeclarationSyntax or SubDeclarationSyntax or FunctionDeclarationSyntax))
            for (var i = declaration.Span.Start; i < declaration.Span.End && i < text.Length; i++)
                if (text[i] is not ('\r' or '\n')) text[i] = ' ';

        return Regex.Matches(new string(text), @"^[ \t]*(?:Private|Public)\s+(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)(?<tail>[^\r\n]*)", RegexOptions.IgnoreCase | RegexOptions.Multiline)
            .Cast<Match>().Select(match =>
            {
                var type = AstExperimentalCompiler.ResolveRuntimeType(match.Groups["type"].Value);
                var tail = match.Groups["tail"].Value.Trim();
                // Initialized declarations and reference types retain the compatibility path.
                var typed = type != typeof(object) && (tail.Length == 0 || tail.StartsWith("'", StringComparison.Ordinal));
                return new Field(new VariableSymbol(match.Groups["name"].Value, type,
                    typed ? XpTypeSymbol.FromClr(type) : XpTypeSymbol.Variant) { HasTypedStorage = typed });
            }).ToArray();
    }
}
