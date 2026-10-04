using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Binding;

public sealed class StatementBinder(SymbolTable? symbols = null)
{
    private readonly SymbolTable _symbols = symbols ?? new SymbolTable();
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    public IReadOnlyList<SyntaxDiagnostic> Diagnostics => _diagnostics;

    public void Bind(StatementSyntax syntax)
    {
        switch (syntax)
        {
            case AssignmentStatementSyntax assignment:
                BindAssignment(assignment.Target, assignment.Expression);
                break;
            case SetStatementSyntax set:
                BindAssignment(set.Target, set.Expression, isSet: true);
                break;
            default:
                _diagnostics.Add(new SyntaxDiagnostic(
                    CompilerDiagnosticCodes.InvalidSyntax,
                    $"Statement binding is not implemented for {syntax.Kind}.",
                    syntax.Span));
                break;
        }
    }

    private void BindAssignment(ExpressionSyntax targetSyntax, ExpressionSyntax valueSyntax, bool isSet = false)
    {
        var targetBinder = new ExpressionBinder(_symbols);
        var target = targetBinder.Bind(targetSyntax);
        _diagnostics.AddRange(targetBinder.Diagnostics);
        if (targetBinder.Diagnostics.Count != 0)
            return;

        if (!IsWritable(target))
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                CompilerDiagnosticCodes.TypeMismatch,
                "Assignment target is not writable.",
                targetSyntax.Span));
            return;
        }

        var valueBinder = new ExpressionBinder(_symbols);
        var value = valueBinder.Bind(valueSyntax);
        _diagnostics.AddRange(valueBinder.Diagnostics);
        if (valueBinder.Diagnostics.Count != 0)
            return;

        if (isSet && !IsReferenceType(target))
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                CompilerDiagnosticCodes.TypeMismatch,
                $"Set requires an object/reference target, not {target.SemanticType.Name}.",
                targetSyntax.Span));
            return;
        }

        if (!IsAssignmentCompatible(target, value))
            _diagnostics.Add(new SyntaxDiagnostic(
                CompilerDiagnosticCodes.TypeMismatch,
                $"Cannot assign {value.SemanticType.Name} to {target.SemanticType.Name}.",
                valueSyntax.Span));
    }

    private static bool IsWritable(BoundExpression target) =>
        target is BoundNameExpression name &&
        name.Symbol is VariableSymbol or LocalSymbol or ParameterSymbol or FieldSymbol
        || target is BoundMemberAccessExpression
        || target is BoundIndexExpression;

    private static bool IsReferenceType(BoundExpression expression) =>
        expression.Type == typeof(object) || !expression.Type.IsValueType && expression.Type != typeof(string);

    private static bool IsAssignmentCompatible(BoundExpression target, BoundExpression value)
    {
        if (target.Type != value.Type)
            return false;

        return string.Equals(
            target.SemanticType.Name,
            value.SemanticType.Name,
            StringComparison.OrdinalIgnoreCase);
    }
}
