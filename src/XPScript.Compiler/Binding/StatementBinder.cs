using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Binding;

public sealed class StatementBinder(SymbolTable? symbols = null, XpTypeSymbol? returnType = null, bool allowsReturnValue = false,
    bool allowDynamicMembers = false, string? functionName = null, LocalSymbol? functionResult = null,
    string? procedureIdentity = null, int forAllDepth = 0)
{
    private readonly SymbolTable _symbols = symbols ?? new SymbolTable();
    private readonly XpTypeSymbol? _returnType = returnType;
    private readonly bool _allowsReturnValue = allowsReturnValue;
    private readonly bool _allowDynamicMembers = allowDynamicMembers;
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    private readonly int _forAllDepth = forAllDepth;
    private BoundExpression? _withReceiver;
    public IReadOnlyList<SyntaxDiagnostic> Diagnostics => _diagnostics;

    public BoundStatement? Bind(StatementSyntax syntax)
    {
        BoundStatement? bound = syntax switch
        {
            ConstStatementSyntax => new BoundNoOpStatement(),
            AssignmentStatementSyntax assignment => BindAssignment(assignment.Target, assignment.Expression),
            DimStatementSyntax dim => BindDim(dim),
            SetStatementSyntax set => BindAssignment(set.Target, set.Expression, isSet: true),
            ExpressionStatementSyntax expression => BindExpressionStatement(expression),
            CallStatementSyntax call => BindCallStatement(call),
            RuntimeFileStatementSyntax runtime => BindRuntimeStatement(runtime),
            ReturnStatementSyntax @return => BindReturn(@return),
            LabelStatementSyntax label => new BoundLabelStatement(label.Identifier.Text),
            GoToStatementSyntax transfer => BindTransfer(transfer),
            ExitStatementSyntax exit => BindExit(exit),
            ErrorStatementSyntax error => BindError(error),
            OnErrorStatementSyntax onError => BindOnError(onError),
            ResumeStatementSyntax resume => BindResume(resume),
            IfStatementSyntax @if => BindIf(@if),
            ForStatementSyntax @for => BindFor(@for),
            ForAllStatementSyntax forAll => BindForAll(forAll),
            WhileStatementSyntax whileStatement => BindWhile(whileStatement),
            DoStatementSyntax @do => BindDo(@do),
            SelectStatementSyntax selectStatement => BindSelect(selectStatement),
            WithStatementSyntax withStatement => BindWith(withStatement),
            _ => BindUnsupported(syntax)
        };
        if (bound is not null)
            bound.Span = syntax.Span;
        return bound;
    }

    private BoundStatement? BindTransfer(GoToStatementSyntax syntax)
    {
        if (!syntax.IsGoSub)
            return new BoundGoToStatement(syntax.Target.Text);
        _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.InvalidSyntax,
            "GoSub is not implemented in the AST compiler. Use a Sub or Function instead.", syntax.Span));
        return null;
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

    private BoundExpression? BindExpression(ExpressionSyntax syntax, bool assignmentTarget = false)
    {
        var binder = new ExpressionBinder(_symbols, _allowDynamicMembers, functionName, functionResult, implicitReceiver: _withReceiver,
            assignmentTarget: assignmentTarget ? syntax : null);
        var expression = binder.Bind(syntax);
        _diagnostics.AddRange(binder.Diagnostics);
        return binder.Diagnostics.Count == 0 ? expression : null;
    }

    private BoundStatement? BindWith(WithStatementSyntax syntax)
    {
        var receiver = BindExpression(syntax.Receiver);
        if (receiver is null) return null;
        var previous = _withReceiver;
        _withReceiver = receiver;
        try
        {
            return new BoundWithStatement(receiver, BindStatements(syntax.Statements));
        }
        finally { _withReceiver = previous; }
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
        if (_allowDynamicMembers && expression is BoundNameExpression or BoundLiteralExpression or BoundMemberAccessExpression or BoundIndexedPropertyExpression)
            return new BoundNoOpStatement();
        return expression is null ? null : LowerCallStatement(expression);
    }

    private BoundStatement LowerCallStatement(BoundExpression expression)
    {
        if (expression is BoundCallExpression { Function.Name: "XPCsvDocument.FromBytes", Target: BoundMemberAccessExpression csvCall } fromBytes)
        {
            // Preserve the legacy assignment and evaluation order: evaluate
            // bytes/override first, then read the current document settings.
            var receiver = csvCall.Receiver;
            var encoding = fromBytes.Arguments.Count == 2 ? fromBytes.Arguments[1]
                : new BoundMemberAccessExpression(receiver, "Encoding", typeof(string));
            BoundExpression[] arguments = [fromBytes.Arguments[0], encoding,
                new BoundMemberAccessExpression(receiver, "Delimiter", typeof(string)),
                new BoundMemberAccessExpression(receiver, "HasHeaders", typeof(bool))];
            var parser = _symbols.LookupAll("XPCsvDocument.ParseBytes").OfType<FunctionSymbol>().Single(f => f.ParameterTypes.Count == 4);
            var type = new BoundNameExpression(new TypeSymbol("XPCsvDocument", typeof(object), XpTypeSymbol.User("XPCsvDocument")));
            var target = new BoundMemberAccessExpression(type, "ParseBytes", typeof(object), XpTypeSymbol.User("XPCsvDocument"));
            return new BoundAssignmentStatement(receiver, new BoundCallExpression(target, parser, arguments), true);
        }
        return new BoundExpressionStatement(expression);
    }

    private BoundStatement? BindDim(DimStatementSyntax syntax)
    {
        var (type, semanticType) = ResolveDimType(syntax.TypeNameToken?.Text, syntax.IsArray, syntax.ArrayLength, syntax.IsList, syntax.ArrayRank);
        var userConstructor = !syntax.IsArray && !syntax.IsList
            ? AstClassConstructorSupport.Find(_symbols, syntax.TypeNameToken?.Text ?? "") : null;
        if (userConstructor is not null) semanticType = userConstructor.SemanticReturnType!;
        var local = new LocalSymbol(syntax.IdentifierToken.Text, type, semanticType)
        {
            ArrayLowerBound = syntax.ArrayLowerBound,
            ArrayLength = syntax.ArrayLength,
            ArrayBounds = syntax.ArrayBounds.Select(bound => (bound.Lower, bound.Upper)).ToArray(),
            ArrayStorage = syntax.IsArray && syntax.ArrayLength is null ? AstArrayStorageKind.ClrDynamic : AstArrayStorageKind.ClrFixed,
            ArrayRank = syntax.ArrayRank,
            IsDynamicArray = syntax.IsArray && syntax.ArrayLength is null
        };
        if (syntax.IsStatic)
        {
            if (procedureIdentity is null)
            {
                _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.InvalidSyntax,
                    "AST Static requires a declaration in a named procedure.", syntax.Span));
                return null;
            }
            // Include the complete procedure signature so overloads cannot share
            // storage. Hashing keeps generated names valid and deterministic.
            var identity = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(procedureIdentity)));
            local = local with { StaticStorageName = $"__xpsStatic_{identity}_{local.Name}" };
        }
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
            var conversion = _symbols.ClassifyConversion(initializer.SemanticType, semanticType);
            if (!conversion.IsImplicit)
            {
                _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                    $"Cannot assign {initializer.SemanticType.Name} to {semanticType.Name}.", syntax.Initializer.Span));
                return null;
            }
            if (!conversion.IsIdentity)
                initializer = new BoundConversionExpression(initializer, semanticType, conversion) { Span = initializer.Span };
        }
        // A declared class reference without New starts as Nothing, not an
        // implicit parameterless construction (which may not even exist).
        if (initializer is null && userConstructor is not null)
            initializer = new BoundLiteralExpression(null, typeof(object), semanticType);
        return new BoundVariableDeclarationStatement(local, initializer, syntax.IsArray && syntax.ArrayLength is null, syntax.ArrayLength);
    }

    private (Type RuntimeType, XpTypeSymbol SemanticType) ResolveDimType(string? name, bool isArray = false, int? arrayLength = null, bool isList = false, int arrayRank = 1)
    {
        // XPJSONElement is a deliberately case-sensitive compatibility spelling.
        // The language is case-insensitive, so resolve this exact Notes JSON name
        // before the native XPJsonElement TypeSymbol can win the lookup.
        var compatibilityType = name is "XPJSONObject" or "XPJSONArray" or "XPJSONElement"
            ? XpTypeSymbol.User(name)
            : null;
        var result = name?.Trim().ToUpperInvariant() switch
        {
            "BOOLEAN" => (typeof(bool), XpTypeSymbol.FromClr(typeof(bool))),
            "BYTE" => (typeof(byte), XpTypeSymbol.FromClr(typeof(byte))),
            "STRING" => (typeof(string), XpTypeSymbol.FromClr(typeof(string))),
            "INTEGER" or "LONG" => (typeof(long), XpTypeSymbol.FromClr(typeof(long))),
            "SINGLE" or "DOUBLE" or "CURRENCY" => (typeof(double), XpTypeSymbol.FromClr(typeof(double))),
            _ => (typeof(object), compatibilityType ?? (string.IsNullOrWhiteSpace(name) ? XpTypeSymbol.Variant :
                AstClassConstructorSupport.Find(_symbols, name.Trim())?.SemanticReturnType ??
                _symbols.LookupAll(name.Trim()).OfType<TypeSymbol>().FirstOrDefault()?.SemanticType ?? XpTypeSymbol.User(name.Trim())))
        };
        if (isList) return (typeof(object), XpTypeSymbol.ListOf(result.Item2));
        if (!isArray) return result;
        // A dynamic array can change rank at ReDim time. Keep its array
        // semantic type for binding, but use System.Array for generated C# so
        // a later rank-two/rank-three ReDim is assignable to the same slot.
        if (arrayLength is null && arrayRank > 1)
            return (typeof(Array), XpTypeSymbol.ArrayOf(result.Item2, arrayRank));
        var arrayType = arrayRank == 1 ? result.Item1.MakeArrayType() : result.Item1.MakeArrayType(arrayRank);
        return (arrayType, XpTypeSymbol.ArrayOf(result.Item2, arrayRank));
    }

    private BoundStatement? BindCallStatement(CallStatementSyntax syntax)
    {
        var binder = new ExpressionBinder(_symbols, _allowDynamicMembers, functionName, functionResult,
            listByRefStatementCall: syntax.Expression, implicitReceiver: _withReceiver);
        var expression = binder.Bind(syntax.Expression);
        _diagnostics.AddRange(binder.Diagnostics);
        return binder.Diagnostics.Count == 0 ? LowerCallStatement(expression) : null;
    }

    private BoundStatement? BindRuntimeStatement(RuntimeFileStatementSyntax syntax)
    {
        if (!syntax.Command.Text.Equals("Print", StringComparison.OrdinalIgnoreCase))
        {
            var argumentResults = syntax.Arguments.Select(argument => BindExpression(argument)).ToArray();
            if (argumentResults.Any(argument => argument is null))
                return null;
            var arguments = argumentResults.Select(argument => argument!).ToArray();
            if (syntax.Command.Text.Equals("Erase", StringComparison.OrdinalIgnoreCase) && arguments.Length == 1)
            {
                BoundExpression? list = null;
                BoundExpression[] callArguments = [];
                var method = "Clear";
                if (arguments[0] is BoundIndexExpression index && index.Expression.SemanticType.IsList)
                {
                    // Erasing a tag must not read its value: a missing tag is
                    // harmless, and removing an active alias preserves its Tag.
                    list = index.Expression;
                    callArguments = [index.Index];
                    method = "Erase";
                }
                else if (arguments[0].SemanticType.IsList) list = arguments[0];
                else if (arguments[0].SemanticType.IsArray)
                {
                    var dynamic = arguments[0] is BoundNameExpression { Symbol: LocalSymbol { IsDynamicArray: true } };
                    return new BoundExpressionStatement(new BoundCallExpression(null,
                        new FunctionSymbol(dynamic ? "EraseDynamicArray" : "EraseArray", typeof(void), [arguments[0].Type]), arguments));
                }
                if (list is not null)
                    return new BoundExpressionStatement(new BoundCallExpression(
                        new BoundMemberAccessExpression(list, method, typeof(void)),
                        new FunctionSymbol(method, typeof(void), callArguments.Select(argument => argument.Type).ToArray()), callArguments));
            }
            var function = new FunctionSymbol(syntax.Command.Text, typeof(object), arguments.Select(argument => argument.Type).ToArray(), XpTypeSymbol.Variant, arguments.Select(_ => XpTypeSymbol.Variant).ToArray());
            return new BoundExpressionStatement(new BoundCallExpression(null, function, arguments));
        }
        if (syntax.Arguments.Count != 1)
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
        var collection = BindExpression(syntax.CollectionExpression);
        if (collection is null) return null;
        if (AstCsvSupport.IsCsv(collection.SemanticType.Name) || AstXmlSupport.IsXml(collection.SemanticType.Name))
        {
            var family = AstXmlSupport.IsXml(collection.SemanticType.Name) ? "XML" : "CSV";
            if ((AstCsvSupport.IterationType(collection.SemanticType.Name) ?? AstXmlSupport.IterationType(collection.SemanticType.Name)) is not { } csvElement)
            {
                _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                    $"{family} ForAll requires a supported collection.", syntax.CollectionExpression.Span));
                return null;
            }
            if (_symbols.TryLookup(syntax.IdentifierToken.Text, out var existing))
            {
                var previous = new BoundNameExpression(existing);
                if (!previous.SemanticType.IsVariant && !_symbols.ClassifyConversion(csvElement, previous.SemanticType).IsImplicit)
                {
                    _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                        $"ForAll variable {previous.SemanticType.Name} is incompatible with {family} element type {csvElement.Name}.", syntax.IdentifierToken.Span));
                    return null;
                }
            }
            var scope = _symbols.CreateChildScope();
            var alias = new VariableSymbol(syntax.IdentifierToken.Text, csvElement.RuntimeType, csvElement)
            {
                IsForAllAlias = true
            };
            scope.Declare(alias);
            var binder = new StatementBinder(scope, _returnType, _allowsReturnValue, _allowDynamicMembers,
                functionName, functionResult, procedureIdentity, _forAllDepth + 1);
            var body = syntax.Statements.Select(binder.Bind).OfType<BoundStatement>().ToArray();
            _diagnostics.AddRange(binder.Diagnostics);
            return new BoundForAllStatement(new BoundNameExpression(alias), collection, body);
        }
        if (collection.SemanticType is { IsList: true, ElementType: { } element })
        {
            // A List alias is a typed view of its entry, not a copied Variant.
            // Bind it in a child scope so nested aliases restore outer names.
            var scope = _symbols.CreateChildScope();
            var alias = new LocalSymbol(syntax.IdentifierToken.Text, element.RuntimeType, element) { IsListAlias = true };
            scope.Declare(alias);
            var binder = new StatementBinder(scope, _returnType, _allowsReturnValue, _allowDynamicMembers,
                functionName, functionResult, procedureIdentity, _forAllDepth + 1);
            var body = syntax.Statements.Select(binder.Bind).OfType<BoundStatement>().ToArray();
            _diagnostics.AddRange(binder.Diagnostics);
            return new BoundForAllStatement(new BoundNameExpression(alias), collection, body);
        }
        if (!collection.SemanticType.IsList && !collection.Type.IsArray && collection.Type != typeof(string) && !(_allowDynamicMembers && (collection.SemanticType.IsVariant || collection.Type == typeof(object))))
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                $"ForAll requires an array collection, not {collection.SemanticType.Name}.", syntax.CollectionExpression.Span));
            return null;
        }

        var bodyScope = _symbols.CreateChildScope();
        BoundNameExpression variable;
        if (_symbols.TryLookup(syntax.IdentifierToken.Text, out var loopSymbol))
        {
            variable = new BoundNameExpression(loopSymbol);
            if (!IsWritable(variable))
                _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                    "ForAll loop variable must be writable.", syntax.IdentifierToken.Span));
        }
        else
        {
            var aliasType = collection.Type.IsArray
                ? collection.SemanticType.ElementType ?? XpTypeSymbol.FromClr(collection.Type.GetElementType()!)
                : collection.Type == typeof(string)
                    ? XpTypeSymbol.FromClr(typeof(string))
                    : XpTypeSymbol.Variant;
            var alias = new VariableSymbol(syntax.IdentifierToken.Text, aliasType.RuntimeType, aliasType) { IsForAllAlias = true };
            bodyScope.Declare(alias);
            variable = new BoundNameExpression(alias);
        }

        if (collection.Type.IsArray)
        {
            var elementType = collection.SemanticType.ElementType ?? XpTypeSymbol.FromClr(collection.Type.GetElementType()!);
            if (!variable.SemanticType.IsVariant && (variable.Type != elementType.RuntimeType ||
                !string.Equals(variable.SemanticType.Name, elementType.Name, StringComparison.OrdinalIgnoreCase))
                )
                _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                    $"ForAll variable {variable.SemanticType.Name} is incompatible with collection element type {elementType.Name}.",
                    syntax.IdentifierToken.Span));
        }

        var bodyBinder = new StatementBinder(bodyScope, _returnType, _allowsReturnValue, _allowDynamicMembers,
            functionName, functionResult, procedureIdentity, _forAllDepth + 1);
        var statements = syntax.Statements.Select(bodyBinder.Bind).OfType<BoundStatement>().ToArray();
        _diagnostics.AddRange(bodyBinder.Diagnostics);
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

        var conversion = _symbols.ClassifyConversion(value.SemanticType, _returnType);
        if (!conversion.IsImplicit)
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                $"Cannot return {value.SemanticType.Name} from a Function returning {_returnType.Name}.", syntax.Expression.Span));
        else if (!conversion.IsIdentity)
            value = new BoundConversionExpression(value, _returnType, conversion) { Span = value.Span };

        return new BoundReturnStatement(value);
    }

    private BoundStatement? BindExit(ExitStatementSyntax syntax)
    {
        if (syntax.TargetKeyword.Kind == SyntaxKind.ForAllKeyword)
        {
            if (_forAllDepth == 0)
            {
                _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.InvalidSyntax,
                    "Exit ForAll is valid only inside ForAll.", syntax.Span));
                return null;
            }
            return new BoundExitForAllStatement();
        }
        if (syntax.TargetKeyword.Kind is not (SyntaxKind.SubKeyword or SyntaxKind.FunctionKeyword))
            return BindUnsupported(syntax);
        // Exit Function returns the current slot, including its default value.
        return new BoundReturnStatement(syntax.TargetKeyword.Kind == SyntaxKind.FunctionKeyword && functionResult is not null
            ? new BoundNameExpression(functionResult) : null);
    }

    private BoundStatement? BindError(ErrorStatementSyntax syntax)
    {
        var number = BindExpression(syntax.NumberExpression);
        var description = syntax.DescriptionExpression is null ? null : BindExpression(syntax.DescriptionExpression);
        return number is null || (syntax.DescriptionExpression is not null && description is null)
            ? null : new BoundErrorStatement(number, description);
    }

    private BoundStatement BindOnError(OnErrorStatementSyntax syntax)
    {
        if (syntax.ActionKeyword.Kind == SyntaxKind.ResumeKeyword)
            return new BoundOnErrorStatement(BoundErrorHandlerMode.ResumeNext, null);
        if (syntax.TargetToken is { Text: "0" })
            return new BoundOnErrorStatement(BoundErrorHandlerMode.Disable, null);
        return new BoundOnErrorStatement(BoundErrorHandlerMode.GoToLabel, syntax.TargetToken?.Text);
    }

    private BoundStatement BindResume(ResumeStatementSyntax syntax) => syntax.TargetToken?.Kind switch
    {
        null => new BoundResumeStatement(BoundResumeMode.Current, null),
        { } kind when kind == SyntaxKind.NextKeyword => new BoundResumeStatement(BoundResumeMode.Next, null),
        _ => new BoundResumeStatement(BoundResumeMode.Label, syntax.TargetToken.Text)
    };

    private BoundStatement? BindAssignment(ExpressionSyntax targetSyntax, ExpressionSyntax valueSyntax, bool isSet = false)
    {
        if (_allowDynamicMembers && targetSyntax is CallExpressionSyntax dynamicIndex
            && dynamicIndex.Target is MemberAccessExpressionSyntax dynamicMember
            && dynamicIndex.Arguments.Count == 1)
        {
            var receiver = BindExpression(dynamicMember.Expression);
            var index = BindExpression(dynamicIndex.Arguments[0]);
            if (receiver is not null && index is not null && receiver.SemanticType.IsVariant)
                return BindBoundAssignment(new BoundIndexExpression(new BoundMemberAccessExpression(receiver, dynamicMember.NameToken.Text, typeof(object), XpTypeSymbol.Variant), index, typeof(object), XpTypeSymbol.Variant), valueSyntax, isSet, targetSyntax);
        }
        var target = BindExpression(targetSyntax, assignmentTarget: true);
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
        var propertyMode = target switch
        {
            BoundMemberAccessExpression member => member.AssignmentKind,
            BoundIndexedPropertyExpression indexed => indexed.Property.AssignmentKind,
            _ => PropertyAssignmentKind.Unspecified
        };
        if (propertyMode != PropertyAssignmentKind.Unspecified && (propertyMode == PropertyAssignmentKind.Set) != isSet)
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                propertyMode == PropertyAssignmentKind.Set ? "Property Set assignment requires Set." : "Property Let assignment cannot use Set.", targetSyntax.Span));
            return null;
        }
        if (target is BoundIndexExpression { Expression: BoundMemberAccessExpression argsProperty } && argsProperty.Receiver.SemanticType.Name == "Application" && argsProperty.Name.Equals("Args", StringComparison.OrdinalIgnoreCase))
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch, "Application is read-only runtime state.", targetSyntax.Span));
            return null;
        }
        if (target is BoundCallExpression { Target: BoundMemberAccessExpression argsMember } && argsMember.Receiver.SemanticType.Name == "Application" && argsMember.Name.Equals("Args", StringComparison.OrdinalIgnoreCase))
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch, "Application is read-only runtime state.", targetSyntax.Span));
            return null;
        }
        if (target is BoundMemberAccessExpression appMember && AstApplicationSupport.IsReadOnly(appMember.Receiver.SemanticType.Name, appMember.Name))
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch, "Application is read-only runtime state.", targetSyntax.Span));
            return null;
        }
        if (target is BoundMemberAccessExpression sheetMember && AstSpreadsheetSupport.IsReadOnly(sheetMember.Receiver.SemanticType.Name, sheetMember.Name))
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch, "Spreadsheet property is read-only.", targetSyntax.Span));
            return null;
        }
        if (target is BoundMemberAccessExpression httpMember && AstHttpSupport.IsReadOnly(httpMember.Receiver.SemanticType.Name, httpMember.Name))
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch, "HTTP response or property is read-only.", targetSyntax.Span));
            return null;
        }
        if (target is BoundMemberAccessExpression xmlMember && AstXmlSupport.IsReadOnly(xmlMember.Receiver.SemanticType.Name, xmlMember.Name) ||
            target is BoundIndexExpression xmlIndex && AstXmlSupport.IterationType(xmlIndex.Expression.SemanticType.Name) is not null)
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch, "XML collection or property is read-only.", targetSyntax.Span));
            return null;
        }
        if (target is BoundMemberAccessExpression csvMember && AstCsvSupport.IsReadOnly(csvMember.Receiver.SemanticType.Name, csvMember.Name) ||
            target is BoundIndexExpression csvIndex && AstCsvSupport.IndexType(csvIndex.Expression.SemanticType.Name) is not null &&
                !csvIndex.Expression.SemanticType.Name.Equals("XPCsvRow", StringComparison.OrdinalIgnoreCase))
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch, "CSV collection or property is read-only.", targetSyntax.Span));
            return null;
        }
        if (target is BoundMemberAccessExpression imageMember && AstImageSupport.IsImage(imageMember.Receiver.SemanticType.Name))
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                $"XPImage property '{AstImageSupport.MemberName(imageMember.Name)}' is read-only.", targetSyntax.Span));
            return null;
        }
        if (!IsWritable(target) && target is not BoundIndexedPropertyExpression { Property.CanWrite: true, Property.SetterName: not null })
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

        if (propertyMode == PropertyAssignmentKind.Set && !IsReferenceType(value))
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                "Property Set requires an object/reference value.", valueSyntax.Span));
            return null;
        }

        if (isSet && !IsReferenceType(target))
        {
            _diagnostics.Add(new SyntaxDiagnostic(
                CompilerDiagnosticCodes.TypeMismatch,
                $"Set requires an object/reference target, not {target.SemanticType.Name}.",
                targetSyntax.Span));
            return new BoundAssignmentStatement(target, value, true);
        }

        var conversion = _symbols.ClassifyConversion(value.SemanticType, target.SemanticType);
        var sameListElementType = value.SemanticType is { IsList: true, ElementType: { } valueElement } &&
            target.SemanticType is { IsList: true, ElementType: { } targetElement } &&
            valueElement.RuntimeType == targetElement.RuntimeType &&
            valueElement.Name.Equals(targetElement.Name, StringComparison.OrdinalIgnoreCase);
        if ((value.SemanticType.IsList || target.SemanticType.IsList) && !conversion.IsImplicit && !sameListElementType)
        {
            _diagnostics.Add(new SyntaxDiagnostic(CompilerDiagnosticCodes.TypeMismatch,
                $"Cannot assign {value.SemanticType.Name} to {target.SemanticType.Name}.", valueSyntax.Span));
            return null;
        }
        if (sameListElementType)
            conversion = new Conversion(ConversionKind.Identity);
        else if (!conversion.IsImplicit && _allowDynamicMembers && propertyMode != PropertyAssignmentKind.Set)
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
