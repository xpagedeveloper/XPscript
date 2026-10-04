namespace XPScript.Compiler.Binding;

public sealed record XpTypeSymbol(string Name, Type RuntimeType, XpTypeSymbol? ElementType = null)
{
    public static XpTypeSymbol Variant { get; } = new("Variant", typeof(object));
    public static XpTypeSymbol Object { get; } = new("Object", typeof(object));
    public bool IsVariant => string.Equals(Name, "Variant", StringComparison.OrdinalIgnoreCase);
    public bool IsObject => string.Equals(Name, "Object", StringComparison.OrdinalIgnoreCase);

    public static XpTypeSymbol FromClr(Type type) =>
        new(type.Name, type, type.IsArray ? FromClr(type.GetElementType()!) : null);

    public static XpTypeSymbol User(string name) => new(name, typeof(object));

    public static XpTypeSymbol ArrayOf(XpTypeSymbol elementType) =>
        new(elementType.Name + "[]", elementType.RuntimeType.MakeArrayType(), elementType);
}
