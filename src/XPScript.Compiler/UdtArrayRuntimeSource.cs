namespace XPScript.Compiler;

public static class UdtArrayRuntimeSource
{
    public const string Code = """
internal static class XPTypeArrayRuntime
{
    public static object? Clone(object? value)
    {
        if (value is null) return null;
        if (value is not LSArray source)
            throw new InvalidOperationException("Type array field does not contain an XPScript array.");

        if (!source.IsAllocated)
            return new LSArray(source.ElementType, true);

        var lower = new int[source.Rank];
        var upper = new int[source.Rank];
        for (var dimension = 1; dimension <= source.Rank; dimension++)
        {
            lower[dimension - 1] = source.LBound(dimension);
            upper[dimension - 1] = source.UBound(dimension);
        }

        var clone = new LSArray(source.ElementType, source.IsDynamic, lower, upper);
        var indexes = new int[source.Rank];
        CopyDimension(0);
        return clone;

        void CopyDimension(int dimension)
        {
            for (var index = lower[dimension]; index <= upper[dimension]; index++)
            {
                indexes[dimension] = index;
                if (dimension + 1 < source.Rank)
                {
                    CopyDimension(dimension + 1);
                    continue;
                }

                var args = indexes.Cast<object?>().ToArray();
                clone.Set(source.Get(args), args);
            }
        }
    }
}
""";
}
