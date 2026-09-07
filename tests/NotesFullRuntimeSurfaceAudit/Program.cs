using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var samplePaths = new[]
{
    Path.Combine(repoRoot, "samples", "notes-full-domino-runtime-test.xps"),
    Path.Combine(repoRoot, "samples", "notes-document-metadata-runtime-test.xps"),
    Path.Combine(repoRoot, "samples", "notes-session-database-open-runtime-test.xps"),
    Path.Combine(repoRoot, "samples", "notes-session-full-runtime-test.xps"),
    Path.Combine(repoRoot, "samples", "notes-database-full-runtime-test.xps"),
    Path.Combine(repoRoot, "samples", "notes-richtext-linked-objects-surface.xps"),
    Path.Combine(repoRoot, "samples", "notes-agent-types-domino-runtime-test.xps"),
    Path.Combine(repoRoot, "samples", "notes-dxl-import-export-surface.xps")
};
foreach (var samplePath in samplePaths)
{
    if (!File.Exists(samplePath)) throw new FileNotFoundException("Notes runtime sample not found.", samplePath);
}

var compilerPath = Path.Combine(AppContext.BaseDirectory, "XPScript.Compiler.Core.dll");
if (!File.Exists(compilerPath)) throw new FileNotFoundException("XPScript.Compiler.Core.dll was not copied to the audit output directory.", compilerPath);
var compiler = Assembly.LoadFrom(compilerPath);
var builder = compiler.GetType("XPScript.Compiler.NotesRuntimeSourceBuilder", throwOnError: true)!;
var build = builder.GetMethod("Build", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, binder: null, Type.EmptyTypes, modifiers: null)
    ?? throw new InvalidOperationException("NotesRuntimeSourceBuilder.Build() not found.");
var source = (string?)build.Invoke(null, null) ?? throw new InvalidOperationException("Notes runtime source was null.");
var sample = string.Join("\n", samplePaths.Select(File.ReadAllText));

var syntaxTree = CSharpSyntaxTree.ParseText(source);
var root = syntaxTree.GetCompilationUnitRoot();
var parseErrors = syntaxTree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Take(20).ToArray();
if (parseErrors.Length != 0)
{
    Console.WriteLine("Generated runtime parse diagnostics:");
    foreach (var diagnostic in parseErrors) Console.WriteLine("  " + diagnostic);
    throw new InvalidOperationException("Generated Notes runtime source could not be parsed for the surface audit.");
}

var classes = new[]
{
    (Runtime: "XPScriptNotesSession", Surface: "NotesSession", Anchor: (string?)null),
    (Runtime: "XPScriptNotesDocument", Surface: "NotesDocument", Anchor: (string?)"NoteID"),
    (Runtime: "XPScriptNotesDatabase", Surface: "NotesDatabase", Anchor: (string?)null),
    (Runtime: "XPScriptNotesItem", Surface: "NotesItem", Anchor: (string?)null),
    (Runtime: "XPScriptNotesView", Surface: "NotesView", Anchor: (string?)null),
    (Runtime: "XPScriptNotesDocumentCollection", Surface: "NotesDocumentCollection", Anchor: (string?)null),
    (Runtime: "XPScriptNotesNoteCollection", Surface: "NotesNoteCollection", Anchor: (string?)null),
    (Runtime: "XPScriptNotesName", Surface: "NotesName", Anchor: (string?)null),
    (Runtime: "XPScriptNotesDateTime", Surface: "NotesDateTime", Anchor: (string?)null),
    (Runtime: "XPScriptNotesAgent", Surface: "NotesAgent", Anchor: (string?)null),
    (Runtime: "XPScriptNotesStream", Surface: "NotesStream", Anchor: (string?)null),
    (Runtime: "XPScriptNotesDXLImporter", Surface: "NotesDXLImporter", Anchor: (string?)null),
    (Runtime: "XPScriptNotesDXLExporter", Surface: "NotesDXLExporter", Anchor: (string?)null),
    (Runtime: "XPScriptNotesViewNavigator", Surface: "NotesViewNavigator", Anchor: (string?)null),
    (Runtime: "XPScriptNotesViewEntry", Surface: "NotesViewEntry", Anchor: (string?)null),
    (Runtime: "XPScriptNotesViewEntryCollection", Surface: "NotesViewEntryCollection", Anchor: (string?)null),
    (Runtime: "XPScriptNotesRichTextItem", Surface: "NotesRichTextItem", Anchor: (string?)null),
    (Runtime: "XPScriptNotesRichTextNavigator", Surface: "NotesRichTextNavigator", Anchor: (string?)null),
    (Runtime: "XPScriptNotesRichTextRange", Surface: "NotesRichTextRange", Anchor: (string?)null),
    (Runtime: "XPScriptNotesRichTextSection", Surface: "NotesRichTextSection", Anchor: (string?)null),
    (Runtime: "XPScriptNotesRichTextTable", Surface: "NotesRichTextTable", Anchor: (string?)null),
    (Runtime: "XPScriptNotesRichTextDocLink", Surface: "NotesRichTextDocLink", Anchor: (string?)null),
    (Runtime: "XPScriptNotesEmbeddedObject", Surface: "NotesEmbeddedObject", Anchor: (string?)null),
    (Runtime: "XPScriptNotesRichTextStyle", Surface: "NotesRichTextStyle", Anchor: (string?)null),
    (Runtime: "XPScriptNotesRichTextParagraphStyle", Surface: "NotesRichTextParagraphStyle", Anchor: (string?)null),
    (Runtime: "XPScriptNotesRichTextTab", Surface: "NotesRichTextTab", Anchor: (string?)null),
    (Runtime: "XPScriptNotesColorObject", Surface: "NotesColorObject", Anchor: (string?)null)
};

var ignoredMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Dispose", "TryGetMember", "TrySetMember", "TryInvokeMember" };
var missing = new List<string>();
var placeholders = new List<string>();
var suspiciousConstants = new List<string>();
var allClassDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();

foreach (var item in classes)
{
    var declarations = item.Anchor is null
        ? allClassDeclarations.Where(c => c.Identifier.ValueText.Equals(item.Runtime, StringComparison.Ordinal)).ToArray()
        : allClassDeclarations
            .Where(c => c.Members.Any(member => member.Modifiers.Any(SyntaxKind.PublicKeyword) && GetMemberName(member)?.Equals(item.Anchor, StringComparison.OrdinalIgnoreCase) == true))
            .Where(c => item.Surface != "NotesDocument" || c.Members.Any(member => member.Modifiers.Any(SyntaxKind.PublicKeyword) && GetMemberName(member)?.Equals("NoteIdHex", StringComparison.OrdinalIgnoreCase) == true))
            .ToArray();

    if (declarations.Length == 0) throw new InvalidOperationException("Generated runtime class was not found: " + item.Surface);
    if (declarations.Length > 1) throw new InvalidOperationException($"Generated runtime class resolution for {item.Surface} was ambiguous: {declarations.Length} classes matched.");

    var members = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var declaration in declarations)
    {
        foreach (var member in declaration.Members)
        {
            if (!member.Modifiers.Any(SyntaxKind.PublicKeyword)) continue;
            var name = GetMemberName(member);
            if (string.IsNullOrEmpty(name) || ignoredMembers.Contains(name)) continue;
            members.Add(name);
            var memberText = member.ToFullString();
            if (Regex.IsMatch(memberText, @"NotImplementedException|NotSupportedException|Unsupported|not supported", RegexOptions.IgnoreCase)) placeholders.Add(item.Surface + "." + name);
            if (Regex.IsMatch(memberText, "=>\\s*(?:false|true|0|\"\")\\s*;", RegexOptions.IgnoreCase)) suspiciousConstants.Add(item.Surface + "." + name);
        }
    }

    Console.WriteLine($"{item.Surface}: {members.Count} declared public members");
    foreach (var member in members)
    {
        var covered = Regex.IsMatch(sample, @"\.\s*" + Regex.Escape(member) + @"\b", RegexOptions.IgnoreCase);
        Console.WriteLine($"  {(covered ? "OK" : "MISSING")} {member}");
        if (!covered) missing.Add(item.Surface + "." + member);
    }
}

Console.WriteLine();
Console.WriteLine("PLACEHOLDER-LIKE MEMBERS:");
foreach (var value in placeholders.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x)) Console.WriteLine("  " + value);
Console.WriteLine("SUSPICIOUS CONSTANT MEMBERS:");
foreach (var value in suspiciousConstants.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x)) Console.WriteLine("  " + value);
Console.WriteLine("MISSING FULLTEST COVERAGE:");
foreach (var value in missing.OrderBy(x => x)) Console.WriteLine("  " + value);

if (placeholders.Count != 0 || missing.Count != 0) Environment.ExitCode = 1;

static string? GetMemberName(MemberDeclarationSyntax member) => member switch
{
    PropertyDeclarationSyntax property => property.Identifier.ValueText,
    MethodDeclarationSyntax method => method.Identifier.ValueText,
    _ => null
};
