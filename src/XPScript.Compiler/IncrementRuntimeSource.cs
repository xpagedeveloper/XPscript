namespace XPScript.Compiler;

internal static class IncrementRuntimeSource
{
    public const string Code = """
internal static class XPScriptIncrementRuntime
{
    public static dynamic Increment(object? value) => Apply(value, 1);
    public static dynamic Decrement(object? value) => Apply(value, -1);

    private static dynamic Apply(object? value, int delta)
    {
        try
        {
            return value switch
            {
                byte v => checked((byte)(v + delta)),
                short v => checked((short)(v + delta)),
                int v => checked(v + delta),
                long v => checked(v + delta),
                float v => v + delta,
                double v => v + delta,
                decimal v => checked(v + delta),
                _ => throw new XPScriptRuntimeException(13,
                    "Increment/decrement requires a numeric assignable target.")
            };
        }
        catch (OverflowException ex)
        {
            throw new XPScriptRuntimeException(6, ex.Message);
        }
    }
}
""";
}
