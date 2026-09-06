using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var samplePath = Path.Combine(repoRoot, "samples", "notes-full-domino-runtime-test.xps");
if (!File.Exists(samplePath))
    throw new FileNotFoundException("Notes full runtime sample not found.", samplePath);

var compilerPath = Path.Combine(AppContext.BaseDirectory, "XPScript.Compiler.Core.dll");
if (!File.Exists(compilerPath))
    throw new FileNotFoundException("XPScript.Compiler.Core.dll was not copied to the audit output directory.", compilerPath);
var compiler = Assembly.LoadFrom(compilerPath);
var builder = compiler.GetType("XPScript.Compiler.NotesRuntimeSourceBuilder", throwOnError: true)!;
var build = builder.GetMethod("Build", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, binder: null, Type.EmptyTypes, modifiers: null)
    ?? throw new InvalidOperationException("NotesRuntimeSourceBuilder.Build() not found.");
var source = (string?)build.Invoke(null, null) ?? throw new InvalidOperationException("Notes runtime source was null.");
var sample = File.ReadAllText(samplePath);

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
    (Runtime: "XPScriptNotesSession", Surface: "NotesSession"),
    (Runtime: "XPScriptNotesDocument", Surface: "NotesDocument"),
    (Runtime: "XPScriptNotesDatabase", Surface: "NotesDatabase")
};

var ignoredMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Dispose", "TryGetMember", "TrySetMember", "TryInvokeMember"
};

var missing = new List<string>();
var placeholders = new List<string>();
var suspiciousConstants = new List<string>();

foreach (var item in classes)
{
    var declarations = root.DescendantNodes()
        .OfType<ClassDeclarationSyntax>()
        .Where(c => c.Identifier.ValueText.Equals(item.Runtime, StringComparison.Ordinal))
        .ToArray();
    if (declarations.Length == 0)
        throw new InvalidOperationException("Generated runtime class was not found: " + item.Runtime);

    var members = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var declaration in declarations)
    {
        foreach (var member in declaration.Members)
        {
            if (!member.Modifiers.Any(SyntaxKind.PublicKeyword)) continue;

            string? name = member switch
            {
                PropertyDeclarationSyntax property => property.Identifier.ValueText,
                MethodDeclarationSyntax method => method.Identifier.ValueText,
                _ => null
            };
            if (string.IsNullOrEmpty(name) || ignoredMembers.Contains(name)) continue;

            members.Add(name);
            var memberText = member.ToFullString();
            if (Regex.IsMatch(memberText, @"NotImplementedException|NotSupportedException|Unsupported|not supported", RegexOptions.IgnoreCase))
                placeholders.Add(item.Surface + "." + name);
            if (Regex.IsMatch(memberText, "=>\\s*(?:false|true|0|\"\")\\s*;", RegexOptions.IgnoreCase))
                suspiciousConstants.Add(item.Surface + "." + name);
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
foreach (var value in placeholders.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
    Console.WriteLine("  " + value);
Console.WriteLine("SUSPICIOUS CONSTANT MEMBERS:");
foreach (var value in suspiciousConstants.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
    Console.WriteLine("  " + value);
Console.WriteLine("MISSING FULLTEST COVERAGE:");
foreach (var value in missing.OrderBy(x => x))
    Console.WriteLine("  " + value);

if (placeholders.Count != 0 || missing.Count != 0)
    Environment.ExitCode = 1;
