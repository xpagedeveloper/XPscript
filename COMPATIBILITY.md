# LS Lite standard library compatibility

LS Lite targets the standard LotusScript language and runtime without Notes/Domino classes.

Implemented runtime areas:

- Core conversion and type inspection: CBool, CByte, CCur, CDat/CDate, CDbl, CInt, CLng, CSng, CStr, CVar, DataType, TypeName, IsArray, IsDate, IsEmpty, IsNull, IsNumeric, IsObject, IsScalar.
- String: Len, LenB, Left, Right, Mid, UCase, LCase, Trim, LTrim, RTrim, FullTrim, Chr, Asc, Instr, StrComp, Replace, Space, String, Split, Join, StrReverse, Format.
- Numeric: Abs, Int, Fix, Round, Sqr, Sgn, Sin, Cos, Tan, ATn, ATn2, ASin, ACos, Exp, Log, Fraction, Rnd, Randomize, Val, Str, Bin, Hex, Oct.
- Date/time: Now, Today, Date, Time, Year, Month, Day, Hour, Minute, Second, DateNumber, TimeNumber, DateValue, TimeValue, Weekday, WeekdayName, MonthName, DateAdd, DateDiff, DatePart, Timer.
- Environment: Environ, CurDir, Command.
- File: FreeFile, Open For Input/Output/Append/Binary/Random, Close, Print #, Write #, Line Input #, Input #, EOF, LOF, Seek, FileLen, FileDateTime, FileCopy, Kill, Name, MkDir, RmDir, ChDir, Dir, GetFileAttr, SetFileAttr.

Compatibility is implemented on top of .NET 10. Locale-sensitive behavior uses the current process culture where practical. Some legacy LotusScript edge cases, binary record serialization, platform-specific character set behavior, and Notes/Domino APIs are intentionally outside the current compatibility target.

The GitHub workflow compiles and executes samples/compatibility.ls on Windows to verify string, numeric, date/time and file behavior.
