namespace XPScript.Compiler;

public static class XPScriptRuntimeSource
{
    public const string Code = """
internal static class XPScriptRuntime
{
    private sealed class FileState
    {
        public required FileStream Stream { get; init; }
        public StreamReader? Reader { get; init; }
        public StreamWriter? Writer { get; init; }
    }

    private static readonly Dictionary<int, FileState> Files = new();
    private static readonly object FileLock = new();
    private static readonly DateTime OaEpoch = new(1899, 12, 30);
    private static Random Random = new();
    private static string[] Args = [];
    private static IEnumerator<string>? DirEnumerator;

    public static void SetArgs(string[] args) => Args = args;
    public static string Command() => string.Join(" ", Args);

    public static int Len(object? value) => CStr(value).Length;
    public static int LenB(object? value) => Encoding.Default.GetByteCount(CStr(value));

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

    public static string Mid(object? value, int start) => Mid(value, start, int.MaxValue);

    public static string Mid(object? value, int start, int count)
    {
        var text = CStr(value);
        var index = Math.Max(0, start - 1);
        if (index >= text.Length) return "";
        count = Math.Max(0, Math.Min(count, text.Length - index));
        return text.Substring(index, count);
    }

    public static string UCase(object? value) => CStr(value).ToUpper(CultureInfo.CurrentCulture);
    public static string LCase(object? value) => CStr(value).ToLower(CultureInfo.CurrentCulture);
    public static string Trim(object? value) => CStr(value).Trim();
    public static string LTrim(object? value) => CStr(value).TrimStart();
    public static string RTrim(object? value) => CStr(value).TrimEnd();
    public static string FullTrim(object? value) =>
        string.Join(" ", CStr(value).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    public static string StrReverse(object? value) => new(CStr(value).Reverse().ToArray());

    public static string CStr(object? value) =>
        value is DateTime dt ? dt.ToString(CultureInfo.CurrentCulture) :
        Convert.ToString(value, CultureInfo.CurrentCulture) ?? "";
    public static byte CByte(object? value) => Convert.ToByte(value, CultureInfo.CurrentCulture);
    public static int CInt(object? value) => Convert.ToInt32(value, CultureInfo.CurrentCulture);
    public static long CLng(object? value) => Convert.ToInt64(value, CultureInfo.CurrentCulture);
    public static double CDbl(object? value) =>
        value is DateTime dt ? dt.ToOADate() : Convert.ToDouble(value, CultureInfo.CurrentCulture);
    public static float CSng(object? value) => Convert.ToSingle(value, CultureInfo.CurrentCulture);
    public static decimal CCur(object? value) => Convert.ToDecimal(value, CultureInfo.CurrentCulture);
    public static bool CBool(object? value) => Convert.ToBoolean(value, CultureInfo.CurrentCulture);
    public static object? CVar(object? value) => value;

    public static DateTime CDat(object? value)
    {
        if (value is DateTime dt) return dt;
        if (value is null) return OaEpoch;
        if (value is IConvertible && value is not string)
            return DateTime.FromOADate(Convert.ToDouble(value, CultureInfo.CurrentCulture));

        var text = CStr(value).Trim();
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var number))
            return DateTime.FromOADate(number);
        return DateTime.Parse(text, CultureInfo.CurrentCulture);
    }

    public static DateTime CDate(object? value) => CDat(value);

    public static int DataType(object? value) => value switch
    {
        null => 0,
        byte => 17,
        short or int => 2,
        long => 3,
        float => 4,
        double => 5,
        decimal => 6,
        DateTime => 7,
        string => 8,
        bool => 11,
        Array => 8192,
        _ => 9
    };

    public static string TypeName(object? value) => value switch
    {
        null => "EMPTY",
        byte => "BYTE",
        short or int => "INTEGER",
        long => "LONG",
        float => "SINGLE",
        double => "DOUBLE",
        decimal => "CURRENCY",
        DateTime => "DATE",
        string => "STRING",
        bool => "BOOLEAN",
        Array => "ARRAY",
        _ => value.GetType().Name.ToUpperInvariant()
    };

    public static bool IsArray(object? value) => value is Array;
    public static bool IsDate(object? value)
    {
        if (value is DateTime) return true;
        return DateTime.TryParse(CStr(value), CultureInfo.CurrentCulture, DateTimeStyles.None, out _);
    }
    public static bool IsEmpty(object? value) => value is null;
    public static bool IsNull(object? value) => value is null;
    public static bool IsObject(object? value) => value is not null && value is not string && !value.GetType().IsValueType;
    public static bool IsScalar(object? value) => value is null || value is string || value.GetType().IsValueType;
    public static bool IsNumeric(object? value) =>
        double.TryParse(CStr(value), NumberStyles.Any, CultureInfo.CurrentCulture, out _);

    public static double Abs(double value) => Math.Abs(value);
    public static double Int(double value) => Math.Floor(value);
    public static double Fix(double value) => Math.Truncate(value);
    public static double Round(double value) => Math.Round(value, MidpointRounding.ToEven);
    public static double Round(double value, int digits) => Math.Round(value, digits, MidpointRounding.ToEven);
    public static double Sqr(double value) => Math.Sqrt(value);
    public static int Sgn(double value) => Math.Sign(value);
    public static double Sin(double value) => Math.Sin(value);
    public static double Cos(double value) => Math.Cos(value);
    public static double Tan(double value) => Math.Tan(value);
    public static double ATn(double value) => Math.Atan(value);
    public static double ATn2(double y, double x) => Math.Atan2(y, x);
    public static double ASin(double value) => Math.Asin(value);
    public static double ACos(double value) => Math.Acos(value);
    public static double Exp(double value) => Math.Exp(value);
    public static double Log(double value) => Math.Log(value);
    public static double Fraction(double value) => value - Math.Truncate(value);
    public static double Rnd() => Random.NextDouble();
    public static double Rnd(double number) => Random.NextDouble();
    public static void Randomize() => Random = new Random();
    public static void Randomize(object? seed) => Random = new Random(CInt(seed));

    public static double Val(object? value)
    {
        var text = CStr(value).TrimStart();
        var match = Regex.Match(text, @"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[Ee][+-]?\d+)?");
        return match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0d;
    }

    public static string Str(object? value)
    {
        if (value is null) return "0";
        if (IsNumeric(value))
        {
            var s = Convert.ToString(value, CultureInfo.CurrentCulture) ?? "0";
            return CDbl(value) >= 0 ? " " + s : s;
        }
        return CStr(value);
    }

    public static string Bin(long value) => Convert.ToString(value, 2);
    public static string Hex(long value) => value.ToString("X", CultureInfo.InvariantCulture);
    public static string Oct(long value) => Convert.ToString(value, 8);

    public static string Chr(int code) => Convert.ToChar(code).ToString();
    public static int Asc(object? value)
    {
        var text = CStr(value);
        return text.Length == 0 ? 0 : text[0];
    }

    public static int Instr(object? source, object? find) => Instr(1, source, find, 0);
    public static int Instr(int start, object? source, object? find) => Instr(start, source, find, 0);
    public static int Instr(int start, object? source, object? find, int compare)
    {
        var comparison = compare == 1 ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
        var text = CStr(source);
        var needle = CStr(find);
        var index = text.IndexOf(needle, Math.Max(0, start - 1), comparison);
        return index < 0 ? 0 : index + 1;
    }

    public static int StrComp(object? left, object? right, int compare = 0) =>
        string.Compare(CStr(left), CStr(right),
            compare == 1 ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture);

    public static string Replace(object? source, object? find, object? replacement, int start = 1, int count = -1, int compare = 0)
    {
        var text = CStr(source);
        var prefixLength = Math.Clamp(start - 1, 0, text.Length);
        var prefix = text[..prefixLength];
        var rest = text[prefixLength..];
        var needle = CStr(find);
        if (needle.Length == 0) return text;

        var comparison = compare == 1 ? StringComparison.CurrentCultureIgnoreCase : StringComparison.CurrentCulture;
        var sb = new StringBuilder();
        var index = 0;
        var replaced = 0;
        while (index < rest.Length && (count < 0 || replaced < count))
        {
            var pos = rest.IndexOf(needle, index, comparison);
            if (pos < 0) break;
            sb.Append(rest, index, pos - index);
            sb.Append(CStr(replacement));
            index = pos + needle.Length;
            replaced++;
        }
        sb.Append(rest[index..]);
        return prefix + sb;
    }

    public static string Space(int count) => new(' ', Math.Max(0, count));

    public static string String(int count, object? character)
    {
        char c;
        if (character is byte or short or int or long)
            c = Convert.ToChar(CInt(character));
        else
        {
            var text = CStr(character);
            c = text.Length == 0 ? '\0' : text[0];
        }
        return new string(c, Math.Max(0, count));
    }

    public static string[] Split(object? value, object? delimiter = null, int count = -1, int compare = 0)
    {
        var text = CStr(value);
        var sep = delimiter is null ? " " : CStr(delimiter);
        if (count == 0) return [];
        if (count < 0) return text.Split([sep], StringSplitOptions.None);
        return text.Split([sep], count, StringSplitOptions.None);
    }

    public static string Join(object? values, object? delimiter = null)
    {
        if (values is not System.Collections.IEnumerable enumerable || values is string) return CStr(values);
        var list = new List<string>();
        foreach (var item in enumerable) list.Add(CStr(item));
        return string.Join(delimiter is null ? " " : CStr(delimiter), list);
    }

    public static string Format(object? value, string? format = null)
    {
        if (string.IsNullOrEmpty(format)) return CStr(value);
        if (value is IFormattable formattable)
            return formattable.ToString(format, CultureInfo.CurrentCulture) ?? "";
        return CStr(value);
    }

    public static DateTime Now() => DateTime.Now;
    public static DateTime Today() => DateTime.Today;
    public static DateTime Date() => DateTime.Today;
    public static DateTime Time() => OaEpoch.Add(DateTime.Now.TimeOfDay);
    public static int Year(DateTime value) => value.Year;
    public static int Month(DateTime value) => value.Month;
    public static int Day(DateTime value) => value.Day;
    public static int Hour(DateTime value) => value.Hour;
    public static int Minute(DateTime value) => value.Minute;
    public static int Second(DateTime value) => value.Second;

    public static DateTime DateNumber(int year, int month, int day) =>
        new DateTime(year, 1, 1).AddMonths(month - 1).AddDays(day - 1);
    public static DateTime TimeNumber(int hour, int minute, int second) =>
        OaEpoch.AddHours(hour).AddMinutes(minute).AddSeconds(second);
    public static DateTime DateValue(object? value) => CDat(value).Date;
    public static DateTime TimeValue(object? value) => OaEpoch.Add(CDat(value).TimeOfDay);

    public static int Weekday(DateTime value, int firstDayOfWeek = 1)
    {
        var sundayBased = (int)value.DayOfWeek + 1;
        return ((sundayBased - firstDayOfWeek + 7) % 7) + 1;
    }

    public static string MonthName(int month, bool abbreviate = false)
    {
        var name = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month);
        return abbreviate && name.Length > 3 ? name[..3] : name;
    }

    public static string WeekdayName(int weekday, bool abbreviate = false, int firstDayOfWeek = 1)
    {
        var index = (weekday + firstDayOfWeek - 2) % 7;
        var name = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName((DayOfWeek)index);
        return abbreviate && name.Length > 3 ? name[..3] : name;
    }

    public static DateTime DateAdd(string interval, double number, DateTime date)
    {
        var n = Convert.ToInt32(number);
        return interval.ToLowerInvariant() switch
        {
            "yyyy" => date.AddYears(n),
            "q" => date.AddMonths(n * 3),
            "m" => date.AddMonths(n),
            "y" or "d" or "w" => date.AddDays(n),
            "ww" => date.AddDays(n * 7),
            "h" => date.AddHours(number),
            "n" => date.AddMinutes(number),
            "s" => date.AddSeconds(number),
            _ => throw new ArgumentException("Unsupported date interval: " + interval)
        };
    }

    public static long DateDiff(string interval, DateTime first, DateTime second)
    {
        var span = second - first;
        return interval.ToLowerInvariant() switch
        {
            "yyyy" => second.Year - first.Year,
            "q" => (second.Year - first.Year) * 4 + ((second.Month - 1) / 3) - ((first.Month - 1) / 3),
            "m" => (second.Year - first.Year) * 12 + second.Month - first.Month,
            "y" or "d" => (long)Math.Truncate(span.TotalDays),
            "w" or "ww" => (long)Math.Truncate(span.TotalDays / 7),
            "h" => (long)Math.Truncate(span.TotalHours),
            "n" => (long)Math.Truncate(span.TotalMinutes),
            "s" => (long)Math.Truncate(span.TotalSeconds),
            _ => throw new ArgumentException("Unsupported date interval: " + interval)
        };
    }

    public static int DatePart(string interval, DateTime date) => interval.ToLowerInvariant() switch
    {
        "yyyy" => date.Year,
        "q" => ((date.Month - 1) / 3) + 1,
        "m" => date.Month,
        "y" => date.DayOfYear,
        "d" => date.Day,
        "w" => Weekday(date),
        "ww" => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstDay, DayOfWeek.Sunday),
        "h" => date.Hour,
        "n" => date.Minute,
        "s" => date.Second,
        _ => throw new ArgumentException("Unsupported date interval: " + interval)
    };

    public static string Environ(object? name) => Environment.GetEnvironmentVariable(CStr(name)) ?? "";
    public static string CurDir() => Environment.CurrentDirectory;
    public static string CurDir(object? drive) => Environment.CurrentDirectory;

    public static int FreeFile()
    {
        lock (FileLock)
        {
            for (var i = 1; i <= 255; i++)
                if (!Files.ContainsKey(i)) return i;
        }
        throw new IOException("No free file numbers are available.");
    }

    public static void OpenFile(object? pathValue, string mode, int fileNumber)
    {
        var path = Path.GetFullPath(CStr(pathValue));
        lock (FileLock)
        {
            if (Files.ContainsKey(fileNumber)) throw new IOException("File number already open: " + fileNumber);
            FileState state = mode.ToLowerInvariant() switch
            {
                "input" => CreateReadState(path),
                "output" => CreateWriteState(path, false),
                "append" => CreateWriteState(path, true),
                "binary" or "random" => CreateBinaryState(path),
                _ => throw new IOException("Unsupported file mode: " + mode)
            };
            Files[fileNumber] = state;
        }
    }

    private static FileState CreateReadState(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return new FileState { Stream = stream, Reader = new StreamReader(stream, Encoding.Default, true, 1024, true) };
    }

    private static FileState CreateWriteState(string path, bool append)
    {
        var stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
        return new FileState { Stream = stream, Writer = new StreamWriter(stream, Encoding.Default, 1024, true) { AutoFlush = true } };
    }

    private static FileState CreateBinaryState(string path)
    {
        var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        return new FileState { Stream = stream };
    }

    private static FileState GetFile(int number) =>
        Files.TryGetValue(number, out var state) ? state : throw new IOException("File number is not open: " + number);

    public static void CloseFile(params int[] numbers)
    {
        lock (FileLock)
        {
            if (numbers.Length == 0) numbers = Files.Keys.ToArray();
            foreach (var number in numbers)
            {
                if (!Files.Remove(number, out var state)) continue;
                state.Writer?.Flush();
                state.Reader?.Dispose();
                state.Writer?.Dispose();
                state.Stream.Dispose();
            }
        }
    }

    public static void PrintFile(int number, params object?[] values)
    {
        var writer = GetFile(number).Writer ?? throw new IOException("File is not open for output.");
        writer.WriteLine(string.Concat(values.Select(CStr)));
    }

    public static void WriteFile(int number, params object?[] values)
    {
        var writer = GetFile(number).Writer ?? throw new IOException("File is not open for output.");
        var encoded = values.Select(v =>
        {
            if (v is null) return "#NULL#";
            if (v is DateTime dt) return "#" + dt.ToString(CultureInfo.InvariantCulture) + "#";
            if (v is string s) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        });
        writer.WriteLine(string.Join(",", encoded));
    }

    public static string LineInput(int number)
    {
        var reader = GetFile(number).Reader ?? throw new IOException("File is not open for input.");
        return reader.ReadLine() ?? "";
    }

    public static string Input(int number)
    {
        var reader = GetFile(number).Reader ?? throw new IOException("File is not open for input.");
        var sb = new StringBuilder();
        var quoted = false;
        while (true)
        {
            var n = reader.Read();
            if (n < 0) break;
            var c = (char)n;
            if (c == '"') { quoted = !quoted; continue; }
            if (!quoted && (c == ',' || c == '\n')) break;
            if (c != '\r') sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    public static bool EOF(int number)
    {
        var state = GetFile(number);
        if (state.Reader is not null) return state.Reader.Peek() < 0;
        return state.Stream.Position >= state.Stream.Length;
    }

    public static long LOF(int number) => GetFile(number).Stream.Length;
    public static long Seek(int number) => GetFile(number).Stream.Position + 1;

    public static void SeekSet(int number, long position)
    {
        var state = GetFile(number);
        state.Writer?.Flush();
        state.Reader?.DiscardBufferedData();
        state.Stream.Seek(Math.Max(0, position - 1), SeekOrigin.Begin);
    }

    public static long FileLen(object? fileName) => new FileInfo(CStr(fileName)).Length;
    public static DateTime FileDateTime(object? fileName) => File.GetLastWriteTime(CStr(fileName));
    public static int GetFileAttr(object? fileName) => (int)File.GetAttributes(CStr(fileName));
    public static void SetFileAttr(object? fileName, int attributes) => File.SetAttributes(CStr(fileName), (FileAttributes)attributes);
    public static void FileCopy(object? source, object? destination) => File.Copy(CStr(source), CStr(destination), true);
    public static void Kill(object? path) => File.Delete(CStr(path));
    public static void NameFile(object? oldPath, object? newPath) => File.Move(CStr(oldPath), CStr(newPath), true);
    public static void MkDir(object? path) => Directory.CreateDirectory(CStr(path));
    public static void RmDir(object? path) => Directory.Delete(CStr(path), false);
    public static void ChDir(object? path) => Environment.CurrentDirectory = Path.GetFullPath(CStr(path));

    public static string Dir(object? pattern = null)
    {
        if (pattern is not null)
        {
            var raw = CStr(pattern);
            var directory = Path.GetDirectoryName(raw);
            if (string.IsNullOrEmpty(directory)) directory = Environment.CurrentDirectory;
            var mask = Path.GetFileName(raw);
            if (string.IsNullOrEmpty(mask)) mask = "*";
            DirEnumerator?.Dispose();
            DirEnumerator = Directory.EnumerateFileSystemEntries(directory, mask)
                .Select(Path.GetFileName)
                .Where(x => x is not null)
                .Cast<string>()
                .GetEnumerator();
        }

        if (DirEnumerator is null || !DirEnumerator.MoveNext()) return "";
        return DirEnumerator.Current;
    }

    public static double Timer() => DateTime.Now.TimeOfDay.TotalSeconds;
    public static void Beep() => Console.Beep();

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
        if (step == 0) throw new InvalidOperationException("For Step cannot be zero.");

        if (step > 0)
            for (var i = start; i <= end; i += step) yield return i;
        else
            for (var i = start; i >= end; i += step) yield return i;
    }
}
""";
}
