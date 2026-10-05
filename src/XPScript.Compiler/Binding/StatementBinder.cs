using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Binding;

public sealed class StatementBinder(SymbolTable? symbols = null, XpTypeSymbol? returnType = null, bool allowsReturnValue = false, bool allowDynamicMembers = false)
{
    private readonly SymbolTable _symbols = symbols ?? new SymbolTable();
    private readonly XpTypeSymbol? _returnType = returnType;
    private readonly bool _allowsReturnValue = allowsReturnValue;
    private readonly bool _allowDynamicMembers = allowDynamicMembers;
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    public IReadOnlyList<SyntaxDiagnostic> Diagnostics => _diagnostics;

    public BoundStatement? Bind(StatementSyntax syntax)
    {
        BoundStatement? bound = syntax switch
        {
            AssignmentStatementSyntax assignment => BindAssignment(assignment.Target, assignment.Expression),
            DimStatementSyntax dim => BindDim(dim),
            SetStatementSyntax set => BindAssignment(set.Target, set.Expression, isSet: true),
            ExpressionStatementSyntax expression => BindExpressionStatement(expression),
            CallStatementSyntax call => BindCallStatement(call),
            RuntimeFileStatementSyntax runtime => BindRuntimeStatement(runtime),
            ReturnStatementSyntax @return => BindReturn(@return),
            ExitStatementSyntax exit => BindExit(exit),
            ErrorStatementSyntax error => BindError(error),
            IfStatementSyntax @if => BindIf(@if),
            ForStatementSyntax @for => BindFor(@for),
            ForAllStatementSyntax forAll => BindForAll(forAll),
            WhileStatementSyntax @while => BindWhile(@while),
            DoStatementSyntax @do => BindDo(@do),
            SelectStatementSyntax select => BindSelect(select),
            _ => BindUnsupported(syntax)
        };
        if (bound is not null)
            bound.Span = syntax.Span;
        return bound;
    }

    private BoundStatement? BindUnsupported(StatementSyntax syntax)
    {
        if (_allowDynamicMembers)
            return new BoundNoOpStatement();
        _diagnostics.Add(new SyntaxDiagnostic(
            CompilerDiagnosticCodes.InvalidSyntax,
            $"Statement binding is not implemented for {syntax.Kind}.",
            syntax.Span));
        return null;
    }

    private BoundExpression? BindExpression(ExpressionSyntax syntax)
    {
        var binder = new ExpressionBinder(_symbols, _allowDynamicMembers);
        var expression = binder.Bind(syntax);
        _diagnostics.AddRange(binder.Diagnostics);
        return binder.Diagnostics.Count == 0 ? expression : null;
    }

    private IReadOnlyList<BoundStatement> BindStatements(IReadOnlyList<StatementSyntax> statements)
    {
        var result = new List<BoundStatement>(statements.Count);
        foreach (var statement in statements)
        {
            var bound = Bind(statement);
            if (bound is not null)
                result.Add(bound);
        }
        return result;
    }

    private BoundStatement? BindExpressionStatement(ExpressionStatementSyntax syntax)
    {
        var expression = BindExpression(syntax.Expression);
        if (_allowDynamicMembers && expression is BoundNameExpression or BoundLiteralExpression)
            return new BoundNoOpStatement();
        return expression is null ? null : new BoundExpressionStatement(expression);
    }

    private BoundStatement? BindDim(DimStatementSyntax syntax)
    {
        var (type, semanticType) = ResolveDimType(syntax.TypeNameToken?.Text, syntax.IsArray, syntax.ArrayLength);
        var local = new LocalSymbol(syntax.IdentifierToken.Text, type, semanticType);
        if (!_symbols.TryDeclare(local, out var code, out var message))
        {
            if (_allowDynamicMembers && _symbols.TryLookup(local.Name, out _))
                return new BoundNoOpStatement();
            _diagnostics.Add(new SyntaxDiagnostic(code ?? CompilerDiagnosticCodes.DuplicateOverload,
                message ?? $"Symbol '{local.Name}' is already declared.", syntax.IdentifierToken.Span));
            return null;
        }

        BoundExpression? initializer = null;
        if (syntax.Initializer is not null)
        {
            initializer = BindExpression(syntax.Initializer);
            if (initializer is null)
                return null;
            var conversion = Conversion.Classify(initializer.SemanticType, semanticType);
            if (!conversion.IsImplicit)
            {
                _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                    $"Cannot assign {initializer.SemanticType.Name} to {semanticType.Name}.", syntax.Initializer.Span));
                return null;
            }
            if (!conversion.IsIdentity)
                initializer = new BoundConversionExpression(initializer, semanticType, conversion) { Span = initializer.Span };
        }
        return new BoundVariableDeclarationStatement(local, initializer);
    }

    private static (Type RuntimeType, XpTypeSymbol SemanticType) ResolveDimType(string? name, bool isArray = false, int? arrayLength = null)
    {
        var result = name?.Trim().ToUpperInvariant() switch
        {
            "BOOLEAN" => (typeof(bool), XpTypeSymbol.FromClr(typeof(bool))),
            "STRING" => (typeof(string), XpTypeSymbol.FromClr(typeof(string))),
            "INTEGER" or "LONG" => (typeof(long), XpTypeSymbol.FromClr(typeof(long))),
            "SINGLE" or "DOUBLE" or "CURRENCY" => (typeof(double), XpTypeSymbol.FromClr(typeof(double))),
            _ => (typeof(object), XpTypeSymbol.Variant)
        };
        if (!isArray) return result;
        var arrayType = result.Item1.MakeArrayType();
        return (arrayType, XpTypeSymbol.ArrayOf(XpTypeSymbol.FromClr(result.Item1)));
    }

    private BoundStatement? BindCallStatement(CallStatementSyntax syntax)
    {
        var expression = BindExpression(syntax.Expression);
        return expression is null ? null : new BoundExpressionStatement(expression);
    }

    private BoundStatement? BindRuntimeStatement(RuntimeFileStatementSyntax syntax)
    {
        if (!syntax.Command.Text.Equals("Print", StringComparison.OrdinalIgnoreCase) || syntax.Arguments.Count != 1)
            return BindUnsupported(syntax);
        var expression = BindExpression(syntax.Arguments[0]);
        return expression is null ? null : new BoundPrintStatement(expression);
    }

    private BoundStatement? BindIf(IfStatementSyntax syntax)
    {
        var condition = BindBooleanCondition(syntax.Condition, "If");
        var thenStatements = BindStatements(syntax.ThenStatements);
        var elseIfClauses = new List<BoundElseIfClause>(syntax.ElseIfClauses.Count);
        foreach (var clause in syntax.ElseIfClauses)
        {
            var elseIfCondition = BindBooleanCondition(clause.Condition, "ElseIf");
            var statements = BindStatements(clause.Statements);
            if (elseIfCondition is not null)
                elseIfClauses.Add(new BoundElseIfClause(elseIfCondition, statements) { Span = clause.Span });
        }
        var elseStatements = BindStatements(syntax.ElseStatements);
        return condition is null ? null : new BoundIfStatement(condition, thenStatements, elseIfClauses, elseStatements);
    }

    private BoundStatement? BindWhile(WhileStatementSyntax syntax)
    {
        var condition = BindBooleanCondition(syntax.Condition, "While");
        var statements = BindStatements(syntax.Statements);
        return condition is null ? null : new BoundWhileStatement(condition, statements);
    }

    private BoundStatement? BindDo(DoStatementSyntax syntax)
    {
        BoundExpression? condition = null;
        if (syntax.Condition is not null)
            condition = BindBooleanCondition(syntax.Condition, "Do/Loop");

        var statements = BindStatements(syntax.Statements);
        if (syntax.Condition is not null && condition is null)
            return null;

        return new BoundDoStatement(condition, syntax.ConditionKeyword?.Kind, syntax.IsPostTest, statements);
    }

    private BoundStatement? BindFor(ForStatementSyntax syntax)
    {
        var variableExpression = BindExpression(new NameExpressionSyntax(syntax.IdentifierToken));
        var from = BindExpression(syntax.FromExpression);
        var to = BindExpression(syntax.ToExpression);
        var step = syntax.StepExpression is null ? null : BindExpression(syntax.StepExpression);

        if (variableExpression is not BoundNameExpression variable || from is null || to is null ||
            (syntax.StepExpression is not null && step is null))
            return null;

        if (!IsWritable(variable) || !IsNumeric(variable.Type))
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                "For loop variable must be a writable numeric variable.", syntax.IdentifierToken.Span));

        ValidateSameNumericType(variable, from, syntax.FromExpression.Span, "For start value");
        ValidateSameNumericType(variable, to, syntax.ToExpression.Span, "For end value");
        if (step is not null)
            ValidateSameNumericType(variable, step, syntax.StepExpression!.Span, "For step value");

        var statements = BindStatements(syntax.Statements);
        return new BoundForStatement(variable, from, to, step, statements);
    }

    private BoundStatement? BindForAll(ForAllStatementSyntax syntax)
    {
        var variableExpression = BindExpression(new NameExpressionSyntax(syntax.IdentifierToken));
        var collection = BindExpression(syntax.CollectionExpression);
        if (variableExpression is not BoundNameExpression variable || collection is null)
            return null;

        if (!IsWritable(variable))
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                "ForAll loop variable must be writable.", syntax.IdentifierToken.Span));

        if (!collection.Type.IsArray && !(_allowDynamicMembers && (collection.SemanticType.IsVariant || collection.Type == typeof(object))))
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                $"ForAll requires an array collection, not {collection.SemanticType.Name}.", syntax.CollectionExpression.Span));
        }
        else
        {
            var elementType = collection.SemanticType.ElementType ?? XpTypeSymbol.FromClr(collection.Type.GetElementType()!);
            if (variable.Type != elementType.RuntimeType ||
                !string.Equals(variable.SemanticType.Name, elementType.Name, StringComparison.OrdinalIgnoreCase))
                _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                    $"ForAll variable {variable.SemanticType.Name} is incompatible with collection element type {elementType.Name}.",
                    syntax.IdentifierToken.Span));
        }

        var statements = BindStatements(syntax.Statements);
        return new BoundForAllStatement(variable, collection, statements);
    }

    private BoundStatement? BindSelect(SelectStatementSyntax syntax)
    {
        var selector = BindExpression(syntax.Expression);
        if (selector is null)
            return null;

        var cases = new List<BoundCaseClause>(syntax.Cases.Count);
        foreach (var clause in syntax.Cases)
        {
            var lower = clause.LowerExpression is null ? null : BindExpression(clause.LowerExpression);
            var upper = clause.UpperExpression is null ? null : BindExpression(clause.UpperExpression);

            if (lower is not null)
                ValidateComparable(selector, lower, clause.LowerExpression!.Span, "Case value");
            if (upper is not null)
                ValidateComparable(selector, upper, clause.UpperExpression!.Span, "Case range value");

            cases.Add(new BoundCaseClause(
                clause.CaseKind,
                clause.OperatorToken?.Kind,
                lower,
                upper,
                BindStatements(clause.Statements)) { Span = clause.Span });
        }

        return new BoundSelectStatement(selector, cases);
    }

    private BoundExpression? BindBooleanCondition(ExpressionSyntax syntax, string construct)
    {
        var condition = BindExpression(syntax);
        if (condition is null)
            return null;
        if (condition.Type != typeof(bool) && !_allowDynamicMembers)
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                $"{construct} condition must be Boolean, not {condition.SemanticType.Name}.", syntax.Span));
            return null;
        }
        return condition;
    }

    private void ValidateSameNumericType(BoundExpression variable, BoundExpression value, TextSpan span, string description)
    {
        if (_allowDynamicMembers && value.Type == typeof(object))
            return;
        if (!IsNumeric(value.Type) || variable.Type != value.Type ||
            !string.Equals(variable.SemanticType.Name, value.SemanticType.Name, StringComparison.OrdinalIgnoreCase))
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                $"{description} must match loop variable type {variable.SemanticType.Name}.", span));
    }

    private void ValidateComparable(BoundExpression selector, BoundExpression value, TextSpan span, string description)
    {
        if (selector.Type != value.Type ||
            !string.Equals(selector.SemanticType.Name, value.SemanticType.Name, StringComparison.OrdinalIgnoreCase))
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                $"{description} type {value.SemanticType.Name} does not match Select expression type {selector.SemanticType.Name}.", span));
    }

    private BoundStatement BindReturn(ReturnStatementSyntax syntax)
    {
        if (!_allowsReturnValue)
        {
            BoundExpression? expression = null;
            if (syntax.Expression is not null)
            {
                expression = BindExpression(syntax.Expression);
                _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch, "A Sub cannot return a value.", syntax.Expression.Span));
            }
            return new BoundReturnStatement(expression);
        }

        if (syntax.Expression is null || _returnType is null)
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch, "A Function return requires a value.", syntax.Span));
            return new BoundReturnStatement(null);
        }

        var value = BindExpression(syntax.Expression);
        if (value is null)
            return new BoundReturnStatement(null);

        var conversion = Conversion.Classify(value.SemanticType, _returnType);
        if (!conversion.IsImplicit)
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                $"Cannot return {value.SemanticType.Name} from a Function returning {_returnType.Name}.", syntax.Expression.Span));
        else if (!conversion.IsIdentity)
            value = new BoundConversionExpression(value, _returnType, conversion) { Span = value.Span };

        return new BoundReturnStatement(value);
    }

    private BoundStatement? BindExit(ExitStatementSyntax syntax)
    {
        if (syntax.TargetKeyword.Kind is not (SyntaxKind.SubKeyword or SyntaxKind.FunctionKeyword))
            return BindUnsupported(syntax);
        return new BoundReturnStatement(null);
    }

    private BoundStatement? BindError(ErrorStatementSyntax syntax)
    {
        var number = BindExpression(syntax.NumberExpression);
        var description = syntax.DescriptionExpression is null ? null : BindExpression(syntax.DescriptionExpression);
        return number is null || (syntax.DescriptionExpression is not null && description is null)
            ? null : new BoundErrorStatement(number, description);
    }

    private BoundStatement? BindAssignment(ExpressionSyntax targetSyntax, ExpressionSyntax valueSyntax, bool isSet = false)
    {
        if (_allowDynamicMembers && targetSyntax is CallExpressionSyntax dynamicIndex
            && dynamicIndex.Target is MemberAccessExpressionSyntax dynamicMember
            && dynamicIndex.Arguments.Count == 1)
        {
            var receiver = BindExpression(dynamicMember.Expression);
            var index = BindExpression(dynamicIndex.Arguments[0]);
            if (receiver is not null && index is not null && receiver.Type == typeof(object))
                return BindBoundAssignment(new BoundIndexExpression(new BoundMemberAccessExpression(receiver, dynamicMember.NameToken.Text, typeof(object), XpTypeSymbol.Variant), index, typeof(object), XpTypeSymbol.Variant), valueSyntax, isSet, targetSyntax);
        }
        var target = BindExpression(targetSyntax);
        if (target is null)
            return null;

        if (target is BoundCallExpression indexedCall && indexedCall.Target is null && indexedCall.Function.Name is var indexedName && indexedCall.Arguments.Count == 1 &&
            _symbols.TryLookup(indexedName, out var indexedSymbol) && indexedSymbol is LocalSymbol local && local.Type.IsArray)
        {
            target = new BoundIndexExpression(new BoundNameExpression(local), indexedCall.Arguments[0], local.Type.GetElementType()!, XpTypeSymbol.FromClr(local.Type.GetElementType()!));
        }

        return BindBoundAssignment(target, valueSyntax, isSet, targetSyntax);
    }

    private BoundStatement? BindBoundAssignment(BoundExpression target, ExpressionSyntax valueSyntax, bool isSet, ExpressionSyntax targetSyntax)
    {
        if (!IsWritable(target))
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                CompilerDiagnosticCodes.TypeMismatch,
                $"Assignment target is not writable ({target.Kind}).",
                targetSyntax.Span));
            return null;
        }

        var value = BindExpression(valueSyntax);
        if (value is null)
            return null;

        if (isSet && !IsReferenceType(target))
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                CompilerDiagnosticCodes.TypeMismatch,
                $"Set requires an object/reference target, not {target.SemanticType.Name}.",
                targetSyntax.Span));
            return new BoundAssignmentStatement(target, value, true);
        }

        var conversion = Conversion.Classify(value.SemanticType, target.SemanticType);
        if (!conversion.IsImplicit && _allowDynamicMembers)
            value = new BoundConversionExpression(value, target.SemanticType, new Conversion(ConversionKind.FromVariant)) { Span = value.Span };
        else if (!conversion.IsImplicit)
            _diagnostics.Add(new SyntaxDiagnostic(
                CompilerDiagnosticCodes.TypeMismatch,
                $"Cannot assign {value.SemanticType.Name} to {target.SemanticType.Name}.",
                valueSyntax.Span));
        else if (!conversion.IsIdentity)
            value = new BoundConversionExpression(value, target.SemanticType, conversion) { Span = value.Span };

        return new BoundAssignmentStatement(target, value, isSet);
    }

    private static bool IsNumeric(Type type) => type == typeof(long) || type == typeof(double);

    private static bool IsWritable(BoundExpression target) =>
        target is BoundNameExpression name &&
        name.Symbol is VariableSymbol or LocalSymbol or ParameterSymbol or FieldSymbol
        || target is BoundMemberAccessExpression
        || target is BoundIndexExpression;

    private static bool IsReferenceType(BoundExpression expression) =>
        expression.Type == typeof(object) || !expression.Type.IsValueType && expression.Type != typeof(string);

}
