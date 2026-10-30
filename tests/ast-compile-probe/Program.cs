using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;

const string source = "Sub Main()\nDim message As String\nmessage = \"AST_XPS_COMPILE_OK\"\nPrint message\nExit Sub\nEnd Sub\n";
var declaration = new DeclarationParser(source).ParseDeclaration();
if (declaration is not SubDeclarationSyntax sub || declaration is null)
    throw new InvalidOperationException("AST declaration parser did not produce a Sub.");
if (sub.Statements.Count != 4 || sub.Statements[0] is not DimStatementSyntax ||
    sub.Statements[1] is not AssignmentStatementSyntax || sub.Statements[2] is not RuntimeFileStatementSyntax ||
    sub.Statements[3] is not ExitStatementSyntax)
    throw new InvalidOperationException("AST declaration parser did not retain the declaration, assignment and Print statements.");

var symbols = new SymbolTable();
var binder = new StatementBinder(symbols);
var bound = sub.Statements.Select(binder.Bind).OfType<BoundStatement>().ToArray();
if (declaration is null || binder.Diagnostics.Count != 0)
    throw new InvalidOperationException(string.Join("; ", binder.Diagnostics.Select(d => d.Message)));

var method = new BoundMethodEmitter().Emit("Main", typeof(void), bound);
var generated = $$"""
using System;
public static class Program
{
    public static class XPScriptRuntime
    {
        public static string PrintText(object? value) => value?.ToString() ?? "Variable is null";
    }
    public static string AstPrintText(object? value) => XPScriptRuntime.PrintText(value);
    {{method}}
}
""";
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
var compilation = CSharpCompilation.Create("AstXpsCompileProbe", [CSharpSyntaxTree.ParseText(generated)], references, new CSharpCompilationOptions(OutputKind.ConsoleApplication, mainTypeName: "Program"));
using var stream = new MemoryStream();
var emit = compilation.Emit(stream);
if (!emit.Success)
    throw new InvalidOperationException(string.Join("\n", emit.Diagnostics));
var program = Assembly.Load(stream.ToArray()).GetType("Program")!;
program.GetMethod("Main")!.Invoke(null, null);

const string unitSource = "Sub Main()\nCall Worker()\nEnd Sub\nSub Worker()\nExit Sub\nEnd Sub\n";
var unitParser = new DeclarationParser(unitSource);
var unit = unitParser.ParseCompilationUnit();
if (unitParser.Diagnostics.Count != 0 || unit.Declarations.Count != 2)
    throw new InvalidOperationException("Compilation-unit declarations failed to parse.");
var unitSymbols = new SymbolTable();
foreach (var procedure in unit.Declarations.OfType<SubDeclarationSyntax>())
    unitSymbols.Declare(new FunctionSymbol(procedure.Identifier.Text, typeof(void), []));
var definitions = new List<BoundMethodDefinition>();
foreach (var procedure in unit.Declarations.OfType<SubDeclarationSyntax>())
{
    var procedureBinder = new StatementBinder(unitSymbols);
    var body = procedure.Statements.Select(procedureBinder.Bind).OfType<BoundStatement>().ToArray();
    if (procedureBinder.Diagnostics.Count != 0)
        throw new InvalidOperationException(string.Join("; ", procedureBinder.Diagnostics.Select(d => d.Message)));
    definitions.Add(new BoundMethodDefinition(procedure.Identifier.Text, typeof(void), [], body));
}
var unitCode = new BoundCompilationUnitEmitter().Emit(definitions);
var unitCompilation = CSharpCompilation.Create("AstCompilationUnitProbe", [CSharpSyntaxTree.ParseText(unitCode)], references,
    new CSharpCompilationOptions(OutputKind.ConsoleApplication, mainTypeName: "Program"));
using var unitStream = new MemoryStream();
var unitEmit = unitCompilation.Emit(unitStream);
if (!unitEmit.Success) throw new InvalidOperationException(string.Join("\n", unitEmit.Diagnostics));
Assembly.Load(unitStream.ToArray()).GetType("Program")!.GetMethod("Main")!.Invoke(null, null);
Console.WriteLine("AST_COMPILATION_UNIT_OK");

// Compile the production reference helper itself; never reproduce its address arithmetic in a test helper.
var runtimeSource = XPScript.Compiler.CoreCompatibilityRuntimeSource.Code;
var referenceStart = runtimeSource.IndexOf("internal static class LSArrayRuntime", StringComparison.Ordinal);
var referenceEnd = runtimeSource.IndexOf("    public static LSArray Fixed", referenceStart, StringComparison.Ordinal);
var referenceProbe = $$"""
using System;
using System.Linq;
using System.Collections.Generic;
internal interface IXPScriptArrayStorage
{
    int Rank { get; }
    bool IsAllocated { get; }
    object? Get(params object?[] indices);
    void Set(object? value, params object?[] indices);
    int LBound(int dimension = 1);
    int UBound(int dimension = 1);
    void Erase();
}
internal static class XPScriptRuntime
{
    public static int CInt(object? value) => Convert.ToInt32(value);
    public static byte CByte(object? value) => Convert.ToByte(value);
}
{{runtimeSource[referenceStart..referenceEnd]}}
}
public static class Guard
{
    private static void Reject(Action action, Type expected)
    {
        try { action(); } catch (Exception ex) when (ex.GetType() == expected) { return; }
        throw new Exception("Array reference guard failed: " + expected.Name);
    }
    public static void Main()
    {
        Reject(() => LSArrayRuntime.RequireArray<long>(new object[] { 1L }, 1), typeof(InvalidCastException));
        Reject(() => LSArrayRuntime.RequireArray<object>(new string[] { "x" }, 1), typeof(InvalidCastException));
        Reject(() => LSArrayRuntime.RequireArray<long>(new long[1, 1], 1), typeof(InvalidCastException));
        if (LSArrayRuntime.RequireArray<long>(null, 1) is not null)
            throw new Exception("A null array conversion allocated storage.");
        Reject(() => { ref var slot = ref LSArrayRuntime.Reference<long>(new object[] { 1L }, 0); }, typeof(InvalidCastException));
        Reject(() => { ref var slot = ref LSArrayRuntime.Reference<object>(new string[] { "x" }, 0); }, typeof(InvalidCastException));
        Reject(() => { ref var slot = ref LSArrayRuntime.Reference<long>(null, 0); }, typeof(InvalidCastException));
        Reject(() => { ref var slot = ref LSArrayRuntime.Reference<long>(new long[0], 0); }, typeof(IndexOutOfRangeException));
        Reject(() => { ref var slot = ref LSArrayRuntime.Reference<long>(new long[1], -1); }, typeof(IndexOutOfRangeException));
        Reject(() => { ref var slot = ref LSArrayRuntime.Reference<long>(new long[1], 1); }, typeof(IndexOutOfRangeException));
        Reject(() => { ref var slot = ref LSArrayRuntime.Reference<long>(new long[1,1], 0); }, typeof(IndexOutOfRangeException));
        for (var rank = 1; rank <= 8; rank++)
        {
            var lengths = Enumerable.Repeat(2, rank).ToArray();
            var lower = Enumerable.Repeat(-1, rank).ToArray();
            var array = Array.CreateInstance(typeof(long), lengths, lower);
            if (!ReferenceEquals(array, LSArrayRuntime.RequireArray<long>(array, rank)))
                throw new Exception("Array conversion lost physical storage identity.");
            var emptyStrings = LSArrayRuntime.CreateManaged<string>(lengths, lower);
            if (emptyStrings.Cast<string>().Any(value => value != string.Empty) || emptyStrings.GetLowerBound(0) != -1)
                throw new Exception("Ranked String allocation lost default values or bounds.");
            var last = Enumerable.Repeat<object?>(0, rank).ToArray();
            ref var slot = ref LSArrayRuntime.Reference<long>(array, last);
            slot = 41;
            if ((long)array.GetValue(new int[rank])! != 41 || (long)array.GetValue(lower)! != 0)
                throw new Exception("Array reference layout/bounds failed.");
            ref var same = ref LSArrayRuntime.Reference<long>(array, last);
            same++;
            if (slot != 42) throw new Exception("Array shared reference failed.");
        }
        var strings = new[] { "a" };
        ref var text = ref LSArrayRuntime.Reference<string>(strings, 0);
        text = "changed";
        if (strings[0] != "changed") throw new Exception("Reference-valued array slot failed.");
        var bytes = new byte[2];
        if (!ReferenceEquals(bytes, LSArrayRuntime.ByteVector(bytes)))
            throw new Exception("Native Byte vector conversion copied a zero-based vector.");
        var boundedBytes = LSArrayRuntime.CreateManaged<byte>([2], [-3]);
        boundedBytes.SetValue((byte)9, -3);
        if (!LSArrayRuntime.ByteVector(boundedBytes)!.SequenceEqual(new byte[] { 9, 0 }))
            throw new Exception("Native Byte vector conversion lost bounded elements.");
        Reject(() => LSArrayRuntime.ByteVector(new byte[1, 1]), typeof(InvalidCastException));
        LSArrayRuntime.Reference<byte>(bytes, 1) = 7;
        var flags = new bool[2];
        LSArrayRuntime.Reference<bool>(flags, 1) = true;
        var doubles = new double[2];
        LSArrayRuntime.Reference<double>(doubles, 1) = 0.5;
        if (bytes[0] != 0 || bytes[1] != 7 || flags[0] || !flags[1] || doubles[0] != 0 || doubles[1] != 0.5)
            throw new Exception("Scalar element layout failed.");
    }
}
""";
var guardCompilation = CSharpCompilation.Create("AstArrayReferenceGuards", [CSharpSyntaxTree.ParseText(referenceProbe)], references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
using var guardStream = new MemoryStream();
var guardEmit = guardCompilation.Emit(guardStream);
if (!guardEmit.Success) throw new InvalidOperationException(string.Join("\n", guardEmit.Diagnostics));
Assembly.Load(guardStream.ToArray()).GetType("Guard")!.GetMethod("Main")!.Invoke(null, null);
Console.WriteLine("AST_BYREF_STORAGE_GUARDS_OK");

var toolingFixture = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "go-to-definition-next.xps");
toolingFixture = Path.GetFullPath(toolingFixture);
var toolingSource = await File.ReadAllTextAsync(toolingFixture);
var definition = XPScript.Compiler.AstTooling.GoToDefinition(toolingSource, toolingFixture, 6, 10)
    ?? throw new InvalidOperationException("Go-to-definition did not resolve Helper.");
if (definition.Name != "Helper" || definition.Kind != "Sub" || definition.Line != 1 || definition.Column != 5)
    throw new InvalidOperationException($"Go-to-definition returned {definition.File}:{definition.Line}:{definition.Column}.");
Console.WriteLine("AST_GOTO_DEFINITION_OK");

var targetFixture = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "target-specific-generated-code-next.xps");
targetFixture = Path.GetFullPath(targetFixture);
var targetOutput = Path.Combine(Path.GetTempPath(), "xpscript-target-source-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(targetOutput);
try
{
    foreach (var rid in new[] { "win-x64", "linux-x64", "osx-x64" })
    {
        var captured = string.Empty;
        try
        {
            await XPScript.Compiler.AstCompiler.CompileWithGeneratedSourceTransformAsync(
                targetFixture, Path.Combine(targetOutput, rid), rid,
                generated =>
                {
                    captured = generated;
                    throw new InvalidOperationException("TARGET_SOURCE_CAPTURED");
                });
        }
        catch (InvalidOperationException ex) when (ex.Message == "TARGET_SOURCE_CAPTURED") { }
        if (captured.Length == 0 || !captured.Contains("public static void Main()", StringComparison.Ordinal))
            throw new InvalidOperationException($"Desktop target {rid} lost its generated Main entry point.");
        if (captured.Contains("__xps_android_entry", StringComparison.Ordinal))
            throw new InvalidOperationException($"Desktop target {rid} received the Android entry point.");
    }
    Console.WriteLine("AST_TARGET_GENERATED_DESKTOP_OK");
    Console.WriteLine("AST_TARGET_GENERATED_ANDROID_SKIPPED=ANDROID_PREREQUISITES_NOT_REQUESTED");
    Console.WriteLine("AST_TARGET_GENERATED_BROWSER_SKIPPED=WASM_WORKLOAD_NOT_REQUESTED");
}
finally
{
    try { Directory.Delete(targetOutput, recursive: true); } catch { }
}
