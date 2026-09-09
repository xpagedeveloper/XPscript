using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class CompilerSourceLineDirectivePostProcessor
{
    private static readonly Regex MarkerPattern = new(
        @"XPSourceLineRuntime\.__XPSOURCE_(?<line>\d+)_(?<source>[0-9A-F]+)\(\)",
        RegexOptions.CultureInvariant);

    private static readonly Regex ScriptProcedurePattern = new(
        @"^\s*(?:public|private)\s+(?:static\s+)?(?:override\s+)?(?:[A-Za-z_]\w*(?:<[^>]+>)?(?:\[\])?\??\s+)?[A-Za-z_]\w*\s*\(",
        RegexOptions.CultureInvariant);

    private static readonly Regex SimpleAssignmentPattern = new(
        @"^\s*(?:(?:var|dynamic|bool|byte|short|int|long|float|double|decimal|string|object|DateTime)\s+)?(?<name>[A-Za-z_]\w*)\s*=\s*(?!=).+;\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ArraySetPattern = new(
        @"^(?<indent>\s*)LSArrayRuntime\.Set\((?<name>[A-Za-z_]\w*)\s*,\s*(?<arguments>.+)\);\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex MemberAssignmentPattern = new(
        @"^(?<indent>\s*)(?<target>(?:this|[A-Za-z_]\w*)\.[A-Za-z_]\w*)\s*=\s*(?!=)(?<value>.+);\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ByRefAssignmentPattern = new(
        @"^(?<indent>\s*)(?<name>[A-Za-z_]\w*)\.Value\s*=\s*(?!=)(?<value>.+);\s*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex ByRefCreatePattern = new(
        @"LSByRefRuntime\.Create\(\(\)\s*=>\s*\(object\?\)\((?<name>[A-Za-z_]\w*)\)\s*,",
        RegexOptions.CultureInvariant);

    private const string RuntimeBoundary = "internal static class LSControlRuntime";
    private const string ScriptBoundary = "internal static class Script";
    private const string NoInliningAttribute = "[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]";

    private const string ArrayMutationRuntime = """
internal static class XPScriptDebugArrayMutationRuntime
{
    public static void Set(string name, object? array, object? newValue, params object?[] indices)
    {
        LSArrayRuntime.Set(array, newValue, indices);
        var actual = LSArrayRuntime.Get(array, indices);
        var indexText = string.Join(",", indices.Select(RenderIndex));
        var elementName = name + "[" + indexText + "]";
        XPScriptDebugRuntime.TrackValue(elementName, actual);
        XPScriptDebugRuntime.TrackValue(name, "<array mutation " + elementName + ">");
    }

    private static string RenderIndex(object? value)
    {
        try { return XPScriptRuntime.CStr(value); }
        catch { return "?"; }
    }
}
""";

    private const string ByRefMutationRuntime = """
internal static class XPScriptDebugByRefMutationRuntime
{
    public static LSByRefValue Create(string name, global::System.Func<object?> get, global::System.Action<object?> set)
    {
        return new LSByRefValue(
            get,
            value =>
            {
                set(value);
                XPScriptDebugRuntime.TrackValue(name, get());
            });
    }
}
""";

    public string Transform(string generated)
    {
        if (string.IsNullOrEmpty(generated)) return generated;

        var lines = generated.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 96);
        var foundMarker = false;
        var runtimeBoundaryInserted = false;
        var scriptDeclarationSeen = false;
        var inScript = false;
        var trackNextSimpleAssignment = false;
        var needsArrayMutationRuntime = false;
        var needsByRefMutationRuntime = false;
        var temporaryId = 0;

        foreach (var rawLine in lines)
        {
            if (!runtimeBoundaryInserted && rawLine.Contains(RuntimeBoundary, StringComparison.Ordinal))
            {
                if (foundMarker) output.Add("#line default");
                runtimeBoundaryInserted = true;
                trackNextSimpleAssignment = false;
            }

            if (!scriptDeclarationSeen && rawLine.Trim().Equals(ScriptBoundary, StringComparison.Ordinal))
            {
                scriptDeclarationSeen = true;
                output.Add(rawLine);
                continue;
            }

            if (scriptDeclarationSeen && !inScript && rawLine.Trim().Equals("{", StringComparison.Ordinal))
            {
                inScript = true;
                output.Add(rawLine);
                continue;
            }

            if (inScript && rawLine.Equals("}", StringComparison.Ordinal))
                inScript = false;

            var match = MarkerPattern.Match(rawLine);
            if (!match.Success)
            {
                if (inScript && ScriptProcedurePattern.IsMatch(rawLine))
                {
                    var indent = Regex.Match(rawLine, @"^\s*").Value;
                    output.Add(indent + NoInliningAttribute);
                }

                var rewritten = rawLine;
                if (inScript)
                {
                    var byRefCreate = ByRefCreatePattern.Match(rewritten);
                    if (byRefCreate.Success)
                    {
                        var callerName = byRefCreate.Groups["name"].Value;
                        rewritten = ByRefCreatePattern.Replace(
                            rewritten,
                            "XPScriptDebugByRefMutationRuntime.Create(\"" + EscapeCSharpString(callerName) + "\", () => (object?)(" + callerName + "),",
                            1);
                        needsByRefMutationRuntime = true;
                    }
                }

                if (trackNextSimpleAssignment && inScript)
                {
                    var arraySet = ArraySetPattern.Match(rewritten);
                    if (arraySet.Success)
                    {
                        var name = arraySet.Groups["name"].Value;
                        rewritten = arraySet.Groups["indent"].Value +
                            "XPScriptDebugArrayMutationRuntime.Set(\"" + EscapeCSharpString(name) + "\", " +
                            name + ", " + arraySet.Groups["arguments"].Value + ");";
                        needsArrayMutationRuntime = true;
                        output.Add(rewritten);
                        trackNextSimpleAssignment = false;
                        continue;
                    }

                    var byRefAssignment = ByRefAssignmentPattern.Match(rewritten);
                    if (byRefAssignment.Success)
                    {
                        var indent = byRefAssignment.Groups["indent"].Value;
                        var name = byRefAssignment.Groups["name"].Value;
                        var temp = "__xps_dbg_value_" + (++temporaryId).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        output.Add(indent + "var " + temp + " = " + byRefAssignment.Groups["value"].Value + ";");
                        output.Add(indent + name + ".Value = " + temp + ";");
                        output.Add(indent + "XPScriptDebugRuntime.TrackValue(\"" + EscapeCSharpString(name) + "\", " + name + ".Value);");
                        trackNextSimpleAssignment = false;
                        continue;
                    }

                    var memberAssignment = MemberAssignmentPattern.Match(rewritten);
                    if (memberAssignment.Success)
                    {
                        var indent = memberAssignment.Groups["indent"].Value;
                        var target = memberAssignment.Groups["target"].Value;
                        var temp = "__xps_dbg_value_" + (++temporaryId).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        output.Add(indent + "var " + temp + " = " + memberAssignment.Groups["value"].Value + ";");
                        output.Add(indent + target + " = " + temp + ";");
                        output.Add(indent + "XPScriptDebugRuntime.TrackValue(\"" + EscapeCSharpString(target) + "\", " + temp + ");");
                        trackNextSimpleAssignment = false;
                        continue;
                    }
                }

                output.Add(rewritten);

                if (trackNextSimpleAssignment && inScript)
                {
                    var assignment = SimpleAssignmentPattern.Match(rewritten);
                    if (assignment.Success)
                    {
                        var name = assignment.Groups["name"].Value;
                        if (!name.StartsWith("__", StringComparison.Ordinal))
                        {
                            var indent = Regex.Match(rewritten, @"^\s*").Value;
                            output.Add(indent + "XPScriptDebugRuntime.TrackValue(\"" + EscapeCSharpString(name) + "\", " + name + ");");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(rewritten) && !rewritten.TrimStart().StartsWith("#", StringComparison.Ordinal))
                        trackNextSimpleAssignment = false;
                }
                continue;
            }

            foundMarker = true;
            trackNextSimpleAssignment = true;
            var markerIndent = Regex.Match(rawLine, @"^\s*").Value;
            var sourceLine = match.Groups["line"].Value;
            var sourceId = DecodeSourceId(match.Groups["source"].Value);
            var directiveSource = EscapeDirectiveString(sourceId);
            var sourceLiteral = EscapeCSharpString(sourceId);

            output.Add(markerIndent + "// XPSOURCE|" + sourceId + "|" + sourceLine);
            output.Add(markerIndent + "#line " + sourceLine + " \"" + directiveSource + "\"");
            output.Add(MarkerPattern.Replace(
                rawLine,
                "XPSourceLineRuntime.Set(" + sourceLine + ", \"" + sourceLiteral + "\")",
                1));
        }

        if (foundMarker && !runtimeBoundaryInserted)
            throw new CompilerException("Unable to restore generated source line mapping before runtime code.");

        if (needsArrayMutationRuntime)
            output.Add(ArrayMutationRuntime);
        if (needsByRefMutationRuntime)
            output.Add(ByRefMutationRuntime);

        return string.Join("\n", output);
    }

    private static string DecodeSourceId(string hex)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromHexString(hex));
        }
        catch (Exception)
        {
            throw new CompilerException("Invalid generated XPScript source mapping marker.");
        }
    }

    private static string EscapeDirectiveString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string EscapeCSharpString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
}
