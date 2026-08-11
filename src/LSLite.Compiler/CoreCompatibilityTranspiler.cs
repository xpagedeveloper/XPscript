using System.Text;
using System.Text.RegularExpressions;

namespace LSLite.Compiler;

internal sealed class CoreCompatibilityTranspiler
{
    private sealed record ParameterInfo(string Name, string Type, bool ByRef, bool IsArray, bool IsList);
    private sealed record ProcedureInfo(int Id, string Name, List<ParameterInfo> Parameters, bool IsStatic, string? ClassName);
    private sealed record ArrayInfo(string Name, string Type, bool Dynamic);
    private sealed record NativeDecl(string Kind, string Name, string Library, string Alias, string ReturnType, List<ParameterInfo> Parameters, bool Unicode);

    private sealed class ControlInfo
    {
        public required int ProcedureId { get; init; }
        public Dictionary<int, string> Handlers { get; } = new();
        public List<int> Statements { get; } = [];
        public Dictionary<int, string> GoSubs { get; } = new();
    }

    private readonly Dictionary<string, ProcedureInfo> _procedures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<char, string> _defTypes = new();
    private readonly HashSet<string> _classes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<NativeDecl> _nativeDecls = [];
    private readonly List<string> _staticMembers = [];
    private readonly Dictionary<int, ControlInfo> _controls = new();
    private readonly Dictionary<int, int> _statementProcedure = new();

    private int _optionBase;
    private int _nextProcedureId;
    private int _nextSelectId;
    private int _nextHandlerId;
    private int _nextStatementId;
    private int _nextGoSubId;

    public string Transpile(string source, string sourceName)
    {
        Reset();
        var lines = Normalize(source);
        Analyze(lines);
        var transformed = TransformModule(lines);
        var generated = new AdvancedLotusTranspiler().Transpile(transformed, sourceName);
        generated = InjectScriptMembers(generated);
        generated = PostProcessMarkers(generated);
        generated += "\n\n" + CoreCompatibilityRuntimeSource.Code + "\n";
        return generated;
    }

    private void Reset()
    {
        _procedures.Clear();
        _defTypes.Clear();
        _classes.Clear();
        _nativeDecls.Clear();
        _staticMembers.Clear();
        _controls.Clear();
        _statementProcedure.Clear();
        _optionBase = 0;
        _nextProcedureId = 1;
        _nextSelectId = 1;
        _nextHandlerId = 1;
        _nextStatementId = 1;
        _nextGoSubId = 1;
    }

    private static string[] Normalize(string source) =>
        source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private void Analyze(string[] lines)
    {
        string? currentClass = null;

        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0) continue;

            var optionBase = Regex.Match(line, @"^Option\s+Base\s+([01])$", RegexOptions.IgnoreCase);
            if (optionBase.Success)
            {
                _optionBase = int.Parse(optionBase.Groups[1].Value);
                continue;
            }

            if (TryAnalyzeDefType(line)) continue;
            if (TryAnalyzeNative(line)) continue;

            var classMatch = Regex.Match(line, @"^(?:(?:Public|Private)\s+)?Class\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups[1].Value;
                _classes.Add(currentClass);
                continue;
            }
            if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase))
            {
                currentClass = null;
                continue;
            }

            var proc = ParseProcedureHeader(line, currentClass);
            if (proc is not null)
                _procedures[ProcedureKey(proc.Name, currentClass)] = proc;
        }
    }

    private bool TryAnalyzeDefType(string line)
    {
        var match = Regex.Match(line, @"^Def(Bool|Byte|Cur|Dbl|Int|Lng|Sng|Str|Var)\s+(.+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var type = match.Groups[1].Value.ToLowerInvariant() switch
        {
            "bool" => "Boolean",
            "byte" => "Byte",
            "cur" => "Currency",
            "dbl" => "Double",
            "int" => "Integer",
            "lng" => "Long",
            "sng" => "Single",
            "str" => "String",
            _ => "Variant"
        };

        foreach (var part in match.Groups[2].Value.Split(','))
        {
            var token = part.Trim();
            if (token.Length == 1)
            {
                _defTypes[char.ToUpperInvariant(token[0])] = type;
                continue;
            }

            var range = Regex.Match(token, @"^([A-Za-z])\s*-\s*([A-Za-z])$");
            if (!range.Success) throw new CompilerException("Invalid Deftype range: " + token);
            var a = char.ToUpperInvariant(range.Groups[1].Value[0]);
            var b = char.ToUpperInvariant(range.Groups[2].Value[0]);
            if (a > b) (a, b) = (b, a);
            for (var c = a; c <= b; c++) _defTypes[c] = type;
        }
        return true;
    }

    private bool TryAnalyzeNative(string line)
    {
        var match = Regex.Match(
            line,
            "^(?:Declare\\s+)(?:(Public|Private)\\s+)?(Function|Sub)\\s+([A-Za-z_]\\w*)\\s+Lib\\s+\"([^\"]+)\"(?:\\s+Alias\\s+\"([^\"]+)\")?\\s*\\((.*)\\)\\s*(?:As\\s+([A-Za-z_]\\w*))?\\s*$",
            RegexOptions.IgnoreCase);
        if (!match.Success) return false;

        var kind = match.Groups[2].Value;
        var name = match.Groups[3].Value;
        var library = match.Groups[4].Value;
        var alias = string.IsNullOrWhiteSpace(match.Groups[5].Value) ? name : match.Groups[5].Value;
        var parameters = ParseParameters(match.Groups[6].Value, treatOmittedAsByRef: true);
        var returnType = kind.Equals("Function", StringComparison.OrdinalIgnoreCase)
            ? (string.IsNullOrWhiteSpace(match.Groups[7].Value) ? ResolveDefaultType(name) : match.Groups[7].Value)
            : "Void";
        var unicode = Regex.IsMatch(match.Groups[6].Value, @"\bUnicode\b", RegexOptions.IgnoreCase);
        _nativeDecls.Add(new NativeDecl(kind, name, library, alias, returnType, parameters, unicode));
        return true;
    }

    private ProcedureInfo? ParseProcedureHeader(string line, string? className)
    {
        var match = Regex.Match(
            line,
            @"^(?:(Static)\s+)?(?:(?:Public|Private)\s+)?(Sub|Function)\s+([A-Za-z_]\w*)\s*\((.*)\)\s*(?:As\s+([A-Za-z_]\w*))?\s*$",
            RegexOptions.IgnoreCase);
        if (!match.Success) return null;

        return new ProcedureInfo(
            _nextProcedureId++,
            match.Groups[3].Value,
            ParseParameters(match.Groups[4].Value, treatOmittedAsByRef: false),
            !string.IsNullOrWhiteSpace(match.Groups[1].Value),
            className);
    }

    private List<ParameterInfo> ParseParameters(string raw, bool treatOmittedAsByRef)
    {
        var result = new List<ParameterInfo>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        foreach (var part in SplitArguments(raw))
        {
            var clean = Regex.Replace(part.Trim(), @"\b(LMBCS|Unicode)\b", "", RegexOptions.IgnoreCase).Trim();
            var match = Regex.Match(clean, @"^(?:(ByVal|ByRef)\s+)?([A-Za-z_]\w*)\s*(\(\))?\s*(List)?\s*(?:As\s+([A-Za-z_]\w*))?$", RegexOptions.IgnoreCase);
            if (!match.Success) throw new CompilerException("Unsupported parameter declaration: " + part.Trim());
            var mode = match.Groups[1].Value;
            var byRef = mode.Equals("ByRef", StringComparison.OrdinalIgnoreCase) || (treatOmittedAsByRef && !mode.Equals("ByVal", StringComparison.OrdinalIgnoreCase));
            var name = match.Groups[2].Value;
            var type = string.IsNullOrWhiteSpace(match.Groups[5].Value) ? ResolveDefaultType(name) : match.Groups[5].Value;
            result.Add(new ParameterInfo(name, type, byRef, !string.IsNullOrWhiteSpace(match.Groups[3].Value), !string.IsNullOrWhiteSpace(match.Groups[4].Value)));
        }
        return result;
    }

    private string TransformModule(string[] lines)
    {
        var output = new List<string>();
        string? currentClass = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var trimmed = StripComment(raw).Trim();

            if (Regex.IsMatch(trimmed, @"^Option\s+Base\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(trimmed, @"^Def(?:Bool|Byte|Cur|Dbl|Int|Lng|Sng|Str|Var)\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(trimmed, @"^Declare\b.*\bLib\b", RegexOptions.IgnoreCase))
                continue;

            var classMatch = Regex.Match(trimmed, @"^(?:(?:Public|Private)\s+)?Class\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups[1].Value;
                output.Add(raw);
                continue;
            }
            if (Regex.IsMatch(trimmed, @"^End\s+Class$", RegexOptions.IgnoreCase))
            {
                currentClass = null;
                output.Add(raw);
                continue;
            }

            var proc = ParseProcedureHeaderForLookup(trimmed, currentClass);
            if (proc is null)
            {
                output.Add(raw);
                continue;
            }

            var endPattern = proc.Name.Equals("__property__", StringComparison.Ordinal) ? @"^End\s+Property$" : @"^End\s+(Sub|Function)$";
            var body = new List<string>();
            var j = i + 1;
            for (; j < lines.Length; j++)
            {
                if (Regex.IsMatch(StripComment(lines[j]).Trim(), endPattern, RegexOptions.IgnoreCase)) break;
                body.Add(lines[j]);
            }
            if (j >= lines.Length) throw new CompilerException("Missing procedure terminator.");

            if (proc.Name.Equals("__property__", StringComparison.Ordinal))
            {
                output.Add(raw);
                output.AddRange(TransformCommonBody(body, null, currentClass, false));
                output.Add(lines[j]);
            }
            else
            {
                output.AddRange(TransformProcedure(trimmed, body, proc, currentClass));
                output.Add(lines[j]);
            }
            i = j;
        }

        return string.Join(Environment.NewLine, output);
    }

    private ProcedureInfo? ParseProcedureHeaderForLookup(string line, string? className)
    {
        if (Regex.IsMatch(line, @"^(?:(?:Public|Private)\s+)?Property\s+(Get|Set)\b", RegexOptions.IgnoreCase))
            return new ProcedureInfo(-1, "__property__", [], false, className);

        var match = Regex.Match(line, @"^(?:Static\s+)?(?:(?:Public|Private)\s+)?(?:Sub|Function)\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        return _procedures.TryGetValue(ProcedureKey(match.Groups[1].Value, className), out var info) ? info : null;
    }

    private IEnumerable<string> TransformProcedure(string header, List<string> body, ProcedureInfo proc, string? className)
    {
        var output = new List<string>();
        var transformedHeader = TransformProcedureHeader(header, proc);
        output.Add(transformedHeader);

        var hasOnError = body.Any(x => Regex.IsMatch(StripComment(x).Trim(), @"^On\s+Error\b", RegexOptions.IgnoreCase));
        var hasGoSub = body.Any(x => Regex.IsMatch(StripComment(x).Trim(), @"^GoSub\b", RegexOptions.IgnoreCase));
        var control = new ControlInfo { ProcedureId = proc.Id };
        _controls[proc.Id] = control;

        if (hasOnError)
        {
            output.Add("    Dim __lsErrCtx As Variant");
            output.Add("    __lsErrCtx = LSControlRuntime.CreateErrorContext()");
        }

        var arrays = DiscoverArrays(body, proc);
        var scalarTypes = DiscoverScalarTypes(body, proc);
        var staticNames = DiscoverStaticLocals(body, proc, className, arrays, scalarTypes);

        var common = TransformCommonBody(body, proc, className, proc.IsStatic);
        var withAndSelect = TransformWithAndSelect(common);

        foreach (var originalLine in withAndSelect)
        {
            var indent = Regex.Match(originalLine, @"^\s*").Value;
            var line = StripComment(originalLine).Trim();
            if (line.Length == 0) { output.Add(originalLine); continue; }

            var transformed = TransformCoreLine(line, proc, arrays, scalarTypes, staticNames, control, hasGoSub);
            foreach (var expanded in transformed)
            {
                var finalLine = RewriteByRefParameterUses(expanded, proc.Parameters.Where(x => x.ByRef && !x.IsArray && !x.IsList).ToList());
                finalLine = RewriteByRefCalls(finalLine, className);
                finalLine = RewriteErrorExpressions(finalLine);
                finalLine = RewriteArrayReads(finalLine, arrays);
                finalLine = RewriteStaticNames(finalLine, staticNames);

                if (hasOnError && IsProtectableStatement(finalLine))
                {
                    var statementId = _nextStatementId++;
                    control.Statements.Add(statementId);
                    _statementProcedure[statementId] = proc.Id;
                    output.Add(indent + $"Call LSCoreMarker.Statement({statementId})");
                }
                output.Add(indent + finalLine);
            }
        }

        return output;
    }

    private IEnumerable<string> TransformCommonBody(List<string> body, ProcedureInfo? proc, string? className, bool staticProcedure)
    {
        foreach (var raw in body)
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0) { yield return raw; continue; }

            var dimNoType = Regex.Match(line, @"^(Dim|Static)\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (dimNoType.Success)
            {
                yield return Regex.Match(raw, @"^\s*").Value + dimNoType.Groups[1].Value + " " + dimNoType.Groups[2].Value + " As " + ResolveDefaultType(dimNoType.Groups[2].Value);
                continue;
            }
            yield return raw;
        }
    }

    private string TransformProcedureHeader(string header, ProcedureInfo proc)
    {
        var result = Regex.Replace(header, @"^Static\s+", "", RegexOptions.IgnoreCase);
        foreach (var p in proc.Parameters)
        {
            if (p.ByRef && !p.IsArray && !p.IsList)
            {
                result = Regex.Replace(result, $@"\bByRef\s+{Regex.Escape(p.Name)}\s*(?:As\s+[A-Za-z_]\w*)?", p.Name + " As Variant", RegexOptions.IgnoreCase);
            }
            else if (p.IsArray)
            {
                result = Regex.Replace(result, $@"(?:(?:ByVal|ByRef)\s+)?{Regex.Escape(p.Name)}\s*\(\)\s*(?:As\s+[A-Za-z_]\w*)?", p.Name + " As Variant", RegexOptions.IgnoreCase);
            }
        }

        if (Regex.IsMatch(result, @"\bFunction\b", RegexOptions.IgnoreCase) && !Regex.IsMatch(result, @"\bAs\s+[A-Za-z_]\w*\s*$", RegexOptions.IgnoreCase))
            result += " As " + ResolveDefaultType(proc.Name);
        return result;
    }

    private Dictionary<string, ArrayInfo> DiscoverArrays(List<string> body, ProcedureInfo proc)
    {
        var arrays = new Dictionary<string, ArrayInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in proc.Parameters.Where(x => x.IsArray)) arrays[p.Name] = new ArrayInfo(p.Name, p.Type, true);

        foreach (var raw in body)
        {
            var line = StripComment(raw).Trim();
            var dim = Regex.Match(line, @"^(?:Dim|Static)\s+([A-Za-z_]\w*)\s*\((.*)\)\s*(?:As\s+([A-Za-z_]\w*))?\s*$", RegexOptions.IgnoreCase);
            if (!dim.Success) continue;
            var type = string.IsNullOrWhiteSpace(dim.Groups[3].Value) ? ResolveDefaultType(dim.Groups[1].Value) : dim.Groups[3].Value;
            arrays[dim.Groups[1].Value] = new ArrayInfo(dim.Groups[1].Value, type, string.IsNullOrWhiteSpace(dim.Groups[2].Value));
        }
        return arrays;
    }

    private Dictionary<string, string> DiscoverScalarTypes(List<string> body, ProcedureInfo proc)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in proc.Parameters) result[p.Name] = p.Type;
        foreach (var raw in body)
        {
            var line = StripComment(raw).Trim();
            var dim = Regex.Match(line, @"^(?:Dim|Static)\s+([A-Za-z_]\w*)\s*(?:As\s+([A-Za-z_]\w*))?\s*$", RegexOptions.IgnoreCase);
            if (dim.Success) result[dim.Groups[1].Value] = string.IsNullOrWhiteSpace(dim.Groups[2].Value) ? ResolveDefaultType(dim.Groups[1].Value) : dim.Groups[2].Value;
        }
        return result;
    }

    private Dictionary<string, string> DiscoverStaticLocals(List<string> body, ProcedureInfo proc, string? className, Dictionary<string, ArrayInfo> arrays, Dictionary<string, string> scalarTypes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in body)
        {
            var line = StripComment(raw).Trim();
            var isStaticLine = Regex.IsMatch(line, @"^Static\s+", RegexOptions.IgnoreCase);
            var dim = Regex.Match(line, @"^(?:Dim|Static)\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
            if (!dim.Success || (!isStaticLine && !proc.IsStatic)) continue;

            var name = dim.Groups[1].Value;
            var generatedName = $"__ls_static_{proc.Id}_{name}";
            result[name] = className is null ? generatedName : "Script." + generatedName;

            if (arrays.TryGetValue(name, out var array))
            {
                _staticMembers.Add($"private static dynamic {generatedName} = LSArrayRuntime.Dynamic(\"{EscapeCSharp(array.Type)}\");");
                continue;
            }

            var type = scalarTypes.TryGetValue(name, out var scalar) ? scalar : ResolveDefaultType(name);
            var mapped = MapCSharpType(type);
            _staticMembers.Add($"private static {mapped} {generatedName} = {DefaultCSharp(mapped)};");
        }
        return result;
    }

    private List<string> TransformWithAndSelect(IEnumerable<string> lines)
    {
        var result = new List<string>();
        var withStack = new Stack<string>();
        var selectStack = new Stack<(string Variable, bool HasCase)>();

        foreach (var raw in lines)
        {
            var indent = Regex.Match(raw, @"^\s*").Value;
            var line = StripComment(raw).Trim();

            var with = Regex.Match(line, @"^With\s+(.+)$", RegexOptions.IgnoreCase);
            if (with.Success)
            {
                var expression = RewriteWithDots(with.Groups[1].Value, withStack.Count > 0 ? withStack.Peek() : null);
                withStack.Push(expression);
                continue;
            }
            if (Regex.IsMatch(line, @"^End\s+With$", RegexOptions.IgnoreCase))
            {
                if (withStack.Count == 0) throw new CompilerException("Unexpected End With.");
                withStack.Pop();
                continue;
            }

            var rewritten = withStack.Count > 0 ? RewriteWithDots(line, withStack.Peek()) : line;
            var select = Regex.Match(rewritten, @"^Select\s+Case\s+(.+)$", RegexOptions.IgnoreCase);
            if (select.Success)
            {
                var variable = "__lsSelect" + _nextSelectId++;
                result.Add(indent + $"Dim {variable} As Variant");
                result.Add(indent + $"{variable} = {select.Groups[1].Value}");
                selectStack.Push((variable, false));
                continue;
            }

            var caseMatch = Regex.Match(rewritten, @"^Case\s+(.+)$", RegexOptions.IgnoreCase);
            if (caseMatch.Success && selectStack.Count > 0)
            {
                var context = selectStack.Pop();
                var isElse = caseMatch.Groups[1].Value.Trim().Equals("Else", StringComparison.OrdinalIgnoreCase);
                if (!context.HasCase)
                    result.Add(indent + (isElse ? "If True Then" : $"If {BuildCaseCondition(context.Variable, caseMatch.Groups[1].Value)} Then"));
                else
                    result.Add(indent + (isElse ? "Else" : $"ElseIf {BuildCaseCondition(context.Variable, caseMatch.Groups[1].Value)} Then"));
                selectStack.Push((context.Variable, true));
                continue;
            }

            if (Regex.IsMatch(rewritten, @"^End\s+Select$", RegexOptions.IgnoreCase) && selectStack.Count > 0)
            {
                var context = selectStack.Pop();
                if (context.HasCase) result.Add(indent + "End If");
                continue;
            }

            result.Add(indent + rewritten);
        }

        if (withStack.Count > 0) throw new CompilerException("Missing End With.");
        if (selectStack.Count > 0) throw new CompilerException("Missing End Select.");
        return result;
    }

    private string BuildCaseCondition(string variable, string raw)
    {
        var conditions = new List<string>();
        foreach (var item in SplitArguments(raw))
        {
            var part = item.Trim();
            var isRel = Regex.Match(part, @"^Is\s*(<=|>=|<>|=|<|>)\s*(.+)$", RegexOptions.IgnoreCase);
            if (isRel.Success)
            {
                conditions.Add($"LSCoreCompare.Rel({variable}, \"{isRel.Groups[1].Value}\", {isRel.Groups[2].Value})");
                continue;
            }
            var range = Regex.Match(part, @"^(.+?)\s+To\s+(.+)$", RegexOptions.IgnoreCase);
            if (range.Success)
            {
                conditions.Add($"LSCoreCompare.Between({variable}, {range.Groups[1].Value}, {range.Groups[2].Value})");
                continue;
            }
            conditions.Add($"LSCoreCompare.Equal({variable}, {part})");
        }
        return string.Join(" Or ", conditions);
    }

    private List<string> TransformCoreLine(string line, ProcedureInfo proc, Dictionary<string, ArrayInfo> arrays, Dictionary<string, string> scalarTypes, Dictionary<string, string> staticNames, ControlInfo control, bool hasGoSub)
    {
        var output = new List<string>();

        var staticDecl = Regex.Match(line, @"^Static\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
        if (staticDecl.Success || (proc.IsStatic && Regex.IsMatch(line, @"^Dim\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase)))
        {
            var name = staticDecl.Success ? staticDecl.Groups[1].Value : Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase).Groups[1].Value;
            if (staticNames.ContainsKey(name)) return output;
        }

        var arrayDim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s*\((.*)\)\s*(?:As\s+([A-Za-z_]\w*))?\s*$", RegexOptions.IgnoreCase);
        if (arrayDim.Success && arrays.TryGetValue(arrayDim.Groups[1].Value, out var dimArray))
        {
            var name = arrayDim.Groups[1].Value;
            output.Add($"Dim {name} As Variant");
            if (string.IsNullOrWhiteSpace(arrayDim.Groups[2].Value)) output.Add($"{name} = LSArrayRuntime.Dynamic(\"{dimArray.Type}\")");
            else
            {
                var bounds = BuildBounds(arrayDim.Groups[2].Value);
                output.Add($"{name} = LSArrayRuntime.Fixed(\"{dimArray.Type}\", {bounds.Lower}, {bounds.Upper})");
            }
            return output;
        }

        var redim = Regex.Match(line, @"^ReDim\s+(Preserve\s+)?([A-Za-z_]\w*)\s*\((.*)\)\s*(?:As\s+([A-Za-z_]\w*))?\s*$", RegexOptions.IgnoreCase);
        if (redim.Success)
        {
            var name = redim.Groups[2].Value;
            var type = !string.IsNullOrWhiteSpace(redim.Groups[4].Value) ? redim.Groups[4].Value : arrays.TryGetValue(name, out var info) ? info.Type : ResolveDefaultType(name);
            var bounds = BuildBounds(redim.Groups[3].Value);
            output.Add($"{name} = LSArrayRuntime.ReDim({name}, \"{type}\", {(!string.IsNullOrWhiteSpace(redim.Groups[1].Value) ? "True" : "False")}, {bounds.Lower}, {bounds.Upper})");
            return output;
        }

        var arrayAssignment = Regex.Match(line, @"^([A-Za-z_]\w*)\s*\((.*)\)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
        if (arrayAssignment.Success && arrays.ContainsKey(arrayAssignment.Groups[1].Value))
        {
            var args = string.Join(", ", SplitArguments(arrayAssignment.Groups[2].Value));
            output.Add($"Call LSArrayRuntime.Set({arrayAssignment.Groups[1].Value}, {arrayAssignment.Groups[3].Value}, {args})");
            return output;
        }

        var eraseArray = Regex.Match(line, @"^Erase\s+([A-Za-z_]\w*)$", RegexOptions.IgnoreCase);
        if (eraseArray.Success && arrays.ContainsKey(eraseArray.Groups[1].Value))
        {
            output.Add($"Call LSArrayRuntime.Erase({eraseArray.Groups[1].Value})");
            return output;
        }

        if (TryTransformFileLine(line, scalarTypes, output)) return output;

        var label = Regex.Match(line, @"^([A-Za-z_]\w*):$", RegexOptions.IgnoreCase);
        if (label.Success)
        {
            output.Add($"Call LSCoreMarker.Label(\"{label.Groups[1].Value}\")");
            return output;
        }

        var gotoMatch = Regex.Match(line, @"^GoTo\s+([A-Za-z_]\w*)$", RegexOptions.IgnoreCase);
        if (gotoMatch.Success)
        {
            output.Add($"Call LSCoreMarker.GoTo(\"{gotoMatch.Groups[1].Value}\")");
            return output;
        }

        var gosub = Regex.Match(line, @"^GoSub\s+([A-Za-z_]\w*)$", RegexOptions.IgnoreCase);
        if (gosub.Success)
        {
            var id = _nextGoSubId++;
            control.GoSubs[id] = gosub.Groups[1].Value;
            output.Add($"Call LSCoreMarker.GoSub(\"{gosub.Groups[1].Value}\", {id}, {proc.Id})");
            return output;
        }

        if (hasGoSub && Regex.IsMatch(line, @"^Return$", RegexOptions.IgnoreCase))
        {
            output.Add($"Call LSCoreMarker.GosubReturn({proc.Id})");
            return output;
        }

        var onErrorResume = Regex.Match(line, @"^On\s+Error\s+Resume\s+Next$", RegexOptions.IgnoreCase);
        if (onErrorResume.Success)
        {
            output.Add("Call LSCoreMarker.OnErrorResumeNext()");
            return output;
        }

        var onError = Regex.Match(line, @"^On\s+Error(?:\s+(.+?))?\s+GoTo\s+([A-Za-z_]\w*|0)$", RegexOptions.IgnoreCase);
        if (onError.Success)
        {
            var errorNumber = string.IsNullOrWhiteSpace(onError.Groups[1].Value) ? "0" : onError.Groups[1].Value;
            var target = onError.Groups[2].Value;
            if (target == "0") output.Add($"Call LSCoreMarker.OnErrorOff({errorNumber})");
            else
            {
                var handlerId = _nextHandlerId++;
                control.Handlers[handlerId] = target;
                output.Add($"Call LSCoreMarker.OnErrorGoto(\"{target}\", {handlerId}, {errorNumber})");
            }
            return output;
        }

        var resume = Regex.Match(line, @"^Resume(?:\s+(0|Next|[A-Za-z_]\w*))?$", RegexOptions.IgnoreCase);
        if (resume.Success)
        {
            var target = resume.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(target) || target == "0") output.Add($"Call LSCoreMarker.ResumeCurrent({proc.Id})");
            else if (target.Equals("Next", StringComparison.OrdinalIgnoreCase)) output.Add($"Call LSCoreMarker.ResumeNext({proc.Id})");
            else output.Add($"Call LSCoreMarker.ResumeLabel(\"{target}\")");
            return output;
        }

        var errorStmt = Regex.Match(line, @"^Error\s+([^,]+)(?:\s*,\s*(.+))?$", RegexOptions.IgnoreCase);
        if (errorStmt.Success)
        {
            output.Add(string.IsNullOrWhiteSpace(errorStmt.Groups[2].Value)
                ? $"Call LotusErrorRuntime.Raise({errorStmt.Groups[1].Value})"
                : $"Call LotusErrorRuntime.Raise({errorStmt.Groups[1].Value}, {errorStmt.Groups[2].Value})");
            return output;
        }

        output.Add(line);
        return output;
    }

    private bool TryTransformFileLine(string line, Dictionary<string, string> scalarTypes, List<string> output)
    {
        var open = Regex.Match(line, @"^Open\s+(.+?)\s+For\s+(Input|Output|Append|Binary|Random)\s+As\s+#?(.+?)(?:\s+Len\s*=\s*(.+))?$", RegexOptions.IgnoreCase);
        if (open.Success)
        {
            var len = string.IsNullOrWhiteSpace(open.Groups[4].Value) ? "0" : open.Groups[4].Value;
            output.Add($"Call LSFileRuntime.Open({open.Groups[1].Value}, \"{open.Groups[2].Value.ToLowerInvariant()}\", {open.Groups[3].Value}, {len})");
            return true;
        }

        var close = Regex.Match(line, @"^Close(?:\s+(.+))?$", RegexOptions.IgnoreCase);
        if (close.Success)
        {
            if (string.IsNullOrWhiteSpace(close.Groups[1].Value)) output.Add("Call LSFileRuntime.Close()");
            else output.Add("Call LSFileRuntime.Close(" + string.Join(", ", SplitArguments(close.Groups[1].Value).Select(x => x.Trim().TrimStart('#'))) + ")");
            return true;
        }

        var print = Regex.Match(line, @"^Print\s+#([^,]+)\s*,\s*(.*)$", RegexOptions.IgnoreCase);
        if (print.Success) { output.Add($"Call LSFileRuntime.PrintFile({print.Groups[1].Value}, {print.Groups[2].Value})"); return true; }
        var write = Regex.Match(line, @"^Write\s+#([^,]+)\s*,\s*(.*)$", RegexOptions.IgnoreCase);
        if (write.Success) { output.Add($"Call LSFileRuntime.WriteFile({write.Groups[1].Value}, {write.Groups[2].Value})"); return true; }
        var lineInput = Regex.Match(line, @"^Line\s+Input\s+#([^,]+)\s*,\s*([A-Za-z_]\w*)$", RegexOptions.IgnoreCase);
        if (lineInput.Success) { output.Add($"{lineInput.Groups[2].Value} = LSFileRuntime.LineInput({lineInput.Groups[1].Value})"); return true; }

        var input = Regex.Match(line, @"^Input\s+#([^,]+)\s*,\s*(.+)$", RegexOptions.IgnoreCase);
        if (input.Success)
        {
            foreach (var rawName in SplitArguments(input.Groups[2].Value))
            {
                var name = rawName.Trim();
                var type = scalarTypes.TryGetValue(name, out var known) ? known : ResolveDefaultType(name);
                output.Add($"{name} = {ConvertExpression(type, $"LSFileRuntime.Input({input.Groups[1].Value})")}");
            }
            return true;
        }

        var seek = Regex.Match(line, @"^Seek\s+#([^,]+)\s*,\s*(.+)$", RegexOptions.IgnoreCase);
        if (seek.Success) { output.Add($"Call LSFileRuntime.SeekSet({seek.Groups[1].Value}, {seek.Groups[2].Value})"); return true; }

        var put = Regex.Match(line, @"^Put\s+#?([^,]+)\s*,\s*([^,]*)\s*,\s*(.+)$", RegexOptions.IgnoreCase);
        if (put.Success)
        {
            var value = put.Groups[3].Value.Trim();
            var type = scalarTypes.TryGetValue(value, out var known) ? known : ResolveDefaultType(value);
            var record = string.IsNullOrWhiteSpace(put.Groups[2].Value) ? "Nothing" : put.Groups[2].Value;
            output.Add($"Call LSFileRuntime.Put({put.Groups[1].Value}, {record}, {value}, \"{type}\")");
            return true;
        }

        var get = Regex.Match(line, @"^Get\s+#?([^,]+)\s*,\s*([^,]*)\s*,\s*([A-Za-z_]\w*)$", RegexOptions.IgnoreCase);
        if (get.Success)
        {
            var name = get.Groups[3].Value;
            var type = scalarTypes.TryGetValue(name, out var known) ? known : ResolveDefaultType(name);
            var record = string.IsNullOrWhiteSpace(get.Groups[2].Value) ? "Nothing" : get.Groups[2].Value;
            output.Add($"{name} = {ConvertExpression(type, $"LSFileRuntime.GetValue({get.Groups[1].Value}, {record}, \"{type}\", {name})")}");
            return true;
        }

        return false;
    }

    private (string Lower, string Upper) BuildBounds(string raw)
    {
        var lower = new List<string>();
        var upper = new List<string>();
        foreach (var item in SplitArguments(raw))
        {
            var bound = Regex.Match(item.Trim(), @"^(.+?)\s+To\s+(.+)$", RegexOptions.IgnoreCase);
            if (bound.Success)
            {
                lower.Add("LotusRuntime.CInt(" + bound.Groups[1].Value + ")");
                upper.Add("LotusRuntime.CInt(" + bound.Groups[2].Value + ")");
            }
            else
            {
                lower.Add(_optionBase.ToString());
                upper.Add("LotusRuntime.CInt(" + item.Trim() + ")");
            }
        }
        return ("new int[] { " + string.Join(", ", lower) + " }", "new int[] { " + string.Join(", ", upper) + " }");
    }

    private string RewriteArrayReads(string line, Dictionary<string, ArrayInfo> arrays)
    {
        line = Regex.Replace(line, @"(?<![\w.])LBound\s*\(", "LSArrayRuntime.LBound(", RegexOptions.IgnoreCase);
        line = Regex.Replace(line, @"(?<![\w.])UBound\s*\(", "LSArrayRuntime.UBound(", RegexOptions.IgnoreCase);
        foreach (var name in arrays.Keys.OrderByDescending(x => x.Length))
            line = ReplaceCall(line, name, args => $"LSArrayRuntime.Get({name}{(args.Length > 0 ? ", " + args : "")})");
        return line;
    }

    private string RewriteByRefParameterUses(string line, List<ParameterInfo> parameters)
    {
        foreach (var p in parameters)
        {
            if (Regex.IsMatch(line, $@"\b{Regex.Escape(p.Name)}\s+As\s+Variant\b", RegexOptions.IgnoreCase)) continue;
            line = ReplaceOutsideStrings(line, $@"(?<![\w.]){Regex.Escape(p.Name)}(?![\w])", p.Name + ".Value");
        }
        return line;
    }

    private string RewriteByRefCalls(string line, string? className)
    {
        foreach (var proc in _procedures.Values.Where(x => x.Parameters.Any(p => p.ByRef && !p.IsArray && !p.IsList)))
        {
            line = ReplaceCall(line, proc.Name, argsRaw =>
            {
                var args = SplitArguments(argsRaw);
                for (var i = 0; i < Math.Min(args.Count, proc.Parameters.Count); i++)
                {
                    var p = proc.Parameters[i];
                    if (!p.ByRef || p.IsArray || p.IsList) continue;
                    var target = args[i].Trim();
                    if (!Regex.IsMatch(target, @"^[A-Za-z_]\w*(?:\.Value)?(?:\.[A-Za-z_]\w*)*$"))
                        throw new CompilerException($"ByRef argument {i + 1} for {proc.Name} must be assignable.");
                    args[i] = $"LSByRefRuntime.Create(() => (object?)({target}), __lsv => {target} = {ConvertExpression(p.Type, "__lsv")})";
                }
                return proc.Name + "(" + string.Join(", ", args) + ")";
            });
        }
        return line;
    }

    private string RewriteErrorExpressions(string line)
    {
        line = Regex.Replace(line, @"(?<![\w.])Error\$?\s*\(", "LotusErrorRuntime.Error(", RegexOptions.IgnoreCase);
        line = Regex.Replace(line, @"(?<![\w.])Err\b", "LotusErrorRuntime.Err", RegexOptions.IgnoreCase);
        line = Regex.Replace(line, @"(?<![\w.])Erl\b", "LotusErrorRuntime.Erl", RegexOptions.IgnoreCase);
        line = Regex.Replace(line, @"(?<![\w.])Error\$?\b(?!\s*\()", "LotusErrorRuntime.Error()", RegexOptions.IgnoreCase);
        line = Regex.Replace(line, @"(?<![\w.])FreeFile\$?\s*\(\s*\)", "LSFileRuntime.FreeFile()", RegexOptions.IgnoreCase);
        line = Regex.Replace(line, @"(?<![\w.])FreeFile\b(?!\s*\()", "LSFileRuntime.FreeFile()", RegexOptions.IgnoreCase);
        foreach (var fn in new[] { "EOF", "LOF", "Seek", "Loc" })
            line = Regex.Replace(line, $@"(?<![\w.]){fn}\s*\(", $"LSFileRuntime.{fn}(", RegexOptions.IgnoreCase);
        return line;
    }

    private static bool IsProtectableStatement(string line)
    {
        var t = StripComment(line).Trim();
        if (t.Length == 0) return false;
        if (Regex.IsMatch(t, @"^(Dim|Static|If|ElseIf|Else|End\s+If|For|Next|ForAll|End\s+ForAll|While|Wend|Do|Loop|Exit\b)", RegexOptions.IgnoreCase)) return false;
        if (t.Contains("LSCoreMarker.", StringComparison.Ordinal)) return false;
        return true;
    }

    private string RewriteStaticNames(string line, Dictionary<string, string> staticNames)
    {
        foreach (var pair in staticNames.OrderByDescending(x => x.Key.Length))
            line = ReplaceOutsideStrings(line, $@"(?<![\w.]){Regex.Escape(pair.Key)}(?![\w])", pair.Value);
        return line;
    }

    private string InjectScriptMembers(string generated)
    {
        var members = new StringBuilder();
        foreach (var field in _staticMembers.Distinct()) members.Append("    ").AppendLine(field);
        foreach (var native in _nativeDecls) members.AppendLine(BuildNativeDeclaration(native));
        if (members.Length == 0) return generated;

        const string marker = "internal static class Script\n{\n";
        if (generated.Contains(marker, StringComparison.Ordinal)) return generated.Replace(marker, marker + members, StringComparison.Ordinal);
        return generated.Replace("internal static class Script\r\n{\r\n", "internal static class Script\r\n{\r\n" + members.ToString().Replace("\n", "\r\n"), StringComparison.Ordinal);
    }

    private string BuildNativeDeclaration(NativeDecl native)
    {
        var returnType = native.Kind.Equals("Sub", StringComparison.OrdinalIgnoreCase) ? "void" : MapCSharpType(native.ReturnType);
        var parameters = string.Join(", ", native.Parameters.Select(p => MapCSharpType(p.Type) + " " + p.Name));
        var charSet = native.Unicode ? "Unicode" : "Ansi";
        return $"    [System.Runtime.InteropServices.DllImport(\"{EscapeCSharp(native.Library)}\", EntryPoint = \"{EscapeCSharp(native.Alias)}\", CharSet = System.Runtime.InteropServices.CharSet.{charSet})]\n    private static extern {returnType} {native.Name}({parameters});\n";
    }

    private string PostProcessMarkers(string generated)
    {
        var lines = generated.Replace("\r\n", "\n").Split('\n').ToList();
        var output = new List<string>();

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var indent = Regex.Match(line, @"^\s*").Value;
            var trimmed = line.Trim();

            var label = Regex.Match(trimmed, "^LSCoreMarker\\.Label\\(\"([^\"]+)\"\\);$");
            if (label.Success) { output.Add(indent + LabelName(label.Groups[1].Value) + ":;"); continue; }

            var goTo = Regex.Match(trimmed, "^LSCoreMarker\\.GoTo\\(\"([^\"]+)\"\\);$");
            if (goTo.Success) { output.Add(indent + "goto " + LabelName(goTo.Groups[1].Value) + ";"); continue; }

            var goSub = Regex.Match(trimmed, "^LSCoreMarker\\.GoSub\\(\"([^\"]+)\",\\s*(\\d+),\\s*(\\d+)\\);$");
            if (goSub.Success)
            {
                var target = goSub.Groups[1].Value; var id = goSub.Groups[2].Value; var proc = goSub.Groups[3].Value;
                output.Add(indent + $"LSControlRuntime.PushGosub({proc}, {id}); goto {LabelName(target)}; {GoSubReturnLabel(int.Parse(id))}:;");
                continue;
            }

            var goReturn = Regex.Match(trimmed, @"^LSCoreMarker\.GosubReturn\((\d+)\);$");
            if (goReturn.Success)
            {
                var procId = int.Parse(goReturn.Groups[1].Value);
                var cases = _controls.TryGetValue(procId, out var control)
                    ? string.Join(" ", control.GoSubs.Keys.Select(id => $"case {id}: goto {GoSubReturnLabel(id)};"))
                    : "";
                output.Add(indent + $"switch (LSControlRuntime.PopGosub({procId})) {{ {cases} default: throw new InvalidOperationException(\"Return without GoSub\"); }}");
                continue;
            }

            var onGoto = Regex.Match(trimmed, "^LSCoreMarker\\.OnErrorGoto\\(\"([^\"]+)\",\\s*(\\d+),\\s*(.+)\\);$");
            if (onGoto.Success)
            {
                output.Add(indent + $"LSControlRuntime.SetGoto(__lsErrCtx, {onGoto.Groups[2].Value}, LotusRuntime.CInt({onGoto.Groups[3].Value}));");
                continue;
            }
            var onResume = Regex.Match(trimmed, @"^LSCoreMarker\.OnErrorResumeNext\(\);$");
            if (onResume.Success) { output.Add(indent + "LSControlRuntime.SetResumeNext(__lsErrCtx);"); continue; }
            var onOff = Regex.Match(trimmed, @"^LSCoreMarker\.OnErrorOff\((.+)\);$");
            if (onOff.Success) { output.Add(indent + $"LSControlRuntime.Disable(__lsErrCtx, LotusRuntime.CInt({onOff.Groups[1].Value}));"); continue; }

            var resumeCurrent = Regex.Match(trimmed, @"^LSCoreMarker\.ResumeCurrent\((\d+)\);$");
            if (resumeCurrent.Success)
            {
                var procId = int.Parse(resumeCurrent.Groups[1].Value);
                output.Add(indent + BuildResumeSwitch(procId, before: true));
                continue;
            }
            var resumeNext = Regex.Match(trimmed, @"^LSCoreMarker\.ResumeNext\((\d+)\);$");
            if (resumeNext.Success)
            {
                var procId = int.Parse(resumeNext.Groups[1].Value);
                output.Add(indent + BuildResumeSwitch(procId, before: false));
                continue;
            }
            var resumeLabel = Regex.Match(trimmed, "^LSCoreMarker\\.ResumeLabel\\(\"([^\"]+)\"\\);$");
            if (resumeLabel.Success)
            {
                output.Add(indent + $"LSControlRuntime.Clear(__lsErrCtx); goto {LabelName(resumeLabel.Groups[1].Value)};");
                continue;
            }

            var statement = Regex.Match(trimmed, @"^LSCoreMarker\.Statement\((\d+)\);$");
            if (statement.Success && i + 1 < lines.Count)
            {
                var statementId = int.Parse(statement.Groups[1].Value);
                var actual = lines[++i].Trim();
                var procId = _statementProcedure[statementId];
                var control = _controls[procId];
                output.Add(indent + StatementBeforeLabel(statementId) + ":;");
                output.Add(indent + $"__lsErrCtx.Statement = {statementId};");
                output.Add(indent + "try { " + actual + " }");
                var handlerCases = string.Join(" ", control.Handlers.Select(h => $"case {h.Key}: goto {LabelName(h.Value)};"));
                output.Add(indent + $"catch (Exception __lsEx) {{ var __lsAction = LSControlRuntime.Capture(__lsErrCtx, __lsEx, {statementId}); if (__lsAction == -1) goto {StatementAfterLabel(statementId)}; switch (__lsAction) {{ {handlerCases} default: throw; }} }}");
                output.Add(indent + StatementAfterLabel(statementId) + ":;");
                continue;
            }

            output.Add(line);
        }

        return string.Join(Environment.NewLine, output);
    }

    private string BuildResumeSwitch(int procId, bool before)
    {
        if (!_controls.TryGetValue(procId, out var control)) return "throw new InvalidOperationException(\"Resume outside error handler\");";
        var cases = string.Join(" ", control.Statements.Select(id => $"case {id}: goto {(before ? StatementBeforeLabel(id) : StatementAfterLabel(id))};"));
        return $"LSControlRuntime.Clear(__lsErrCtx); switch (__lsErrCtx.Statement) {{ {cases} default: throw new InvalidOperationException(\"No statement to resume\"); }}";
    }

    private string ResolveDefaultType(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Variant";
        return _defTypes.TryGetValue(char.ToUpperInvariant(name[0]), out var type) ? type : "Variant";
    }

    private static string MapCSharpType(string lotusType) => lotusType.ToLowerInvariant() switch
    {
        "string" => "string",
        "integer" => "int",
        "long" => "long",
        "double" => "double",
        "single" => "float",
        "boolean" => "bool",
        "byte" => "byte",
        "currency" => "decimal",
        "date" => "DateTime",
        "variant" => "dynamic",
        "object" => "object",
        "void" => "void",
        _ => $"LSRef<{lotusType}>"
    };

    private static string DefaultCSharp(string type) => type switch
    {
        "string" => "\"\"",
        "bool" => "false",
        "DateTime" => "default",
        "dynamic" or "object" => "null!",
        _ when type.StartsWith("LSRef<", StringComparison.Ordinal) => "new()",
        _ => "0"
    };

    private static string ConvertExpression(string lotusType, string expression) => lotusType.ToLowerInvariant() switch
    {
        "string" => $"LotusRuntime.CStr({expression})",
        "integer" => $"LotusRuntime.CInt({expression})",
        "long" => $"LotusRuntime.CLng({expression})",
        "double" => $"LotusRuntime.CDbl({expression})",
        "single" => $"LotusRuntime.CSng({expression})",
        "boolean" => $"LotusRuntime.CBool({expression})",
        "byte" => $"LotusRuntime.CByte({expression})",
        "currency" => $"LotusRuntime.CCur({expression})",
        "date" => $"LotusRuntime.CDat({expression})",
        _ => expression
    };

    private static string RewriteWithDots(string line, string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return line;
        return ReplaceOutsideStrings(line, @"(?<![\w.])\.([A-Za-z_]\w*)", expression + ".$1");
    }

    private static string ReplaceOutsideStrings(string input, string pattern, string replacement)
    {
        var sb = new StringBuilder();
        var current = new StringBuilder();
        var inString = false;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '"')
            {
                if (inString && i + 1 < input.Length && input[i + 1] == '"') { current.Append("\"\""); i++; continue; }
                if (!inString) { sb.Append(Regex.Replace(current.ToString(), pattern, replacement, RegexOptions.IgnoreCase)); current.Clear(); current.Append(c); inString = true; }
                else { current.Append(c); sb.Append(current); current.Clear(); inString = false; }
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0) sb.Append(inString ? current.ToString() : Regex.Replace(current.ToString(), pattern, replacement, RegexOptions.IgnoreCase));
        return sb.ToString();
    }

    private static string ReplaceCall(string input, string functionName, Func<string, string> replacement)
    {
        var pattern = new Regex($@"(?<![\w.]){Regex.Escape(functionName)}\s*\(", RegexOptions.IgnoreCase);
        var offset = 0;
        while (true)
        {
            var match = pattern.Match(input, offset);
            if (!match.Success) break;
            var open = input.IndexOf('(', match.Index);
            var close = FindMatchingParen(input, open);
            if (close < 0) break;
            var args = input[(open + 1)..close];
            var text = replacement(args);
            input = input[..match.Index] + text + input[(close + 1)..];
            offset = match.Index + text.Length;
        }
        return input;
    }

    private static int FindMatchingParen(string input, int open)
    {
        var depth = 0;
        var inString = false;
        for (var i = open; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '"')
            {
                if (inString && i + 1 < input.Length && input[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return i;
        }
        return -1;
    }

    private static List<string> SplitArguments(string raw)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return result;
        var current = new StringBuilder();
        var inString = false;
        var depth = 0;
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '"')
            {
                current.Append(c);
                if (inString && i + 1 < raw.Length && raw[i + 1] == '"') { current.Append(raw[++i]); continue; }
                inString = !inString;
                continue;
            }
            if (!inString)
            {
                if (c is '(' or '[' or '{') depth++;
                else if (c is ')' or ']' or '}') depth--;
                else if (c == ',' && depth == 0) { result.Add(current.ToString()); current.Clear(); continue; }
            }
            current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = !inString;
            }
            else if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }

    private static string ProcedureKey(string name, string? className) => (className ?? "") + "::" + name;
    private static string LabelName(string label) => "__ls_label_" + Regex.Replace(label, @"\W", "_");
    private static string StatementBeforeLabel(int id) => "__ls_stmt_before_" + id;
    private static string StatementAfterLabel(int id) => "__ls_stmt_after_" + id;
    private static string GoSubReturnLabel(int id) => "__ls_gosub_return_" + id;
    private static string EscapeCSharp(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
