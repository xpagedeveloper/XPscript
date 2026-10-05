using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;

const string source = "Sub Main()\nCall AstPrint()\nEnd Sub\n";
var declaration = new DeclarationParser(source).ParseDeclaration();
if (declaration is not SubDeclarationSyntax sub || declaration is null)
    throw new InvalidOperationException("AST declaration parser did not produce a Sub.");
if (sub.Statements.Count != 1 || sub.Statements[0] is not CallStatementSyntax)
    throw new InvalidOperationException("AST declaration parser did not retain the call statement.");

var symbols = new SymbolTable();
symbols.Declare(new FunctionSymbol("AstPrint", typeof(void), []));
var binder = new StatementBinder(symbols);
var bound = sub.Statements.Select(binder.Bind).OfType<BoundStatement>().ToArray();
if (declaration is null || binder.Diagnostics.Count != 0)
    throw new InvalidOperationException(string.Join("; ", binder.Diagnostics.Select(d => d.Message)));

var method = new BoundMethodEmitter().Emit("Main", typeof(void), bound);
var generated = $$"""
using System;
public static class Program
{
    public static void AstPrint() => Console.WriteLine("AST_XPS_COMPILE_OK");
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
