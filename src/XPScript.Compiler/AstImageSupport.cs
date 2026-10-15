using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler;

/// <summary>AST bridge to the existing image runtime; its public source is the API source of truth.</summary>
internal static class AstImageSupport
{
    internal sealed record Callable(string Name, string ReturnType, string[] ParameterNames, string[] NativeTypes, bool IsStatic);
    private static readonly ClassDeclarationSyntax Runtime = CSharpSyntaxTree.ParseText(XPImageRuntimeSource.Code)
        .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
    internal static readonly Callable[] Constructors = Runtime.Members.OfType<ConstructorDeclarationSyntax>()
        .Where(c => c.Modifiers.Any(SyntaxKind.PublicKeyword))
        .Select(c => Create("New", "XPImage", c.ParameterList, false)).ToArray();
    internal static readonly Callable[] Methods = Runtime.Members.OfType<MethodDeclarationSyntax>()
        .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword) && m.Identifier.ValueText != "RecycleOwned")
        .Select(m => Create(m.Identifier.ValueText, m.ReturnType.ToString(), m.ParameterList, m.Modifiers.Any(SyntaxKind.StaticKeyword))).ToArray();
    internal static readonly PropertyDeclarationSyntax[] Properties = Runtime.Members.OfType<PropertyDeclarationSyntax>()
        .Where(p => p.Modifiers.Any(SyntaxKind.PublicKeyword)).ToArray();

    private static Callable Create(string name, string result, ParameterListSyntax parameters, bool isStatic) =>
        new(name, result, parameters.Parameters.Select(p => p.Identifier.ValueText).ToArray(),
            parameters.Parameters.Select(p => p.Type!.ToString()).ToArray(), isStatic);
    internal static bool IsImage(string? name) => string.Equals(name, "XPImage", StringComparison.OrdinalIgnoreCase);
    internal static XpTypeSymbol SemanticType(string type) => type switch
    {
        "int" or "int?" or "long" => XpTypeSymbol.FromClr(typeof(long)),
        "double" => XpTypeSymbol.FromClr(typeof(double)), "bool" => XpTypeSymbol.FromClr(typeof(bool)),
        "string" => XpTypeSymbol.FromClr(typeof(string)), "byte[]" => XpTypeSymbol.Variant,
        "XPImage" => XpTypeSymbol.User("XPImage"), "XPScriptJsonObject" => XpTypeSymbol.User("XPJsonObject"),
        "XPScriptJsonArray" => XpTypeSymbol.User("XPJsonArray"), "void" => XpTypeSymbol.FromClr(typeof(void)),
        _ => throw new InvalidOperationException($"Unmapped image runtime type '{type}'.")
    };
    private static string LanguageType(string type) => type switch
    {
        "int" or "int?" => "Integer", "long" => "Long", "double" => "Double", "bool" => "Boolean",
        "string" => "String", "byte[]" => "Variant", "XPScriptJsonObject" => "XPJsonObject",
        "XPScriptJsonArray" => "XPJsonArray", _ => type
    };
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => Methods.GroupBy(m => m.Name).Select(group =>
    {
        var m = group.Last();
        var name = "XPImage." + m.Name;
        var signatures = group.Select(c => name + "(" + string.Join(", ", c.ParameterNames.Zip(c.NativeTypes,
            (n, t) => n + " As " + LanguageType(t))) + ")" + (c.ReturnType == "void" ? "" : " As " + LanguageType(c.ReturnType)));
        return new CompilerSymbolDefinition(name, "method", string.Join("; ", signatures),
            m.ParameterNames.Zip(m.NativeTypes, (n, t) => new CompilerSymbolParameter(n, LanguageType(t))).ToArray(),
            m.ReturnType == "void" ? null : LanguageType(m.ReturnType), [], "api." + name);
    }).Concat(Properties.Select(p => new CompilerSymbolDefinition("XPImage." + p.Identifier.ValueText, "property",
        "XPImage." + p.Identifier.ValueText + " As " + LanguageType(p.Type.ToString()), [], LanguageType(p.Type.ToString()), [],
        "api.XPImage." + p.Identifier.ValueText)));

    internal static void Import(SymbolTable symbols)
    {
        foreach (var p in Properties)
        {
            var type = SemanticType(p.Type.ToString());
            symbols.Declare(new PropertySymbol("XPImage." + p.Identifier.ValueText, type.RuntimeType, type));
        }
        foreach (var m in Methods)
        {
            var result = SemanticType(m.ReturnType);
            var args = m.NativeTypes.Select(SemanticType).ToArray();
            symbols.Declare(new FunctionSymbol("XPImage." + m.Name, result.RuntimeType, args.Select(a => a.RuntimeType).ToArray(), result, args)
            { NativeParameterTypes = m.NativeTypes, IsNativeStatic = m.IsStatic });
        }
    }
    internal static string MemberName(string name) => Properties.Select(p => p.Identifier.ValueText).Concat(Methods.Select(m => m.Name))
        .FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? name;
    internal static string ConvertArgument(string value, string type) => type switch
    {
        "int" or "int?" => $"checked((int)XPScriptRuntime.CLng({value}))",
        "byte[]" => $"(byte[])({value})", "XPImage" => $"(XPImage)({value})", _ => value
    };
    internal static string RuntimeCode(string source) => AstObjectSupportRegistry.UsesObject(source, "XPImage")
        ? FileSystemPortabilityRuntimeSource.Code + Environment.NewLine + XPImageRuntimeSource.Code + Environment.NewLine + OwnershipCode : "";

    internal static bool IsImageStorage(BoundExpression target) => IsImage(target.SemanticType.Name) &&
        target is BoundNameExpression { Symbol: FieldSymbol } or BoundMemberAccessExpression or BoundIndexedPropertyExpression;

    internal static bool IsOwningValue(BoundExpression value) => value switch
    {
        BoundConversionExpression conversion => IsOwningValue(conversion.Expression),
        BoundNewExpression or BoundCallExpression or BoundIndexedPropertyExpression => true,
        BoundMemberAccessExpression { IsFieldStorage: false } => true,
        _ => false
    };

    private const string OwnershipCode = """
internal sealed class AstImageScope : System.IDisposable
{
    private readonly HashSet<XPImage> _owned = new(System.Collections.Generic.ReferenceEqualityComparer.Instance);
    private readonly HashSet<XPImage> _escaped = new(System.Collections.Generic.ReferenceEqualityComparer.Instance);
    private readonly HashSet<XPImage> _borrowed = new(System.Collections.Generic.ReferenceEqualityComparer.Instance);
    private readonly List<Func<XPImage?>> _locations = new();
    public XPImage? Own(XPImage? image) { if (image is not null && !_borrowed.Contains(image)) _owned.Add(image); return image; }
    public void Borrow(XPImage? image) { if (image is not null) _borrowed.Add(image); }
    public void Observe(Func<XPImage?> location) => _locations.Add(location);
    public XPImage? Escape(XPImage? image) { if (image is not null) _escaped.Add(image); return image; }
    public T EscapeValue<T>(T value) { if (value is XPImage image) Escape(image); return value; }
    public XPImage? Replace(XPImage? previous, XPImage? next) => Own(previous is not null && _borrowed.Contains(previous) ? next : ReplaceBorrowed(previous, next));
    public static void StoreField(ref XPImage? field, XPImage? value)
    {
        if (object.ReferenceEquals(field, value)) return;
        // Clone before replacing/recycling so a failed clone preserves the old field.
        var snapshot = value?.Clone();
        var previous = field;
        field = snapshot;
        previous?.Recycle();
    }
    public static XPImage? ReplaceBorrowed(XPImage? previous, XPImage? next)
    {
        if (!object.ReferenceEquals(previous, next)) previous?.Recycle();
        return next;
    }
    public void Dispose()
    {
        foreach (var location in _locations) Own(location());
        foreach (var image in _owned) if (!_escaped.Contains(image)) image.Recycle();
        _owned.Clear(); _escaped.Clear(); _borrowed.Clear(); _locations.Clear();
    }
}
""";
}
