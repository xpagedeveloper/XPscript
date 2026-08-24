using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Web.Compiler;

internal sealed record BrowserWasmServerBridgeParameter(string Name, string TypeName);

internal sealed record BrowserWasmServerBridgeProcedure(
    string Id,
    string Name,
    bool IsFunction,
    string ReturnType,
    IReadOnlyList<BrowserWasmServerBridgeParameter> Parameters);

internal sealed record BrowserWasmServerBridgePlan(
    string BrowserSource,
    IReadOnlyDictionary<string, BrowserWasmServerBridgeProcedure> Procedures,
    bool UsesAi,
    bool UsesSqlite,
    bool UsesMsSql)
{
    private const string CapabilityVariable = "XpscriptWasmBridgeCapabilityCache";
    private const string CapabilityFunction = "XpscriptWasmBridgeCapability";
    private const string InvokeFunction = "XpscriptWasmBridgeInvoke";

    private static readonly Regex ProcedureHeader = new(
        @"^(?:(?:Static|Public|Private)\s+)*(Sub|Function)\s+([A-Za-z_]\w*)\s*\((.*)\)\s*(?:As\s+([A-Za-z_]\w*))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ModuleDeclaration = new(
        @"^(?:(?:Dim|Static|Public|Private)\s+)([A-Za-z_]\w*)\s+(?:(?:List)\s+)?As\s+(?:New\s+)?([A-Za-z_]\w*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ServerType = new(
        @"\b(XPAi|XPAiResponse|XPDBSQLite|XPDbMsSql)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> SerializableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Variant", "String", "Integer", "Long", "Double", "Single", "Boolean", "Byte", "Currency", "Date"
    };

    public static BrowserWasmServerBridgePlan Create(string source, string sourceIdentity)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIdentity);

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        EnsureHelperNamesAreAvailable(source);

        var procedures = ParseProcedures(lines);
        var moduleGlobals = ParseModuleGlobals(lines, procedures);
        var serverStateGlobals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in moduleGlobals)
        {
            if (IsServerType(pair.Value)) serverStateGlobals.Add(pair.Key);
        }

        var remote = new HashSet<ProcedureBlock>();
        foreach (var procedure in procedures)
        {
            var body = BodyText(lines, procedure);
            if (!ServerType.IsMatch(body)) continue;
            ValidateRemoteProcedure(procedure);
            remote.Add(procedure);

            foreach (var global in moduleGlobals.Keys)
            {
                if (Regex.IsMatch(body,
                    $@"\b{Regex.Escape(global)}\s*=\s*(?:New\s+)?(?:XPAi|XPDBSQLite|XPDbMsSql)\b",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    serverStateGlobals.Add(global);
            }
        }

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var procedure in procedures)
            {
                if (remote.Contains(procedure)) continue;
                var body = BodyText(lines, procedure);
                if (!serverStateGlobals.Any(global => Regex.IsMatch(body, $@"\b{Regex.Escape(global)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
                    continue;
                ValidateRemoteProcedure(procedure);
                remote.Add(procedure);
                changed = true;
            }
        }

        if (remote.Count == 0)
            return new BrowserWasmServerBridgePlan(source, new Dictionary<string, BrowserWasmServerBridgeProcedure>(StringComparer.Ordinal), false, false, false);

        var manifest = new Dictionary<string, BrowserWasmServerBridgeProcedure>(StringComparer.Ordinal);
        foreach (var procedure in remote.OrderBy(x => x.StartLine))
        {
            ValidateSerializableSignature(procedure);
            var id = ProcedureId(sourceIdentity, procedure.Name);
            if (!manifest.TryAdd(id, new BrowserWasmServerBridgeProcedure(id, procedure.Name, procedure.IsFunction, procedure.ReturnType, procedure.Parameters)))
                throw new XpsWebCompilationException("browser-wasm server bridge generated a duplicate procedure id.");
        }

        var remoteByStart = remote.ToDictionary(x => x.StartLine);
        var browser = new StringBuilder(source.Length + 4096);
        for (var i = 0; i < lines.Length; i++)
        {
            if (IsServerStateGlobalLine(i, lines, procedures, moduleGlobals, serverStateGlobals))
            {
                browser.AppendLine("' browser-wasm server state is kept on the web server");
                continue;
            }

            if (!remoteByStart.TryGetValue(i, out var procedure))
            {
                browser.AppendLine(lines[i]);
                continue;
            }

            browser.AppendLine(lines[i]);
            AppendStub(browser, manifest.Values.Single(x => x.Name.Equals(procedure.Name, StringComparison.OrdinalIgnoreCase)));
            browser.AppendLine(lines[procedure.EndLine]);
            i = procedure.EndLine;
        }

        AppendClientRuntime(browser);

        return new BrowserWasmServerBridgePlan(
            browser.ToString(),
            manifest,
            remote.Any(x => ServerType.IsMatch(BodyText(lines, x)) && Regex.IsMatch(BodyText(lines, x), @"\bXPAi(?:Response)?\b", RegexOptions.IgnoreCase)),
            remote.Any(x => Regex.IsMatch(BodyText(lines, x), @"\bXPDBSQLite\b", RegexOptions.IgnoreCase)) || serverStateGlobals.Any(name => moduleGlobals.TryGetValue(name, out var type) && type.Equals("XPDBSQLite", StringComparison.OrdinalIgnoreCase)),
            remote.Any(x => Regex.IsMatch(BodyText(lines, x), @"\bXPDbMsSql\b", RegexOptions.IgnoreCase)) || serverStateGlobals.Any(name => moduleGlobals.TryGetValue(name, out var type) && type.Equals("XPDbMsSql", StringComparison.OrdinalIgnoreCase)));
    }

    private static IReadOnlyList<ProcedureBlock> ParseProcedures(string[] lines)
    {
        var result = new List<ProcedureBlock>();
        var classDepth = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var clean = StripComment(lines[i]).Trim();
            if (Regex.IsMatch(clean, @"^(?:(?:Public|Private)\s+)?Class\b", RegexOptions.IgnoreCase))
            {
                classDepth++;
                continue;
            }
            if (Regex.IsMatch(clean, @"^End\s+Class$", RegexOptions.IgnoreCase))
            {
                classDepth = Math.Max(0, classDepth - 1);
                continue;
            }

            var match = ProcedureHeader.Match(clean);
            if (!match.Success) continue;

            var isFunction = match.Groups[1].Value.Equals("Function", StringComparison.OrdinalIgnoreCase);
            var name = match.Groups[2].Value;
            var returnType = isFunction && !string.IsNullOrWhiteSpace(match.Groups[4].Value) ? match.Groups[4].Value : isFunction ? "Variant" : "Void";
            var parameters = ParseParameters(match.Groups[3].Value);
            var endPattern = isFunction ? @"^End\s+Function$" : @"^End\s+Sub$";
            var end = i + 1;
            for (; end < lines.Length; end++)
            {
                if (Regex.IsMatch(StripComment(lines[end]).Trim(), endPattern, RegexOptions.IgnoreCase)) break;
            }
            if (end >= lines.Length)
                throw new XpsWebCompilationException($"browser-wasm server bridge could not find the end of procedure '{name}'.");

            result.Add(new ProcedureBlock(i, end, classDepth, name, isFunction, returnType, parameters));
            i = end;
        }

        return result;
    }

    private static Dictionary<string, string> ParseModuleGlobals(string[] lines, IReadOnlyList<ProcedureBlock> procedures)
    {
        var procedureLines = new HashSet<int>();
        foreach (var procedure in procedures)
            for (var i = procedure.StartLine; i <= procedure.EndLine; i++) procedureLines.Add(i);

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var classDepth = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            if (procedureLines.Contains(i)) continue;
            var clean = StripComment(lines[i]).Trim();
            if (Regex.IsMatch(clean, @"^(?:(?:Public|Private)\s+)?Class\b", RegexOptions.IgnoreCase)) { classDepth++; continue; }
            if (Regex.IsMatch(clean, @"^End\s+Class$", RegexOptions.IgnoreCase)) { classDepth = Math.Max(0, classDepth - 1); continue; }
            if (classDepth != 0) continue;
            var match = ModuleDeclaration.Match(clean);
            if (match.Success) result[match.Groups[1].Value] = match.Groups[2].Value;
        }
        return result;
    }

    private static IReadOnlyList<BrowserWasmServerBridgeParameter> ParseParameters(string raw)
    {
        var result = new List<BrowserWasmServerBridgeParameter>();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        foreach (var part in SplitArguments(raw))
        {
            var clean = part.Trim();
            if (Regex.IsMatch(clean, @"\bByRef\b", RegexOptions.IgnoreCase))
                throw new XpsWebCompilationException("browser-wasm server bridge does not support ByRef parameters. Split the server operation into a value-returning helper function.");
            if (Regex.IsMatch(clean, @"\(\)\s*(?:As\b|$)", RegexOptions.IgnoreCase) || Regex.IsMatch(clean, @"\bList\b", RegexOptions.IgnoreCase))
                throw new XpsWebCompilationException("browser-wasm server bridge does not support array or List parameters.");

            var match = Regex.Match(clean,
                @"^(?:ByVal\s+)?([A-Za-z_]\w*)\s*(?:As\s+([A-Za-z_]\w*))?$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
                throw new XpsWebCompilationException("browser-wasm server bridge encountered an unsupported procedure parameter: " + clean);
            result.Add(new BrowserWasmServerBridgeParameter(
                match.Groups[1].Value,
                string.IsNullOrWhiteSpace(match.Groups[2].Value) ? "Variant" : match.Groups[2].Value));
        }
        return result;
    }

    private static void ValidateRemoteProcedure(ProcedureBlock procedure)
    {
        if (procedure.ClassDepth != 0)
            throw new XpsWebCompilationException($"browser-wasm server bridge cannot proxy class method '{procedure.Name}' yet. Move the server-only operation to a module Function or Sub.");
        if (procedure.Name.Equals("Main", StringComparison.OrdinalIgnoreCase) || procedure.Name.Equals("Index", StringComparison.OrdinalIgnoreCase))
            throw new XpsWebCompilationException($"browser-wasm entry procedure '{procedure.Name}' contains server-only code. Move XPAi/XPDB work into a helper Function or Sub so the browser entry point can remain local.");
    }

    private static void ValidateSerializableSignature(ProcedureBlock procedure)
    {
        foreach (var parameter in procedure.Parameters)
        {
            if (!SerializableTypes.Contains(parameter.TypeName))
                throw new XpsWebCompilationException($"browser-wasm server bridge parameter '{parameter.Name}' in '{procedure.Name}' uses non-serializable type '{parameter.TypeName}'. Use a scalar or Variant containing native JSON.");
        }
        if (procedure.IsFunction && !SerializableTypes.Contains(procedure.ReturnType))
            throw new XpsWebCompilationException($"browser-wasm server bridge Function '{procedure.Name}' returns non-serializable type '{procedure.ReturnType}'. Use a scalar or Variant containing native JSON.");
    }

    private static bool IsServerStateGlobalLine(
        int lineIndex,
        string[] lines,
        IReadOnlyList<ProcedureBlock> procedures,
        IReadOnlyDictionary<string, string> moduleGlobals,
        IReadOnlySet<string> serverStateGlobals)
    {
        if (procedures.Any(p => lineIndex >= p.StartLine && lineIndex <= p.EndLine)) return false;
        var match = ModuleDeclaration.Match(StripComment(lines[lineIndex]).Trim());
        return match.Success && serverStateGlobals.Contains(match.Groups[1].Value) && moduleGlobals.ContainsKey(match.Groups[1].Value);
    }

    private static void AppendStub(StringBuilder output, BrowserWasmServerBridgeProcedure procedure)
    {
        var suffix = procedure.Id[..8];
        var argsName = "XpscriptWasmBridgeArgs" + suffix;
        var resultName = "XpscriptWasmBridgeResult" + suffix;
        output.AppendLine($"    Dim {argsName} As New JsonArray");
        foreach (var parameter in procedure.Parameters)
            output.AppendLine($"    Call {argsName}.Add({parameter.Name})");

        if (!procedure.IsFunction)
        {
            output.AppendLine($"    Dim {resultName} As Variant");
            output.AppendLine($"    {resultName} = {InvokeFunction}(\"{procedure.Id}\", {argsName})");
            return;
        }

        var call = $"{InvokeFunction}(\"{procedure.Id}\", {argsName})";
        output.AppendLine($"    {procedure.Name} = {ConvertReturn(call, procedure.ReturnType)}");
    }

    private static string ConvertReturn(string expression, string typeName) => typeName.ToUpperInvariant() switch
    {
        "STRING" => $"CStr({expression})",
        "INTEGER" => $"CInt({expression})",
        "LONG" => $"CLng({expression})",
        "DOUBLE" => $"CDbl({expression})",
        "SINGLE" => $"CSng({expression})",
        "BOOLEAN" => $"CBool({expression})",
        "BYTE" => $"CByte({expression})",
        "CURRENCY" => $"CCur({expression})",
        "DATE" => $"CDate({expression})",
        _ => expression
    };

    private static void AppendClientRuntime(StringBuilder output)
    {
        output.AppendLine();
        output.AppendLine("Private " + CapabilityVariable + " As String");
        output.AppendLine();
        output.AppendLine("Private Function " + CapabilityFunction + "() As String");
        output.AppendLine("    Dim http As New HttpClient");
        output.AppendLine("    Dim document As JsonDocument");
        output.AppendLine("    Dim root As Variant");
        output.AppendLine("    If " + CapabilityVariable + " = \"\" Then");
        output.AppendLine("        Call http.SetHeader(\"X-XPS-WASM-Bridge\", \"1\")");
        output.AppendLine("        Set document = http.GetJson(\"__xpscript_bridge/capability\")");
        output.AppendLine("        root = document.Root.AsObject()");
        output.AppendLine("        " + CapabilityVariable + " = CStr(root.Get(\"capability\"))");
        output.AppendLine("    End If");
        output.AppendLine("    Call http.Dispose()");
        output.AppendLine("    " + CapabilityFunction + " = " + CapabilityVariable);
        output.AppendLine("End Function");
        output.AppendLine();
        output.AppendLine("Private Function " + InvokeFunction + "(procedureId As String, arguments As Variant) As Variant");
        output.AppendLine("    Dim http As New HttpClient");
        output.AppendLine("    Dim payload As New JsonObject");
        output.AppendLine("    Dim response As HttpResponse");
        output.AppendLine("    Dim document As JsonDocument");
        output.AppendLine("    Dim root As Variant");
        output.AppendLine("    Call http.SetHeader(\"X-XPS-WASM-Bridge\", \"1\")");
        output.AppendLine("    Call http.SetHeader(\"X-XPS-WASM-Capability\", " + CapabilityFunction + "())");
        output.AppendLine("    Call payload.Set(\"procedure\", procedureId)");
        output.AppendLine("    Call payload.Set(\"arguments\", arguments)");
        output.AppendLine("    Set response = http.PostJson(\"__xpscript_bridge\", payload)");
        output.AppendLine("    Set document = response.Json()");
        output.AppendLine("    root = document.Root.AsObject()");
        output.AppendLine("    " + InvokeFunction + " = root.Get(\"result\")");
        output.AppendLine("    Call http.Dispose()");
        output.AppendLine("End Function");
    }

    private static void EnsureHelperNamesAreAvailable(string source)
    {
        foreach (var name in new[] { CapabilityVariable, CapabilityFunction, InvokeFunction })
        {
            if (Regex.IsMatch(source, $@"\b{Regex.Escape(name)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                throw new XpsWebCompilationException("browser-wasm server bridge reserved identifier is already used by the source: " + name);
        }
    }

    private static string ProcedureId(string sourceIdentity, string procedureName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sourceIdentity + "\0" + procedureName.ToUpperInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..32];
    }

    private static bool IsServerType(string typeName) =>
        typeName.Equals("XPAi", StringComparison.OrdinalIgnoreCase) ||
        typeName.Equals("XPAiResponse", StringComparison.OrdinalIgnoreCase) ||
        typeName.Equals("XPDBSQLite", StringComparison.OrdinalIgnoreCase) ||
        typeName.Equals("XPDbMsSql", StringComparison.OrdinalIgnoreCase);

    private static string BodyText(string[] lines, ProcedureBlock procedure) =>
        string.Join("\n", lines[(procedure.StartLine + 1)..procedure.EndLine]);

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }

    private static IReadOnlyList<string> SplitArguments(string raw)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        var inString = false;
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '"')
            {
                if (inString && i + 1 < raw.Length && raw[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')') depth = Math.Max(0, depth - 1);
            else if (c == ',' && depth == 0)
            {
                result.Add(raw[start..i]);
                start = i + 1;
            }
        }
        result.Add(raw[start..]);
        return result;
    }

    private sealed record ProcedureBlock(
        int StartLine,
        int EndLine,
        int ClassDepth,
        string Name,
        bool IsFunction,
        string ReturnType,
        IReadOnlyList<BrowserWasmServerBridgeParameter> Parameters);
}
