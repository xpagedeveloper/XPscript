using System.Reflection;
using System.Text.RegularExpressions;

var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var samplePath = Path.Combine(repoRoot, "samples", "notes-full-domino-runtime-test.xps");
if (!File.Exists(samplePath))
    throw new FileNotFoundException("Notes full runtime sample not found.", samplePath);

var compilerPath = Path.Combine(AppContext.BaseDirectory, "XPScript.Compiler.dll");
if (!File.Exists(compilerPath))
    throw new FileNotFoundException("XPScript.Compiler.dll was not copied to the audit output directory.", compilerPath);
var compiler = Assembly.LoadFrom(compilerPath);
var builder = compiler.GetType("XPScript.Compiler.NotesRuntimeSourceBuilder", throwOnError: true)!;
var build = builder.GetMethod("Build", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, binder: null, Type.EmptyTypes, modifiers: null)
    ?? throw new InvalidOperationException("NotesRuntimeSourceBuilder.Build() not found.");
var source = (string?)build.Invoke(null, null) ?? throw new InvalidOperationException("Notes runtime source was null.");
var sample = File.ReadAllText(samplePath);

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
    var bodies = ExtractClassBodies(source, item.Runtime);
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

static List<string> ExtractClassBodies(string source, string className)
{
    var result = new List<string>();
    var regex = new Regex(@"(?m)^\s*(?:internal|public)\s+(?:sealed\s+|partial\s+|abstract\s+)*class\s+" + Regex.Escape(className) + @"\b[^\{]*\{");
    foreach (Match match in regex.Matches(source))
    {
        var open = source.IndexOf('{', match.Index);
        if (open < 0) continue;
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    result.Add(source.Substring(open + 1, i - open - 1));
                    break;
                }
            }
        }
    }
    return result;
}
