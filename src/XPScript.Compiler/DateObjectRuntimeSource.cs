namespace XPScript.Compiler;

internal static class DateObjectRuntimeSource
{
    public const string Code = """
internal static class XPDateRuntime
{
    public static DateTime Adjust(object? dateValue, object? years, object? months, object? days, object? hours, object? minutes, object? seconds)
    {
        var value = XPScriptRuntime.CDate(dateValue);
        try
        {
            value = value.AddYears(XPScriptRuntime.CInt(years));
            value = value.AddMonths(XPScriptRuntime.CInt(months));
            value = value.AddDays(XPScriptRuntime.CInt(days));
            value = value.AddHours(XPScriptRuntime.CInt(hours));
            value = value.AddMinutes(XPScriptRuntime.CInt(minutes));
            value = value.AddSeconds(XPScriptRuntime.CInt(seconds));
            return value;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new XPScriptRuntimeException(5, "Date.Adjust produced a date outside the supported range: " + ex.Message);
        }
    }

    public static double Difference(object? currentDate, object? otherDate)
    {
        var current = XPScriptRuntime.CDate(currentDate);
        var other = XPScriptRuntime.CDate(otherDate);
        return (other - current).TotalSeconds;
    }

    public static int Compare(object? left, object? right) =>
        DateTime.Compare(XPScriptRuntime.CDate(left), XPScriptRuntime.CDate(right));
}
""";
}
