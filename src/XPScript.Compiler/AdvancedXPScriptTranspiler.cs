using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class AdvancedXPScriptTranspiler
{
    private static readonly Regex SourceLineMarkerPattern = new(
        @"__XPSOURCE_(?<line>\d+)_(?<source>[0-9A-F]+)\(\)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["String"] = "string", ["Integer"] = "int", ["Long"] = "long", ["Double"] = "double",
        ["Single"] = "float", ["Boolean"] = "bool", ["Byte"] = "byte", ["Currency"] = "decimal",
        ["Date"] = "DateTime", ["Variant"] = "dynamic", ["Object"] = "object"
    };

    private static readonly string[] RuntimeFunctions =
    [
        "Len", "LenB", "Left", "Right", "Mid", "UCase", "LCase", "Trim", "LTrim", "RTrim", "FullTrim", "StrReverse",
        "CStr", "CByte", "CInt", "CLng", "CDbl", "CSng", "CCur", "CBool", "CVar", "CDat", "CDate", "DataType", "TypeName",
        "IsArray", "IsDate", "IsEmpty", "IsNull", "IsObject", "IsScalar", "IsNumeric", "Abs", "Int", "Fix", "Round", "Sqr", "Sgn",
        "Sin", "Cos", "Tan", "ATn", "ATn2", "ASin", "ACos", "Exp", "Log", "Fraction", "Rnd", "Val", "Str", "Bin", "Hex", "Oct",
        "Chr", "Asc", "Instr", "StrComp", "Replace", "Space", "String", "Split", "Join", "Format", "Now", "Today", "Date", "Time",
        "Year", "Month", "Day", "Hour", "Minute", "Second", "DateNumber", "TimeNumber", "DateValue", "TimeValue", "Weekday", "MonthName",
        "WeekdayName", "DateAdd", "DateDiff", "DatePart", "Environ", "CurDir", "FreeFile", "EOF", "LOF", "Seek", "FileLen", "FileDateTime",
        "GetFileAttr", "Dir", "Timer", "Command", "InputBox", "MsgBox"
    ];

    private static readonly string[] ZeroArgRuntimeFunctions = ["Now", "Today", "Date", "Time", "FreeFile", "Timer", "Command", "CurDir"];

    private sealed class ClassInfo
    {
        public required string Name { get; init; }
        public string? BaseName { get; set; }
        public string Visibility { get; set; } = "private";
        public Dictionary<string, FieldInfo> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, PropertyInfo> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FieldInfo
    {
        public required string Name { get; init; }
        public required string XPScriptType { get; init; }
        public bool IsList { get; init; }
        public string Visibility { get; init; } = "private";
    }

    private sealed class PropertyInfo
    {
        public required string Name { get; init; }
        public required string XPScriptType { get; set; }
        public string Visibility { get; set; } = "public";
        public bool HasGet { get; set; }
        public bool HasSet { get; set; }
        public bool HasParameters { get; set; }
    }

    private enum ProcedureKind
    {
        None,
        Sub,
        Function,
        Constructor,
        Destructor,
        PropertyGet,
        PropertySet
    }

    private sealed record ForAllContext(string Alias, string ElementType, bool IsListAlias);

    private readonly Dictionary<string, ClassInfo> _classes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _variableTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _objectVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _listVariables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<ForAllContext> _forAll = new();

    private string? _currentClass;
    private string? _currentProcedure;
    private string? _currentReturnType;
    private string? _currentProperty;
    private ProcedureKind _procedureKind;
    private int _indent;

    public string Transpile(string source, string sourceName)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        AnalyzeSource(lines);
        var entryPoint = FindEntryPoint(lines);

        var body = new StringBuilder();
        ResetState();
        SourceMap.Location? currentSourceLocation = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var original = lines[i];
            var line = StripComment(original).Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var sourceMarker = SourceLineMarkerPattern.Match(line);
            if (sourceMarker.Success)
                currentSourceLocation = DecodeSourceLineMarker(sourceMarker);

            try
            {
                EmitLine(body, line);
            }
            catch (CompilerException ex)
            {
                var diagnosticSource = currentSourceLocation?.SourcePath ?? sourceName;
                var diagnosticLine = currentSourceLocation?.Line ?? i + 1;
                throw new CompilerException(
                    $"{diagnosticSource}({diagnosticLine}): {ex.Message}" +
                    Environment.NewLine +
                    $"  {original.Trim()}");
            }
        }

        if (_currentProcedure is not null)
            throw new CompilerException($"Missing procedure terminator for '{_currentProcedure}'.");
        if (_currentClass is not null)
            throw new CompilerException($"Missing End Class for '{_currentClass}'.");

        return $$"""
// Generated by XPScript Compiler.
// Source: {{EscapeComment(sourceName)}}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

internal static class Program
{
    public static void Main(string[] args)
    {
        XPScriptRuntime.SetArgs(args);
        Script.{{entryPoint}}();
    }
}

internal static class Script
{
{{body}}
}

{{XPScriptObjectRuntimeSource.Code}}

{{XPScriptListRuntimeSource.Code}}

internal static class LSForAllRuntime
{
    public static System.Collections.IEnumerable Enumerate(object? value)
    {
        if (value is null) yield break;
        if (value is string) throw new XPScriptRuntimeException(13, "ForAll requires a list, array, or enumerable value.");
        if (value is LSArray array)
        {
            if (!array.IsAllocated) yield break;
            if (array.Rank != 1) throw new XPScriptRuntimeException(13, "ForAll currently supports one-dimensional arrays.");
            for (var i = array.LBound(); i <= array.UBound(); i++) yield return array.Get(new object?[] { i });
            yield break;
        }
        if (value is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable) yield return item;
            yield break;
        }
        throw new XPScriptRuntimeException(13, "ForAll requires a list, array, or enumerable value.");
    }
}

{{XPScriptRuntimeSource.Code}}
""";
    }

    private static SourceMap.Location DecodeSourceLineMarker(Match marker)
    {
        try
        {
            var source = System.Text.Encoding.UTF8.GetString(Convert.FromHexString(marker.Groups["source"].Value));
            var line = int.Parse(marker.Groups["line"].Value, System.Globalization.CultureInfo.InvariantCulture);
            return new SourceMap.Location(source, line, "");
        }
        catch (Exception exception)
        {
            throw new CompilerException("Invalid generated XPScript source mapping marker: " + exception.Message);
        }
    }

    private void ResetState()
    {
        _currentClass = null;
        _currentProcedure = null;
        _currentReturnType = null;
        _currentProperty = null;
        _procedureKind = ProcedureKind.None;
        _variableTypes.Clear();
        _objectVariables.Clear();
        _listVariables.Clear();
        _forAll.Clear();
        _indent = 1;
    }

    private void AnalyzeSource(string[] lines)
    {
        _classes.Clear();
        ClassInfo? current = null;

        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var classMatch = Regex.Match(
                line,
                @"^(?:(Public|Private)\s+)?Class\s+([A-Za-z_]\w*)(?:\s+As\s+([A-Za-z_]\w*))?\s*$",
                RegexOptions.IgnoreCase);

            if (classMatch.Success)
            {
                var info = new ClassInfo
                {
                    Name = classMatch.Groups[2].Value,
                    BaseName = string.IsNullOrWhiteSpace(classMatch.Groups[3].Value) ? null : classMatch.Groups[3].Value,
                    Visibility = NormalizeVisibility(classMatch.Groups[1].Value, "private")
                };
                _classes[info.Name] = info;
                current = info;
                continue;
            }

            if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase))
            {
                current = null;
                continue;
            }

            if (current is null)
                continue;

            var propertyMatch = Regex.Match(
                line,
                @"^(?:(Public|Private)\s+)?Property\s+(Get|Set)\s+([A-Za-z_]\w*)\s*(?:\((.*)\))?\s*(?:As\s+([A-Za-z_]\w*))?\s*$",
                RegexOptions.IgnoreCase);

            if (propertyMatch.Success)
            {
                var name = propertyMatch.Groups[3].Value;
                var xpscriptType = string.IsNullOrWhiteSpace(propertyMatch.Groups[5].Value) ? "Variant" : propertyMatch.Groups[5].Value;
                if (!current.Properties.TryGetValue(name, out var property))
                {
                    property = new PropertyInfo { Name = name, XPScriptType = xpscriptType };
                    current.Properties[name] = property;
                }

                property.XPScriptType = xpscriptType;
                property.Visibility = NormalizeVisibility(propertyMatch.Groups[1].Value, property.Visibility);
                property.HasParameters |= !string.IsNullOrWhiteSpace(propertyMatch.Groups[4].Value);
                if (propertyMatch.Groups[2].Value.Equals("Get", StringComparison.OrdinalIgnoreCase))
                    property.HasGet = true;
                else
                    property.HasSet = true;
                continue;
            }

            var fieldMatch = Regex.Match(
                line,
                @"^(?:(Public|Private)\s+)?([A-Za-z_]\w*)\s+(?:(List)\s+)?As\s+([A-Za-z_]\w*)\s*$",
                RegexOptions.IgnoreCase);

            if (fieldMatch.Success)
            {
                current.Fields[fieldMatch.Groups[2].Value] = new FieldInfo
                {
                    Name = fieldMatch.Groups[2].Value,
                    XPScriptType = fieldMatch.Groups[4].Value,
                    IsList = !string.IsNullOrWhiteSpace(fieldMatch.Groups[3].Value),
                    Visibility = NormalizeVisibility(fieldMatch.Groups[1].Value, "private")
                };
            }
        }
    }

    private void EmitLine(StringBuilder sb, string line)
    {
        if (Regex.IsMatch(line, @"^Option\s+(Declare|Public|Private|Compare|Base)\b", RegexOptions.IgnoreCase))
            return;

        if (_currentProcedure is null)
        {
            if (TryEmitClassBoundary(sb, line))
                return;

            if (_currentClass is not null)
            {
                if (TryEmitClassField(sb, line))
                    return;
                if (TryBeginProperty(sb, line))
                    return;
            }

            if (TryBeginProcedure(sb, line))
                return;
        }

        if (_currentProcedure is not null)
        {
            if (TryEndProcedure(sb, line))
                return;

            EmitStatement(sb, line);
            return;
        }

        throw new CompilerException($"Unsupported module/class declaration: {line}");
    }

    private bool TryEmitClassBoundary(StringBuilder sb, string line)
    {
        var classMatch = Regex.Match(
            line,
            @"^(?:(Public|Private)\s+)?Class\s+([A-Za-z_]\w*)(?:\s+As\s+([A-Za-z_]\w*))?\s*$",
            RegexOptions.IgnoreCase);

        if (classMatch.Success)
        {
            if (_currentClass is not null)
                throw new CompilerException("Classes cannot be nested.");

            var name = classMatch.Groups[2].Value;
            var info = _classes[name];
            var baseType = string.IsNullOrWhiteSpace(info.BaseName) ? "LSObjectBase" : info.BaseName;
            Write(sb, $"{info.Visibility} class {name} : {baseType}");
            Write(sb, "{");
            _indent++;
            _currentClass = name;
            return true;
        }

        if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase))
        {
            if (_currentClass is null)
                throw new CompilerException("Unexpected End Class.");

            EmitPropertyWrappers(sb, _classes[_currentClass]);
            _indent--;
            Write(sb, "}");
            _currentClass = null;
            return true;
        }

        return false;
    }

    private bool TryEmitClassField(StringBuilder sb, string line)
    {
        var fieldMatch = Regex.Match(
            line,
            @"^(?:(Public|Private)\s+)?([A-Za-z_]\w*)\s+(?:(List)\s+)?As\s+([A-Za-z_]\w*)\s*$",
            RegexOptions.IgnoreCase);

        if (!fieldMatch.Success)
            return false;

        var name = fieldMatch.Groups[2].Value;
        var xpscriptType = fieldMatch.Groups[4].Value;
        var visibility = NormalizeVisibility(fieldMatch.Groups[1].Value, "private");

        if (!string.IsNullOrWhiteSpace(fieldMatch.Groups[3].Value))
        {
            Write(sb, $"{visibility} LSList<{MapType(xpscriptType)}> {name} = new();");
            return true;
        }

        var type = MapType(xpscriptType);
        Write(sb, $"{visibility} {type} {name} = {DefaultValue(type)};");
        return true;
    }

    private bool TryBeginProperty(StringBuilder sb, string line)
    {
        var match = Regex.Match(
            line,
            @"^(?:(Public|Private)\s+)?Property\s+(Get|Set)\s+([A-Za-z_]\w*)\s*(?:\((.*)\))?\s*(?:As\s+([A-Za-z_]\w*))?\s*$",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return false;
        if (_currentClass is null)
            throw new CompilerException("Properties are currently supported only inside classes.");
        if (!string.IsNullOrWhiteSpace(match.Groups[4].Value))
            throw new CompilerException("Parameterized properties are not supported yet.");

        var name = match.Groups[3].Value;
        var xpscriptType = string.IsNullOrWhiteSpace(match.Groups[5].Value) ? "Variant" : match.Groups[5].Value;
        var type = MapType(xpscriptType);
        _currentProcedure = name;
        _currentProperty = name;
        _currentReturnType = type;
        _variableTypes.Clear();
        _objectVariables.Clear();
        _listVariables.Clear();

        if (match.Groups[2].Value.Equals("Get", StringComparison.OrdinalIgnoreCase))
        {
            _procedureKind = ProcedureKind.PropertyGet;
            Write(sb, $"private {type} __get_{name}()");
            Write(sb, "{");
            _indent++;
            Write(sb, $"{type} __result = {DefaultValue(type)};");
        }
        else
        {
            _procedureKind = ProcedureKind.PropertySet;
            RegisterVariable(name, xpscriptType, false);
            Write(sb, $"private void __set_{name}({type} {name})");
            Write(sb, "{");
            _indent++;
        }

        return true;
    }

    private bool TryBeginProcedure(StringBuilder sb, string line)
    {
        var sub = Regex.Match(
            line,
            @"^(?:(Public|Private)\s+)?Sub\s+([A-Za-z_]\w*)\s*\((.*)\)\s*$",
            RegexOptions.IgnoreCase);

        if (sub.Success)
        {
            var name = sub.Groups[2].Value;
            var args = ParseArguments(sub.Groups[3].Value);
            var visibility = NormalizeVisibility(sub.Groups[1].Value, "public");

            _currentProcedure = name;
            _currentReturnType = null;
            _variableTypes.Clear();
            _objectVariables.Clear();
            _listVariables.Clear();
            RegisterArguments(sub.Groups[3].Value);

            if (_currentClass is not null && name.Equals("New", StringComparison.OrdinalIgnoreCase))
            {
                _procedureKind = ProcedureKind.Constructor;
                Write(sb, $"public {_currentClass}({args})");
            }
            else if (_currentClass is not null && name.Equals("Delete", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(sub.Groups[3].Value))
                    throw new CompilerException("Sub Delete cannot have parameters.");
                _procedureKind = ProcedureKind.Destructor;
                Write(sb, "public override void __Delete()");
            }
            else
            {
                _procedureKind = ProcedureKind.Sub;
                var modifier = _currentClass is null ? "static " : "";
                Write(sb, $"{visibility} {modifier}void {name}({args})");
            }

            Write(sb, "{");
            _indent++;
            return true;
        }

        var fn = Regex.Match(
            line,
            @"^(?:(Public|Private)\s+)?Function\s+([A-Za-z_]\w*)\s*\((.*)\)\s*(?:As\s+([A-Za-z_]\w*))?\s*$",
            RegexOptions.IgnoreCase);

        if (!fn.Success)
            return false;

        var nameFn = fn.Groups[2].Value;
        var xpscriptReturnType = string.IsNullOrWhiteSpace(fn.Groups[4].Value) ? "Variant" : fn.Groups[4].Value;
        var returnType = MapType(xpscriptReturnType);
        var arguments = ParseArguments(fn.Groups[3].Value);
        var visibilityFn = NormalizeVisibility(fn.Groups[1].Value, "public");

        _currentProcedure = nameFn;
        _currentReturnType = returnType;
        _procedureKind = ProcedureKind.Function;
        _variableTypes.Clear();
        _objectVariables.Clear();
        _listVariables.Clear();
        RegisterArguments(fn.Groups[3].Value);

        var modifierFn = _currentClass is null ? "static " : "";
        Write(sb, $"{visibilityFn} {modifierFn}{returnType} {nameFn}({arguments})");
        Write(sb, "{");
        _indent++;
        Write(sb, $"{returnType} __result = {DefaultValue(returnType)};");
        return true;
    }

    private bool TryEndProcedure(StringBuilder sb, string line)
    {
        if (_procedureKind is ProcedureKind.PropertyGet or ProcedureKind.PropertySet)
        {
            if (!Regex.IsMatch(line, @"^End\s+Property$", RegexOptions.IgnoreCase))
                return false;

            if (_procedureKind == ProcedureKind.PropertyGet)
                Write(sb, "return __result;");
            _indent--;
            Write(sb, "}");
            ClearProcedureState();
            return true;
        }

        if (!Regex.IsMatch(line, @"^End\s+(Sub|Function)$", RegexOptions.IgnoreCase))
            return false;

        if (_procedureKind == ProcedureKind.Function)
            Write(sb, "return __result;");
        else if (_procedureKind == ProcedureKind.Destructor)
            Write(sb, "base.__Delete();");

        _indent--;
        Write(sb, "}");
        ClearProcedureState();
        return true;
    }

    private void ClearProcedureState()
    {
        _currentProcedure = null;
        _currentReturnType = null;
        _currentProperty = null;
        _procedureKind = ProcedureKind.None;
        _variableTypes.Clear();
        _objectVariables.Clear();
        _listVariables.Clear();
        _forAll.Clear();
    }

    private void EmitStatement(StringBuilder sb, string line)
    {
        if (TryEmitConst(sb, line)) return;
        if (TryEmitDim(sb, line)) return;

        if (TryEmitSingleLineIf(sb, line)) return;

        var ifMatch = Regex.Match(line, @"^If\s+(.+)\s+Then$", RegexOptions.IgnoreCase);
        if (ifMatch.Success) { Write(sb, $"if ({TransformCondition(ifMatch.Groups[1].Value)})"); Write(sb, "{"); _indent++; return; }
        var elseifInline = Regex.Match(line, @"^ElseIf\s+(.+?)\s+Then\s+(.+)$", RegexOptions.IgnoreCase);
        if (elseifInline.Success)
        {
            _indent--;
            Write(sb, "}");
            Write(sb, $"else if ({TransformCondition(elseifInline.Groups[1].Value)})");
            Write(sb, "{");
            _indent++;
            EmitStatement(sb, elseifInline.Groups[2].Value.Trim());
            return;
        }
        var elseif = Regex.Match(line, @"^ElseIf\s+(.+)\s+Then$", RegexOptions.IgnoreCase);
        if (elseif.Success) { _indent--; Write(sb, "}"); Write(sb, $"else if ({TransformCondition(elseif.Groups[1].Value)})"); Write(sb, "{"); _indent++; return; }
        if (Regex.IsMatch(line, @"^Else$", RegexOptions.IgnoreCase)) { _indent--; Write(sb, "}"); Write(sb, "else"); Write(sb, "{"); _indent++; return; }
        if (Regex.IsMatch(line, @"^End\s+If$", RegexOptions.IgnoreCase)) { _indent--; Write(sb, "}"); return; }

        var forAll = Regex.Match(line, @"^ForAll\s+([A-Za-z_]\w*)\s+In\s+(.+)$", RegexOptions.IgnoreCase);
        if (forAll.Success)
        {
            var alias = forAll.Groups[1].Value;
            var sourceName = forAll.Groups[2].Value.Trim();
            var list = ResolveList(sourceName);
            if (list is not null)
            {
                Write(sb, $"foreach (var {alias} in {list.Value.Expression}.Aliases())");
                Write(sb, "{");
                _indent++;
                _forAll.Push(new ForAllContext(alias, list.Value.ElementType, true));
                return;
            }

            var isDeclaredValue = _variableTypes.ContainsKey(sourceName) || _objectVariables.ContainsKey(sourceName);
            var isMemberExpression = Regex.IsMatch(sourceName, @"^[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)+$");
            if (!isDeclaredValue && !isMemberExpression)
                throw new CompilerException($"ForAll source '{sourceName}' is not a declared list, array, or enumerable variable.");

            Write(sb, $"foreach (dynamic {alias} in LSForAllRuntime.Enumerate({TransformExpression(sourceName)}))");
            Write(sb, "{");
            _indent++;
            _forAll.Push(new ForAllContext(alias, "Variant", false));
            return;
        }
        if (Regex.IsMatch(line, @"^End\s+ForAll$", RegexOptions.IgnoreCase))
        {
            if (_forAll.Count == 0) throw new CompilerException("Unexpected End ForAll.");
            _forAll.Pop(); _indent--; Write(sb, "}"); return;
        }
        if (Regex.IsMatch(line, @"^Exit\s+ForAll$", RegexOptions.IgnoreCase))
        {
            if (_forAll.Count == 0) throw new CompilerException("Exit ForAll is valid only inside ForAll.");
            Write(sb, "break;"); return;
        }

        var forMatch = Regex.Match(line, @"^For\s+([A-Za-z_]\w*)\s*=\s*(.+?)\s+To\s+(.+?)(?:\s+Step\s+(.+))?$", RegexOptions.IgnoreCase);
        if (forMatch.Success)
        {
            var variable = forMatch.Groups[1].Value;
            var start = TransformExpression(forMatch.Groups[2].Value);
            var end = TransformExpression(forMatch.Groups[3].Value);
            var step = string.IsNullOrWhiteSpace(forMatch.Groups[4].Value) ? "1" : TransformExpression(forMatch.Groups[4].Value);
            Write(sb, $"foreach (var __forValue in XPScriptRuntime.Range({start}, {end}, {step}))"); Write(sb, "{"); _indent++;
            Write(sb, $"{variable} = {ConvertForValue(variable, "__forValue")};"); return;
        }
        if (Regex.IsMatch(line, @"^Next(?:\s+[A-Za-z_]\w*)?$", RegexOptions.IgnoreCase)) { _indent--; Write(sb, "}"); return; }

        var whileMatch = Regex.Match(line, @"^While\s+(.+)$", RegexOptions.IgnoreCase);
        if (whileMatch.Success) { Write(sb, $"while ({TransformCondition(whileMatch.Groups[1].Value)})"); Write(sb, "{"); _indent++; return; }
        if (Regex.IsMatch(line, @"^Wend$", RegexOptions.IgnoreCase)) { _indent--; Write(sb, "}"); return; }
        if (Regex.IsMatch(line, @"^Do$", RegexOptions.IgnoreCase)) { Write(sb, "while (true)"); Write(sb, "{"); _indent++; return; }
        var doWhile = Regex.Match(line, @"^Do\s+While\s+(.+)$", RegexOptions.IgnoreCase);
        if (doWhile.Success) { Write(sb, $"while ({TransformCondition(doWhile.Groups[1].Value)})"); Write(sb, "{"); _indent++; return; }
        var doUntil = Regex.Match(line, @"^Do\s+Until\s+(.+)$", RegexOptions.IgnoreCase);
        if (doUntil.Success) { Write(sb, $"while (!({TransformCondition(doUntil.Groups[1].Value)}))"); Write(sb, "{"); _indent++; return; }
        if (Regex.IsMatch(line, @"^Loop$", RegexOptions.IgnoreCase)) { _indent--; Write(sb, "}"); return; }
        var loopWhile = Regex.Match(line, @"^Loop\s+While\s+(.+)$", RegexOptions.IgnoreCase);
        if (loopWhile.Success) { Write(sb, $"if (!({TransformCondition(loopWhile.Groups[1].Value)})) break;"); _indent--; Write(sb, "}"); return; }
        var loopUntil = Regex.Match(line, @"^Loop\s+Until\s+(.+)$", RegexOptions.IgnoreCase);
        if (loopUntil.Success) { Write(sb, $"if ({TransformCondition(loopUntil.Groups[1].Value)}) break;"); _indent--; Write(sb, "}"); return; }
        if (Regex.IsMatch(line, @"^Exit\s+(For|Do|While)$", RegexOptions.IgnoreCase)) { Write(sb, "break;"); return; }
        if (Regex.IsMatch(line, @"^Exit\s+Sub$", RegexOptions.IgnoreCase)) { Write(sb, "return;"); return; }
        if (Regex.IsMatch(line, @"^Exit\s+Function$", RegexOptions.IgnoreCase)) { Write(sb, "return __result;"); return; }

        if (TryEmitErase(sb, line)) return;
        if (TryEmitDelete(sb, line)) return;
        if (TryEmitSet(sb, line)) return;
        if (TryEmitFileStatement(sb, line)) return;

        var randomize = Regex.Match(line, @"^Randomize(?:\s+(.+))?$", RegexOptions.IgnoreCase);
        if (randomize.Success) { Write(sb, string.IsNullOrWhiteSpace(randomize.Groups[1].Value) ? "XPScriptRuntime.Randomize();" : $"XPScriptRuntime.Randomize({TransformExpression(randomize.Groups[1].Value)});"); return; }
        if (Regex.IsMatch(line, @"^Beep$", RegexOptions.IgnoreCase)) { Write(sb, "XPScriptRuntime.Beep();"); return; }

        var ret = Regex.Match(line, @"^Return(?:\s+(.+))?$", RegexOptions.IgnoreCase);
        if (ret.Success) { Write(sb, string.IsNullOrWhiteSpace(ret.Groups[1].Value) ? "return;" : $"return {TransformExpression(ret.Groups[1].Value)};"); return; }
        var print = Regex.Match(line, @"^Print\s+(.+)$", RegexOptions.IgnoreCase);
        if (print.Success) { Write(sb, $"Console.WriteLine(XPScriptRuntime.PrintText({TransformExpression(print.Groups[1].Value)}));"); return; }

        var call = Regex.Match(line, @"^Call\s+(.+?)\s*\((.*)\)$", RegexOptions.IgnoreCase);
        if (call.Success) { Write(sb, $"{TransformCallableTarget(call.Groups[1].Value.Trim())}({TransformArgumentList(call.Groups[2].Value)});"); return; }

        var listAssignment = Regex.Match(line, @"^(?:Let\s+)?([A-Za-z_]\w*)\s*\((.+)\)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
        if (listAssignment.Success)
        {
            var list = ResolveList(listAssignment.Groups[1].Value);
            if (list is not null)
            {
                Write(sb, $"{list.Value.Expression}[{TransformExpression(listAssignment.Groups[2].Value)}] = {TransformExpression(listAssignment.Groups[3].Value)};");
                return;
            }
        }

        var assignment = Regex.Match(line, @"^(?:Let\s+)?([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
        if (assignment.Success)
        {
            var lhs = assignment.Groups[1].Value;
            var rhs = TransformExpression(assignment.Groups[2].Value);
            var alias = FindForAllAlias(lhs);
            if (alias is not null) { Write(sb, $"{alias.Alias}.Value = {rhs};"); return; }
            if (_procedureKind == ProcedureKind.Function && lhs.Equals(_currentProcedure, StringComparison.OrdinalIgnoreCase)) { Write(sb, $"__result = {rhs};"); return; }
            if (_procedureKind == ProcedureKind.PropertyGet && lhs.Equals(_currentProperty, StringComparison.OrdinalIgnoreCase)) { Write(sb, $"__result = {rhs};"); return; }
            if (IsObjectReferenceTarget(lhs)) throw new CompilerException("Object references must be assigned with Set.");
            Write(sb, $"{TransformAssignmentTarget(lhs)} = {rhs};"); return;
        }

        var bareCall = Regex.Match(line, @"^([A-Za-z_]\w*)\s*\((.*)\)$", RegexOptions.IgnoreCase);
        if (bareCall.Success)
        {
            var name = bareCall.Groups[1].Value; var args = TransformArgumentList(bareCall.Groups[2].Value);
            Write(sb, RuntimeFunctions.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)) ? $"XPScriptRuntime.{name}({args});" : $"{name}({args});");
            return;
        }

        if (Regex.IsMatch(line, @"^[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+\s*\(.*\)$", RegexOptions.IgnoreCase))
        {
            Write(sb, $"{TransformExpression(line)};"); return;
        }

        throw new CompilerException($"Unsupported statement: {line}");
    }

    private bool TryEmitConst(StringBuilder sb, string line)
    {
        var match = Regex.Match(
            line,
            @"^Const\s+([A-Za-z_]\w*)\s*(?:As\s+(String|Integer|Long|Double|Boolean))?\s*=\s*(.+)$",
            RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var name = match.Groups[1].Value;
        var rawValue = match.Groups[3].Value.Trim();
        var xpscriptType = string.IsNullOrWhiteSpace(match.Groups[2].Value)
            ? InferConstType(rawValue)
            : match.Groups[2].Value;
        var mapped = MapType(xpscriptType);
        RegisterVariable(name, xpscriptType, false);
        Write(sb, $"const {mapped} {name} = {TransformExpression(rawValue)};");
        return true;
    }

    private static string InferConstType(string rawValue)
    {
        var value = rawValue.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') return "String";
        if (value.Equals("True", StringComparison.OrdinalIgnoreCase) || value.Equals("False", StringComparison.OrdinalIgnoreCase)) return "Boolean";
        if (Regex.IsMatch(value, @"^[+-]?\d+$"))
        {
            if (int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _)) return "Integer";
            if (long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _)) return "Long";
        }
        if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)) return "Double";
        throw new CompilerException("Const without As requires a String, Integer, Long, Double, or Boolean literal.");
    }

    private bool TryEmitDim(StringBuilder sb, string line)
    {
        var list = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+List\s+As\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
        if (list.Success)
        {
            var name = list.Groups[1].Value; var type = MapType(list.Groups[2].Value);
            _listVariables[name] = type; Write(sb, $"LSList<{type}> {name} = new();"); return true;
        }

        var newObject = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+([A-Za-z_]\w*)\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
        if (newObject.Success)
        {
            var name = newObject.Groups[1].Value; var className = newObject.Groups[2].Value;
            EnsureClassType(className); _objectVariables[name] = className; _variableTypes[name] = $"LSRef<{className}>";
            Write(sb, $"LSRef<{className}> {name} = LSRef<{className}>.Create(new {className}({TransformArgumentList(newObject.Groups[3].Value)}));"); return true;
        }

        var dim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s*(?:As\s+([A-Za-z_]\w*))?$", RegexOptions.IgnoreCase);
        if (!dim.Success) return false;
        var variable = dim.Groups[1].Value; var xpscriptType = string.IsNullOrWhiteSpace(dim.Groups[2].Value) ? "Variant" : dim.Groups[2].Value;
        var mapped = MapType(xpscriptType); RegisterVariable(variable, xpscriptType, false); Write(sb, $"{mapped} {variable} = {DefaultValue(mapped)};"); return true;
    }

    private bool TryEmitSingleLineIf(StringBuilder sb, string line)
    {
        var match = Regex.Match(line, @"^If\s+(.+?)\s+Then\s+(.+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var condition = match.Groups[1].Value.Trim();
        var tail = match.Groups[2].Value.Trim();
        var elseIndex = FindTopLevelElse(tail);
        var trueStatement = elseIndex >= 0 ? tail[..elseIndex].Trim() : tail;
        var falseStatement = elseIndex >= 0 ? tail[(elseIndex + 4)..].Trim() : null;
        if (trueStatement.Length == 0)
            throw new CompilerException("Single-line If requires a statement after Then.");
        if (falseStatement is not null && falseStatement.Length == 0)
            throw new CompilerException("Single-line If Else requires a statement after Else.");

        Write(sb, $"if ({TransformCondition(condition)})");
        Write(sb, "{");
        _indent++;
        EmitStatement(sb, trueStatement);
        _indent--;
        Write(sb, "}");

        if (falseStatement is not null)
        {
            Write(sb, "else");
            Write(sb, "{");
            _indent++;
            EmitStatement(sb, falseStatement);
            _indent--;
            Write(sb, "}");
        }
        return true;
    }

    private static int FindTopLevelElse(string value)
    {
        var inString = false;
        var depth = 0;
        for (var i = 0; i <= value.Length - 4; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '(') { depth++; continue; }
            if (c == ')') { depth = Math.Max(0, depth - 1); continue; }
            if (depth != 0) continue;
            if (!value.AsSpan(i, 4).Equals("Else", StringComparison.OrdinalIgnoreCase)) continue;
            var beforeOk = i == 0 || char.IsWhiteSpace(value[i - 1]);
            var after = i + 4;
            var afterOk = after >= value.Length || char.IsWhiteSpace(value[after]);
            if (beforeOk && afterOk) return i;
        }
        return -1;
    }

    private bool TryEmitErase(StringBuilder sb, string line)
    {
        var element = Regex.Match(line, @"^Erase\s+([A-Za-z_]\w*)\s*\((.+)\)\s*$", RegexOptions.IgnoreCase);
        if (element.Success)
        {
            var list = ResolveList(element.Groups[1].Value); if (list is null) return false;
            Write(sb, $"{list.Value.Expression}.Erase({TransformExpression(element.Groups[2].Value)});"); return true;
        }
        var whole = Regex.Match(line, @"^Erase\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
        if (!whole.Success) return false;
        var wholeList = ResolveList(whole.Groups[1].Value); if (wholeList is null) return false;
        Write(sb, $"{wholeList.Value.Expression}.Clear();"); return true;
    }

    private bool TryEmitDelete(StringBuilder sb, string line)
    {
        var match = Regex.Match(line, @"^Delete\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        var target = TransformObjectReferenceTarget(match.Groups[1].Value);
        if (target is null) throw new CompilerException($"Delete requires an object reference: {match.Groups[1].Value}");
        Write(sb, $"{target}.Delete();"); return true;
    }

    private bool TryEmitSet(StringBuilder sb, string line)
    {
        var match = Regex.Match(line, @"^Set\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        var lhsRaw = match.Groups[1].Value; var lhs = TransformObjectReferenceTarget(lhsRaw);
        if (lhs is null) throw new CompilerException($"Set target is not an object reference: {lhsRaw}");
        var targetClass = ResolveObjectReferenceClass(lhsRaw) ?? throw new CompilerException($"Cannot determine object type for Set target: {lhsRaw}");
        var rhsRaw = match.Groups[2].Value.Trim();
        if (rhsRaw.Equals("Nothing", StringComparison.OrdinalIgnoreCase)) { Write(sb, $"{lhs} = new LSRef<{targetClass}>();"); return true; }

        var newMatch = Regex.Match(rhsRaw, @"^New\s+([A-Za-z_]\w*)\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
        if (newMatch.Success)
        {
            var className = newMatch.Groups[1].Value; EnsureClassType(className);
            Write(sb, $"{lhs} = LSRef<{className}>.Create(new {className}({TransformArgumentList(newMatch.Groups[2].Value)}));"); return true;
        }

        var rhs = TransformObjectReferenceTarget(rhsRaw);
        if (rhs is null) throw new CompilerException("Set requires Nothing, New Class(...), or another object reference.");
        Write(sb, $"{lhs} = {rhs};"); return true;
    }

    private bool TryEmitFileStatement(StringBuilder sb, string line)
    {
        var open = Regex.Match(line, @"^Open\s+(.+?)\s+For\s+(Input|Output|Append|Binary|Random)\s+As\s+#?(.+?)(?:\s+Len\s*=\s*(.+))?$", RegexOptions.IgnoreCase);
        if (open.Success) { Write(sb, $"XPScriptRuntime.OpenFile({TransformExpression(open.Groups[1].Value)}, \"{open.Groups[2].Value.ToLowerInvariant()}\", XPScriptRuntime.CInt({TransformExpression(open.Groups[3].Value)}));"); return true; }

        var close = Regex.Match(line, @"^Close(?:\s+(.+))?$", RegexOptions.IgnoreCase);
        if (close.Success)
        {
            if (string.IsNullOrWhiteSpace(close.Groups[1].Value)) Write(sb, "XPScriptRuntime.CloseFile();");
            else
            {
                var nums = SplitOutsideStrings(close.Groups[1].Value, ',').Select(x => x.Trim().TrimStart('#')).Select(TransformExpression).Select(x => $"XPScriptRuntime.CInt({x})");
                Write(sb, $"XPScriptRuntime.CloseFile({string.Join(", ", nums)});");
            }
            return true;
        }

        var filePrint = Regex.Match(line, @"^Print\s+#([^,]+)\s*,\s*(.*)$", RegexOptions.IgnoreCase);
        if (filePrint.Success) { Write(sb, $"XPScriptRuntime.PrintFile(XPScriptRuntime.CInt({TransformExpression(filePrint.Groups[1].Value)}), {TransformArgumentList(filePrint.Groups[2].Value)});"); return true; }
        var writeFile = Regex.Match(line, @"^Write\s+#([^,]+)\s*,\s*(.*)$", RegexOptions.IgnoreCase);
        if (writeFile.Success) { Write(sb, $"XPScriptRuntime.WriteFile(XPScriptRuntime.CInt({TransformExpression(writeFile.Groups[1].Value)}), {TransformArgumentList(writeFile.Groups[2].Value)});"); return true; }
        var lineInput = Regex.Match(line, @"^Line\s+Input\s+#([^,]+)\s*,\s*([A-Za-z_]\w*)$", RegexOptions.IgnoreCase);
        if (lineInput.Success) { Write(sb, $"{lineInput.Groups[2].Value} = XPScriptRuntime.LineInput(XPScriptRuntime.CInt({TransformExpression(lineInput.Groups[1].Value)}));"); return true; }
        var input = Regex.Match(line, @"^Input\s+#([^,]+)\s*,\s*(.+)$", RegexOptions.IgnoreCase);
        if (input.Success)
        {
            var fileNo = $"XPScriptRuntime.CInt({TransformExpression(input.Groups[1].Value)})";
            foreach (var rawName in SplitOutsideStrings(input.Groups[2].Value, ','))
            {
                var name = rawName.Trim(); if (!Regex.IsMatch(name, @"^[A-Za-z_]\w*$")) throw new CompilerException("Input supports variable targets only.");
                Write(sb, $"{name} = {ConvertInputValue(name, fileNo)};");
            }
            return true;
        }
        var seekSet = Regex.Match(line, @"^Seek\s+#([^,]+)\s*,\s*(.+)$", RegexOptions.IgnoreCase);
        if (seekSet.Success) { Write(sb, $"XPScriptRuntime.SeekSet(XPScriptRuntime.CInt({TransformExpression(seekSet.Groups[1].Value)}), XPScriptRuntime.CLng({TransformExpression(seekSet.Groups[2].Value)}));"); return true; }
        var fileCopy = Regex.Match(line, @"^FileCopy\s+(.+?)\s*,\s*(.+)$", RegexOptions.IgnoreCase);
        if (fileCopy.Success) { Write(sb, $"XPScriptRuntime.FileCopy({TransformExpression(fileCopy.Groups[1].Value)}, {TransformExpression(fileCopy.Groups[2].Value)});"); return true; }
        var nameFile = Regex.Match(line, @"^Name\s+(.+?)\s+As\s+(.+)$", RegexOptions.IgnoreCase);
        if (nameFile.Success) { Write(sb, $"XPScriptRuntime.NameFile({TransformExpression(nameFile.Groups[1].Value)}, {TransformExpression(nameFile.Groups[2].Value)});"); return true; }
        if (EmitUnaryRuntimeStatement(sb, line, "Kill", "Kill")) return true;
        if (EmitUnaryRuntimeStatement(sb, line, "MkDir", "MkDir")) return true;
        if (EmitUnaryRuntimeStatement(sb, line, "RmDir", "RmDir")) return true;
        if (EmitUnaryRuntimeStatement(sb, line, "ChDir", "ChDir")) return true;
        var setAttr = Regex.Match(line, @"^SetFileAttr\s+(.+?)\s*,\s*(.+)$", RegexOptions.IgnoreCase);
        if (setAttr.Success) { Write(sb, $"XPScriptRuntime.SetFileAttr({TransformExpression(setAttr.Groups[1].Value)}, XPScriptRuntime.CInt({TransformExpression(setAttr.Groups[2].Value)}));"); return true; }
        return false;
    }

    private bool EmitUnaryRuntimeStatement(StringBuilder sb, string line, string keyword, string method)
    {
        var match = Regex.Match(line, $@"^{keyword}\s+(.+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;
        Write(sb, $"XPScriptRuntime.{method}({TransformExpression(match.Groups[1].Value)});"); return true;
    }

    private void EmitPropertyWrappers(StringBuilder sb, ClassInfo info)
    {
        foreach (var property in info.Properties.Values)
        {
            if (property.HasParameters) continue;
            var type = MapType(property.XPScriptType);
            Write(sb, $"{property.Visibility} {type} {property.Name}"); Write(sb, "{"); _indent++;
            if (property.HasGet) Write(sb, $"get => __get_{property.Name}();");
            if (property.HasSet) Write(sb, $"set => __set_{property.Name}(value);");
            _indent--; Write(sb, "}");
        }
    }

    private void RegisterArguments(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;
        foreach (var part in SplitOutsideStrings(raw, ','))
        {
            var declaration = ParseArgumentDeclaration(part.Trim()); RegisterVariable(declaration.Name, declaration.XPScriptType, declaration.IsList);
        }
    }

    private string ParseArguments(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var result = new List<string>();
        foreach (var part in SplitOutsideStrings(raw, ','))
        {
            var declaration = ParseArgumentDeclaration(part.Trim());
            if (declaration.IsByRef && !declaration.IsList && !_classes.ContainsKey(declaration.XPScriptType)) throw new CompilerException("ByRef scalar parameters are not supported yet.");
            result.Add(declaration.IsList ? $"LSList<{MapType(declaration.XPScriptType)}> {declaration.Name}" : $"{MapType(declaration.XPScriptType)} {declaration.Name}");
        }
        return string.Join(", ", result);
    }

    private (string Name, string XPScriptType, bool IsList, bool IsByRef) ParseArgumentDeclaration(string raw)
    {
        var match = Regex.Match(raw, @"^(?:(ByVal|ByRef)\s+)?([A-Za-z_]\w*)\s*(?:(List))?\s*(?:As\s+([A-Za-z_]\w*))?$", RegexOptions.IgnoreCase);
        if (!match.Success) throw new CompilerException($"Unsupported argument declaration: {raw}");
        return (match.Groups[2].Value, string.IsNullOrWhiteSpace(match.Groups[4].Value) ? "Variant" : match.Groups[4].Value, !string.IsNullOrWhiteSpace(match.Groups[3].Value), match.Groups[1].Value.Equals("ByRef", StringComparison.OrdinalIgnoreCase));
    }

    private void RegisterVariable(string name, string xpscriptType, bool isList)
    {
        if (isList) { _listVariables[name] = MapType(xpscriptType); return; }
        var type = MapType(xpscriptType); _variableTypes[name] = type;
        if (_classes.ContainsKey(xpscriptType)) _objectVariables[name] = xpscriptType;
    }

    private string TransformCondition(string expression) => Regex.Replace(TransformExpression(expression), @"(?<![<>=!])=(?!=)", "==");

    private string TransformExpression(string expression)
    {
        var prepared = TransformListSyntax(expression); var result = new StringBuilder();
        foreach (var piece in SplitStringLiterals(prepared))
        {
            if (piece.IsString) { result.Append(ConvertXPScriptStringLiteral(piece.Text)); continue; }
            result.Append(TransformNonStringExpression(piece.Text));
        }
        return result.ToString().Trim();
    }

    private string TransformListSyntax(string expression)
    {
        var text = Regex.Replace(expression, @"\bMe\b", "this", RegexOptions.IgnoreCase);
        foreach (var alias in _forAll)
            text = Regex.Replace(text, $@"\bListTag\s*\(\s*{Regex.Escape(alias.Alias)}\s*\)", $"__LSLISTTAG_{alias.Alias}__", RegexOptions.IgnoreCase);

        foreach (var list in GetAccessibleLists().OrderByDescending(x => x.SourceName.Length))
        {
            var namePattern = Regex.Escape(list.SourceName); var prefix = list.SourceName.Contains('.') ? @"\b" : @"(?<![\w.])";
            text = Regex.Replace(text, $@"\bIsElement\s*\(\s*{prefix}{namePattern}\s*\(([^()]*)\)\s*\)", $"{list.Expression}.ContainsTag($1)", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, $@"{prefix}{namePattern}\s*\(([^()]*)\)", $"{list.Expression}[$1]", RegexOptions.IgnoreCase);
        }
        return text;
    }

    private string TransformNonStringExpression(string text)
    {
        text = text.Replace("<>", "!=", StringComparison.Ordinal);
        // An equals sign inside an expression is always a comparison. Assignments are
        // removed by the statement parser before expressions reach this method, so this
        // also safely handles comparisons nested inside function-call arguments.
        text = Regex.Replace(text, @"(?<![<>=!])=(?![=>])", "==");
        text = Regex.Replace(text, @"\bMe\b", "this", RegexOptions.IgnoreCase);

        foreach (var objectVariable in _objectVariables)
        {
            var name = Regex.Escape(objectVariable.Key);
            text = Regex.Replace(text, $@"\b{name}\s+Is\s+Not\s+Nothing\b", $"!{objectVariable.Key}.IsNothing", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, $@"\b{name}\s+Is\s+Nothing\b", $"{objectVariable.Key}.IsNothing", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, $@"\b{name}\.", $"{objectVariable.Key}.Value!.", RegexOptions.IgnoreCase);
        }

        if (_currentClass is not null && _classes.TryGetValue(_currentClass, out var classInfo))
        {
            foreach (var field in classInfo.Fields.Values)
            {
                if (!_classes.ContainsKey(field.XPScriptType) || field.IsList) continue;
                var name = Regex.Escape(field.Name);
                text = Regex.Replace(text, $@"\bthis\.{name}\s+Is\s+Not\s+Nothing\b", $"!this.{field.Name}.IsNothing", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, $@"\bthis\.{name}\s+Is\s+Nothing\b", $"this.{field.Name}.IsNothing", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, $@"\bthis\.{name}\.", $"this.{field.Name}.Value!.", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, $@"(?<![\w.]){name}\.", $"this.{field.Name}.Value!.", RegexOptions.IgnoreCase);
            }
        }

        foreach (var alias in _forAll)
        {
            if (alias.IsListAlias)
            {
                text = Regex.Replace(text, $@"\b{Regex.Escape(alias.Alias)}\b", $"{alias.Alias}.Value", RegexOptions.IgnoreCase);
                text = text.Replace($"__LSLISTTAG_{alias.Alias}.Value__", $"{alias.Alias}.Tag", StringComparison.OrdinalIgnoreCase);
                text = text.Replace($"__LSLISTTAG_{alias.Alias}__", $"{alias.Alias}.Tag", StringComparison.OrdinalIgnoreCase);
            }
        }

        text = Regex.Replace(text, @"\bAnd\b", "&&", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bOr\b", "||", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bNot\b", "!", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bMod\b", "%", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bTrue\b", "true", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bFalse\b", "false", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bNothing\b", "null", RegexOptions.IgnoreCase);
        text = text.Replace("&", "+", StringComparison.Ordinal);

        foreach (var fn in RuntimeFunctions)
        {
            text = Regex.Replace(text, $@"(?<![\w.]){Regex.Escape(fn)}\$\s*\(", $"XPScriptRuntime.{fn}(", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, $@"(?<![\w.]){Regex.Escape(fn)}\s*\(", $"XPScriptRuntime.{fn}(", RegexOptions.IgnoreCase);
        }
        foreach (var fn in ZeroArgRuntimeFunctions)
            text = Regex.Replace(text, $@"(?<![\w.]){Regex.Escape(fn)}\$?(?!\s*\(|[\w])", $"XPScriptRuntime.{fn}()", RegexOptions.IgnoreCase);
        return text;
    }

    private string TransformAssignmentTarget(string lhs)
    {
        var text = Regex.Replace(lhs, @"^Me\.", "this.", RegexOptions.IgnoreCase);
        foreach (var objectVariable in _objectVariables)
            if (text.StartsWith(objectVariable.Key + ".", StringComparison.OrdinalIgnoreCase)) return objectVariable.Key + ".Value!." + text[(objectVariable.Key.Length + 1)..];

        if (_currentClass is not null && _classes.TryGetValue(_currentClass, out var classInfo))
        {
            if (classInfo.Properties.ContainsKey(text)) return "this." + text;
            foreach (var field in classInfo.Fields.Values)
            {
                if (!_classes.ContainsKey(field.XPScriptType) || field.IsList) continue;
                if (text.StartsWith(field.Name + ".", StringComparison.OrdinalIgnoreCase)) return "this." + field.Name + ".Value!." + text[(field.Name.Length + 1)..];
                if (text.StartsWith("this." + field.Name + ".", StringComparison.OrdinalIgnoreCase)) return "this." + field.Name + ".Value!." + text[("this." + field.Name + ".").Length..];
            }
        }
        return text;
    }

    private string TransformCallableTarget(string target)
    {
        var text = Regex.Replace(target, @"^Me\.", "this.", RegexOptions.IgnoreCase);
        foreach (var objectVariable in _objectVariables)
            if (text.StartsWith(objectVariable.Key + ".", StringComparison.OrdinalIgnoreCase)) return objectVariable.Key + ".Value!." + text[(objectVariable.Key.Length + 1)..];
        if (_currentClass is not null && _classes.TryGetValue(_currentClass, out var classInfo))
            foreach (var field in classInfo.Fields.Values)
                if (_classes.ContainsKey(field.XPScriptType) && !field.IsList && text.StartsWith(field.Name + ".", StringComparison.OrdinalIgnoreCase)) return "this." + field.Name + ".Value!." + text[(field.Name.Length + 1)..];
        return text;
    }

    private string? TransformObjectReferenceTarget(string raw)
    {
        var text = Regex.Replace(raw.Trim(), @"^Me\.", "this.", RegexOptions.IgnoreCase);
        if (_objectVariables.ContainsKey(text)) return text;
        if (_currentClass is not null && _classes.TryGetValue(_currentClass, out var current))
        {
            if (current.Fields.TryGetValue(text, out var field) && !field.IsList && _classes.ContainsKey(field.XPScriptType)) return "this." + text;
            if (text.StartsWith("this.", StringComparison.OrdinalIgnoreCase))
            {
                var member = text["this.".Length..];
                if (current.Fields.TryGetValue(member, out var meField) && !meField.IsList && _classes.ContainsKey(meField.XPScriptType)) return text;
            }
        }
        var dot = text.IndexOf('.');
        if (dot > 0)
        {
            var root = text[..dot]; var member = text[(dot + 1)..];
            if (_objectVariables.TryGetValue(root, out var className) && _classes.TryGetValue(className, out var classInfo) && classInfo.Fields.TryGetValue(member, out var memberField) && !memberField.IsList && _classes.ContainsKey(memberField.XPScriptType)) return $"{root}.Value!.{member}";
        }
        return null;
    }

    private string? ResolveObjectReferenceClass(string raw)
    {
        var text = Regex.Replace(raw.Trim(), @"^Me\.", "this.", RegexOptions.IgnoreCase);
        if (_objectVariables.TryGetValue(text, out var localClass)) return localClass;
        if (_currentClass is not null && _classes.TryGetValue(_currentClass, out var current))
        {
            var member = text.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ? text["this.".Length..] : text;
            if (current.Fields.TryGetValue(member, out var field) && !field.IsList && _classes.ContainsKey(field.XPScriptType)) return field.XPScriptType;
        }
        var dot = text.IndexOf('.');
        if (dot > 0)
        {
            var root = text[..dot]; var member = text[(dot + 1)..];
            if (_objectVariables.TryGetValue(root, out var className) && _classes.TryGetValue(className, out var classInfo) && classInfo.Fields.TryGetValue(member, out var memberField) && !memberField.IsList && _classes.ContainsKey(memberField.XPScriptType)) return memberField.XPScriptType;
        }
        return null;
    }

    private bool IsObjectReferenceTarget(string lhs) => ResolveObjectReferenceClass(lhs) is not null;

    private ForAllContext? FindForAllAlias(string name)
    {
        foreach (var context in _forAll) if (context.Alias.Equals(name, StringComparison.OrdinalIgnoreCase)) return context;
        return null;
    }

    private (string Expression, string ElementType)? ResolveList(string name)
    {
        if (_listVariables.TryGetValue(name, out var localType)) return (name, localType);
        if (_currentClass is not null && _classes.TryGetValue(_currentClass, out var info) && info.Fields.TryGetValue(name, out var field) && field.IsList) return ($"this.{name}", MapType(field.XPScriptType));
        return null;
    }

    private IEnumerable<(string SourceName, string Expression, string ElementType)> GetAccessibleLists()
    {
        foreach (var item in _listVariables) yield return (item.Key, item.Key, item.Value);
        if (_currentClass is null || !_classes.TryGetValue(_currentClass, out var info)) yield break;
        foreach (var field in info.Fields.Values.Where(x => x.IsList))
        {
            yield return (field.Name, $"this.{field.Name}", MapType(field.XPScriptType));
            yield return ($"this.{field.Name}", $"this.{field.Name}", MapType(field.XPScriptType));
        }
    }

    private string TransformArgumentList(string raw) => string.IsNullOrWhiteSpace(raw) ? "" : string.Join(", ", SplitOutsideStrings(raw, ',').Select(TransformExpression));

    private string ConvertInputValue(string name, string fileNo)
    {
        var raw = $"XPScriptRuntime.Input({fileNo})";
        if (!_variableTypes.TryGetValue(name, out var type)) return raw;
        return type switch { "string" => raw, "byte" => $"XPScriptRuntime.CByte({raw})", "int" => $"XPScriptRuntime.CInt({raw})", "long" => $"XPScriptRuntime.CLng({raw})", "double" => $"XPScriptRuntime.CDbl({raw})", "float" => $"XPScriptRuntime.CSng({raw})", "decimal" => $"XPScriptRuntime.CCur({raw})", "bool" => $"XPScriptRuntime.CBool({raw})", "DateTime" => $"XPScriptRuntime.CDat({raw})", _ => raw };
    }

    private string ConvertForValue(string name, string raw)
    {
        if (!_variableTypes.TryGetValue(name, out var type)) return raw;
        return type switch { "byte" => $"Convert.ToByte({raw})", "int" => $"Convert.ToInt32({raw})", "long" => $"Convert.ToInt64({raw})", "double" => $"Convert.ToDouble({raw})", "float" => $"Convert.ToSingle({raw})", "decimal" => $"Convert.ToDecimal({raw})", _ => raw };
    }

    private string MapType(string xpscriptType)
    {
        var normalized = xpscriptType.Trim();
        if (TypeMap.TryGetValue(normalized, out var type)) return type;
        if (_classes.ContainsKey(normalized)) return $"LSRef<{normalized}>";
        throw new CompilerException($"Unsupported type: {xpscriptType}");
    }

    private void EnsureClassType(string className) { if (!_classes.ContainsKey(className)) throw new CompilerException($"Unknown class: {className}"); }

    private static string DefaultValue(string type)
    {
        if (type.StartsWith("LSRef<", StringComparison.Ordinal)) return $"new {type}()";
        if (type.StartsWith("LSList<", StringComparison.Ordinal)) return "new()";
        return type switch { "string" => "\"\"", "bool" => "false", "DateTime" => "default", "dynamic" => "null!", "object" => "null!", _ => "0" };
    }

    private static string FindEntryPoint(string[] lines)
    {
        var inClass = false;
        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            if (Regex.IsMatch(line, @"^(?:(Public|Private)\s+)?Class\b", RegexOptions.IgnoreCase)) { inClass = true; continue; }
            if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase)) { inClass = false; continue; }
            if (!inClass && Regex.IsMatch(line, @"^(?:Public\s+|Private\s+)?Sub\s+Main\b", RegexOptions.IgnoreCase)) return "Main";
        }
        inClass = false;
        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            if (Regex.IsMatch(line, @"^(?:(Public|Private)\s+)?Class\b", RegexOptions.IgnoreCase)) { inClass = true; continue; }
            if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase)) { inClass = false; continue; }
            if (!inClass && Regex.IsMatch(line, @"^(?:Public\s+|Private\s+)?Sub\s+Initialize\b", RegexOptions.IgnoreCase)) return "Initialize";
        }
        throw new CompilerException("No entry point found. Add Sub Main() or Sub Initialize().");
    }

    private static string NormalizeVisibility(string value, string defaultValue) => string.IsNullOrWhiteSpace(value) ? defaultValue : value.ToLowerInvariant();

    private void Write(StringBuilder sb, string text)
    {
        if (_indent < 1) throw new CompilerException("Unexpected block terminator.");
        sb.Append(' ', _indent * 4); sb.AppendLine(text);
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') { if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; } inString = !inString; continue; }
            if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }

    private static List<(string Text, bool IsString)> SplitStringLiterals(string expression)
    {
        var result = new List<(string, bool)>(); var current = new StringBuilder(); var inString = false;
        for (var i = 0; i < expression.Length; i++)
        {
            var c = expression[i];
            if (c == '"')
            {
                if (inString && i + 1 < expression.Length && expression[i + 1] == '"') { current.Append("\"\""); i++; continue; }
                if (inString) { current.Append(c); result.Add((current.ToString(), true)); current.Clear(); inString = false; }
                else { if (current.Length > 0) { result.Add((current.ToString(), false)); current.Clear(); } current.Append(c); inString = true; }
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0) result.Add((current.ToString(), inString));
        if (inString) throw new CompilerException("Unterminated string literal.");
        return result;
    }

    private static string ConvertXPScriptStringLiteral(string literal)
    {
        if (literal.Length < 2 || literal[0] != '"' || literal[^1] != '"') throw new CompilerException("Invalid string literal.");
        var inner = literal[1..^1].Replace("\"\"", "\\\"");
        inner = inner.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        inner = inner.Replace("\\\\\"", "\\\"");
        return "\"" + inner + "\"";
    }

    private static List<string> SplitOutsideStrings(string value, char separator)
    {
        var result = new List<string>(); var current = new StringBuilder(); var inString = false; var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"') { current.Append(c); if (inString && i + 1 < value.Length && value[i + 1] == '"') { current.Append(value[++i]); continue; } inString = !inString; continue; }
            if (!inString)
            {
                if (c == '(') depth++; if (c == ')') depth--;
                if (c == separator && depth == 0) { result.Add(current.ToString()); current.Clear(); continue; }
            }
            current.Append(c);
        }
        result.Add(current.ToString()); return result;
    }

    private static string EscapeComment(string value) => value.Replace("\r", " ").Replace("\n", " ");
}
