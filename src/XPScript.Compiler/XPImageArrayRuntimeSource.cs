namespace XPScript.Compiler;

internal static class XPImageArrayRuntimeSource
{
    public const string Code = """
internal static class XPImageArrayRuntime
{
    public static System.Array? Clone(System.Array? source)
    {
        if (source is null) return null;
        if (source.GetType().GetElementType() != typeof(XPImage))
            throw new InvalidCastException("XPImage array clone requires XPImage elements.");
        var lengths = Enumerable.Range(0, source.Rank).Select(source.GetLength).ToArray();
        var lower = Enumerable.Range(0, source.Rank).Select(source.GetLowerBound).ToArray();
        var result = System.Array.CreateInstance(typeof(XPImage), lengths, lower);
        var created = new List<XPImage>();
        var indexes = new int[source.Rank];
        try
        {
            Copy(0);
            return result;
        }
        catch
        {
            foreach (var image in created) image.Recycle();
            throw;
        }

        void Copy(int dimension)
        {
            for (var index = lower[dimension]; index < lower[dimension] + lengths[dimension]; index++)
            {
                indexes[dimension] = index;
                if (dimension + 1 < source.Rank) { Copy(dimension + 1); continue; }
                var image = (XPImage?)source.GetValue(indexes);
                var clone = image?.Clone();
                if (clone is not null) created.Add(clone);
                result.SetValue(clone, indexes);
            }
        }
    }
}
""";
}
