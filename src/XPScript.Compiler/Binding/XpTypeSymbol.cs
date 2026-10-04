namespace XPScript.Compiler.Binding;

public enum XpTypeShape
{
    Scalar,
    Array,
    List
}

public sealed record XpTypeSymbol(
    string Name,
    Type RuntimeType,
    XpTypeSymbol? ElementType = null,
    XpTypeShape Shape = XpTypeShape.Scalar)
{
    public static XpTypeSymbol Variant { get; } = new("Variant", typeof(object));
    public static XpTypeSymbol Object { get; } = new("Object", typeof(object));
    public static XpTypeSymbol Empty { get; } = new("Empty", typeof(object));
    public static XpTypeSymbol Null { get; } = new("Null", typeof(object));
    public static XpTypeSymbol Nothing { get; } = new("Nothing", typeof(object));
    public bool IsVariant => string.Equals(Name, "Variant", StringComparison.OrdinalIgnoreCase) && Shape == XpTypeShape.Scalar;
    public bool IsObject => string.Equals(Name, "Object", StringComparison.OrdinalIgnoreCase) && Shape == XpTypeShape.Scalar;
    public bool IsEmpty => string.Equals(Name, "Empty", StringComparison.OrdinalIgnoreCase) && Shape == XpTypeShape.Scalar;
    public bool IsNull => string.Equals(Name, "Null", StringComparison.OrdinalIgnoreCase) && Shape == XpTypeShape.Scalar;
    public bool IsNothing => string.Equals(Name, "Nothing", StringComparison.OrdinalIgnoreCase) && Shape == XpTypeShape.Scalar;
    public bool IsArray => Shape == XpTypeShape.Array;
    public bool IsList => Shape == XpTypeShape.List;

    public static XpTypeSymbol FromClr(Type type) =>
        type.IsArray
            ? ArrayOf(FromClr(type.GetElementType()!))
            : new(type.Name, type);

    public static XpTypeSymbol User(string name) => new(name, typeof(object));

    public static XpTypeSymbol ArrayOf(XpTypeSymbol elementType) =>
        new(elementType.Name + "[]", elementType.RuntimeType.MakeArrayType(), elementType, XpTypeShape.Array);

    public static XpTypeSymbol ListOf(XpTypeSymbol elementType) =>
        new("List As " + elementType.Name, typeof(object), elementType, XpTypeShape.List);
}
