using System.Text;
using System.Text.RegularExpressions;

namespace LSLite.Compiler;

internal sealed class ExtendedCompatibilityTranspiler
{
    private static readonly string[] SaxTypes = ["NotesSAXParser", "NotesSAXAttributeList", "NotesSAXException"];
    private static readonly string[] JsonHttpTypes = ["NotesHTTPRequest", "NotesJSONNavigator", "NotesJSONObject", "NotesJSONArray", "NotesJSONElement"];
    private static readonly string[] ExtendedFunctions =
    [
        "Environ", "Format", "FormatNumber", "FormatPercent", "Evaluate", "GetObject", "InputBox", "MessageBox", "Shell"
    ];

    private static readonly Dictionary<string, string[]> TypeMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NotesHTTPRequest"] =
        [
            "Get", "Post", "Put", "Patch", "DeleteResource", "SetHeaderField", "ResetHeaders", "GetResponseHeaders",
            "SetProxy", "SetProxyUser", "ResetProxy", "ResponseCode", "TimeoutSec", "MaxRedirects", "PreferStrings",
            "PreferUTF8", "PreferJSONNavigator"
        ],
        ["NotesJSONNavigator"] =
        [
            "GetElementByName", "GetElementByPointer", "GetFirstElement", "GetNextElement", "GetNthElement", "Stringify",
            "AppendElement", "AppendArray", "AppendObject", "PreferJSONNavigator", "PreferUTF8"
        ],
        ["NotesJSONObject"] =
        [
            "Size", "GetElementByName", "GetFirstElement", "GetNextElement", "GetNthElement", "AppendElement", "AppendArray",
            "AppendObject", "Copy"
        ],
        ["NotesJSONArray"] =
        [
            "Size", "GetFirstElement", "GetNextElement", "GetNthElement", "AppendElement", "AppendArray", "AppendObject", "Copy"
        ],
        ["NotesJSONElement"] = ["Name", "Type", "Value", "Copy"]
    };

    private static readonly Dictionary<string, string[]> NoArgMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NotesHTTPRequest"] = ["ResetHeaders", "GetResponseHeaders", "ResetProxy"],
        ["NotesJSONNavigator"] = ["GetFirstElement", "GetNextElement", "Stringify"],
        ["NotesJSONObject"] = ["GetFirstElement", "GetNextElement"],
        ["NotesJSONArray"] = ["GetFirstElement", "GetNextElement"]
    };

    public string Transform(string source)
    {
        var lines = JoinContinuations(source);
        var output = new List<string>();
        var compatibilityVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var indent = Regex.Match(raw, @"^\s*").Value;
            var line = StripComment(raw).Trim();
            if (line.Length == 0)
            {
                output.Add(raw);
                continue;
            }

            DiscoverCompatibilityVariables(line, compatibilityVariables);

            var dimNewSax = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+NotesSAXParser\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNewSax.Success)
            {
                output.Add(indent + $"Dim {dimNewSax.Groups[1].Value} As Variant");
                output.Add(indent + $"{dimNewSax.Groups[1].Value} = LSSaxRuntime.CreateParser({dimNewSax.Groups[2].Value})");
                continue;
            }

            var setNewSax = Regex.Match(line, @"^Set\s+([A-Za-z_]\w*)\s*=\s*New\s+NotesSAXParser\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (setNewSax.Success)
            {
                output.Add(indent + $"{setNewSax.Groups[1].Value} = LSSaxRuntime.CreateParser({setNewSax.Groups[2].Value})");
                continue;
            }

            var dimNewCompat = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+(NotesHTTPRequest|NotesJSONNavigator|NotesJSONObject|NotesJSONArray|NotesJSONElement)\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNewCompat.Success)
            {
                var variable = dimNewCompat.Groups[1].Value;
                var type = CanonicalType(dimNewCompat.Groups[2].Value);
                compatibilityVariables[variable] = type;
                output.Add(indent + $"Dim {variable} As Variant");
                output.Add(indent + $"{variable} = {BuildCompatibilityConstructor(type, dimNewCompat.Groups[3].Value)}");
                continue;
            }

            var setNewCompat = Regex.Match(line, @"^Set\s+([A-Za-z_]\w*)\s*=\s*New\s+(NotesHTTPRequest|NotesJSONNavigator|NotesJSONObject|NotesJSONArray|NotesJSONElement)\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (setNewCompat.Success)
            {
                var variable = setNewCompat.Groups[1].Value;
                var type = CanonicalType(setNewCompat.Groups[2].Value);
                compatibilityVariables[variable] = type;
                output.Add(indent + $"{variable} = {BuildCompatibilityConstructor(type, setNewCompat.Groups[3].Value)}");
                continue;
            }

            var directNewCompat = Regex.Match(line, @"^([A-Za-z_]\w*)\s*=\s*New\s+(NotesHTTPRequest|NotesJSONNavigator|NotesJSONObject|NotesJSONArray|NotesJSONElement)\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (directNewCompat.Success)
            {
                var variable = directNewCompat.Groups[1].Value;
                var type = CanonicalType(directNewCompat.Groups[2].Value);
                compatibilityVariables[variable] = type;
                output.Add(indent + $"{variable} = {BuildCompatibilityConstructor(type, directNewCompat.Groups[3].Value)}");
                continue;
            }

            var legacySession = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+NotesSession\s*(?:\(\s*\))?\s*$", RegexOptions.IgnoreCase);
            if (legacySession.Success)
            {
                output.Add(indent + $"Dim {legacySession.Groups[1].Value} As Variant");
                output.Add(indent + $"{legacySession.Groups[1].Value} = Nothing");
                continue;
            }

            line = RewriteSessionFactories(line);
            line = CanonicalizeCompatibilityMembers(line, compatibilityVariables);
            line = RewriteNoArgCompatibilityCalls(line, compatibilityVariables);

            var compatibilityStatement = RewriteCompatibilityStatement(line, compatibilityVariables);
            if (compatibilityStatement is not null)
            {
                output.Add(indent + compatibilityStatement);
                continue;
            }

            var onEventCall = Regex.Match(line, @"^On\s+Event\s+([A-Za-z_]\w*)\s+From\s+(.+?)\s+Call\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (onEventCall.Success)
            {
                output.Add(indent + $"Call LSSaxRuntime.Bind({onEventCall.Groups[2].Value}, \"{onEventCall.Groups[1].Value}\", \"{onEventCall.Groups[3].Value}\")");
                continue;
            }

            var onEventRemove = Regex.Match(line, @"^On\s+Event\s+([A-Za-z_]\w*)\s+From\s+(.+?)\s+Remove(?:\s+([A-Za-z_]\w*))?\s*$", RegexOptions.IgnoreCase);
            if (onEventRemove.Success)
            {
                var handler = string.IsNullOrWhiteSpace(onEventRemove.Groups[3].Value) ? "Nothing" : $"\"{onEventRemove.Groups[3].Value}\"";
                output.Add(indent + $"Call LSSaxRuntime.Remove({onEventRemove.Groups[2].Value}, \"{onEventRemove.Groups[1].Value}\", {handler})");
                continue;
            }

            var saxNoArgs = Regex.Match(line, @"^([A-Za-z_]\w*)\.(Process|Parse)\s*$", RegexOptions.IgnoreCase);
            if (saxNoArgs.Success)
            {
                output.Add(indent + $"Call {saxNoArgs.Groups[1].Value}.{saxNoArgs.Groups[2].Value}()");
                continue;
            }

            var saxStatementArgs = Regex.Match(line, @"^([A-Za-z_]\w*)\.(Parse|SetInput|SetOutput|Output)\s+(.+)$", RegexOptions.IgnoreCase);
            if (saxStatementArgs.Success && !saxStatementArgs.Groups[3].Value.TrimStart().StartsWith("(", StringComparison.Ordinal))
            {
                output.Add(indent + $"Call {saxStatementArgs.Groups[1].Value}.{saxStatementArgs.Groups[2].Value}({saxStatementArgs.Groups[3].Value})");
                continue;
            }

            var sleep = Regex.Match(line, @"^Sleep\s*(?:\((.+)\)|\s+(.+))$", RegexOptions.IgnoreCase);
            if (sleep.Success)
            {
                var expression = sleep.Groups[1].Success ? sleep.Groups[1].Value : sleep.Groups[2].Value;
                output.Add(indent + $"Call LSExtendedRuntime.Sleep({expression})");
                continue;
            }

            if (Regex.IsMatch(line, @"^Stop\s*$", RegexOptions.IgnoreCase))
            {
                output.Add(indent + "Call LSExtendedRuntime.Stop()");
                continue;
            }

            var messageStatement = Regex.Match(line, @"^(MessageBox|MsgBox)\s+(.+)$", RegexOptions.IgnoreCase);
            if (messageStatement.Success && !messageStatement.Groups[2].Value.TrimStart().StartsWith("(", StringComparison.Ordinal))
            {
                var args = FillOmittedArguments(messageStatement.Groups[2].Value);
                output.Add(indent + $"Call LSExtendedRuntime.MessageBox({args})");
                continue;
            }

            line = ReplaceOutsideStrings(line, @"(?<![\w.])Err\s*\(\s*\)", "Err");
            line = ReplaceOutsideStrings(line, @"(?<![\w.])Erl\s*\(\s*\)", "Erl");
            line = ReplaceOutsideStrings(line, @"(?<![\w.])Error\$(?!\s*\()", "Error()");

            foreach (var constant in JsonElementConstants)
                line = ReplaceOutsideStrings(line, $@"(?<![\w.]){Regex.Escape(constant.Key)}(?![\w])", constant.Value.ToString());

            foreach (var type in SaxTypes)
                line = ReplaceOutsideStrings(line, $@"\b{type}\b", "Variant");
            foreach (var type in JsonHttpTypes)
                line = ReplaceOutsideStrings(line, $@"\b{type}\b", "Variant");
            line = ReplaceOutsideStrings(line, @"\bNotesSession\b", "Variant");

            foreach (var fn in ExtendedFunctions)
            {
                if (fn.Equals("MessageBox", StringComparison.OrdinalIgnoreCase))
                {
                    line = ReplaceOutsideStrings(line, @"(?<![\w.])MsgBox\s*\(", "LSExtendedRuntime.MessageBox(");
                    line = ReplaceOutsideStrings(line, @"(?<![\w.])MessageBox\s*\(", "LSExtendedRuntime.MessageBox(");
                    continue;
                }

                var dollar = fn is "Environ" or "Format" ? @"\$?" : "";
                line = ReplaceOutsideStrings(line, $@"(?<![\w.]){Regex.Escape(fn)}{dollar}\s*\(", $"LSExtendedRuntime.{fn}(");
            }

            output.Add(indent + line);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static readonly Dictionary<string, int> JsonElementConstants = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Jsonelem_type_object"] = 1,
        ["Jsonelem_type_array"] = 2,
        ["Jsonelem_type_string"] = 3,
        ["Jsonelem_type_number"] = 4,
        ["Jsonelem_type_boolean"] = 5,
        ["Jsonelem_type_utf8_bytearray"] = 6,
        ["Jsonelem_type_empty"] = 64
    };

    private static void DiscoverCompatibilityVariables(string line, Dictionary<string, string> variables)
    {
        foreach (var type in JsonHttpTypes)
        {
            foreach (Match match in Regex.Matches(line, $@"\b([A-Za-z_]\w*)\s*(?:\(\))?\s+As\s+(?:New\s+)?{Regex.Escape(type)}\b", RegexOptions.IgnoreCase))
                variables[match.Groups[1].Value] = type;
        }
    }

    private static string CanonicalType(string value) =>
        JsonHttpTypes.First(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));

    private static string BuildCompatibilityConstructor(string type, string rawArgs)
    {
        var args = rawArgs?.Trim() ?? "";
        return type switch
        {
            "NotesHTTPRequest" => "LSJsonHttpRuntime.CreateHTTPRequest()",
            "NotesJSONNavigator" => $"LSJsonHttpRuntime.CreateJSONNavigator({(args.Length == 0 ? "Nothing" : args)})",
            "NotesJSONObject" => "LSJsonHttpRuntime.CreateJSONObject()",
            "NotesJSONArray" => "LSJsonHttpRuntime.CreateJSONArray()",
            "NotesJSONElement" => $"LSJsonHttpRuntime.CreateJSONElement({(args.Length == 0 ? "Nothing" : args)})",
            _ => throw new CompilerException("Unsupported standalone compatibility class: " + type)
        };
    }

    private static string RewriteSessionFactories(string line)
    {
        line = ReplaceOutsideStrings(line, @"\b[A-Za-z_]\w*\.CreateHTTPRequest\s*(?:\(\s*\))?", "LSJsonHttpRuntime.CreateHTTPRequest()");
        line = ReplaceFactoryCall(line, "CreateJSONNavigator", args => $"LSJsonHttpRuntime.CreateJSONNavigator({(string.IsNullOrWhiteSpace(args) ? "Nothing" : args)})");
        return line;
    }

    private static string CanonicalizeCompatibilityMembers(string line, Dictionary<string, string> variables)
    {
        foreach (var variable in variables)
        {
            if (!TypeMembers.TryGetValue(variable.Value, out var members)) continue;
            foreach (var member in members)
                line = ReplaceOutsideStrings(line, $@"\b{Regex.Escape(variable.Key)}\s*\.\s*{Regex.Escape(member)}\b", $"{variable.Key}.{member}");
        }
        return line;
    }

    private static string RewriteNoArgCompatibilityCalls(string line, Dictionary<string, string> variables)
    {
        foreach (var variable in variables)
        {
            if (!NoArgMethods.TryGetValue(variable.Value, out var methods)) continue;
            foreach (var method in methods)
            {
                line = ReplaceOutsideStrings(
                    line,
                    $@"\b{Regex.Escape(variable.Key)}\.{Regex.Escape(method)}\b(?!\s*\()",
                    $"{variable.Key}.{method}()");
            }
        }
        return line;
    }

    private static string? RewriteCompatibilityStatement(string line, Dictionary<string, string> variables)
    {
        var match = Regex.Match(line, @"^([A-Za-z_]\w*)\.([A-Za-z_]\w*)\s+(.+)$", RegexOptions.IgnoreCase);
        if (!match.Success || !variables.TryGetValue(match.Groups[1].Value, out var type) || !TypeMembers.TryGetValue(type, out var members))
            return null;

        var canonical = members.FirstOrDefault(x => x.Equals(match.Groups[2].Value, StringComparison.OrdinalIgnoreCase));
        if (canonical is null || canonical is "ResponseCode" or "TimeoutSec" or "MaxRedirects" or "PreferStrings" or "PreferUTF8" or "PreferJSONNavigator" or "Size" or "Name" or "Type" or "Value")
            return null;

        var args = match.Groups[3].Value.Trim();
        if (args.StartsWith("=", StringComparison.Ordinal) || args.StartsWith("(", StringComparison.Ordinal)) return null;
        return $"Call {match.Groups[1].Value}.{canonical}({FillOmittedArguments(args)})";
    }

    private static string ReplaceFactoryCall(string input, string methodName, Func<string, string> replacement)
    {
        var regex = new Regex($@"\b[A-Za-z_]\w*\.{Regex.Escape(methodName)}\s*\(", RegexOptions.IgnoreCase);
        var offset = 0;
        while (true)
        {
            var match = regex.Match(input, offset);
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

    private static string[] JoinContinuations(string source)
    {
        var physical = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var logical = new List<string>();
        var current = new StringBuilder();

        foreach (var raw in physical)
        {
            var code = StripComment(raw);
            var trimmed = code.TrimEnd();
            var continued = trimmed.EndsWith("_", StringComparison.Ordinal);
            if (continued) trimmed = trimmed[..^1].TrimEnd();

            if (current.Length == 0) current.Append(trimmed);
            else current.Append(' ').Append(trimmed.TrimStart());

            if (continued) continue;
            logical.Add(current.ToString());
            current.Clear();
        }

        if (current.Length > 0) logical.Add(current.ToString());
        return [.. logical];
    }

    private static string FillOmittedArguments(string raw)
    {
        var args = SplitArguments(raw);
        for (var i = 0; i < args.Count; i++)
            if (string.IsNullOrWhiteSpace(args[i])) args[i] = "Nothing";
        return string.Join(", ", args);
    }

    private static List<string> SplitArguments(string raw)
    {
        var result = new List<string>();
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
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0) { result.Add(current.ToString()); current.Clear(); continue; }
            }
            current.Append(c);
        }
        result.Add(current.ToString());
        return result;
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
                if (!inString)
                {
                    sb.Append(Regex.Replace(current.ToString(), pattern, replacement, RegexOptions.IgnoreCase));
                    current.Clear();
                    current.Append(c);
                    inString = true;
                }
                else
                {
                    current.Append(c);
                    sb.Append(current);
                    current.Clear();
                    inString = false;
                }
                continue;
            }
            current.Append(c);
        }

        if (current.Length > 0)
            sb.Append(inString ? current.ToString() : Regex.Replace(current.ToString(), pattern, replacement, RegexOptions.IgnoreCase));
        return sb.ToString();
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
}
