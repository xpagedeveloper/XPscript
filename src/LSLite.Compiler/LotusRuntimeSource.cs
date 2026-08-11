namespace LSLite.Compiler;

public static class LotusRuntimeSource
{
    public const string Code = """
internal static class LotusRuntime
{
    private static readonly Random Random = new();

    public static int Len(object? value) => CStr(value).Length;

    public static string Left(object? value, int count)
    {
        var text = CStr(value);
        count = Math.Clamp(count, 0, text.Length);
        return text[..count];
    }

    public static string Right(object? value, int count)
    {
        var text = CStr(value);
        count = Math.Clamp(count, 0, text.Length);
        return text[(text.Length - count)..];
    }

    public static string Mid(object? value, int start) =>
        Mid(value, start, int.MaxValue);

    public static string Mid(object? value, int start, int count)
    {
        var text = CStr(value);
        var index = Math.Max(0, start - 1);
        if (index >= text.Length)
            return "";

        count = Math.Max(0, Math.Min(count, text.Length - index));
        return text.Substring(index, count);
    }

    public static string UCase(object? value) => CStr(value).ToUpperInvariant();
    public static string LCase(object? value) => CStr(value).ToLowerInvariant();
    public static string Trim(object? value) => CStr(value).Trim();
    public static string LTrim(object? value) => CStr(value).TrimStart();
    public static string RTrim(object? value) => CStr(value).TrimEnd();

    public static string CStr(object? value) =>
        Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";

    public static int CInt(object? value) =>
        Convert.ToInt32(value, CultureInfo.CurrentCulture);

    public static long CLng(object? value) =>
        Convert.ToInt64(value, CultureInfo.CurrentCulture);

    public static double CDbl(object? value) =>
        Convert.ToDouble(value, CultureInfo.CurrentCulture);

    public static float CSng(object? value) =>
        Convert.ToSingle(value, CultureInfo.CurrentCulture);

    public static bool CBool(object? value) =>
        Convert.ToBoolean(value, CultureInfo.CurrentCulture);

    public static double Abs(double value) => Math.Abs(value);
    public static double Int(double value) => Math.Floor(value);
    public static double Fix(double value) => Math.Truncate(value);
    public static double Round(double value) => Math.Round(value);
    public static double Round(double value, int digits) => Math.Round(value, digits);
    public static double Sqr(double value) => Math.Sqrt(value);
    public static double Rnd() => Random.NextDouble();

    public static DateTime Now() => DateTime.Now;
    public static DateTime Today() => DateTime.Today;
    public static DateTime Date() => DateTime.Today;
    public static DateTime Time() => DateTime.Today.Add(DateTime.Now.TimeOfDay);

    public static int Year(DateTime value) => value.Year;
    public static int Month(DateTime value) => value.Month;
    public static int Day(DateTime value) => value.Day;
    public static int Hour(DateTime value) => value.Hour;
    public static int Minute(DateTime value) => value.Minute;
    public static int Second(DateTime value) => value.Second;

    public static string Chr(int code) => Convert.ToChar(code).ToString();

    public static int Asc(object? value)
    {
        var text = CStr(value);
        return text.Length == 0 ? 0 : text[0];
    }

    public static int Instr(object? source, object? find) =>
        Instr(1, source, find);

    public static int Instr(int start, object? source, object? find)
    {
        var text = CStr(source);
        var needle = CStr(find);
        var index = text.IndexOf(
            needle,
            Math.Max(0, start - 1),
            StringComparison.CurrentCulture);

        return index < 0 ? 0 : index + 1;
    }

    public static string Replace(object? source, object? find, object? replacement) =>
        CStr(source).Replace(
            CStr(find),
            CStr(replacement),
            StringComparison.CurrentCulture);

    public static string Space(int count) =>
        new(' ', Math.Max(0, count));

    public static string String(int count, object? character)
    {
        var text = CStr(character);
        var c = text.Length == 0 ? '\0' : text[0];
        return new string(c, Math.Max(0, count));
    }

    public static bool IsNumeric(object? value) =>
        double.TryParse(
            CStr(value),
            NumberStyles.Any,
            CultureInfo.CurrentCulture,
            out _);

    public static double Val(object? value)
    {
        var text = CStr(value).Trim();
        var normalized = text.Replace(',', '.');
        return double.TryParse(
            normalized,
            NumberStyles.Float | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out var number)
            ? number
            : 0d;
    }

    public static string Hex(long value) => value.ToString("X", CultureInfo.InvariantCulture);

    public static string Oct(long value)
    {
        if (value == 0)
            return "0";

        var negative = value < 0;
        ulong n = (ulong)Math.Abs(value);
        var result = "";

        while (n > 0)
        {
            result = (n % 8).ToString(CultureInfo.InvariantCulture) + result;
            n /= 8;
        }

        return negative ? "-" + result : result;
    }

    public static string Format(object? value, string format)
    {
        if (value is IFormattable formattable)
            return formattable.ToString(format, CultureInfo.CurrentCulture) ?? "";

        return CStr(value);
    }

    public static string[] Split(object? value, object? delimiter) =>
        CStr(value).Split(
            [CStr(delimiter)],
            StringSplitOptions.None);

    public static string Join(string[] values, object? delimiter) =>
        string.Join(CStr(delimiter), values);

    public static string InputBox(object? prompt)
    {
        Console.Write(CStr(prompt));
        return Console.ReadLine() ?? "";
    }

    public static int MsgBox(object? prompt)
    {
        Console.WriteLine(CStr(prompt));
        return 1;
    }

    public static IEnumerable<long> Range(object? startValue, object? endValue, object? stepValue)
    {
        var start = CLng(startValue);
        var end = CLng(endValue);
        var step = CLng(stepValue);

        if (step == 0)
            throw new InvalidOperationException("For Step cannot be zero.");

        if (step > 0)
        {
            for (var i = start; i <= end; i += step)
                yield return i;
        }
        else
        {
            for (var i = start; i >= end; i += step)
                yield return i;
        }
    }
}
""";
}
