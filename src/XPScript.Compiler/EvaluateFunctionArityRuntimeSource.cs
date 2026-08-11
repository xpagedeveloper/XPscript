namespace XPScript.Compiler;

internal static class EvaluateFunctionArityRuntimeSource
{
    public const string Code = """
internal static class XPScriptEvaluateFunctionArityRuntime
{
    private static readonly Dictionary<string, string> ExpectedArguments = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CStr"] = "1 argument",
        ["CInt"] = "1 argument",
        ["CLng"] = "1 argument",
        ["CDbl"] = "1 argument",
        ["CSng"] = "1 argument",
        ["CCur"] = "1 argument",
        ["CByte"] = "1 argument",
        ["CBool"] = "1 argument",
        ["CDate"] = "1 argument",
        ["CDat"] = "1 argument",
        ["CVar"] = "1 argument",
        ["TypeName"] = "1 argument",
        ["DataType"] = "1 argument",
        ["IsArray"] = "1 argument",
        ["IsDate"] = "1 argument",
        ["IsEmpty"] = "1 argument",
        ["IsNull"] = "1 argument",
        ["IsObject"] = "1 argument",
        ["IsScalar"] = "1 argument",
        ["IsNumeric"] = "1 argument",
        ["LBound"] = "1 or 2 arguments",
        ["UBound"] = "1 or 2 arguments",
        ["Len"] = "1 argument",
        ["Left"] = "2 arguments",
        ["Right"] = "2 arguments",
        ["Mid"] = "2 or 3 arguments",
        ["LCase"] = "1 argument",
        ["UCase"] = "1 argument",
        ["Trim"] = "1 argument",
        ["LTrim"] = "1 argument",
        ["RTrim"] = "1 argument",
        ["FullTrim"] = "1 argument",
        ["StrReverse"] = "1 argument",
        ["Instr"] = "2, 3 or 4 arguments",
        ["Replace"] = "3 to 6 arguments",
        ["Space"] = "1 argument",
        ["String"] = "2 arguments",
        ["Chr"] = "1 argument",
        ["Asc"] = "1 argument",
        ["Abs"] = "1 argument",
        ["Int"] = "1 argument",
        ["Fix"] = "1 argument",
        ["Round"] = "1 or 2 arguments",
        ["Sqr"] = "1 argument",
        ["Sgn"] = "1 argument",
        ["Sin"] = "1 argument",
        ["Cos"] = "1 argument",
        ["Tan"] = "1 argument",
        ["ATn"] = "1 argument",
        ["ATn2"] = "2 arguments",
        ["ASin"] = "1 argument",
        ["ACos"] = "1 argument",
        ["Exp"] = "1 argument",
        ["Log"] = "1 argument",
        ["Fraction"] = "1 argument",
        ["Val"] = "1 argument",
        ["Str"] = "1 argument",
        ["Bin"] = "1 argument",
        ["Hex"] = "1 argument",
        ["Oct"] = "1 argument",
        ["Year"] = "1 argument",
        ["Month"] = "1 argument",
        ["Day"] = "1 argument",
        ["Hour"] = "1 argument",
        ["Minute"] = "1 argument",
        ["Second"] = "1 argument",
        ["DateValue"] = "1 argument",
        ["TimeValue"] = "1 argument",
        ["DateNumber"] = "3 arguments",
        ["TimeNumber"] = "3 arguments",
        ["DateAdd"] = "3 arguments",
        ["DateDiff"] = "3 arguments",
        ["DatePart"] = "2 arguments"
    };

    public static object? Throw(string name, int actualCount)
    {
        if (ExpectedArguments.TryGetValue(name, out var expected))
        {
            var actual = actualCount == 1 ? "1 argument" : actualCount.ToString(CultureInfo.InvariantCulture) + " arguments";
            throw new XPScriptRuntimeException(5,
                $"Evaluate function {name} expects {expected}, but received {actual}.");
        }

        throw new XPScriptRuntimeException(5,
            "Function is not available inside Evaluate: " + name);
    }
}
""";
}
