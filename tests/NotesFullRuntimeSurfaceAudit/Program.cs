using System.Reflection;
using System.Text.RegularExpressions;

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

var classes = new[]
{
    (Runtime: "XPScriptNotesSession", Surface: "NotesSession", Anchor: "public string Username"),
    (Runtime: "XPScriptNotesDocument", Surface: "NotesDocument", Anchor: "public string UniversalId"),
    (Runtime: "XPScriptNotesDatabase", Surface: "NotesDatabase", Anchor: "public string Server")
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
    var bodies = ExtractClassBodies(source, item.Runtime, item.Anchor);
    if (bodies.Count == 0)
        throw new InvalidOperationException("Generated runtime class was not found: " + item.Runtime);

    var members = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var body in bodies)
    {
        foreach (Match match in Regex.Matches(body,
                     @"(?m)^\s*public\s+(?:override\s+)?(?:static\s+)?(?:[\w\.<>\[\],?]+\s+)+(?<name>[A-Za-z_]\w*)\s*(?<tail>\(|\{|=>)"))
        {
            var name = match.Groups["name"].Value;
            if (!ignoredMembers.Contains(name)) members.Add(name);

            var start = match.Index;
            var next = Regex.Match(body[(start + match.Length)..], @"(?m)^\s*public\s+");
            var end = next.Success ? start + match.Length + next.Index : Math.Min(body.Length, start + 1600);
            var memberText = body[start..end];

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

static List<string> ExtractClassBodies(string source, string className, string fallbackAnchor)
{
    var result = new List<string>();
    var regex = new Regex(@"\bclass\s+" + Regex.Escape(className) + @"\b[^\{]*\{");
    foreach (Match match in regex.Matches(source))
        AddBody(source, source.IndexOf('{', match.Index), result);

    if (result.Count != 0) return result;

    // Some post-processed runtime declarations no longer retain the original class header text.
    // Resolve those classes from a stable public member and the nearest enclosing class declaration.
    var anchor = source.IndexOf(fallbackAnchor, StringComparison.Ordinal);
    if (anchor >= 0)
    {
        var classStart = source.LastIndexOf("class ", anchor, StringComparison.Ordinal);
        if (classStart >= 0)
            AddBody(source, source.IndexOf('{', classStart), result);
    }
    return result;
}

static void AddBody(string source, int open, List<string> result)
{
    if (open < 0) return;
    var depth = 0;
    var inString = false;
    var verbatim = false;
    for (var i = open; i < source.Length; i++)
    {
        var c = source[i];
        if (inString)
        {
            if (!verbatim && c == '\\') { i++; continue; }
            if (c == '"')
            {
                if (verbatim && i + 1 < source.Length && source[i + 1] == '"') { i++; continue; }
                inString = false;
                verbatim = false;
            }
            continue;
        }
        if (c == '"')
        {
            inString = true;
            verbatim = i > 0 && source[i - 1] == '@';
            continue;
        }
        if (c == '{') depth++;
        else if (c == '}')
        {
            depth--;
            if (depth == 0)
            {
                result.Add(source.Substring(open + 1, i - open - 1));
                return;
            }
        }
    }
}
