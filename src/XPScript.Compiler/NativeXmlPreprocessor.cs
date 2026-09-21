using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NativeXmlPreprocessor
{
    private const string NativeXmlTypePattern = "XPXmlDocument|XPXmlElement|XPXmlNode|XPXmlNodeCollection|XPXmlAttribute|XPXmlAttributeCollection|XPXmlValidationResult|XPXmlValidationError|XPXmlValidationErrorCollection";

    public string Transform(string source)
    {
        if (!source.Contains("Xml", StringComparison.OrdinalIgnoreCase)) return source;

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 8);
        var nativeVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();

            var dimNew = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+New\s+({NativeXmlTypePattern})\s*(?:\((.*)\))?\s*$", RegexOptions.IgnoreCase);
            if (dimNew.Success)
            {
                var name = dimNew.Groups[1].Value;
                var type = dimNew.Groups[2].Value;
                nativeVariables.Add(name);
                output.Add(indent + $"Dim {name} As Variant");
                output.Add(indent + $"{name} = {CreateExpression(type, dimNew.Groups[3].Value, raw)}");
                continue;
            }

            var dim = Regex.Match(line, $@"^Dim\s+([A-Za-z_]\w*)\s+As\s+({NativeXmlTypePattern})\s*$", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                nativeVariables.Add(dim.Groups[1].Value);
                output.Add(indent + $"Dim {dim.Groups[1].Value} As Variant");
                continue;
            }

            var rewritten = line;
            rewritten = Regex.Replace(rewritten, @"\bXPXmlDocument\.Parse\s*\(", "XPScriptNativeXml.Parse(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bXmlParse\s*\(", "XPScriptNativeXml.Parse(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bXmlStringify\s*\(", "XPScriptNativeXml.Stringify(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bXmlEscape\s*\(", "XPScriptNativeXml.Escape(", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPXmlDocument\s*(?:\(\s*\))?", "XPScriptNativeXml.CreateDocument()", RegexOptions.IgnoreCase);
            rewritten = Regex.Replace(rewritten, @"\bNew\s+XPXmlElement\s*\((.*)\)", "XPScriptNativeXml.CreateElement($1)", RegexOptions.IgnoreCase);

            var set = Regex.Match(rewritten, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (set.Success && (nativeVariables.Contains(set.Groups[1].Value) || set.Groups[2].Value.Contains("XPScriptNativeXml", StringComparison.Ordinal)))
                rewritten = set.Groups[1].Value + " = " + set.Groups[2].Value;

            output.Add(indent + rewritten);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static CompilerException NativeConstructorDiagnostic(
        string diagnosticCode,
        string message,
        string sourceLine,
        params (string Name, string Value)[] properties)
    {
        var safeSource = CompilerDiagnosticRedaction.MaskStringLiterals(sourceLine).TrimEnd();
        var position = Math.Max(1, sourceLine.IndexOf("New", StringComparison.OrdinalIgnoreCase) + 1);
        var diagnostic = new CompileDiagnostic
        {
            File = Path.GetFileName(ExpandedSourceContext.Current?.SourcePath ?? "script.xps"),
            Position = position,
            EndColumn = position + 3,
            Description = message,
            DiagnosticCode = diagnosticCode,
            Category = "syntax",
            Properties = properties.Select(property => new CompileDiagnosticProperty
            {
                Name = property.Name,
                Value = property.Value
            }).ToList(),
            SourceCode = safeSource,
            MarkedCode = safeSource + Environment.NewLine + new string(' ', Math.Max(0, position - 1)) + "^"
        };
        return new CompilerException(message, diagnosticCode, "syntax", [diagnostic]);
    }

    private static string CreateExpression(string type, string rawArguments, string sourceLine)
    {
        var args = rawArguments.Trim();
        if (type.Equals("XPXmlDocument", StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(args) ? "XPScriptNativeXml.CreateDocument()" : $"XPScriptNativeXml.Parse({args})";
        if (type.Equals("XPXmlElement", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(args)) throw NativeConstructorDiagnostic(CompilerDiagnosticCodes.MissingConstructorArgument, "XPXmlElement requires an element name argument.", sourceLine, ("symbol", "XPXmlElement"), ("symbolKind", "type"), ("expectedArgument", "element name"));
            return $"XPScriptNativeXml.CreateElement({args})";
        }
        throw NativeConstructorDiagnostic(CompilerDiagnosticCodes.InvalidNativeConstructor, "Only XPXmlDocument and XPXmlElement can be created with New.", sourceLine, ("symbol", type), ("symbolKind", "type"), ("expectedConstruct", "New XPXmlDocument or New XPXmlElement"));
    }
}
