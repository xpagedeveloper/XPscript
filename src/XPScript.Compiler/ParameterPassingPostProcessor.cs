using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ParameterPassingPostProcessor
{
    private const string ByRefPrefix = "__xps_byref_";
    private const string ByValPrefix = "__xps_byval_";

    private sealed record ProcedureSignature(string Name, bool[] ByRef);

    private static readonly Regex MethodDeclaration = new(
        @"^(?<indent>\s*)(?<prefix>(?:public|private|internal)\s+(?:static\s+)?[^\r\n(]+?\s+)(?<name>[A-Za-z_]\w*)\((?<params>[^\r\n()]*)\)(?<tail>\s*)$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string Transform(string generated)
    {
        if (string.IsNullOrEmpty(generated)) return generated;

        var signatures = new Dictionary<string, ProcedureSignature>(StringComparer.OrdinalIgnoreCase);
        generated = MethodDeclaration.Replace(generated, match => RewriteDeclaration(match, signatures));
        if (signatures.Count == 0) return generated;

        foreach (var signature in signatures.Values.OrderByDescending(x => x.Name.Length))
            generated = RewriteCalls(generated, signature);
        return generated;
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
        signatures[name] = new ProcedureSignature(name, byRef);
        return match.Groups["indent"].Value + match.Groups["prefix"].Value + name + "(" + string.Join(", ", rawParameters.Select(x => x.Trim())) + ")" + match.Groups["tail"].Value;
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
            if (args.Count == signature.ByRef.Length)
            {
                for (var argIndex = 0; argIndex < args.Count; argIndex++)
                {
                    if (!signature.ByRef[argIndex]) continue;
                    var trimmed = args[argIndex].TrimStart();
                    if (!trimmed.StartsWith("ref ", StringComparison.Ordinal))
                        args[argIndex] = "ref " + args[argIndex].Trim();
                }
            }

            output.Append(identifier);
            output.Append(generated, i, afterName - i);
            output.Append('(').Append(string.Join(", ", args.Select(x => x.Trim()))).Append(')');
            i = close + 1;
        }
        return output.ToString();
    }

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
        if (value[index] == '\'' )
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
            var end = value.IndexOf('\n', index + 2);
            copiedTo = end < 0 ? value.Length : end;
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
}
