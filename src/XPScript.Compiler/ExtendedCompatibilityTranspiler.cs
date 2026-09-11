using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

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
            "PreferUTF8", "PreferJSONNavigator", "CertificateValidation"
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

            var sleep = Regex.Match(line, @"^(?:Call\s+)?Sleep\s*(?:\((.+)\)|\s+(.+))$", RegexOptions.IgnoreCase);
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

            output.Add(indent + line);
        }

        return string.Join("\n", output);
    }

    private static string[] JoinContinuations(string source)
    {
        var input = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var output = new List<string>();
        var current = new StringBuilder();

        foreach (var line in input)
        {
            var trimmed = line.TrimEnd();
            if (trimmed.EndsWith(" _", StringComparison.Ordinal))
            {
                current.Append(trimmed[..^2]);
                current.Append(' ');
                continue;
            }

            if (current.Length > 0)
            {
                current.Append(line.TrimStart());
                output.Add(current.ToString());
                current.Clear();
            }
            else
            {
                output.Add(line);
            }
        }

        if (current.Length > 0) output.Add(current.ToString());
        return output.ToArray();
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++;
                    continue;
                }
                inString = !inString;
            }
            else if (line[i] == '\'' && !inString)
            {
                return line[..i];
            }
        }
        return line;
    }

    private static void DiscoverCompatibilityVariables(string line, Dictionary<string, string> variables)
    {
        var match = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s+As\s+(NotesHTTPRequest|NotesJSONNavigator|NotesJSONObject|NotesJSONArray|NotesJSONElement)\b", RegexOptions.IgnoreCase);
        if (match.Success) variables[match.Groups[1].Value] = CanonicalType(match.Groups[2].Value);
    }

    private static string CanonicalType(string value)
    {
        foreach (var type in JsonHttpTypes)
            if (string.Equals(type, value, StringComparison.OrdinalIgnoreCase)) return type;
        return value;
    }

    private static string RewriteSessionFactories(string line) => line;
    private static string CanonicalizeCompatibilityMembers(string line, Dictionary<string, string> variables) => line;
    private static string RewriteNoArgCompatibilityCalls(string line, Dictionary<string, string> variables) => line;
    private static string? RewriteCompatibilityStatement(string line, Dictionary<string, string> variables) => null;
    private static string BuildCompatibilityConstructor(string type, string args) => $"New {type}({args})";
    private static string FillOmittedArguments(string args) => args;
}