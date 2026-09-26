using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ParameterPassingPostProcessor
{
    private const string ByRefPrefix = "__xps_byref_";
    private const string ByValPrefix = "__xps_byval_";

    private sealed record ProcedureSignature(string Name, bool[] ByRef, bool ReturnsVoid);

    private static readonly Regex MethodDeclaration = new(
        @"^(?<indent>\s*)(?<prefix>(?:public|private|internal)\s+(?:static\s+)?[^\r\n(]+?\s+)(?<name>[A-Za-z_]\w*)\((?<params>[^\r\n()]*)\)(?<tail>\s*)$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string Transform(string generated)
    {
        if (string.IsNullOrEmpty(generated)) return generated;

        var signatures = new Dictionary<string, ProcedureSignature>(StringComparer.OrdinalIgnoreCase);
        generated = MethodDeclaration.Replace(generated, match => RewriteDeclaration(match, signatures));

        foreach (var signature in signatures.Values.OrderByDescending(x => x.Name.Length))
            generated = RewriteCalls(generated, signature);

        return generated + "\n\n" + ByRefCallRuntimeSource + "\n";
    }

    private static string RewriteDeclaration(Match match, IDictionary<string, ProcedureSignature> signatures)
    {
        var rawParameters = SplitArguments(match.Groups["params"].Value);
        if (rawParameters.Count == 0 || rawParameters.All(x => !x.Contains(ByRefPrefix, StringComparison.Ordinal) && !x.Contains(ByValPrefix, StringComparison.Ordinal)))
            return match.Value;

        var byRef = new bool[rawParameters.Count];
        for (var i = 0; i < rawParameters.Count; i++)
        {
            var parameter = rawParameters[i].Trim();
            if (parameter.Contains(ByRefPrefix, StringComparison.Ordinal))
            {
                byRef[i] = true;
                if (!Regex.IsMatch(parameter, @"^ref\s+", RegexOptions.CultureInvariant))
                    rawParameters[i] = "ref " + parameter;
            }
        }

        var name = match.Groups["name"].Value;
        var prefix = match.Groups["prefix"].Value;
        var returnsVoid = Regex.IsMatch(prefix, @"\bvoid\s+$", RegexOptions.CultureInvariant);
        signatures[name] = new ProcedureSignature(name, byRef, returnsVoid);
        return match.Groups["indent"].Value + prefix + name + "(" + string.Join(", ", rawParameters.Select(x => x.Trim())) + ")" + match.Groups["tail"].Value;
    }

    private static string RewriteCalls(string generated, ProcedureSignature signature)
    {
        var output = new StringBuilder(generated.Length + 64);
        var i = 0;
        while (i < generated.Length)
        {
            if (IsStartOfStringOrComment(generated, i, out var copiedTo))
            {
                output.Append(generated, i, copiedTo - i);
                i = copiedTo;
                continue;
            }

            if (!IsIdentifierStart(generated[i]))
            {
                output.Append(generated[i++]);
                continue;
            }

            var start = i++;
            while (i < generated.Length && IsIdentifierPart(generated[i])) i++;
            var identifier = generated[start..i];
            if (!identifier.Equals(signature.Name, StringComparison.OrdinalIgnoreCase))
            {
                output.Append(identifier);
                continue;
            }

            var afterName = i;
            while (afterName < generated.Length && char.IsWhiteSpace(generated[afterName])) afterName++;
            if (afterName >= generated.Length || generated[afterName] != '(' || IsDeclarationOccurrence(generated, start))
            {
                output.Append(generated, start, i - start);
                continue;
            }

            var close = FindMatchingParen(generated, afterName);
            if (close < 0)
            {
                output.Append(generated, start, i - start);
                continue;
            }

            var rawArgs = generated[(afterName + 1)..close];
            var args = SplitArguments(rawArgs);
            if (args.Count != signature.ByRef.Length)
            {
                output.Append(generated, start, close - start + 1);
                i = close + 1;
                continue;
            }

            var needsTemporaryScope = false;
            for (var argIndex = 0; argIndex < args.Count; argIndex++)
            {
                if (signature.ByRef[argIndex] && !IsDirectRefArgument(args[argIndex].Trim()))
                {
                    needsTemporaryScope = true;
                    break;
                }
            }

            if (!needsTemporaryScope)
            {
                for (var argIndex = 0; argIndex < args.Count; argIndex++)
                {
                    if (!signature.ByRef[argIndex]) continue;
                    var trimmed = args[argIndex].TrimStart();
                    if (!trimmed.StartsWith("ref ", StringComparison.Ordinal))
                        args[argIndex] = "ref " + args[argIndex].Trim();
                }

                output.Append(identifier);
                output.Append(generated, i, afterName - i);
                output.Append('(').Append(string.Join(", ", args.Select(x => x.Trim()))).Append(')');
                i = close + 1;
                continue;
            }

            var receiver = TakeTrailingMemberReceiver(output);
            var callTarget = receiver.Length == 0 ? identifier : receiver + identifier;
            output.Append(BuildTemporaryByRefCall(callTarget, args, signature));
            i = close + 1;
        }
        return output.ToString();
    }

    private static string BuildTemporaryByRefCall(string callTarget, IReadOnlyList<string> sourceArgs, ProcedureSignature signature)
    {
        var callArgs = new string[sourceArgs.Count];
        var declarations = new List<string>();
        var writeBacks = new List<string>();

        for (var argIndex = 0; argIndex < sourceArgs.Count; argIndex++)
        {
            var argument = sourceArgs[argIndex].Trim();
            if (!signature.ByRef[argIndex])
            {
                callArgs[argIndex] = argument;
                continue;
            }

            if (IsDirectRefArgument(argument))
            {
                callArgs[argIndex] = "ref " + NormalizeDirectRefArgument(argument);
                continue;
            }

            var tempName = "__xps_byref_temp_" + argIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
            declarations.Add("var " + tempName + " = " + argument + ";");
            callArgs[argIndex] = "ref " + tempName;
            if (IsAssignableArgument(argument))
                writeBacks.Add(argument + " = " + tempName + ";");
        }

        var body = new StringBuilder();
        body.Append("XPScriptByRefCallRuntime.Invoke(() => { ");
        foreach (var declaration in declarations) body.Append(declaration).Append(' ');

        if (signature.ReturnsVoid)
        {
            body.Append(callTarget).Append('(').Append(string.Join(", ", callArgs)).Append("); ");
            foreach (var writeBack in writeBacks) body.Append(writeBack).Append(' ');
            body.Append("})");
        }
        else
        {
            body.Append("var __xps_byref_result = ").Append(callTarget).Append('(').Append(string.Join(", ", callArgs)).Append("); ");
            foreach (var writeBack in writeBacks) body.Append(writeBack).Append(' ');
            body.Append("return __xps_byref_result; })");
        }

        return body.ToString();
    }

    private static string TakeTrailingMemberReceiver(StringBuilder output)
    {
        if (output.Length < 2) return "";

        var end = output.Length;
        while (end > 0 && char.IsWhiteSpace(output[end - 1])) end--;
        if (end < 2 || output[end - 1] != '.') return "";

        var dot = end - 1;
        var cursor = dot - 1;
        while (cursor >= 0 && (output[cursor] == '!' || output[cursor] == '?')) cursor--;
        if (cursor < 0) return "";

        int start;
        if (output[cursor] == ')')
        {
            start = FindMatchingOpenParen(output, cursor);
            if (start < 0) return "";
        }
        else
        {
            if (!IsIdentifierPart(output[cursor])) return "";
            while (cursor >= 0 && (IsIdentifierPart(output[cursor]) || output[cursor] == '.')) cursor--;
            start = cursor + 1;
        }

        var receiver = output.ToString(start, dot - start + 1);
        output.Length = start;
        return receiver;
    }

    private static int FindMatchingOpenParen(StringBuilder value, int closeIndex)
    {
        var depth = 0;
        for (var i = closeIndex; i >= 0; i--)
        {
            if (value[i] == ')') depth++;
            else if (value[i] == '(' && --depth == 0) return i;
        }
        return -1;
    }

    private static bool IsDirectRefArgument(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("ref ", StringComparison.Ordinal))
            trimmed = trimmed[4..].Trim();
        return Regex.IsMatch(trimmed, @"^[A-Za-z_]\w*$", RegexOptions.CultureInvariant);
    }

    private static string NormalizeDirectRefArgument(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("ref ", StringComparison.Ordinal) ? trimmed[4..].Trim() : trimmed;
    }

    private static bool IsAssignableArgument(string value) =>
        Regex.IsMatch(value, @"^(?:this\.)?[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*$", RegexOptions.CultureInvariant);

    private static bool IsDeclarationOccurrence(string generated, int identifierStart)
    {
        var lineStart = generated.LastIndexOf('\n', Math.Max(0, identifierStart - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var prefix = generated[lineStart..identifierStart].TrimStart();
        return prefix.StartsWith("public ", StringComparison.Ordinal)
            || prefix.StartsWith("private ", StringComparison.Ordinal)
            || prefix.StartsWith("internal ", StringComparison.Ordinal);
    }

    private static int FindMatchingParen(string value, int openIndex)
    {
        var depth = 0;
        var inString = false;
        var inChar = false;
        for (var i = openIndex; i < value.Length; i++)
        {
            var c = value[i];
            if (inString)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"') inString = false;
                continue;
            }
            if (inChar)
            {
                if (c == '\\') { i++; continue; }
                if (c == '\'') inChar = false;
                continue;
            }
            if (c == '"') { inString = true; continue; }
            if (c == '\'') { inChar = true; continue; }
            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return i;
        }
        return -1;
    }

    private static List<string> SplitArguments(string value)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(value)) return result;

        var current = new StringBuilder();
        var parens = 0;
        var brackets = 0;
        var braces = 0;
        var inString = false;
        var inChar = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (inString)
            {
                current.Append(c);
                if (c == '\\' && i + 1 < value.Length) current.Append(value[++i]);
                else if (c == '"') inString = false;
                continue;
            }
            if (inChar)
            {
                current.Append(c);
                if (c == '\\' && i + 1 < value.Length) current.Append(value[++i]);
                else if (c == '\'') inChar = false;
                continue;
            }
            if (c == '"') { inString = true; current.Append(c); continue; }
            if (c == '\'') { inChar = true; current.Append(c); continue; }
            if (c == '(') parens++;
            else if (c == ')') parens--;
            else if (c == '[') brackets++;
            else if (c == ']') brackets--;
            else if (c == '{') braces++;
            else if (c == '}') braces--;
            else if (c == ',' && parens == 0 && brackets == 0 && braces == 0)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }

    private static bool IsStartOfStringOrComment(string value, int index, out int copiedTo)
    {
        copiedTo = index;
        if (value[index] == '"')
        {
            var i = index + 1;
            while (i < value.Length)
            {
                if (value[i] == '\\') { i += 2; continue; }
                if (value[i++] == '"') break;
            }
            copiedTo = Math.Min(i, value.Length);
            return true;
        }
        if (value[index] == '\'')
        {
            var i = index + 1;
            while (i < value.Length)
            {
                if (value[i] == '\\') { i += 2; continue; }
                if (value[i++] == '\'') break;
            }
            copiedTo = Math.Min(i, value.Length);
            return true;
        }
        if (index + 1 < value.Length && value[index] == '/' && value[index + 1] == '/')
        {
            var newline = value.IndexOf('\n', index + 2);
            copiedTo = newline < 0 ? value.Length : newline;
            return true;
        }
        if (index + 1 < value.Length && value[index] == '/' && value[index + 1] == '*')
        {
            var end = value.IndexOf("*/", index + 2, StringComparison.Ordinal);
            copiedTo = end < 0 ? value.Length : end + 2;
            return true;
        }
        return false;
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private const string ByRefCallRuntimeSource = """
internal static class XPScriptByRefCallRuntime
{
    internal static void Invoke(System.Action action) => action();
    internal static T Invoke<T>(System.Func<T> action) => action();
}
""";
}
