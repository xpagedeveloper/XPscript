namespace XPScript.Compiler;

internal static class NotesRuntimeValueSource
{
    public const string Code = """
internal sealed class XPScriptNotesName : XPScriptNotesObject
{
    private readonly Dictionary<string, string> _parts = new(StringComparer.OrdinalIgnoreCase);

    internal XPScriptNotesName(XPScriptNotesSession session, string value) : base(session)
    {
        Source = value.Trim();
        Canonical = session.Api.CanonicalizeName(Source);
        Abbreviated = session.Api.AbbreviateName(Canonical);
        ParseCanonical(Canonical);
        ParseInternet(Source);
    }

    public XPScriptNotesSession Parent { get { EnsureAlive(); return Session; } }
    public string Source { get; }
    public string Canonical { get; }
    public string Abbreviated { get; }
    public bool IsHierarchical => Canonical.Contains('=');
    public string Common => Part("CN");
    public string Country => Part("C");
    public string Organization => Part("O");
    public string OrgUnit1 => Part("OU1");
    public string OrgUnit2 => Part("OU2");
    public string OrgUnit3 => Part("OU3");
    public string OrgUnit4 => Part("OU4");
    public string ADMD => Part("A");
    public string PRMD => Part("P");
    public string Addr821 => Part("ADDR821");
    public string Addr822LocalPart => Part("LOCALPART");
    public string Addr822Phrase => Part("PHRASE");

    private string Part(string key)
    {
        EnsureAlive();
        return _parts.TryGetValue(key, out var value) ? value : "";
    }

    private void ParseCanonical(string value)
    {
        var ou = 0;
        foreach (var raw in value.Split('/'))
        {
            var part = raw.Trim();
            var separator = part.IndexOf('=');
            if (separator <= 0) continue;
            var key = part[..separator].Trim().ToUpperInvariant();
            var text = part[(separator + 1)..].Trim();
            if (key == "OU")
            {
                ou++;
                if (ou <= 4) _parts["OU" + ou.ToString(System.Globalization.CultureInfo.InvariantCulture)] = text;
            }
            else _parts[key] = text;
        }
    }

    private void ParseInternet(string source)
    {
        var at = source.LastIndexOf('@');
        if (at <= 0 || at >= source.Length - 1) return;
        _parts["ADDR821"] = source;
        var before = source[..at].Trim();
        var lt = before.LastIndexOf('<');
        var gt = before.LastIndexOf('>');
        if (lt >= 0 && gt > lt)
        {
            _parts["PHRASE"] = before[..lt].Trim().Trim('"');
            _parts["LOCALPART"] = before[(lt + 1)..gt].Split('@')[0];
        }
        else _parts["LOCALPART"] = before;
    }

    protected override void ReleaseNative() => _parts.Clear();
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct XPScriptNotesTimeDate
{
    public uint Innards0;
    public uint Innards1;
}

internal sealed class XPScriptNotesDateTime : XPScriptNotesObject
{
    private XPScriptNotesTimeDate _value;

    internal XPScriptNotesDateTime(XPScriptNotesSession session, string value) : base(session)
    {
        if (value.Trim().Length == 0)
            throw new XPScriptRuntimeException(5, "NotesDateTime requires a date/time value.");
        _value = session.Api.ParseTimeDate(value);
    }

    private XPScriptNotesDateTime(XPScriptNotesSession session, XPScriptNotesTimeDate value) : base(session) => _value = value;

    internal static XPScriptNotesDateTime CreateNow(XPScriptNotesSession session) => new(session, session.Api.CurrentTimeDate());
    internal static XPScriptNotesDateTime FromNative(XPScriptNotesSession session, XPScriptNotesTimeDate value) => new(session, value);
    internal static object FromNativeObject(XPScriptNotesTimeDate value) => throw new XPScriptRuntimeException(13, "Use NotesDocument.GetDateTime for Notes time/date fields.");

    public XPScriptNotesSession Parent { get { EnsureAlive(); return Session; } }
    public bool IsValidDate { get { EnsureAlive(); return true; } }
    public string LocalTime { get { EnsureAlive(); return Session.Api.FormatTimeDate(_value); } }
    public string DateOnly
    {
        get
        {
            EnsureAlive();
            var text = LocalTime;
            return DateTime.TryParse(text, out var value) ? value.ToShortDateString() : text;
        }
    }
    public string TimeOnly
    {
        get
        {
            EnsureAlive();
            var text = LocalTime;
            return DateTime.TryParse(text, out var value) ? value.ToLongTimeString() : text;
        }
    }

    public void AdjustSecond(object? amount) => Adjust(XPScriptRuntime.CInt(amount), 0, 0, 0, 0, 0);
    public void AdjustMinute(object? amount) => Adjust(0, XPScriptRuntime.CInt(amount), 0, 0, 0, 0);
    public void AdjustHour(object? amount) => Adjust(0, 0, XPScriptRuntime.CInt(amount), 0, 0, 0);
    public void AdjustDay(object? amount) => Adjust(0, 0, 0, XPScriptRuntime.CInt(amount), 0, 0);
    public void AdjustMonth(object? amount) => Adjust(0, 0, 0, 0, XPScriptRuntime.CInt(amount), 0);
    public void AdjustYear(object? amount) => Adjust(0, 0, 0, 0, 0, XPScriptRuntime.CInt(amount));

    private void Adjust(int seconds, int minutes, int hours, int days, int months, int years)
    {
        EnsureAlive();
        Session.Api.AdjustTimeDate(ref _value, seconds, minutes, hours, days, months, years);
    }

    internal XPScriptNotesTimeDate NativeValue { get { EnsureAlive(); return _value; } }
    protected override void ReleaseNative() => _value = default;
}
""";
}
