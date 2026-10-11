using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;
using SyntaxKind = XPScript.Compiler.Syntax.SyntaxKind;
using Conversion = XPScript.Compiler.Binding.Conversion;

var expressions = new BoundExpressionEmitter();
var copybackList = new BoundNameExpression(new LocalSymbol("copybackValues", typeof(object), XpTypeSymbol.ListOf(XpTypeSymbol.FromClr(typeof(long)))));
var copybackElement = new BoundIndexExpression(copybackList, new BoundLiteralExpression("item", typeof(string)), typeof(long), XpTypeSymbol.FromClr(typeof(long)));
var copybackCall = new BoundCallExpression(null, new FunctionSymbol("MutateThenThrow", typeof(void), [typeof(long)], ByRefParameters: [true]), [copybackElement]);
var exceptionalCopybackBody = new BoundStatementEmitter().Emit([new BoundExpressionStatement(copybackCall)]);
var copybackAlias = new BoundNameExpression(new LocalSymbol("alias", typeof(long), XpTypeSymbol.FromClr(typeof(long))) { IsListAlias = true });
var aliasCall = new BoundCallExpression(null, copybackCall.Function, [copybackAlias]);
var exceptionalAliasBody = new BoundStatementEmitter().Emit([new BoundForAllStatement(copybackAlias, copybackList, [new BoundExpressionStatement(aliasCall)])]);
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
var boundIf = binder.Bind(parser.ParseStatement()) as BoundIfStatement ?? throw new InvalidOperationException("If failed to bind.");
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
Equal("public static long GeneratedValue()\n{\n    return 42L;\n}\n", method);
var parameter = new ParameterSymbol("value", typeof(long), IsByRef: false);
var byRefParameter = new ParameterSymbol("result", typeof(long), IsByRef: true);
var parameterMethod = methods.Emit("AddInto", typeof(void), [parameter, byRefParameter], [new BoundAssignmentStatement(new BoundNameExpression(byRefParameter), new BoundBinaryExpression(new BoundNameExpression(parameter), SyntaxKind.PlusToken, new BoundLiteralExpression(1L, typeof(long)), typeof(long)), false)]);
Equal("public static void AddInto(long value, ref long result)\n{\n    result = (value + 1L);\n}\n", parameterMethod);
var instanceMethod = methods.Emit("InstanceValue", typeof(long), [], [new BoundReturnStatement(new BoundLiteralExpression(7L, typeof(long)))], isStatic: false);
Equal("public long InstanceValue()\n{\n    return 7L;\n}\n", instanceMethod);
var mapped = statements.EmitWithSourceMap([boundIf], "If True Then\nnumber = 1\nEnd If", "flow.xps");
if (!mapped.Code.Contains("#line 1 \"flow.xps\"", StringComparison.Ordinal) || !mapped.Code.EndsWith("#line default\n", StringComparison.Ordinal) || mapped.SourceMappings.Count < 2)
    throw new InvalidOperationException("Bound statement emission did not preserve source mapping.");
var firstMapping = mapped.SourceMappings[0];
if (firstMapping.SourcePath != "flow.xps" || firstMapping.Position.Line != 1 || firstMapping.Position.Column != 1 || firstMapping.Span != boundIf.Span || firstMapping.GeneratedLine <= 1)
    throw new InvalidOperationException("Bound statement emission produced an inaccurate source mapping.");
if (mapped.SourceMappings.Any(mapping => mapping.SourcePath != "flow.xps" || mapping.GeneratedLine <= 1))
    throw new InvalidOperationException("Nested bound statements produced an invalid source mapping entry.");
if (boundIf.ThenStatements.Count == 0 || !mapped.SourceMappings.Any(mapping => mapping.Span == boundIf.ThenStatements[0].Span))
    throw new InvalidOperationException("Nested bound statement source span was not preserved.");
var nestedMapping = mapped.SourceMappings.Single(mapping => mapping.Span == boundIf.ThenStatements[0].Span);
if (nestedMapping.Position.Line != 2 || nestedMapping.Position.Column != 2 || mapped.SourceMappings.Any(mapping => mapping.Span.Length <= 0))
    throw new InvalidOperationException($"Nested bound statement source position was not preserved accurately: {nestedMapping.Position.Line}:{nestedMapping.Position.Column}.");
var generatedMappingLines = mapped.SourceMappings.Select(mapping => mapping.GeneratedLine).ToArray();
if (!generatedMappingLines.SequenceEqual(generatedMappingLines.OrderBy(line => line)) || generatedMappingLines.Distinct().Count() != generatedMappingLines.Length)
    throw new InvalidOperationException("Bound statement source mappings were not deterministic by generated line.");
var quotedPathMapping = statements.EmitWithSourceMap([boundIf], "If True Then\nnumber = 1\nEnd If", "flow \"quoted\".xps");
if (!quotedPathMapping.Code.Contains("#line 1 \"flow \\\"quoted\\\".xps\"", StringComparison.Ordinal) || quotedPathMapping.SourceMappings[0].SourcePath != "flow \"quoted\".xps")
    throw new InvalidOperationException("Bound statement emission did not escape a quoted source path.");
var mappedStatementIds = new[] { boundIf.StatementId }.Concat(boundIf.ThenStatements.Select(statement => statement.StatementId)).ToArray();
if (mappedStatementIds.Length < 2 || mappedStatementIds.Any(id => id <= 0) || mappedStatementIds.Distinct().Count() != mappedStatementIds.Length)
    throw new InvalidOperationException("Bound statement emission did not assign unique positive statement IDs.");
statements.EmitWithSourceMap([boundIf], "If True Then\nnumber = 1\nEnd If", "flow.xps");
var repeatedStatementIds = new[] { boundIf.StatementId }.Concat(boundIf.ThenStatements.Select(statement => statement.StatementId)).ToArray();
if (!mappedStatementIds.SequenceEqual(repeatedStatementIds))
    throw new InvalidOperationException("Bound statement IDs were not deterministic across emissions.");
var resumeNext = new BoundOnErrorStatement(BoundErrorHandlerMode.ResumeNext, null);
var disableErrors = new BoundOnErrorStatement(BoundErrorHandlerMode.Disable, null);
var handler = new BoundOnErrorStatement(BoundErrorHandlerMode.GoToLabel, "ErrorHandler");
var resumeCurrent = new BoundResumeStatement(BoundResumeMode.Current, null);
var resumeNextTarget = new BoundResumeStatement(BoundResumeMode.Next, null);
var resumeLabel = new BoundResumeStatement(BoundResumeMode.Label, "Retry");
if (resumeNext.State != new BoundErrorHandlerState(BoundErrorHandlerMode.ResumeNext) ||
    disableErrors.State != new BoundErrorHandlerState(BoundErrorHandlerMode.Disable) ||
    handler.State != new BoundErrorHandlerState(BoundErrorHandlerMode.GoToLabel, "ErrorHandler") ||
    resumeCurrent.ResumeTarget != new BoundResumeTarget(BoundResumeMode.Current) ||
    resumeNextTarget.ResumeTarget != new BoundResumeTarget(BoundResumeMode.Next) ||
    resumeLabel.ResumeTarget != new BoundResumeTarget(BoundResumeMode.Label, "Retry"))
    throw new InvalidOperationException("Bound error-handler state did not preserve its mode or target.");
BoundStatement BindHandlerStatement(string text)
{
    var handlerParser = new StatementParser(text);
    var bound = new StatementBinder(new SymbolTable()).Bind(handlerParser.ParseStatement());
    if (handlerParser.Diagnostics.Count != 0 || bound is null || bound.Span is not { Start: 0, Length: var length } || length != text.Length)
        throw new InvalidOperationException($"Could not bind error-handler fixture: {text}");
    return bound;
}
if (BindHandlerStatement("On Error Resume Next") is not BoundOnErrorStatement { Mode: BoundErrorHandlerMode.ResumeNext } ||
    BindHandlerStatement("On Error GoTo 0") is not BoundOnErrorStatement { Mode: BoundErrorHandlerMode.Disable } ||
    BindHandlerStatement("On Error GoTo ErrorHandler") is not BoundOnErrorStatement { Mode: BoundErrorHandlerMode.GoToLabel, Target: "ErrorHandler" } ||
    BindHandlerStatement("Resume") is not BoundResumeStatement { Mode: BoundResumeMode.Current } ||
    BindHandlerStatement("Resume Next") is not BoundResumeStatement { Mode: BoundResumeMode.Next } ||
    BindHandlerStatement("Resume Retry") is not BoundResumeStatement { Mode: BoundResumeMode.Label, Target: "Retry" })
    throw new InvalidOperationException("Statement binding did not preserve error-handler syntax semantics.");
foreach (var unsupportedHandler in new BoundStatement[]
{
    resumeNext, disableErrors, handler,
    resumeCurrent, resumeNextTarget, resumeLabel
})
{
    try
    {
        statements.Emit([unsupportedHandler]);
        throw new InvalidOperationException("Unsupported error-handler statement was emitted silently.");
    }
    catch (NotSupportedException)
    {
        // Runtime lowering is intentionally deferred; dropping the statement would be unsafe.
    }
}
var selectBody = statements.Emit([new BoundSelectStatement(new BoundLiteralExpression(2L, typeof(long)), [new BoundCaseClause(SelectCaseKind.Value, null, new BoundLiteralExpression(2L, typeof(long)), null, [new BoundAssignmentStatement(sum, new BoundLiteralExpression(9L, typeof(long)), false)])])]);
Equal("var __xpsEmit0 = 2L;\nif (LSCoreCompare.Equal(__xpsEmit0, 2L))\n{\n    number = 9L;\n}\n", selectBody);
var reservedTemporary = new BoundNameExpression(new VariableSymbol("__xpsEmit0", typeof(long)));
var collisionBody = statements.Emit([new BoundSelectStatement(new BoundLiteralExpression(2L, typeof(long)), [new BoundCaseClause(SelectCaseKind.Value, null, new BoundLiteralExpression(2L, typeof(long)), null, [new BoundAssignmentStatement(reservedTemporary, new BoundLiteralExpression(9L, typeof(long)), false)])])]);
if (!collisionBody.Contains("var __xpsEmit1 = 2L;", StringComparison.Ordinal) || !collisionBody.Contains("__xpsEmit0 = 9L;", StringComparison.Ordinal))
    throw new InvalidOperationException("Bound emission temporary names collided with a user symbol.");
var nestedTemporaryBody = statements.Emit([
    new BoundSelectStatement(new BoundLiteralExpression(1L, typeof(long)), [new BoundCaseClause(SelectCaseKind.Value, null, new BoundLiteralExpression(1L, typeof(long)), null, [])]),
    new BoundSelectStatement(new BoundLiteralExpression(2L, typeof(long)), [new BoundCaseClause(SelectCaseKind.Value, null, new BoundLiteralExpression(2L, typeof(long)), null, [])])
]);
if (!nestedTemporaryBody.Contains("var __xpsEmit0 = 1L;", StringComparison.Ordinal) || !nestedTemporaryBody.Contains("var __xpsEmit1 = 2L;", StringComparison.Ordinal))
    throw new InvalidOperationException("Bound emission did not allocate unique temporaries for multiple selectors.");
var caseInsensitiveCollision = new BoundNameExpression(new VariableSymbol("__XPSEMIT0", typeof(long)));
var caseInsensitiveBody = statements.Emit([new BoundSelectStatement(new BoundLiteralExpression(3L, typeof(long)), [new BoundCaseClause(SelectCaseKind.Value, null, new BoundLiteralExpression(3L, typeof(long)), null, [new BoundAssignmentStatement(caseInsensitiveCollision, new BoundLiteralExpression(4L, typeof(long)), false)])])]);
if (!caseInsensitiveBody.Contains("var __xpsEmit1 = 3L;", StringComparison.Ordinal) || !caseInsensitiveBody.Contains("__XPSEMIT0 = 4L;", StringComparison.Ordinal))
    throw new InvalidOperationException("Bound emission temporary names were not case-insensitively collision-safe.");
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
    private sealed class CopybackList
    {
        private long value = 1;
        public long this[object tag] { get => value; set => this.value = value; }
        public CopybackAlias[] Aliases() => [new CopybackAlias(this)];
    }
    private sealed class CopybackAlias(CopybackList list)
    {
        public long Value { get => list["item"]; set => list["item"] = value; }
        public string Tag => "item";
    }
    private static void MutateThenThrow(ref long value)
    {
        value += 10;
        throw new InvalidOperationException("expected");
    }
    public static long ExceptionalCopyback()
    {
        var copybackValues = new CopybackList();
        try { {{exceptionalCopybackBody}} }
        catch (InvalidOperationException) { }
        return copybackValues["item"];
    }
    public static long ExceptionalAliasCopyback()
    {
        var copybackValues = new CopybackList();
        try { {{exceptionalAliasBody}} }
        catch (InvalidOperationException) { }
        return copybackValues["item"];
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
Equal(11L, Invoke("ExceptionalCopyback"));
Equal(11L, Invoke("ExceptionalAliasCopyback"));
// Reflection boxes ref arguments and writes the updated value back into the array.
var refArgs = new object?[] { 3L, 0L };
type.GetMethod("AddInto")!.Invoke(null, refArgs);
Equal(4L, refArgs[1]);
Console.WriteLine("AST_EMISSION_OK");
