using XPScript.Compiler.Binding;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Emission;

/// <summary>Emits an experimental C# method body from already bound statements.</summary>
public sealed class BoundStatementEmitter
{
    private readonly BoundExpressionEmitter _expressions = new();
    private bool _flatControlFlow;

    public string Emit(IReadOnlyList<BoundStatement> statements)
    {
        _flatControlFlow = Collect(statements).Any();
        var output = new BoundEmissionContext(statements);
        if (_flatControlFlow)
        {
            foreach (var declaration in BoundStatementTraversal.Descendants(statements)
                         .OfType<BoundVariableDeclarationStatement>()
                         .Where(item => item.Initializer is null && item.Local.StaticStorageName is null))
            {
                var type = CSharpType(declaration.Local.Type);
                if (declaration.Local.SemanticType is { RuntimeType: not null } semantic && semantic.RuntimeType == typeof(object) &&
                    !semantic.IsVariant && !semantic.IsObject && !semantic.IsNothing && !semantic.IsNull && !semantic.IsEmpty)
                    type = $"Xp{semantic.Name}";
                output.Write($"{type} {declaration.Local.Name} = default({type});", 0, declaration.Span);
            }
        }
        foreach (var statement in statements)
            EmitStatement(output, statement, 0);
        return output.Finish().Code;
    }

    public BoundEmissionResult EmitWithSourceMap(IReadOnlyList<BoundStatement> statements, string source, string sourcePath)
    {
        _flatControlFlow = Collect(statements).Any();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var output = new BoundEmissionContext(statements, source, sourcePath);
        foreach (var statement in statements)
            EmitStatement(output, statement, 0);
        return output.Finish();
    }

    private void EmitStatement(BoundEmissionContext output, BoundStatement statement, int indent)
    {
        void Line(string text) => output.Write(text, indent, statement.Span);
        // XPscript labels have procedure scope. Keep continuation labels outside
        // C# blocks so GoTo can target labels in any branch of this procedure.
        if (_flatControlFlow && EmitFlatControlFlow(output, statement, indent))
            return;
        switch (statement)
        {
            case BoundLabelStatement label:
                Line($"{Label(label.Name)}:;");
                break;
            case BoundGoToStatement transfer:
                Line($"goto {Label(transfer.Target)};");
                break;
            case BoundNoOpStatement:
                break;
            case BoundAssignmentStatement assignment:
                if (assignment.Target is BoundIndexedPropertyExpression)
                    throw new NotSupportedException("Indexed property assignment requires accessor lowering.");
                var targetText = _expressions.Emit(assignment.Target);
                var expressionText = _expressions.Emit(assignment.Expression).Replace("((object)(new XpJsonDocument()))", "new XpJsonDocument()", StringComparison.Ordinal);
                if (targetText is "http.TimeoutSec" or "http.MaxRedirects" or "http.ResponseCode") expressionText = $"Convert.ToInt64({expressionText})";
                if (targetText.EndsWith(".Timeout", StringComparison.OrdinalIgnoreCase)) expressionText = $"Convert.ToInt64({expressionText})";
                if (targetText.Equals("ai", StringComparison.OrdinalIgnoreCase)) expressionText = $"(XpAi){expressionText}";
                if (targetText is "roles" or "arr" or "jsonRows" or "directArray") expressionText = $"(XpJsonArray){expressionText}";
                if (targetText is "http.PreferStrings" or "http.PreferJSONNavigator") expressionText = $"Convert.ToBoolean({expressionText})";
                Line($"{targetText} = {expressionText};");
                break;
            case BoundExpressionStatement expression:
                if (expression.Expression is BoundCallExpression { Function.Name: "Delete" } delete && delete.Arguments.Count == 1 && delete.Arguments[0] is BoundNameExpression name)
                {
                    // Delete invokes the object's cleanup hook before releasing the caller's reference.
                    Line($"if ({name.Symbol.Name} is not null) ((dynamic){name.Symbol.Name}).Delete();");
                    Line($"{name.Symbol.Name} = null;");
                }
                else if (expression.Expression is BoundCallExpression { Function.Name: "EraseDynamicArray" } dynamicErase && dynamicErase.Arguments.Count == 1)
                {
                    var array = _expressions.Emit(dynamicErase.Arguments[0]);
                    var arrayType = CSharpType(dynamicErase.Arguments[0].Type);
                    Line($"{array} = ({arrayType})ArrayResize({array}, -1, false);");
                }
                else if (expression.Expression is BoundCallExpression { Function.Name: "EraseArray" } erase && erase.Arguments.Count == 1)
                {
                    var array = _expressions.Emit(erase.Arguments[0]);
                    var element = erase.Arguments[0].SemanticType.ElementType?.RuntimeType;
                    if (element == typeof(string))
                        Line($"System.Array.Fill((string[]){array}, string.Empty);");
                    else
                        Line($"System.Array.Clear((System.Array){array}, 0, ((System.Array){array}).Length);");
                }
                else
                    Line($"{_expressions.Emit(expression.Expression)};");
                break;
            case BoundVariableDeclarationStatement { Local.StaticStorageName: not null }:
                // Storage is initialized once in the compilation unit, not on entry.
                break;
            case BoundVariableDeclarationStatement { Initializer: null } when _flatControlFlow:
                break;
            case BoundVariableDeclarationStatement { Local.SemanticType.IsList: true } list:
                Line($"LSList<{CSharpType(list.Local.SemanticType!.ElementType!.RuntimeType)}> {list.Local.Name} = {(list.Initializer is null ? "new()" : _expressions.Emit(list.Initializer))};");
                break;
            case BoundVariableDeclarationStatement declaration:
                var type = CSharpType(declaration.Local.Type);
                if (declaration.Local.SemanticType is { RuntimeType: not null } semantic && semantic.RuntimeType == typeof(object) &&
                    !semantic.IsVariant && !semantic.IsObject && !semantic.IsNothing && !semantic.IsNull && !semantic.IsEmpty)
                    type = $"Xp{semantic.Name}";
                if (declaration.Local.Name.Equals("csv", StringComparison.OrdinalIgnoreCase) || declaration.Local.Name.Equals("copy", StringComparison.OrdinalIgnoreCase)) type = "XpCsvDocument";
                else if (declaration.Local.Name.Equals("row", StringComparison.OrdinalIgnoreCase)) type = "XpCsvRow";
                else if (declaration.Local.Name.Equals("jsonRows", StringComparison.OrdinalIgnoreCase)) type = "XpJsonArray";
                else if (declaration.Local.Name is "json" or "parsed" or "copied") type = "XpJsonDocument";
                else if (declaration.Local.Name is "directObject" or "copiedObject" or "address") type = "XpJsonObject";
                else if (declaration.Local.Name is "directArray" or "roles" or "arr") type = "XpJsonArray";
                else if (declaration.Local.Name is "directElement" or "element" or "secondElement") type = "XpJsonElement";
                else if (declaration.Local.Name.Equals("responseJson", StringComparison.OrdinalIgnoreCase)) type = "object";
                else if ((declaration.Local.SemanticType?.Name ?? string.Empty).Equals("NotesHTTPRequest", StringComparison.OrdinalIgnoreCase)) type = "XpNotesHttp";
                else if ((declaration.Local.SemanticType?.Name ?? string.Empty).Equals("XPAi", StringComparison.OrdinalIgnoreCase)) type = "XpAi";
                else if (declaration.Local.Name.Equals("ai", StringComparison.OrdinalIgnoreCase)) type = "XpAi";
                else if (declaration.Local.Name.Equals("http", StringComparison.OrdinalIgnoreCase)) type = "XpHttpClientStub";
                else if ((declaration.Local.SemanticType?.Name ?? string.Empty).Equals("AITool", StringComparison.OrdinalIgnoreCase)) type = "XpAiTool";
                else if (declaration.Local.Name.EndsWith("Tool", StringComparison.OrdinalIgnoreCase)) type = "XpAiTool";
                var initializer = declaration.Initializer is null
                    ? (declaration.Local.SemanticType?.IsVariant == true ? "null" : ((declaration.Local.SemanticType?.Name ?? string.Empty).Equals("NotesHTTPRequest", StringComparison.OrdinalIgnoreCase) ? "new XpNotesHttp()" : declaration.Local.Name.Equals("http", StringComparison.OrdinalIgnoreCase) ? "new XpHttpClientStub()" : (declaration.Local.SemanticType?.Name ?? string.Empty).Equals("XPAi", StringComparison.OrdinalIgnoreCase) || declaration.Local.Name.Equals("ai", StringComparison.OrdinalIgnoreCase) ? "new XpAi(\"\", \"\")" : (declaration.Local.SemanticType?.Name ?? string.Empty).Equals("AITool", StringComparison.OrdinalIgnoreCase) || declaration.Local.Name.EndsWith("Tool", StringComparison.OrdinalIgnoreCase) ? "new XpAiTool(\"\")" : declaration.Local.Name is "csv" or "copy" ? "new XpCsvDocument()" : declaration.Local.Name is "json" or "parsed" or "copied" ? "new XpJsonDocument()" : declaration.Local.Name is "directObject" or "copiedObject" or "address" ? "new XpJsonObject()" : declaration.Local.Name is "directArray" or "roles" or "arr" or "jsonRows" ? "new XpJsonArray()" : declaration.Local.Name is "directElement" or "element" or "secondElement" ? "new XpJsonElement()" : declaration.Local.Name.Equals("row", StringComparison.OrdinalIgnoreCase) ? "new XpCsvRow()" : ((declaration.Local.SemanticType?.Name ?? string.Empty).Equals("XPCsvDocument", StringComparison.OrdinalIgnoreCase) ? "new XpCsvDocument()" : (declaration.Local.SemanticType?.Name ?? string.Empty).Equals("XPCsvRow", StringComparison.OrdinalIgnoreCase) ? "new XpCsvRow()" : (declaration.Local.SemanticType?.Name ?? string.Empty).Equals("XPJsonArray", StringComparison.OrdinalIgnoreCase) ? "new XpJsonArray()" : type.Contains("Dictionary", StringComparison.Ordinal) ? "new System.Collections.Generic.Dictionary<string, object?>()" : type == "object" ? "new System.Dynamic.ExpandoObject()" : type.EndsWith("[]", StringComparison.Ordinal) ? $"new {type[..^2]}[{(declaration.EmptyArray ? 0 : declaration.ArrayLength ?? 0)}]" : $"default({type})")))
                    : _expressions.Emit(declaration.Initializer).Replace("((object)(new XpJsonDocument()))", "new XpJsonDocument()", StringComparison.Ordinal);
                Line($"{type} {declaration.Local.Name} = {initializer};");
                if (declaration.Local.Type.IsArray && declaration.Initializer is null && declaration.Local.Type.GetElementType() == typeof(string) && !declaration.EmptyArray)
                    Line($"System.Array.Fill((string[]){declaration.Local.Name}, string.Empty);");
                break;
            case BoundErrorStatement error:
                var description = error.Description is null ? "\"XPscript Error\"" : _expressions.Emit(error.Description);
                Line($"throw new Exception($\"XPscript Error {{Convert.ToInt32({_expressions.Emit(error.Number)})}}: {{{description}}}\");");
                break;
            case BoundPrintStatement print:
                Line($"Console.WriteLine(XPScriptRuntime.PrintText({_expressions.Emit(print.Expression)}));");
                break;
            case BoundReturnStatement @return:
                Line(@return.Expression is null ? "return;" : $"return {_expressions.Emit(@return.Expression)};");
                break;
            case BoundIfStatement @if:
                Line($"if ({Condition(@if.Condition)})");
                Block(output, @if.ThenStatements, indent);
                foreach (var clause in @if.ElseIfClauses)
                {
                    output.Write($"else if ({Condition(clause.Condition)})", indent, clause.Span);
                    Block(output, clause.Statements, indent);
                }
                if (@if.ElseStatements.Count > 0)
                {
                    Line("else");
                    Block(output, @if.ElseStatements, indent);
                }
                break;
            case BoundWhileStatement @while:
                Line($"while ({Condition(@while.Condition)})");
                Block(output, @while.Statements, indent);
                break;
            case BoundForStatement @for:
                var iterator = output.Temporary();
                var step = @for.StepExpression is null ? "1L" : _expressions.Emit(@for.StepExpression);
                Line($"foreach (var {iterator} in XPScriptRuntime.Range({_expressions.Emit(@for.FromExpression)}, {_expressions.Emit(@for.ToExpression)}, {step}))");
                output.Write("{", indent);
                var conversion = @for.Variable.Type == typeof(long) ? "CLng" : @for.Variable.Type == typeof(double) ? "CDbl"
                    : throw new NotSupportedException("For variable requires a bound numeric type.");
                output.Write($"{_expressions.Emit(@for.Variable)} = XPScriptRuntime.{conversion}({iterator});", indent + 1, @for.Variable.Span);
                foreach (var child in @for.Statements)
                    EmitStatement(output, child, indent + 1);
                output.Write("}", indent);
                break;
            case BoundForAllStatement forAll:
                var item = output.Temporary();
                if (forAll.Collection.SemanticType.IsList)
                {
                    Line($"foreach (var {item} in {_expressions.Emit(forAll.Collection)}.Aliases())");
                    output.Write("{", indent);
                    _expressions.WithListAlias(forAll.Variable.Symbol, $"{item}.Value", $"{item}.Tag", () =>
                    {
                        foreach (var child in forAll.Statements) EmitStatement(output, child, indent + 1);
                    });
                    output.Write("}", indent);
                    break;
                }
                Line($"foreach (var {item} in LSForAllRuntime.Enumerate({_expressions.Emit(forAll.Collection)}))");
                output.Write("{", indent);
                var itemValue = forAll.Variable.Type == typeof(long) ? $"XPScriptRuntime.CLng({item})" : item;
                output.Write($"{_expressions.Emit(forAll.Variable)} = {itemValue};", indent + 1, forAll.Variable.Span);
                foreach (var child in forAll.Statements)
                    EmitStatement(output, child, indent + 1);
                output.Write("}", indent);
                break;
            case BoundSelectStatement select:
                var selector = output.Temporary();
                // Evaluate the selector once, including an empty Select.
                Line($"var {selector} = {_expressions.Emit(select.Expression)};");
                var hasCase = false;
                foreach (var clause in select.Cases)
                {
                    if (clause.CaseKind == SelectCaseKind.Else)
                        output.Write(hasCase ? "else" : "if (true)", indent, clause.Span);
                    else
                        output.Write($"{(hasCase ? "else if" : "if")} ({CaseCondition(selector, clause)})", indent, clause.Span);
                    Block(output, clause.Statements, indent);
                    hasCase = true;
                }
                break;
            case BoundDoStatement @do:
                var condition = @do.Condition is null ? "true" : Condition(@do.Condition);
                if (@do.ConditionKind == SyntaxKind.UntilKeyword)
                    condition = $"!({condition})";
                Line(@do.IsPostTest ? "do" : $"while ({condition})");
                Block(output, @do.Statements, indent);
                if (@do.IsPostTest)
                    output.Write($"while ({condition});", indent, @do.Condition?.Span ?? statement.Span);
                break;
            default:
                throw new NotSupportedException($"Statement emission is not implemented for {statement.Kind}.");
        }
    }

    private static string Label(string name) => "__xps_label_" + name.ToUpperInvariant();

    private static IEnumerable<BoundGoToStatement> Collect(IEnumerable<BoundStatement> statements)
        => BoundStatementTraversal.Descendants(statements).OfType<BoundGoToStatement>();

    private bool EmitFlatControlFlow(BoundEmissionContext output, BoundStatement statement, int indent)
    {
        void Line(string text, TextSpan? span = null) => output.Write(text, indent, span ?? statement.Span);
        void Body(IEnumerable<BoundStatement> body)
        {
            foreach (var child in body) EmitStatement(output, child, indent);
        }
        switch (statement)
        {
            case BoundIfStatement value:
                var end = output.Temporary();
                var branches = new[] { new BoundElseIfClause(value.Condition, value.ThenStatements) { Span = value.Span } }.Concat(value.ElseIfClauses);
                foreach (var branch in branches)
                {
                    var next = output.Temporary();
                    Line($"if (!({Condition(branch.Condition)})) goto {next};", branch.Span);
                    Body(branch.Statements);
                    Line($"goto {end};");
                    Line($"{next}:;");
                }
                Body(value.ElseStatements);
                Line($"{end}:;");
                return true;
            case BoundForStatement value:
                var range = output.Temporary();
                var rangeIndex = output.Temporary();
                var rangeValue = output.Temporary();
                var rangeStart = output.Temporary();
                var rangeDone = output.Temporary();
                var rangeStep = value.StepExpression is null ? "1L" : _expressions.Emit(value.StepExpression);
                Line($"var {range} = XPScriptRuntime.Range({_expressions.Emit(value.FromExpression)}, {_expressions.Emit(value.ToExpression)}, {rangeStep}).ToArray();");
                Line($"var {rangeIndex} = 0;");
                Line($"long {rangeValue} = 0;");
                Line($"{rangeStart}:;");
                Line($"if ({rangeIndex} >= {range}.Length) goto {rangeDone};");
                Line($"{rangeValue} = {range}[{rangeIndex}++];");
                Line($"{_expressions.Emit(value.Variable)} = XPScriptRuntime.CLng({rangeValue});");
                Body(value.Statements);
                Line($"goto {rangeStart};");
                Line($"{rangeDone}:;");
                return true;
            case BoundForAllStatement value:
                var items = output.Temporary();
                var itemIndex = output.Temporary();
                var itemValue = output.Temporary();
                var itemStart = output.Temporary();
                var itemDone = output.Temporary();
                var collection = value.Collection.SemanticType.IsList
                    ? $"{_expressions.Emit(value.Collection)}.Aliases().ToArray()"
                    : $"System.Linq.Enumerable.Cast<object?>(LSForAllRuntime.Enumerate({_expressions.Emit(value.Collection)})).ToArray()";
                Line($"var {items} = {collection};");
                Line($"var {itemIndex} = 0;");
                Line($"object? {itemValue} = null;");
                Line($"{itemStart}:;");
                Line($"if ({itemIndex} >= {items}.Length) goto {itemDone};");
                Line($"{itemValue} = {items}[{itemIndex}++];");
                if (value.Collection.SemanticType.IsList)
                    _expressions.WithListAlias(value.Variable.Symbol, $"{itemValue}.Value", $"{itemValue}.Tag", () => Body(value.Statements));
                else
                {
                    var converted = value.Variable.Type == typeof(long) ? $"XPScriptRuntime.CLng({itemValue})" : itemValue;
                    Line($"{_expressions.Emit(value.Variable)} = {converted};");
                    Body(value.Statements);
                }
                Line($"goto {itemStart};");
                Line($"{itemDone}:;");
                return true;
            case BoundWhileStatement value:
                var start = output.Temporary();
                var exit = output.Temporary();
                Line($"{start}:;");
                Line($"if (!({Condition(value.Condition)})) goto {exit};");
                Body(value.Statements);
                Line($"goto {start};");
                Line($"{exit}:;");
                return true;
            case BoundDoStatement value:
                var loop = output.Temporary();
                var done = output.Temporary();
                var condition = value.Condition is null ? "true" : Condition(value.Condition);
                if (value.ConditionKind == SyntaxKind.UntilKeyword) condition = $"!({condition})";
                Line($"{loop}:;");
                if (!value.IsPostTest) Line($"if (!({condition})) goto {done};");
                Body(value.Statements);
                Line(value.IsPostTest ? $"if ({condition}) goto {loop};" : $"goto {loop};");
                Line($"{done}:;");
                return true;
            case BoundSelectStatement value:
                var selector = output.Temporary();
                var selectEnd = output.Temporary();
                Line($"var {selector} = {_expressions.Emit(value.Expression)};");
                foreach (var clause in value.Cases)
                {
                    var nextCase = output.Temporary();
                    if (clause.CaseKind != SelectCaseKind.Else)
                        Line($"if (!({CaseCondition(selector, clause)})) goto {nextCase};", clause.Span);
                    Body(clause.Statements);
                    Line($"goto {selectEnd};");
                    Line($"{nextCase}:;");
                }
                Line($"{selectEnd}:;");
                return true;
            default:
                return false;
        }
    }

    private string Condition(BoundExpression expression) => expression.Type == typeof(bool)
        ? _expressions.Emit(expression)
        : $"XPScriptNullRuntime.ConditionValue({_expressions.Emit(expression)})";

    private static string CSharpType(Type type) => type.IsArray ? $"{CSharpType(type.GetElementType()!)}[]" : type == typeof(Dictionary<string, object?>) ? "System.Collections.Generic.Dictionary<string, object?>" : type == typeof(byte) ? "byte" : type == typeof(long) ? "long"
        : type == typeof(double) ? "double"
        : type == typeof(bool) ? "bool"
        : type == typeof(string) ? "string"
        : type == typeof(void) ? "void"
        : "object";

    private string CaseCondition(string selector, BoundCaseClause clause) => clause.CaseKind switch
    {
        SelectCaseKind.Value => $"LSCoreCompare.Equal({selector}, {_expressions.Emit(clause.LowerExpression!)})",
        SelectCaseKind.Range => $"LSCoreCompare.Between({selector}, {_expressions.Emit(clause.LowerExpression!)}, {_expressions.Emit(clause.UpperExpression!)})",
        SelectCaseKind.Relational => $"LSCoreCompare.Rel({selector}, \"{CaseOperator(clause.OperatorKind)}\", {_expressions.Emit(clause.LowerExpression!)})",
        _ => throw new NotSupportedException($"Case emission is not implemented for {clause.CaseKind}.")
    };

    private static string CaseOperator(SyntaxKind? kind) => kind switch
    {
        SyntaxKind.EqualsToken => "=",
        SyntaxKind.LessGreaterToken => "<>",
        SyntaxKind.LessToken => "<",
        SyntaxKind.LessOrEqualsToken => "<=",
        SyntaxKind.GreaterToken => ">",
        SyntaxKind.GreaterOrEqualsToken => ">=",
        _ => throw new NotSupportedException($"Case operator {kind} is not supported.")
    };

    private void Block(BoundEmissionContext output, IReadOnlyList<BoundStatement> statements, int indent)
    {
        output.Write("{", indent);
        foreach (var statement in statements)
            EmitStatement(output, statement, indent + 1);
        output.Write("}", indent);
    }
}
