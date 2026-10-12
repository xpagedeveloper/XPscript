using XPScript.Compiler.Binding;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Emission;

/// <summary>Turns protected procedures into resumable instruction graphs.</summary>
internal sealed class BoundErrorFlowEmitter
{
    private sealed record Instruction(BoundStatement Source, int Next, Action<int> Write);
    private readonly List<Instruction> _instructions = [];
    private readonly Dictionary<string, int> _labels = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _storage = [];
    private readonly BoundExpressionEmitter _expressions = new();
    private BoundEmissionContext _output = null!;
    private string _pc = "", _next = "", _mode = "", _handler = "", _active = "", _fault = "", _after = "";

    public string Emit(IReadOnlyList<BoundStatement> statements) => Build(statements, null, null).Code;
    public BoundEmissionResult EmitMapped(IReadOnlyList<BoundStatement> statements, string source, string path)
        => Build(statements, source, path);

    private BoundEmissionResult Build(IReadOnlyList<BoundStatement> statements, string? source, string? path)
    {
        _output = new BoundEmissionContext(statements, source, path);
        _pc = _output.Temporary(); _next = _output.Temporary(); _mode = _output.Temporary();
        _handler = _output.Temporary(); _active = _output.Temporary(); _fault = _output.Temporary(); _after = _output.Temporary();
        var entry = Sequence(statements, -1, -1);
        foreach (var declaration in BoundStatementTraversal.Descendants(statements).OfType<BoundVariableDeclarationStatement>()
                     .Where(item => item.Local.StaticStorageName is null))
        {
            var type = declaration.Local.SemanticType?.IsList == true
                ? $"LSList<{BoundMethodEmitter.CSharpType(declaration.Local.SemanticType.ElementType!.RuntimeType)}>"
                : BoundMethodEmitter.CSharpType(declaration.Local.Type);
            _output.Write($"{type} {declaration.Local.Name} = default({type})!;", 0, declaration.Span);
        }
        foreach (var storage in _storage) _output.Write(storage, 0);
        _output.Write($"int {_pc} = {entry}, {_next} = -1, {_mode} = 0, {_handler} = -1, {_fault} = -1, {_after} = -1;", 0);
        _output.Write($"bool {_active} = false;", 0);
        _output.Write($"while ({_pc} >= 0)", 0);
        _output.Write("{", 0);
        _output.Write("try", 1); _output.Write("{", 1);
        _output.Write($"switch ({_pc})", 2); _output.Write("{", 2);
        for (var id = 0; id < _instructions.Count; id++)
        {
            var instruction = _instructions[id];
            _output.Write($"case {id}:", 3, instruction.Source.Span); _output.Write("{", 3);
            _output.Write($"{_next} = {instruction.Next};", 4, instruction.Source.Span);
            instruction.Write(4);
            _output.Write($"{_pc} = {_next};", 4); _output.Write("break;", 4); _output.Write("}", 3);
        }
        _output.Write("}", 2); _output.Write("}", 1);
        var exception = _output.Temporary();
        _output.Write($"catch (Exception {exception})", 1); _output.Write("{", 1);
        _output.Write($"if ({_mode} == 0 || {_active}) throw;", 2);
        var sourceLine = source is null ? _pc : $"({_pc} switch {{ {string.Join(", ", _instructions.Select((instruction, id) => $"{id} => {SourceTextMap.GetPosition(source, instruction.Source.Span?.Start ?? 0).Line}"))}, _ => 0 }})";
        _output.Write($"XPScriptErrorRuntime.Capture(LSExtendedErrorRuntime.Normalize({exception}), {sourceLine});", 2);
        _output.Write($"if ({_mode} == 1) {_pc} = {_next};", 2);
        _output.Write($"else {{ {_fault} = {_pc}; {_after} = {_next}; {_active} = true; {_pc} = {_handler}; }}", 2);
        _output.Write("}", 1); _output.Write("}", 0);
        return _output.Finish();
    }

    private int Add(BoundStatement source, int next, Action<int> write)
    {
        var id = _instructions.Count;
        var captured = _expressions.CaptureAliasScope(() => write(4));
        _instructions.Add(new Instruction(source, next, _ => captured()));
        return id;
    }

    private string E(BoundExpression expression) => _expressions.Emit(expression);
    private string Condition(BoundExpression expression) => expression.Type == typeof(bool)
        ? E(expression) : $"XPScriptNullRuntime.ConditionValue({E(expression)})";
    private void Write(string code, int indent, BoundStatement source) => _output.Write(code, indent, source.Span);
    private int Target(string name) => _labels.TryGetValue(name, out var id) ? id
        : throw new InvalidOperationException($"Unknown label '{name}'.");

    private int Sequence(IEnumerable<BoundStatement> statements, int next, int exit)
    {
        foreach (var statement in statements.Reverse()) next = Lower(statement, next, exit);
        return next;
    }

    private int Lower(BoundStatement statement, int next, int exit)
    {
        switch (statement)
        {
            case BoundLabelStatement label:
                _labels.Add(label.Name, next);
                return next;
            case BoundGoToStatement transfer:
                return Add(statement, next, indent => Write($"{_next} = {Target(transfer.Target)};", indent, statement));
            case BoundOnErrorStatement handler:
                return Add(statement, next, indent =>
                {
                    Write($"{_mode} = {(handler.Mode == BoundErrorHandlerMode.Disable ? 0 : handler.Mode == BoundErrorHandlerMode.ResumeNext ? 1 : 2)};", indent, statement);
                    if (handler.Target is not null) Write($"{_handler} = {Target(handler.Target)};", indent, statement);
                });
            case BoundResumeStatement resume:
                return Add(statement, next, indent =>
                {
                    Write($"if (!{_active}) throw new InvalidOperationException(\"Resume without an active error handler.\");", indent, statement);
                    Write($"{_active} = false;", indent, statement);
                    Write($"{_next} = {(resume.Mode == BoundResumeMode.Current ? _fault : resume.Mode == BoundResumeMode.Next ? _after : Target(resume.Target!).ToString())};", indent, statement);
                    Write("XPScriptErrorRuntime.Clear();", indent, statement);
                });
            case BoundIfStatement conditional:
                var branch = Sequence(conditional.ElseStatements, next, exit);
                foreach (var clause in conditional.ElseIfClauses.Reverse())
                {
                    var body = Sequence(clause.Statements, next, exit);
                    var alternative = branch;
                    branch = Add(statement, next, indent => Write($"{_next} = {Condition(clause.Condition)} ? {body} : {alternative};", indent, statement));
                }
                var then = Sequence(conditional.ThenStatements, next, exit);
                var otherwise = branch;
                return Add(statement, next, indent => Write($"{_next} = {Condition(conditional.Condition)} ? {then} : {otherwise};", indent, statement));
            case BoundWhileStatement loop:
                var test = _instructions.Count;
                var bodyStart = -1;
                Add(statement, next, indent => Write($"{_next} = {Condition(loop.Condition)} ? {bodyStart} : {next};", indent, statement));
                bodyStart = Sequence(loop.Statements, test, exit);
                return test;
            case BoundDoStatement loop:
                var doTest = _instructions.Count;
                var doBody = -1;
                var condition = loop.Condition is null ? "true" : Condition(loop.Condition);
                if (loop.ConditionKind == SyntaxKind.UntilKeyword) condition = $"!({condition})";
                Add(statement, next, indent => Write($"{_next} = {condition} ? {doBody} : {next};", indent, statement));
                doBody = Sequence(loop.Statements, doTest, exit);
                return loop.IsPostTest ? doBody : doTest;
            case BoundForStatement loop:
                var values = _output.Temporary(); var index = _output.Temporary();
                _storage.Add($"long[] {values} = []; int {index} = 0;");
                var forTest = _instructions.Count; var forBody = -1;
                Add(statement, next, indent =>
                {
                    Write($"if ({index} < {values}.Length) {{ {E(loop.Variable)} = XPScriptRuntime.CLng({values}[{index}++]); {_next} = {forBody}; }}", indent, statement);
                });
                forBody = Sequence(loop.Statements, forTest, exit);
                return Add(statement, next, indent => Write($"{values} = XPScriptRuntime.Range({E(loop.FromExpression)}, {E(loop.ToExpression)}, {(loop.StepExpression is null ? "1L" : E(loop.StepExpression))}).ToArray(); {index} = 0; {_next} = {forTest};", indent, statement));
            case BoundForAllStatement loop:
                var items = _output.Temporary(); var itemIndex = _output.Temporary();
                var isList = loop.Collection.SemanticType.IsList;
                _storage.Add(isList
                    ? $"dynamic[] {items} = []; int {itemIndex} = 0;"
                    : $"object?[] {items} = []; int {itemIndex} = 0;");
                var allTest = _instructions.Count; var allBody = -1;
                Add(statement, next, indent =>
                {
                    Write($"if ({itemIndex} < {items}.Length) {{", indent, statement);
                    if (!isList) Write($"{E(loop.Variable)} = {(loop.Variable.Type == typeof(long) ? $"XPScriptRuntime.CLng({items}[{itemIndex}])" : $"{items}[{itemIndex}]")};", indent + 1, statement);
                    Write($"{itemIndex}++; {_next} = {allBody}; }}", indent + 1, statement);
                });
                if (isList)
                    _expressions.WithListAlias(loop.Variable.Symbol, $"{items}[{itemIndex} - 1].Value", $"{items}[{itemIndex} - 1].Tag",
                        () => allBody = Sequence(loop.Statements, allTest, next));
                else allBody = Sequence(loop.Statements, allTest, next);
                return Add(statement, next, indent => Write($"{items} = {(isList ? $"{E(loop.Collection)}.Aliases().Cast<dynamic>().ToArray()" : $"System.Linq.Enumerable.Cast<object?>(LSForAllRuntime.Enumerate({E(loop.Collection)})).ToArray()")}; {itemIndex} = 0; {_next} = {allTest};", indent, statement));
            case BoundExitForAllStatement:
                return Add(statement, next, indent => Write($"{_next} = {exit};", indent, statement));
            case BoundSelectStatement select:
                var selector = _output.Temporary();
                _storage.Add($"object? {selector} = null;");
                var selectBranch = next;
                foreach (var clause in select.Cases.Reverse())
                {
                    var clauseBody = Sequence(clause.Statements, next, exit);
                    var alternative = selectBranch;
                    var comparison = clause.CaseKind switch
                    {
                        SelectCaseKind.Value => $"LSCoreCompare.Equal({selector}, {E(clause.LowerExpression!)})",
                        SelectCaseKind.Range => $"LSCoreCompare.Between({selector}, {E(clause.LowerExpression!)}, {E(clause.UpperExpression!)})",
                        SelectCaseKind.Relational => $"LSCoreCompare.Rel({selector}, \"{clause.OperatorKind switch { SyntaxKind.EqualsToken => "=", SyntaxKind.LessToken => "<", SyntaxKind.GreaterToken => ">", SyntaxKind.LessOrEqualsToken => "<=", SyntaxKind.GreaterOrEqualsToken => ">=", _ => "<>" }}\", {E(clause.LowerExpression!)})",
                        _ => "true"
                    };
                    selectBranch = Add(statement, next, indent => Write($"{_next} = {comparison} ? {clauseBody} : {alternative};", indent, statement));
                }
                var selectStart = selectBranch;
                return Add(statement, next, indent => Write($"{selector} = {E(select.Expression)}; {_next} = {selectStart};", indent, statement));
            default:
                // Reuse ordinary statement lowering, including ByRef copy-back
                // and Static initialization, inside the protected instruction.
                return Add(statement, next, indent =>
                {
                    if (statement is BoundVariableDeclarationStatement declaration && declaration.Local.StaticStorageName is null)
                    {
                        var temporaryOutput = new BoundEmissionContext([statement]);
                        new BoundStatementEmitter(_expressions).EmitAtomic(temporaryOutput, statement, 0);
                        var code = temporaryOutput.Finish().Code;
                        var start = code.IndexOf(declaration.Local.Name + " = ", StringComparison.Ordinal);
                        foreach (var line in code[start..].TrimEnd().Split('\n'))
                            Write(line, indent, statement);
                    }
                    else new BoundStatementEmitter(_expressions).EmitAtomic(_output, statement, indent);
                });
        }
    }
}
