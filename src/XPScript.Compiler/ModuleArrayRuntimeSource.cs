namespace XPScript.Compiler;

internal static class ModuleArrayRuntimeSource
{
    public const string Code = """
internal static class XPModuleArrayRuntime
{
    public static object? ReDim(object? current, string elementType, bool preserve, params object?[] bounds)
    {
        if (bounds.Length == 0 || bounds.Length % 2 != 0)
            throw new InvalidOperationException("ReDim requires lower/upper bound pairs.");

        var rank = bounds.Length / 2;
        if (rank < 1 || rank > 8)
            throw new InvalidOperationException("Arrays must have between one and eight dimensions.");

        var lower = new int[rank];
        var upper = new int[rank];
        for (var i = 0; i < rank; i++)
        {
            lower[i] = XPScriptRuntime.CInt(bounds[i * 2]);
            upper[i] = XPScriptRuntime.CInt(bounds[i * 2 + 1]);
        }

        return LSArrayRuntime.ReDim(current, elementType, preserve, lower, upper);
    }
}
""";
}
