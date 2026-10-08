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
