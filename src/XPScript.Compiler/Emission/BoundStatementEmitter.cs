using XPScript.Compiler.Binding;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Emission;

/// <summary>Emits an experimental C# method body from already bound statements.</summary>
public sealed class BoundStatementEmitter
{
    private readonly BoundExpressionEmitter _expressions;
    public BoundStatementEmitter() : this(new BoundExpressionEmitter()) { }
    internal BoundStatementEmitter(BoundExpressionEmitter expressions) => _expressions = expressions;
    private bool _flatControlFlow;
    internal bool ImageScopeEnabled { get; set; }

    public string Emit(IReadOnlyList<BoundStatement> statements)
    {
        if (BoundStatementTraversal.Descendants(statements).Any(item => item is BoundOnErrorStatement or BoundResumeStatement))
            return new BoundErrorFlowEmitter(ImageScopeEnabled).Emit(statements);
        _flatControlFlow = Collect(statements).Any();
        var output = new BoundEmissionContext(statements);
        if (_flatControlFlow)
        {
            foreach (var declaration in BoundStatementTraversal.Descendants(statements)
                         .OfType<BoundVariableDeclarationStatement>()
                         .Where(item => item.Initializer is null && item.Local.StaticStorageName is null))
            {
                var type = CSharpType(declaration.Local.Type);
                if (declaration.Local.Type.IsArray || declaration.Local.Type == typeof(Array))
                    type = "System.Array";
                type = BoundMethodEmitter.SemanticStorageType(declaration.Local, type);
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
        if (BoundStatementTraversal.Descendants(statements).Any(item => item is BoundOnErrorStatement or BoundResumeStatement))
            return new BoundErrorFlowEmitter(ImageScopeEnabled).EmitMapped(statements, source, sourcePath);
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
            case BoundExitForAllStatement:
                Line("break;");
                break;
            case BoundNoOpStatement:
                break;
            case BoundAssignmentStatement assignment:
                if (assignment.Target is BoundNameExpression { Symbol: LocalSymbol { IsDynamicArray: true } })
                {
                    var value = assignment.Expression is BoundConversionExpression dynamicArrayConversion ? dynamicArrayConversion.Expression : assignment.Expression;
                    Line($"{_expressions.Emit(assignment.Target)} = (System.Array)({_expressions.Emit(value)});");
                    break;
                }
                if (assignment.Target is BoundIndexExpression { Expression: BoundNameExpression { Symbol: ParameterSymbol { Type.IsArray: true } } } typedParameterArray)
                {
                    Line($"LSArrayRuntime.Set({_expressions.Emit(typedParameterArray.Expression)}, {_expressions.Emit(assignment.Expression)}, {_expressions.EmitArrayIndexArguments(typedParameterArray)});");
                    break;
                }
                if (assignment.Target is BoundIndexExpression { Expression.SemanticType.IsVariant: true } variantIndex &&
                    !variantIndex.Expression.Type.IsArray && !variantIndex.Expression.SemanticType.IsArray &&
                    variantIndex.Expression is not BoundNameExpression { Symbol: ParameterSymbol { Type.IsArray: true } } &&
                    variantIndex.Indices.Count == 1)
                {
                    Line($"LSArrayRuntime.SetIndex({_expressions.Emit(variantIndex.Expression)}, {_expressions.Emit(variantIndex.Index)}, {_expressions.Emit(assignment.Expression)});");
                    break;
                }
                if (assignment.Target is BoundIndexedPropertyExpression indexedProperty)
                {
                    if (indexedProperty.Property.SetterName is null)
                        throw new NotSupportedException("Indexed property assignment requires accessor lowering.");
                    var indexes = _expressions.EmitIndexedArguments(indexedProperty);
                    Line($"{_expressions.Emit(indexedProperty.Receiver)}.{indexedProperty.Property.SetterName}({indexes}{(indexes.Length == 0 ? "" : ", ")}{ImageStorageValue(assignment)});");
                    break;
                }
                if (assignment.Target is BoundIndexExpression arrayTarget &&
                    (arrayTarget.Indices.Count > 1 || arrayTarget.Expression.SemanticType.IsArray || arrayTarget.Expression is BoundNameExpression { Symbol: LocalSymbol { IsDynamicArray: true } } ||
                     arrayTarget.Expression is BoundNameExpression { Symbol: VariableSymbol { SemanticType.IsArray: true } }))
                {
                    Line($"LSArrayRuntime.Set({_expressions.Emit(arrayTarget.Expression)}, {_expressions.Emit(assignment.Expression)}, {_expressions.EmitArrayIndexArguments(arrayTarget)});");
                    break;
                }
                var targetText = _expressions.Emit(assignment.Target);
                var expressionText = ImageStorageValue(assignment);
                if (assignment.Target is BoundMemberAccessExpression property)
                    expressionText = AstApplicationSupport.ConvertPropertyValue(property.Receiver.SemanticType.Name, property.Name, expressionText);
                if (BoundExpressionEmitter.RuntimeObjectType(assignment.Target.SemanticType.Name) is { } targetJsonType)
                    expressionText = $"({targetJsonType})({expressionText})";
                if (AstImageSupport.IsImage(assignment.Target.SemanticType.Name) &&
                    assignment.Target is BoundNameExpression { Symbol: FieldSymbol } or BoundMemberAccessExpression { IsFieldStorage: true })
                {
                    Line($"AstImageScope.StoreField(ref {targetText}, {expressionText});");
                    break;
                }
                if (AstImageSupport.IsImage(assignment.Target.SemanticType.Name) && assignment.Target is BoundNameExpression imageTarget)
                {
                    var owns = ImageScopeEnabled && imageTarget.Symbol is LocalSymbol { StaticStorageName: null } or ParameterSymbol { IsByRef: false } or ParameterSymbol { IsBorrowedImageSnapshot: true };
                    expressionText = $"{(owns ? "__imageScope.Replace" : "AstImageScope.ReplaceBorrowed")}({targetText}, {expressionText})";
                }
                if (targetText is "http.TimeoutSec" or "http.MaxRedirects" or "http.ResponseCode") expressionText = $"Convert.ToInt64({expressionText})";
                if (targetText.Equals("ai", StringComparison.OrdinalIgnoreCase)) expressionText = $"(XPScriptAi){expressionText}";
                if (targetText is "http.PreferStrings" or "http.PreferJSONNavigator") expressionText = $"Convert.ToBoolean({expressionText})";
                if (assignment.Target is BoundMemberAccessExpression { Receiver.SemanticType.Name: "NotesHTTPRequest" } notesProperty &&
                    notesProperty.Name is "TimeoutSec" or "MaxRedirects" or "ResponseCode")
                    expressionText = $"Convert.ToInt32({expressionText})";
                if (assignment.Target is BoundMemberAccessExpression { Receiver.SemanticType.Name: "NotesHTTPRequest" } notesBoolean &&
                    notesBoolean.Name is "PreferStrings" or "PreferJSONNavigator")
                    expressionText = $"Convert.ToBoolean({expressionText})";
                Line($"{targetText} = {expressionText};");
                break;
            case BoundExpressionStatement expression:
                if (expression.Expression is BoundCallExpression listCall && listCall.Target is null &&
                    listCall.Function.ReturnType == typeof(void) && listCall.Arguments.Count == 1 &&
                    listCall.Function.ByRefParameters?[0] == true &&
                    (listCall.Arguments[0] is BoundIndexExpression { Expression.SemanticType.IsList: true } ||
                     listCall.Arguments[0] is BoundNameExpression aliasArgument && _expressions.IsListAlias(aliasArgument.Symbol)))
                {
                    var value = output.Temporary();
                    Line("{");
                    string location;
                    var argument = listCall.Arguments[0];
                    if (argument is BoundIndexExpression listElement)
                    {
                        var receiver = output.Temporary();
                        var tag = output.Temporary();
                        output.Write($"var {receiver} = {_expressions.Emit(listElement.Expression)};", indent + 1, listElement.Expression.Span);
                        output.Write($"var {tag} = {_expressions.Emit(listElement.Index)};", indent + 1, listElement.Index.Span);
                        location = $"{receiver}[{tag}]";
                    }
                    else location = _expressions.Emit(argument);
                    output.Write($"var {value} = {location};", indent + 1, argument.Span);
                    output.Write("try", indent + 1, statement.Span);
                    output.Write("{", indent + 1, statement.Span);
                    output.Write($"{listCall.Function.Name}(ref {value});", indent + 2, statement.Span);
                    output.Write("}", indent + 1, statement.Span);
                    output.Write("finally", indent + 1, statement.Span);
                    output.Write("{", indent + 1, statement.Span);
                    output.Write($"{location} = {value};", indent + 2, argument.Span);
                    output.Write("}", indent + 1, statement.Span);
                    Line("}");
                }
                else if (expression.Expression is BoundCallExpression multiListCall && multiListCall.Target is null &&
                    multiListCall.Function.ReturnType == typeof(void) &&
                    multiListCall.Function.ByRefParameters is not null &&
                    multiListCall.Arguments.Zip(multiListCall.Function.ByRefParameters).Any(pair =>
                        pair.Second && (pair.First is BoundIndexExpression { Expression.SemanticType.IsList: true } ||
                                        pair.First is BoundNameExpression alias && _expressions.IsListAlias(alias.Symbol))))
                {
                    Line("{");
                    var callArguments = new List<string>();
                    var copyBacks = new List<(string Location, string Value, TextSpan? Span)>();
                    for (var i = 0; i < multiListCall.Arguments.Count; i++)
                    {
                        var argument = multiListCall.Arguments[i];
                        var byRef = multiListCall.Function.ByRefParameters[i];
                        if (byRef && argument is BoundIndexExpression listElement && listElement.Expression.SemanticType.IsList)
                        {
                            var receiver = output.Temporary();
                            var tag = output.Temporary();
                            var value = output.Temporary();
                            output.Write($"var {receiver} = {_expressions.Emit(listElement.Expression)};", indent + 1, listElement.Expression.Span);
                            output.Write($"var {tag} = {_expressions.Emit(listElement.Index)};", indent + 1, listElement.Index.Span);
                            var location = $"{receiver}[{tag}]";
                            output.Write($"var {value} = {location};", indent + 1, argument.Span);
                            callArguments.Add($"ref {value}");
                            copyBacks.Add((location, value, argument.Span));
                        }
                        else if (byRef && argument is BoundNameExpression alias && _expressions.IsListAlias(alias.Symbol))
                        {
                            var value = output.Temporary();
                            var location = _expressions.Emit(argument);
                            output.Write($"var {value} = {location};", indent + 1, argument.Span);
                            callArguments.Add($"ref {value}");
                            copyBacks.Add((location, value, argument.Span));
                        }
                        else
                            callArguments.Add((byRef ? "ref " : string.Empty) + _expressions.Emit(argument));
                    }
                    output.Write("try", indent + 1, statement.Span);
                    output.Write("{", indent + 1, statement.Span);
                    output.Write($"{multiListCall.Function.Name}({string.Join(", ", callArguments)});", indent + 2, statement.Span);
                    output.Write("}", indent + 1, statement.Span);
                    output.Write("finally", indent + 1, statement.Span);
                    output.Write("{", indent + 1, statement.Span);
                    foreach (var copyBack in copyBacks)
                        output.Write($"{copyBack.Location} = {copyBack.Value};", indent + 2, copyBack.Span);
                    output.Write("}", indent + 1, statement.Span);
                    Line("}");
                }
                else if (expression.Expression is BoundCallExpression { Function.Name: "Delete" } delete && delete.Arguments.Count == 1)
                {
                    // Delete invokes the object's cleanup hook before releasing the caller's reference.
                    var argument = delete.Arguments[0];
                    while (argument is BoundConversionExpression conversionArgument) argument = conversionArgument.Expression;
                    if (argument is BoundNameExpression name)
                    {
                        var target = name.Symbol.Name;
                        Line($"try {{ if ({target} is not null) ((dynamic){target}).Delete(); }} finally {{ {target} = null; }}");
                    }
                    else
                        Line($"{_expressions.Emit(delete)};");
                }
                else if (expression.Expression is BoundCallExpression { Function.Name: "EraseDynamicArray" } dynamicErase && dynamicErase.Arguments.Count == 1)
                {
                    var array = _expressions.Emit(dynamicErase.Arguments[0]);
                    var arrayType = "System.Array";
                    Line($"{array} = ({arrayType})ArrayResize({array}, -1, false);");
                }
                else if (expression.Expression is BoundCallExpression { Function.Name: "EraseArray" } erase && erase.Arguments.Count == 1)
                {
                    Line($"LSArrayRuntime.Erase({_expressions.Emit(erase.Arguments[0])});");
                }
                else
                    Line($"{_expressions.Emit(expression.Expression)};");
                break;
            case BoundVariableDeclarationStatement { Local.StaticStorageName: not null } persistent:
                if (persistent.Initializer is not null)
                {
                    var storage = persistent.Local.StaticStorageName;
                    Line($"lock ({storage}_initGate)");
                    Line("{");
                    output.Write($"if ({storage}_initState == 1) throw new InvalidOperationException(\"Recursive Static initialization detected.\");", indent + 1, persistent.Span);
                    output.Write($"if ({storage}_initState == 0)", indent + 1, persistent.Span);
                    output.Write("{", indent + 1, persistent.Span);
                    output.Write($"{storage}_initState = 1;", indent + 2, persistent.Span);
                    output.Write($"{storage} = {_expressions.Emit(persistent.Initializer)};", indent + 2, persistent.Initializer.Span);
                    output.Write($"{storage}_initState = 2;", indent + 2, persistent.Span);
                    output.Write("}", indent + 1, persistent.Span);
                    Line("}");
                }
                break;
            case BoundVariableDeclarationStatement { Initializer: null } flatDeclaration when _flatControlFlow:
                if (flatDeclaration.Local.IsDynamicArray || flatDeclaration.Local.Type.IsArray || flatDeclaration.Local.Type == typeof(Array) || flatDeclaration.Local.SemanticType?.IsArray == true)
                    Line($"{_expressions.Emit(new BoundNameExpression(flatDeclaration.Local))} = {BoundArrayAllocation.Emit(flatDeclaration.Local, flatDeclaration.EmptyArray, flatDeclaration.ArrayLength)};");
                break;
            case BoundVariableDeclarationStatement { Local.SemanticType.IsList: true } list:
                Line($"{BoundMethodEmitter.CSharpType(list.Local.SemanticType!)} {list.Local.Name} = {(list.Initializer is null ? "new()" : _expressions.Emit(list.Initializer))};");
                break;
            case BoundVariableDeclarationStatement declaration:
                var type = CSharpType(declaration.Local.Type);
                if (declaration.Local.IsDynamicArray)
                    type = "System.Array";
                if (declaration.Local.Type.IsArray || declaration.Local.Type == typeof(Array))
                    type = "System.Array";
                type = BoundMethodEmitter.SemanticStorageType(declaration.Local, type);
                if (declaration.Local.ArrayLength is not null || declaration.Local.ArrayBounds.Count > 0 || declaration.Local.ArrayLowerBound != 0 || declaration.Local.ArrayRank > 1)
                    type = "System.Array";
                if ((declaration.Local.SemanticType?.Name ?? string.Empty).Equals("XPAi", StringComparison.OrdinalIgnoreCase)) type = "XPScriptAi";
                else if ((declaration.Local.SemanticType?.Name ?? string.Empty).Equals("XPAITool", StringComparison.OrdinalIgnoreCase)) type = "XPScriptAiTool";
                var initializer = declaration.Initializer is null
                    ? DefaultInitializer(declaration, type)
                    : _expressions.Emit(declaration.Initializer);
                if (declaration.Local.SemanticType?.Name.Equals("NotesHTTPRequest", StringComparison.OrdinalIgnoreCase) == true && declaration.Initializer is null)
                    initializer = "new NotesHTTPRequest()";
                if (BoundExpressionEmitter.RuntimeObjectType(declaration.Local.SemanticType?.Name ?? "") is { } jsonType)
                {
                    type = jsonType;
                    initializer = declaration.Initializer is null ? "null" : $"({jsonType})({initializer})";
                }
                if (ImageScopeEnabled && AstImageSupport.IsImage(declaration.Local.SemanticType?.Name) && declaration.Local.StaticStorageName is null)
                    initializer = $"__imageScope.Own({initializer})";
                if (declaration.Initializer is null && (declaration.Local.IsDynamicArray || declaration.Local.Type.IsArray || declaration.Local.Type == typeof(Array) || declaration.Local.SemanticType?.IsArray == true))
                    initializer = BoundArrayAllocation.Emit(declaration.Local, declaration.EmptyArray, declaration.ArrayLength);
                if (declaration.Initializer is null && declaration.Local.SemanticType?.IsObject == true)
                    initializer = "null";
                if (declaration.Initializer is null && declaration.Local.Type.IsValueType && initializer == "null")
                    initializer = $"default({type})";
                if (initializer.Contains("LSArrayRuntime.CreateManaged<", StringComparison.Ordinal))
                    type = "System.Array";
                Line($"{type} {declaration.Local.Name} = {initializer};");
                if (ImageScopeEnabled && AstImageSupport.IsImage(declaration.Local.SemanticType?.Name) && declaration.Local.StaticStorageName is null)
                    Line($"__imageScope.Observe(() => {declaration.Local.Name});");
                break;
            case BoundErrorStatement error:
                var description = error.Description is null ? "\"XPscript Error\"" : _expressions.Emit(error.Description);
                Line($"XPScriptErrorRuntime.Raise(Convert.ToInt32({_expressions.Emit(error.Number)}), {description});");
                break;
            case BoundPrintStatement print:
                Line($"Console.WriteLine(Program.AstPrintText({_expressions.Emit(print.Expression)}));");
                break;
            case BoundReturnStatement @return:
                var returnedValue = @return.Expression is null ? null : _expressions.Emit(@return.Expression);
                if (ImageScopeEnabled && @return.Expression is not null)
                    returnedValue = $"__imageScope.EscapeValue({returnedValue})";
                Line(returnedValue is null ? "return;" : $"return {returnedValue};");
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
                var itemValue = _expressions.IterationValue(forAll.Variable, item);
                var variable = _expressions.Emit(forAll.Variable);
                output.Write(forAll.Variable.Symbol is VariableSymbol { IsForAllAlias: true }
                    ? $"var {variable} = {itemValue};"
                    : $"{variable} = {itemValue};", indent + 1, forAll.Variable.Span);
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
            case BoundWithStatement with:
                // The receiver is already captured in each bound member access;
                // emitting the body preserves one evaluation per access and
                // supports nested With scopes.
                foreach (var child in with.Statements)
                    EmitStatement(output, child, indent);
                break;
            default:
                throw new NotSupportedException($"Statement emission is not implemented for {statement.Kind}.");
        }
    }

    internal void EmitAtomic(BoundEmissionContext output, BoundStatement statement, int indent)
        => EmitStatement(output, statement, indent);

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
                var itemType = value.Collection.SemanticType.IsList
                    ? $"LSListAlias<{BoundMethodEmitter.CSharpType(value.Collection.SemanticType.ElementType!)}>" : "object?";
                Line($"{itemType} {itemValue} = null;");
                Line($"{itemStart}:;");
                Line($"if ({itemIndex} >= {items}.Length) goto {itemDone};");
                Line($"{itemValue} = {items}[{itemIndex}++];");
                if (value.Collection.SemanticType.IsList)
                    _expressions.WithListAlias(value.Variable.Symbol, $"{itemValue}.Value", $"{itemValue}.Tag", () => Body(value.Statements));
                else
                {
                    var converted = _expressions.IterationValue(value.Variable, itemValue);
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

    private static string DefaultInitializer(BoundVariableDeclarationStatement declaration, string type)
    {
        var semanticName = declaration.Local.SemanticType?.Name ?? string.Empty;
        if (declaration.Local.SemanticType?.IsVariant == true) return "null";
        if (semanticName.Equals("XPAi", StringComparison.OrdinalIgnoreCase)) return "new XpAi(\"\", \"\")";
        if (semanticName.Equals("XPAITool", StringComparison.OrdinalIgnoreCase)) return "new XpAiTool(\"\")";
        if (semanticName.Equals("XPJsonArray", StringComparison.OrdinalIgnoreCase)) return "new XpJsonArray()";
        if (type.Contains("Dictionary", StringComparison.Ordinal)) return "new System.Collections.Generic.Dictionary<string, object?>()";
        if (type == "object") return "new System.Dynamic.ExpandoObject()";
        if (type.EndsWith("[]", StringComparison.Ordinal))
        {
            var length = declaration.EmptyArray
                ? 0
                : (declaration.Local.ArrayBounds.Count > 0 ? declaration.Local.ArrayBounds[0].Upper : declaration.ArrayLength ?? 0) - declaration.Local.ArrayLowerBound + 1;
            return $"new {type[..^2]}[{length}]";
        }
        if (type.StartsWith("Xp", StringComparison.Ordinal)) return $"new {type}()";
        return $"default({type})";
    }

    private static string CSharpType(Type type) => type.IsArray ? $"{CSharpType(type.GetElementType()!)}[{(type.GetArrayRank() == 1 ? string.Empty : new string(',', type.GetArrayRank() - 1))}]" : type == typeof(Array) ? "System.Array" : type == typeof(Dictionary<string, object?>) ? "System.Collections.Generic.Dictionary<string, object?>" : type == typeof(byte) ? "byte" : type == typeof(long) ? "long"
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

    private string ImageStorageValue(BoundAssignmentStatement assignment)
    {
        var value = _expressions.Emit(assignment.Expression);
        return ImageScopeEnabled && AstImageSupport.IsImageStorage(assignment.Target) && AstImageSupport.IsOwningValue(assignment.Expression)
            ? $"__imageScope.Own((XPImage)({value}))" : value;
    }

    private void Block(BoundEmissionContext output, IReadOnlyList<BoundStatement> statements, int indent)
    {
        output.Write("{", indent);
        foreach (var statement in statements)
            EmitStatement(output, statement, indent + 1);
        output.Write("}", indent);
    }
}
