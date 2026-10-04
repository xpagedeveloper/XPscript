namespace XPScript.Compiler.Binding;

public enum ConversionKind
{
    None,
    Identity,
    NumericWidening
}

public readonly record struct Conversion(ConversionKind Kind)
{
    public bool Exists => Kind != ConversionKind.None;
    public bool IsIdentity => Kind == ConversionKind.Identity;
    public bool IsImplicit => Exists;

    public static Conversion Classify(XpTypeSymbol source, XpTypeSymbol target)
    {
        if (source.RuntimeType == target.RuntimeType &&
            string.Equals(source.Name, target.Name, StringComparison.OrdinalIgnoreCase))
            return new Conversion(ConversionKind.Identity);

        // Variant/Object/Null have dedicated Phase 7 semantics and are intentionally
        // not treated as catch-all conversions here.
        if (source.RuntimeType == typeof(object) || target.RuntimeType == typeof(object))
            return new Conversion(ConversionKind.None);

        if (source.RuntimeType == typeof(long) && target.RuntimeType == typeof(double))
            return new Conversion(ConversionKind.NumericWidening);

        return new Conversion(ConversionKind.None);
    }
}
