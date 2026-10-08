using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;
using SyntaxKind = XPScript.Compiler.Syntax.SyntaxKind;
using Conversion = XPScript.Compiler.Binding.Conversion;

var expressions = new BoundExpressionEmitter();
var commandSymbols = new SymbolTable();
commandSymbols.Declare(new FunctionSymbol("RunCommand", typeof(bool), [typeof(string), typeof(string[])]));
commandSymbols.Declare(new FunctionSymbol("Array", typeof(object), [typeof(string)]));
string EmitCommand(string text)
{
    var binder = new ExpressionBinder(commandSymbols);
    var bound = binder.Bind(new ExpressionParser(text).ParseExpression());
    if (binder.Diagnostics.Count != 0) throw new InvalidOperationException("RunCommand failed to bind.");
    return expressions.Emit(bound);
}
var notCommand = EmitCommand("Not RunCommand(\"where.exe\", Array(\"winget\"))");
var falseCommand = EmitCommand("RunCommand(\"where.exe\", Array(\"winget\")) = False");
string EmitExpression(string text)
{
    var parser = new ExpressionParser(text);
    var binder = new ExpressionBinder();
    var bound = binder.Bind(parser.ParseExpression());
    if (parser.Diagnostics.Count != 0 || binder.Diagnostics.Count != 0)
        throw new InvalidOperationException($"Invalid probe input: {text}");
    return expressions.Emit(bound);
}

void Equal(object? expected, object? actual)
{
    if (!Equals(expected, actual))
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
}

// Verify C# literal types as well as their values through Roslyn.
var escaped = "quote\" slash\\ line\nreturn\rtab\tzero\0";
var statements = new BoundStatementEmitter();
var methods = new BoundMethodEmitter();
var symbols = new SymbolTable();
symbols.Declare(new VariableSymbol("number", typeof(long)));
var parser = new StatementParser("If False Then\nnumber = 9\nElseIf True Then\nnumber = 2\nElse\nnumber = 8\nEnd If");
var binder = new StatementBinder(symbols);
var boundIf = binder.Bind(parser.ParseStatement()) ?? throw new InvalidOperationException("If failed to bind.");
if (parser.Diagnostics.Count != 0 || binder.Diagnostics.Count != 0)
    throw new InvalidOperationException("If probe must bind without diagnostics.");
var name = new BoundNameExpression(new VariableSymbol("number", typeof(long)));
var increment = new BoundAssignmentStatement(name, new BoundBinaryExpression(name, SyntaxKind.PlusToken, new BoundLiteralExpression(1L, typeof(long)), typeof(long)), false);
var lessThan = new BoundBinaryExpression(name, SyntaxKind.LessToken, new BoundLiteralExpression(4L, typeof(long)), typeof(bool));
var body = statements.Emit([
    boundIf,
    new BoundWhileStatement(lessThan, [increment]),
    new BoundDoStatement(new BoundLiteralExpression(true, typeof(bool)), SyntaxKind.UntilKeyword, true, [increment]),
    new BoundReturnStatement(name)
]);
Equal("return 1L;\n", statements.Emit([new BoundReturnStatement(new BoundLiteralExpression(1L, typeof(long)))]));
Equal("while (!(true))\n{\n}\n", statements.Emit([new BoundDoStatement(new BoundLiteralExpression(true, typeof(bool)), SyntaxKind.UntilKeyword, false, [])]));
var widened = new BoundConversionExpression(new BoundLiteralExpression(7L, typeof(long)), XpTypeSymbol.FromClr(typeof(double)), new Conversion(ConversionKind.NumericWidening));
Equal("((double)(7L))", expressions.Emit(widened));
var variant = new BoundNameExpression(new VariableSymbol("value", typeof(object), XpTypeSymbol.Variant));
Equal("XPScriptRuntime.CLng(value)", expressions.Emit(new BoundConversionExpression(variant, XpTypeSymbol.FromClr(typeof(long)), new Conversion(ConversionKind.FromVariant))));
Equal("while (XPScriptNullRuntime.ConditionValue(value))\n{\n}\n", statements.Emit([new BoundWhileStatement(variant, [])]));
Equal("while (true)\n{\n}\n", statements.Emit([new BoundDoStatement(null, null, false, [])]));
var boxed = new BoundConversionExpression(new BoundLiteralExpression(7L, typeof(long)), XpTypeSymbol.Variant, new Conversion(ConversionKind.ToVariant));
var forBody = statements.Emit([new BoundForStatement(name, new BoundLiteralExpression(0L, typeof(long)), new BoundLiteralExpression(1L, typeof(long)), null, [increment])]);
if (!forBody.Contains("XPScriptRuntime.Range(0L, 1L, 1L)", StringComparison.Ordinal) || !forBody.Contains("number = XPScriptRuntime.CLng", StringComparison.Ordinal))
    throw new InvalidOperationException("For emission did not use the shared runtime range helper.");
var values = new BoundNameExpression(new VariableSymbol("values", typeof(long[])));
var item = new BoundNameExpression(new VariableSymbol("item", typeof(long)));
var sum = new BoundNameExpression(new VariableSymbol("number", typeof(long)));
var forAllBody = statements.Emit([new BoundForAllStatement(item, values, [new BoundExpressionStatement(item)])]);
if (!forAllBody.Contains("LSForAllRuntime.Enumerate(values)", StringComparison.Ordinal))
    throw new InvalidOperationException("ForAll emission did not use the shared enumerable runtime helper.");
var selectClause = new BoundCaseClause(SelectCaseKind.Value, null, new BoundLiteralExpression(2L, typeof(long)), null, [increment]);
var select = statements.Emit([new BoundSelectStatement(name, [selectClause])]);
if (!select.Contains("LSCoreCompare.Equal", StringComparison.Ordinal))
    throw new InvalidOperationException("Select Case emission did not use the shared comparison runtime helper.");
var method = methods.Emit("GeneratedValue", typeof(long), [new BoundReturnStatement(new BoundLiteralExpression(42L, typeof(long))) ]);
if (!method.Contains("public static long GeneratedValue()", StringComparison.Ordinal) || !method.Contains("return 42L;", StringComparison.Ordinal))
    throw new InvalidOperationException("Bound method emission did not produce a complete C# method.");
var parameter = new ParameterSymbol("value", typeof(long), IsByRef: false);
var byRefParameter = new ParameterSymbol("result", typeof(long), IsByRef: true);
var parameterMethod = methods.Emit("AddInto", typeof(void), [parameter, byRefParameter], [new BoundAssignmentStatement(new BoundNameExpression(byRefParameter), new BoundBinaryExpression(new BoundNameExpression(parameter), SyntaxKind.PlusToken, new BoundLiteralExpression(1L, typeof(long)), typeof(long)), false)]);
if (!parameterMethod.Contains("public static void AddInto(long value, ref long result)", StringComparison.Ordinal) || !parameterMethod.Contains("result = (value + 1L);", StringComparison.Ordinal))
    throw new InvalidOperationException("Bound method parameter emission did not preserve ByRef semantics.");
var mapped = statements.EmitWithSourceMap([boundIf], "If True Then\nnumber = 1\nEnd If", "flow.xps");
if (!mapped.Code.Contains("#line 1 \"flow.xps\"", StringComparison.Ordinal) || mapped.SourceMappings.Count == 0)
    throw new InvalidOperationException("Bound statement emission did not preserve source mapping.");
var selectBody = statements.Emit([new BoundSelectStatement(new BoundLiteralExpression(2L, typeof(long)), [new BoundCaseClause(SelectCaseKind.Value, null, new BoundLiteralExpression(2L, typeof(long)), null, [new BoundAssignmentStatement(sum, new BoundLiteralExpression(9L, typeof(long)), false)])])]);
var source = $$"""
using System;
public static class Probe
{
    public static bool CommandResult;
    public static object Array(string value) => new string[] { value };
    public static bool RunCommand(string command, string[] args)
    {
        if (command != "where.exe" || args.Length != 1 || args[0] != "winget")
            throw new System.Exception("RunCommand arguments changed.");
        return CommandResult;
    }
    public static bool NotCommand() => {{notCommand}};
    public static bool FalseCommand() => {{falseCommand}};
    public static class LSForAllRuntime
    {
        public static System.Collections.IEnumerable Enumerate(object? value) => (System.Collections.IEnumerable)value!;
    }
    public static class LSCoreCompare
    {
        public static bool Equal(object? left, object? right) => Equals(left, right);
    }
    public static class XPScriptRuntime
    {
        public static long CLng(object value) => Convert.ToInt64(value);
    }
    public static object Integer() => {{EmitExpression("1")}};
    public static object Floating() => {{EmitExpression("1.0")}};
    public static object Null() => {{EmitExpression("Null")}};
    public static object? Empty() => {{EmitExpression("Empty")}};
    public static string Escaped() => {{expressions.Emit(new BoundLiteralExpression(escaped, typeof(string)))}};
    public static double Widened() => {{expressions.Emit(widened)}};
    public static object Boxed() => {{expressions.Emit(boxed)}};
    public static long Minimum() => {{expressions.Emit(new BoundLiteralExpression(long.MinValue, typeof(long)))}};
    public static double Infinity() => {{expressions.Emit(new BoundLiteralExpression(double.PositiveInfinity, typeof(double)))}};
    public static long Flow() { long number = 0; {{body}} }
    public static long ForAllFlow()
    {
        long number = 0;
        long item = 0;
        long[] values = [1L, 2L, 3L];
        {{statements.Emit([new BoundForAllStatement(item, values, [new BoundAssignmentStatement(sum, new BoundBinaryExpression(sum, SyntaxKind.PlusToken, item, typeof(long)), false)])])}}
        return number;
    }
    public static long SelectFlow()
    {
        long number = 0;
        {{selectBody}}
        return number;
    }
    {{parameterMethod}}
}
""";
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
var compilation = CSharpCompilation.Create("AstEmissionProbeGenerated", [CSharpSyntaxTree.ParseText(source)], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
using var stream = new MemoryStream();
var result = compilation.Emit(stream);
if (!result.Success)
    throw new InvalidOperationException(string.Join("\n", result.Diagnostics) + "\n" + source);
var type = Assembly.Load(stream.ToArray()).GetType("Probe")!;
object? Invoke(string method) => type.GetMethod(method)!.Invoke(null, null);
foreach (var commandResult in new[] { false, true })
{
    type.GetField("CommandResult")!.SetValue(null, commandResult);
    Equal(!commandResult, Invoke("NotCommand"));
    Equal(!commandResult, Invoke("FalseCommand"));
}
Equal(typeof(long), Invoke("Integer")!.GetType());
Equal(typeof(double), Invoke("Floating")!.GetType());
Equal(DBNull.Value, Invoke("Null"));
Equal(null, Invoke("Empty"));
Equal(escaped, Invoke("Escaped"));
Equal(7D, Invoke("Widened"));
Equal(7L, Invoke("Boxed"));
Equal(long.MinValue, Invoke("Minimum"));
Equal(double.PositiveInfinity, Invoke("Infinity"));
Equal(5L, Invoke("Flow"));
Equal(6L, Invoke("ForAllFlow"));
Equal(9L, Invoke("SelectFlow"));
// Reflection boxes ref arguments and writes the updated value back into the array.
var refArgs = new object?[] { 3L, 0L };
type.GetMethod("AddInto")!.Invoke(null, refArgs);
Equal(4L, refArgs[1]);
Console.WriteLine("AST_EMISSION_OK");
