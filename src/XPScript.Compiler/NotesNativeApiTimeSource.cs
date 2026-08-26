namespace XPScript.Compiler;

internal static class NotesNativeApiTimeSource
{
    public const string Code = """
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct XPScriptNotesExpandedTime
{
    public int Year;
    public int Month;
    public int Day;
    public int Weekday;
    public int Hour;
    public int Minute;
    public int Second;
    public int Hundredth;
    public int Dst;
    public int Zone;
    public XPScriptNotesTimeDate GM;
}

internal sealed partial class XPScriptNotesNativeApi
{
    internal XPScriptNotesExpandedTime ExpandTimeDate(XPScriptNotesTimeDate value)
    {
        EnsureInitialized();
        var expanded = new XPScriptNotesExpandedTime { GM = value };
        if (Resolve<TimeGMToLocalDelegate>("TimeGMToLocal")(ref expanded) != 0)
            throw new XPScriptRuntimeException(5, "TimeGMToLocal failed.");
        return expanded;
    }

    internal XPScriptNotesExpandedTime ExpandTimeDateGmt(XPScriptNotesTimeDate value)
    {
        EnsureInitialized();
        var expanded = new XPScriptNotesExpandedTime
        {
            GM = value,
            Zone = 0,
            Dst = 0
        };
        if (Resolve<TimeGMToLocalZoneDelegate>("TimeGMToLocalZone")(ref expanded) != 0)
            throw new XPScriptRuntimeException(5, "TimeGMToLocalZone failed.");
        return expanded;
    }

    internal string FormatExpandedTime(XPScriptNotesExpandedTime value)
    {
        return value.Year.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + "-" +
            value.Month.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "-" +
            value.Day.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + " " +
            value.Hour.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + ":" +
            value.Minute.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + ":" +
            value.Second.ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate int TimeGMToLocalDelegate(ref XPScriptNotesExpandedTime value);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate int TimeGMToLocalZoneDelegate(ref XPScriptNotesExpandedTime value);
}
""";
}
