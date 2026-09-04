using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Web.Compiler;

public sealed record XpsOpenApiImportResult(
    string OpenApiVersion,
    string Source,
    IReadOnlyList<string> AddedClasses,
    IReadOnlyList<string> AddedProperties,
    IReadOnlyList<string> AddedProcedures,
    IReadOnlyList<string> Warnings)
{
    public bool Changed => AddedClasses.Count > 0 || AddedProperties.Count > 0 || AddedProcedures.Count > 0;
}

public sealed class XpsOpenApiImporter
{
    private static readonly Regex ClassPattern = new(
        @"(?ims)^(?<header>(?:(?:Public|Private)\s+)?Class\s+(?<name>[A-Za-z_]\w*)[^\r\n]*\r?\n)(?<body>.*?)(?<end>^End\s+Class\s*$)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FieldPattern = new(
        @"(?im)^(?<attrs>(?:\s*\[[^\r\n]+\]\r?\n)*)\s*Public\s+(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FunctionPattern = new(
        @"(?ims)^(?<prefix>(?:(?:\s*\[[^\r\n]+\]|\s*' OpenAPI responses:[^\r\n]*)\r?\n)*)(?<decl>(?:(?:Public|Private|Static)\s+)*Function\s+(?<name>[A-Za-z_]\w*)[^\r\n]*)(?:\r?\n)(?<body>.*?)^End\s+Function\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SubPattern = new(
        @"(?ims)^(?<prefix>(?:(?:\s*\[[^\r\n]+\]|\s*' OpenAPI responses:[^\r\n]*)\r?\n)*)(?<decl>(?:(?:Public|Private|Static)\s+)*Sub\s+(?<name>[A-Za-z_]\w*)[^\r\n]*)(?:\r?\n)(?<body>.*?)^End\s+Sub\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly XpsOpenApiGenerator _generator = new();

    public XpsOpenApiImportResult ImportFile(string specificationPath, string existingSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specificationPath);
        var fullPath = Path.GetFullPath(specificationPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("OpenAPI specification file was not found.", fullPath);
        return Import(File.ReadAllText(fullPath), existingSource, Path.GetFileName(fullPath));
    }

    public XpsOpenApiImportResult Import(string specification, string existingSource, string? sourceName = null)
    {
        ArgumentNullException.ThrowIfNull(existingSource);
        var desired = _generator.Generate(specification, sourceName);
        var source = existingSource;
        var newline = DetectNewline(source);
        var addedClasses = new List<string>();
        var addedProperties = new List<string>();
        var addedProcedures = new List<string>();
        var warnings = new List<string>();

        foreach (var desiredClass in ParseClasses(desired.Source))
        {
            var existingClass = FindClass(source, desiredClass.Name);
            if (existingClass is null)
            {
                source = AppendBlock(source, NormalizeNewlines(desiredClass.Text, newline), newline);
                addedClasses.Add(desiredClass.Name);
                continue;
            }

            var desiredFields = ParseFields(desiredClass.Text);
            if (desiredFields.Count == 0) continue;
            var existingFields = ParseFields(existingClass.Text)
                .ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);
            var inserts = new List<string>();

            foreach (var field in desiredFields)
            {
                if (!existingFields.TryGetValue(field.Name, out var existingField))
                {
                    inserts.Add(NormalizeNewlines(field.Text.TrimEnd('\r', '\n'), newline));
                    addedProperties.Add(desiredClass.Name + "." + field.Name);
                    continue;
                }

                if (!existingField.TypeName.Equals(field.TypeName, StringComparison.OrdinalIgnoreCase))
                    warnings.Add($"{desiredClass.Name}.{field.Name}: existing type '{existingField.TypeName}' preserved; OpenAPI generates '{field.TypeName}'.");

                var existingAttributes = NormalizeAttributes(existingField.Attributes);
                var desiredAttributes = NormalizeAttributes(field.Attributes);
                if (!existingAttributes.SetEquals(desiredAttributes))
                    warnings.Add($"{desiredClass.Name}.{field.Name}: existing validation attributes preserved; OpenAPI validation metadata differs.");
            }

            if (inserts.Count == 0) continue;
            existingClass = FindClass(source, desiredClass.Name)
                ?? throw new InvalidOperationException($"Unable to relocate class '{desiredClass.Name}' during OpenAPI import.");
            source = InsertClassFields(source, existingClass, inserts, newline);
        }

        var existingClassSpans = ParseClasses(source).Select(x => (x.Start, x.End)).ToArray();
        var existingProcedures = ParseProcedures(source, existingClassSpans)
            .ToDictionary(procedure => ProcedureKey(procedure.Kind, procedure.Name), StringComparer.OrdinalIgnoreCase);

        foreach (var procedure in ParseProcedures(desired.Source, ParseClasses(desired.Source).Select(x => (x.Start, x.End)).ToArray()))
        {
            var key = ProcedureKey(procedure.Kind, procedure.Name);
            if (!existingProcedures.TryGetValue(key, out var existing))
            {
                source = AppendBlock(source, NormalizeNewlines(procedure.Text, newline), newline);
                addedProcedures.Add(procedure.Kind + " " + procedure.Name);
                existingProcedures[key] = procedure;
                continue;
            }

            if (!NormalizeSignature(existing.Declaration).Equals(NormalizeSignature(procedure.Declaration), StringComparison.OrdinalIgnoreCase))
                warnings.Add($"{procedure.Kind} {procedure.Name}: existing signature preserved; OpenAPI-generated signature differs.");

            if (procedure.Kind.Equals("Sub", StringComparison.OrdinalIgnoreCase) && procedure.Name.StartsWith("Endpoint", StringComparison.OrdinalIgnoreCase))
            {
                var existingAttributes = NormalizeAttributes(existing.Prefix);
                var desiredAttributes = NormalizeAttributes(procedure.Prefix);
                if (!existingAttributes.SetEquals(desiredAttributes))
                    warnings.Add($"Sub {procedure.Name}: existing route/method/security attributes preserved; OpenAPI endpoint metadata differs.");
            }
        }

        return new XpsOpenApiImportResult(
            desired.OpenApiVersion,
            source,
            addedClasses,
            addedProperties,
            addedProcedures,
            warnings.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static string InsertClassFields(string source, ClassBlock block, IReadOnlyList<string> fields, string newline)
    {
        var endClassOffset = block.EndClassStart;
        var prefix = source[..endClassOffset];
        var suffix = source[endClassOffset..];
        var builder = new StringBuilder(prefix);
        if (builder.Length > 0 && !EndsWithNewline(builder)) builder.Append(newline);
        if (builder.Length > 0 && !EndsWithBlankLine(builder, newline)) builder.Append(newline);
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) builder.Append(newline).Append(newline);
            builder.Append(fields[i]);
        }
        builder.Append(newline);
        return builder + suffix;
    }

    private static string AppendBlock(string source, string block, string newline)
    {
        if (string.IsNullOrWhiteSpace(source)) return block.TrimEnd('\r', '\n') + newline;
        var builder = new StringBuilder(source.TrimEnd('\r', '\n'));
        builder.Append(newline).Append(newline).Append(block.Trim('\r', '\n')).Append(newline);
        return builder.ToString();
    }

    private static List<ClassBlock> ParseClasses(string source)
    {
        var result = new List<ClassBlock>();
        foreach (Match match in ClassPattern.Matches(source))
        {
            var endClassStart = match.Groups["end"].Index;
            result.Add(new ClassBlock(
                match.Groups["name"].Value,
                match.Value,
                match.Index,
                match.Index + match.Length,
                endClassStart));
        }
        return result;
    }

    private static ClassBlock? FindClass(string source, string name) =>
        ParseClasses(source).FirstOrDefault(block => block.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static List<FieldBlock> ParseFields(string classText)
    {
        var result = new List<FieldBlock>();
        foreach (Match match in FieldPattern.Matches(classText))
        {
            result.Add(new FieldBlock(
                match.Groups["name"].Value,
                match.Groups["type"].Value,
                match.Groups["attrs"].Value,
                match.Value));
        }
        return result;
    }

    private static List<ProcedureBlock> ParseProcedures(string source, IReadOnlyList<(int Start, int End)> classSpans)
    {
        var result = new List<ProcedureBlock>();
        AddProcedures(result, FunctionPattern, "Function", source, classSpans);
        AddProcedures(result, SubPattern, "Sub", source, classSpans);
        return result.OrderBy(procedure => procedure.Start).ToList();
    }

    private static void AddProcedures(
        List<ProcedureBlock> result,
        Regex pattern,
        string kind,
        string source,
        IReadOnlyList<(int Start, int End)> classSpans)
    {
        foreach (Match match in pattern.Matches(source))
        {
            var declarationStart = match.Groups["decl"].Index;
            if (classSpans.Any(span => declarationStart >= span.Start && declarationStart < span.End)) continue;
            result.Add(new ProcedureBlock(
                kind,
                match.Groups["name"].Value,
                match.Groups["decl"].Value,
                match.Groups["prefix"].Value,
                match.Value,
                match.Index));
        }
    }

    private static HashSet<string> NormalizeAttributes(string text)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(text, @"(?m)^\s*(\[[^\r\n]+\])\s*$", RegexOptions.CultureInvariant))
            result.Add(Regex.Replace(match.Groups[1].Value, @"\s+", string.Empty, RegexOptions.CultureInvariant));
        return result;
    }

    private static string NormalizeSignature(string declaration) =>
        Regex.Replace(declaration.Trim(), @"\s+", " ", RegexOptions.CultureInvariant);

    private static string ProcedureKey(string kind, string name) => kind + "\0" + name;

    private static string DetectNewline(string source) => source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static string NormalizeNewlines(string value, string newline) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal).Replace("\n", newline, StringComparison.Ordinal);

    private static bool EndsWithNewline(StringBuilder builder) =>
        builder.Length > 0 && (builder[^1] == '\n' || builder[^1] == '\r');

    private static bool EndsWithBlankLine(StringBuilder builder, string newline) =>
        builder.ToString().EndsWith(newline + newline, StringComparison.Ordinal);

    private sealed record ClassBlock(string Name, string Text, int Start, int End, int EndClassStart);
    private sealed record FieldBlock(string Name, string TypeName, string Attributes, string Text);
    private sealed record ProcedureBlock(string Kind, string Name, string Declaration, string Prefix, string Text, int Start);
}
