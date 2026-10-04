namespace XPScript.Compiler.Binding;

public enum ConversionKind
{
    None,
    Identity,
    NumericWidening,
    ToVariant,
    FromVariant,
    ToObject
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

        // Variant is XPscript's dynamic value carrier. Values can flow into and out of
        // Variant; the latter remains a runtime-checked conversion.
        if (target.IsVariant)
            return new Conversion(ConversionKind.ToVariant);
        if (source.IsVariant)
            return new Conversion(ConversionKind.FromVariant);

        // Object is an object-reference target, not a dynamic scalar container.
        // Only reference-shaped values can flow into it implicitly.
        if (target.IsObject)
            return source.RuntimeType == typeof(object) || (!source.RuntimeType.IsValueType && source.RuntimeType != typeof(string))
                ? new Conversion(ConversionKind.ToObject)
                : new Conversion(ConversionKind.None);

        // Object cannot implicitly flow back to a concrete/user type. That requires
        // object-reference semantics (Set/cast) rather than Variant-style coercion.
        if (source.IsObject || source.RuntimeType == typeof(object) || target.RuntimeType == typeof(object))
            return new Conversion(ConversionKind.None);

        if (source.RuntimeType == typeof(long) && target.RuntimeType == typeof(double))
            return new Conversion(ConversionKind.NumericWidening);

        return new Conversion(ConversionKind.None);
    }
}
