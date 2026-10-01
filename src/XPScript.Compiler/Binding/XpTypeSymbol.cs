namespace XPScript.Compiler.Binding;

public sealed record XpTypeSymbol(string Name, Type RuntimeType, XpTypeSymbol? ElementType = null)
{
    public static XpTypeSymbol FromClr(Type type) =>
        new(type.Name, type, type.IsArray ? FromClr(type.GetElementType()!) : null);

    public static XpTypeSymbol User(string name) => new(name, typeof(object));

    public static XpTypeSymbol ArrayOf(XpTypeSymbol elementType) =>
        new(elementType.Name + "[]", elementType.RuntimeType.MakeArrayType(), elementType);
}
