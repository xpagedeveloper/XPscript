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
                    : symbols.LookupAll(match.Groups["type"].Value).OfType<TypeSymbol>().FirstOrDefault()?.SemanticType
                        ?? (match.Groups["type"].Value.Equals("Date", StringComparison.OrdinalIgnoreCase) ? XpTypeSymbol.User("Date") : null);
                var tail = match.Groups["tail"].Value.Trim();
                var initializer = Regex.Match(tail, @"^=\s*(?<value>-?(?:\d+(?:\.\d*)?|\.\d+)|True|False)\s*(?:'.*)?$", RegexOptions.IgnoreCase);
                var stringInitializer = Regex.Match(tail, "^=\\s*\\\"(?<value>(?:[^\\\"]|\\\"\\\")*)\\\"\\s*(?:'.*)?$", RegexOptions.IgnoreCase);
                var nothingInitializer = Regex.IsMatch(tail, @"^=\s*Nothing\s*(?:'.*)?$", RegexOptions.IgnoreCase);
                var newInitializer = Regex.Match(tail, @"^=\s*New\s+(?<type>[A-Za-z_]\w*)\s*\(\s*(?<args>-?(?:\d+(?:\.\d*)?|\.\d+)(?:\s*,\s*-?(?:\d+(?:\.\d*)?|\.\d+))*)?\s*\)\s*(?:'.*)?$", RegexOptions.IgnoreCase);
                var dateInitializer = Regex.Match(tail, @"^=\s*DateNumber\s*\(\s*(?<year>\d+)\s*,\s*(?<month>\d+)\s*,\s*(?<day>\d+)\s*\)\s*(?:'.*)?$", RegexOptions.IgnoreCase);
                // Numeric literal initializers have the same direct typed storage contract as
                // uninitialized scalar fields. Other initialized/reference forms retain the
                // compatibility path until their expression/lifetime lowering is migrated.
                var typed = (type != typeof(object) || semantic is not null && !semantic.IsVariant) &&
                    (tail.Length == 0 || tail.StartsWith("'", StringComparison.Ordinal) || initializer.Success || stringInitializer.Success || nothingInitializer || newInitializer.Success || dateInitializer.Success) &&
                    (initializer.Success ? type == typeof(long) || type == typeof(double) || type == typeof(bool) :
                        stringInitializer.Success ? type == typeof(string) :
                        newInitializer.Success ? type == typeof(object) && semantic is { Shape: XpTypeShape.Scalar, IsVariant: false } && semantic.Name.Equals(newInitializer.Groups["type"].Value, StringComparison.OrdinalIgnoreCase) :
                        dateInitializer.Success ? type == typeof(object) && semantic is { Shape: XpTypeShape.Scalar, IsVariant: false } && semantic.Name.Equals("Date", StringComparison.OrdinalIgnoreCase) : true);
                return new Field(new VariableSymbol(match.Groups["name"].Value, type,
                    typed ? semantic ?? XpTypeSymbol.FromClr(type) : XpTypeSymbol.Variant)
                {
                    HasTypedStorage = typed,
                    TypedInitializerLiteral = typed && initializer.Success
                        ? type == typeof(bool) ? initializer.Groups["value"].Value.ToLowerInvariant() : initializer.Groups["value"].Value
                        : typed && stringInitializer.Success
                        ? $"\"{stringInitializer.Groups["value"].Value.Replace("\"\"", "\\\"")}\""
                        : typed && nothingInitializer ? "null"
                        : typed && newInitializer.Success ? InitializerExpression(newInitializer)
                        : typed && dateInitializer.Success ? $"new XpDate(new System.DateTime({dateInitializer.Groups["year"].Value}, {dateInitializer.Groups["month"].Value}, {dateInitializer.Groups["day"].Value}))"
                        : null
                });
            }).ToArray();
    }

    private static string InitializerExpression(Match initializer)
    {
        var typeName = initializer.Groups["type"].Value;
        var arguments = initializer.Groups["args"].Value;
        var runtimeType = BoundExpressionEmitter.RuntimeObjectType(typeName);
        return typeName.Equals("XPXmlDocument", StringComparison.OrdinalIgnoreCase) && arguments.Length == 0
            ? "XPScriptNativeXml.CreateDocument()"
            : AstImageSupport.IsImage(typeName)
            ? $"new XPImage({arguments})"
            : runtimeType is not null
                ? $"new {runtimeType}({arguments})"
                : $"new Xp{typeName}({arguments})";
    }
}
