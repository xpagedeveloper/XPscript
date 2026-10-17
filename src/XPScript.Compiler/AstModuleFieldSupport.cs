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
            ? $"    public static {BoundMethodEmitter.CSharpType(Symbol.SemanticType ?? XpTypeSymbol.FromClr(Symbol.Type))} {Symbol.Name} = {Symbol.TypedInitializerLiteral ?? (Symbol.Type == typeof(string) ? "string.Empty" : "default")};"
            : $"    public static dynamic {Symbol.Name} = null;";
    }

    internal static string DeclarationSource(string source)
    {
        // Do not mistake fields or declaration-like text inside procedure bodies for globals.
        var text = source.ToCharArray();
        var unit = new DeclarationParser(source).ParseCompilationUnit();
        foreach (var declaration in unit.Declarations.Where(d => d is ClassDeclarationSyntax or SubDeclarationSyntax or FunctionDeclarationSyntax))
            for (var i = declaration.Span.Start; i < declaration.Span.End && i < text.Length; i++)
                if (text[i] is not ('\r' or '\n')) text[i] = ' ';

        return new string(text);
    }

    internal static IReadOnlyList<Field> Find(string source, SymbolTable symbols)
    {
        return Regex.Matches(DeclarationSource(source), @"^[ \t]*(?:Private|Public)\s+(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)(?<tail>[^\r\n]*)", RegexOptions.IgnoreCase | RegexOptions.Multiline)
            .Cast<Match>().Select(match =>
            {
                var type = AstExperimentalCompiler.ResolveRuntimeType(match.Groups["type"].Value);
                var semantic = match.Groups["type"].Value.Equals("Object", StringComparison.OrdinalIgnoreCase)
                    ? XpTypeSymbol.Object
                    : symbols.LookupAll(match.Groups["type"].Value).OfType<TypeSymbol>().FirstOrDefault()?.SemanticType;
                var tail = match.Groups["tail"].Value.Trim();
                var initializer = Regex.Match(tail, @"^=\s*(?<value>-?(?:\d+(?:\.\d*)?|\.\d+)|True|False)\s*(?:'.*)?$", RegexOptions.IgnoreCase);
                var stringInitializer = Regex.Match(tail, "^=\\s*\\\"(?<value>(?:[^\\\"]|\\\"\\\")*)\\\"\\s*(?:'.*)?$", RegexOptions.IgnoreCase);
                // Numeric literal initializers have the same direct typed storage contract as
                // uninitialized scalar fields. Other initialized/reference forms retain the
                // compatibility path until their expression/lifetime lowering is migrated.
                var typed = (type != typeof(object) || semantic is not null && !semantic.IsVariant) &&
                    (tail.Length == 0 || tail.StartsWith("'", StringComparison.Ordinal) || initializer.Success || stringInitializer.Success) &&
                    (initializer.Success ? type == typeof(long) || type == typeof(double) || type == typeof(bool) :
                        stringInitializer.Success ? type == typeof(string) : true);
                return new Field(new VariableSymbol(match.Groups["name"].Value, type,
                    typed ? semantic ?? XpTypeSymbol.FromClr(type) : XpTypeSymbol.Variant)
                {
                    HasTypedStorage = typed,
                    TypedInitializerLiteral = typed && initializer.Success
                        ? type == typeof(bool) ? initializer.Groups["value"].Value.ToLowerInvariant() : initializer.Groups["value"].Value
                        : typed && stringInitializer.Success
                        ? $"\"{stringInitializer.Groups["value"].Value.Replace("\"\"", "\\\"")}\""
                        : null
                });
            }).ToArray();
    }
}
