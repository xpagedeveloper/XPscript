using XPScript.Compiler.Binding;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Emission;

/// <summary>Emits an experimental C# method body from already bound statements.</summary>
public sealed class BoundStatementEmitter
{
    private readonly BoundExpressionEmitter _expressions = new();

    public string Emit(IReadOnlyList<BoundStatement> statements)
    {
        var output = new BoundEmissionContext(statements);
        foreach (var statement in statements)
            EmitStatement(output, statement, 0);
        return output.Finish().Code;
    }

    public BoundEmissionResult EmitWithSourceMap(IReadOnlyList<BoundStatement> statements, string source, string sourcePath)
    {
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
        switch (statement)
        {
            case BoundAssignmentStatement assignment:
                if (assignment.Target is BoundIndexedPropertyExpression)
                    throw new NotSupportedException("Indexed property assignment requires accessor lowering.");
                Line($"{_expressions.Emit(assignment.Target)} = {_expressions.Emit(assignment.Expression)};");
                break;
            case BoundExpressionStatement expression:
                Line($"{_expressions.Emit(expression.Expression)};");
                break;
            case BoundVariableDeclarationStatement declaration:
                var type = CSharpType(declaration.Local.Type);
                var initializer = declaration.Initializer is null ? $"default({type})" : _expressions.Emit(declaration.Initializer);
                Line($"{type} {declaration.Local.Name} = {initializer};");
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

    private string Condition(BoundExpression expression) => expression.Type == typeof(bool)
        ? _expressions.Emit(expression)
        : $"XPScriptNullRuntime.ConditionValue({_expressions.Emit(expression)})";

    private static string CSharpType(Type type) => type.IsArray ? $"{CSharpType(type.GetElementType()!)}[]" : type == typeof(long) ? "long"
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
