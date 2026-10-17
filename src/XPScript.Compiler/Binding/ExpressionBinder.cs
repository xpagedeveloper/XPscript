using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Binding;

public sealed class ExpressionBinder(SymbolTable? symbols = null, bool allowDynamicMembers = false,
    string? functionName = null, LocalSymbol? functionResult = null, ExpressionSyntax? listByRefStatementCall = null,
    BoundExpression? implicitReceiver = null, ExpressionSyntax? assignmentTarget = null)
{
    private readonly SymbolTable _symbols = symbols ?? new SymbolTable();
    private readonly bool _allowDynamicMembers = allowDynamicMembers;
    private readonly List<SyntaxDiagnostic> _diagnostics = [];
    public IReadOnlyList<SyntaxDiagnostic> Diagnostics => _diagnostics;

    public BoundExpression Bind(ExpressionSyntax syntax)
    {
        BoundExpression bound = syntax switch
        {
        LiteralExpressionSyntax literal => BindLiteral(literal),
        NameExpressionSyntax name => BindName(name),
        CallExpressionSyntax call => BindCall(call),
        ArrayExpressionSyntax array => BindArray(array),
        MemberAccessExpressionSyntax member => BindMemberAccess(member),
        IndexExpressionSyntax index => BindIndex(index),
        NewExpressionSyntax @new => BindNew(@new),
        UnaryExpressionSyntax unary => BindUnary(unary),
        BinaryExpressionSyntax binary => BindBinary(binary),
        ParenthesizedExpressionSyntax parenthesized => Bind(parenthesized.Expression),
        ImplicitMemberAccessExpressionSyntax implicitMember => BindImplicitMemberAccess(implicitMember),
        _ => Error(syntax, $"Binding is not implemented for {syntax.Kind}.")
        };
        bound.Span = syntax.Span;
        return bound;
    }

    private BoundExpression BindImplicitMemberAccess(ImplicitMemberAccessExpressionSyntax syntax)
    {
        if (implicitReceiver is null)
            return Error(syntax, CompilerDiagnosticCodes.UnknownMember, $"Implicit member '{syntax.NameToken.Text}' requires an active With scope.");
        if (_symbols.TryLookupMember(implicitReceiver.SemanticType, syntax.NameToken.Text, out var symbol))
        {
            if (symbol is PropertySymbol property)
                return BindProperty(syntax, implicitReceiver, property);
            if (symbol is FunctionSymbol function)
                return new BoundMemberAccessExpression(implicitReceiver, syntax.NameToken.Text, function.ReturnType, function.SemanticReturnType);
        }
        return _allowDynamicMembers && implicitReceiver.SemanticType.IsVariant
            ? new BoundMemberAccessExpression(implicitReceiver, syntax.NameToken.Text, typeof(object), XpTypeSymbol.Variant)
            : Error(syntax.NameToken, CompilerDiagnosticCodes.UnknownMember, $"Undefined member '{syntax.NameToken.Text}' on '{implicitReceiver.SemanticType.Name}'.");
    }
    private BoundExpression BindLiteral(LiteralExpressionSyntax syntax)
    {
        if (syntax.LiteralToken.Kind == SyntaxKind.TrueKeyword)
            return new BoundLiteralExpression(true, typeof(bool));
        if (syntax.LiteralToken.Kind == SyntaxKind.FalseKeyword)
            return new BoundLiteralExpression(false, typeof(bool));
        if (syntax.LiteralToken.Kind == SyntaxKind.EmptyKeyword)
            return new BoundLiteralExpression(null, typeof(object), XpTypeSymbol.Empty);
        if (syntax.LiteralToken.Kind == SyntaxKind.NullKeyword)
            return new BoundLiteralExpression(DBNull.Value, typeof(object), XpTypeSymbol.Null);
        if (syntax.LiteralToken.Kind == SyntaxKind.NothingKeyword)
            return new BoundLiteralExpression(null, typeof(object), XpTypeSymbol.Nothing);
        if (syntax.LiteralToken.Kind == SyntaxKind.NumberToken)
            return syntax.Value switch
            {
                long => new BoundLiteralExpression(syntax.Value, typeof(long)),
                double => new BoundLiteralExpression(syntax.Value, typeof(double)),
                _ => Error(syntax, "Unsupported numeric literal.")
            };
        if (syntax.LiteralToken.Kind == SyntaxKind.StringToken)
            return new BoundLiteralExpression(syntax.Value, typeof(string));
        return Error(syntax, "Unsupported literal.");
    }

    private BoundExpression BindName(NameExpressionSyntax syntax)
    {
        var name = syntax.IdentifierToken.Text;
        if (_symbols.TryLookup(name, out var nativeType) && nativeType is TypeSymbol && (AstSpreadsheetSupport.IsSpreadsheet(name) || AstHttpSupport.IsHttp(name) || AstImageSupport.IsImage(name) || AstCsvSupport.IsCsv(name) || AstXmlSupport.IsXml(name)))
            return new BoundNameExpression(nativeType);
        // A bare function name denotes its result slot; a call still resolves
        // the procedure signature, so recursion is unaffected.
        if (functionResult is not null && name.Equals(functionName, StringComparison.OrdinalIgnoreCase))
            return new BoundNameExpression(functionResult);
        if (_symbols.TryLookup(name, out var ownProperty) && ownProperty is PropertySymbol property &&
            _symbols.TryLookup("Me", out var me))
            return BindProperty(syntax, new BoundNameExpression(me), property);
        // Statement-specific parsers can recover an omitted optional argument
        // with a zero-width identifier. Dynamic AST compilation must preserve
        // the surrounding statement and let its runtime helper supply the
        // default, rather than reporting a second synthetic unknown-variable
        // error for the recovery token.
        if (_allowDynamicMembers && string.IsNullOrEmpty(name))
            return new BoundLiteralExpression(null, typeof(object));
        if (_symbols.TryLookup(name, out var symbol) &&
            symbol is VariableSymbol or LocalSymbol or ParameterSymbol or FieldSymbol)
            return new BoundNameExpression(symbol);
        if (_symbols.LookupAll(name).OfType<FunctionSymbol>().FirstOrDefault(function => function.ParameterTypes.Count == 0) is { } zeroArgumentFunction)
            return new BoundCallExpression(null, zeroArgumentFunction, []);
        if (_allowDynamicMembers && name.Equals("Object", StringComparison.OrdinalIgnoreCase))
            return new BoundLiteralExpression(null, typeof(object));
        return Error(syntax.IdentifierToken, CompilerDiagnosticCodes.UnknownSymbol, $"Undefined variable '{name}'.");
    }

    private BoundExpression BindNew(NewExpressionSyntax syntax)
    {
        var typeName = syntax.TypeName.Text;
        if (AstApplicationSupport.OwnsSymbol(typeName))
            return Error(syntax.TypeName, CompilerDiagnosticCodes.InvalidNativeConstructor, "Application is runtime state and cannot be created with New.");
        if (AstSpreadsheetSupport.IsSpreadsheet(typeName))
        {
            if (!typeName.Equals("XPSpreadsheet", StringComparison.OrdinalIgnoreCase))
                return Error(syntax.TypeName, CompilerDiagnosticCodes.InvalidNativeConstructor, AstSpreadsheetSupport.ConstructorMessage);
            if (syntax.Arguments.Count > 1)
                return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, "No matching constructor for 'XPSpreadsheet'.");
            return new BoundNewExpression(typeof(object), syntax.Arguments.Select(Bind).ToArray(), XpTypeSymbol.User("XPSpreadsheet"));
        }
        if (AstHttpSupport.IsHttp(typeName))
        {
            if (!typeName.Equals("XPHttpClient", StringComparison.OrdinalIgnoreCase) && !typeName.Equals("XPHttpRequest", StringComparison.OrdinalIgnoreCase))
                return Error(syntax.TypeName, CompilerDiagnosticCodes.InvalidNativeConstructor, "Only XPHttpClient and XPHttpRequest can be created with New.");
            if (syntax.Arguments.Count != 0)
                return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, $"No matching constructor for '{typeName}'.");
            return new BoundNewExpression(typeof(object), [], XpTypeSymbol.User(typeName.Equals("XPHttpClient", StringComparison.OrdinalIgnoreCase) ? "XPHttpClient" : "XPHttpRequest"));
        }
        if (AstXmlSupport.IsXml(typeName))
        {
            if (!typeName.Equals("XPXmlDocument", StringComparison.OrdinalIgnoreCase) && !typeName.Equals("XPXmlElement", StringComparison.OrdinalIgnoreCase))
                return Error(syntax.TypeName, CompilerDiagnosticCodes.InvalidNativeConstructor, "Only XPXmlDocument and XPXmlElement can be created with New.");
            if (typeName.Equals("XPXmlElement", StringComparison.OrdinalIgnoreCase) && syntax.Arguments.Count == 0)
                return Error(syntax.TypeName, CompilerDiagnosticCodes.MissingConstructorArgument, "XPXmlElement requires an element name argument.");
            if (syntax.Arguments.Count > 1)
                return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, $"No matching constructor for '{typeName}'.");
            return new BoundNewExpression(typeof(object), syntax.Arguments.Select(Bind).ToArray(),
                XpTypeSymbol.User(typeName.Equals("XPXmlDocument", StringComparison.OrdinalIgnoreCase) ? "XPXmlDocument" : "XPXmlElement"));
        }
        if (AstCsvSupport.IsCsv(typeName))
        {
            if (!typeName.Equals("XPCsvDocument", StringComparison.OrdinalIgnoreCase))
                return Error(syntax.TypeName, CompilerDiagnosticCodes.InvalidNativeConstructor, "Only XPCsvDocument can be created with New.");
            if (syntax.Arguments.Count > 3)
                return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, "No matching constructor for 'XPCsvDocument'.");
            return new BoundNewExpression(typeof(object), syntax.Arguments.Select(Bind).ToArray(), XpTypeSymbol.User("XPCsvDocument"));
        }
        if (AstImageSupport.IsImage(typeName))
        {
            var imageArguments = syntax.Arguments.Select(Bind).ToArray();
            var imageConstructor = AstImageSupport.Constructors.FirstOrDefault(c => c.NativeTypes.Length == imageArguments.Length &&
                c.NativeTypes.Select(AstImageSupport.SemanticType).Zip(imageArguments,
                    (parameter, argument) => _symbols.ClassifyConversion(argument.SemanticType, parameter).IsImplicit).All(matches => matches));
            if (imageConstructor is null)
                return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, "No matching constructor for 'XPImage'.");
            var function = new FunctionSymbol("XPImage.New", typeof(object), imageConstructor.NativeTypes.Select(AstImageSupport.SemanticType).Select(t => t.RuntimeType).ToArray(),
                XpTypeSymbol.User("XPImage"), imageConstructor.NativeTypes.Select(AstImageSupport.SemanticType).ToArray());
            return new BoundNewExpression(typeof(object), ConvertArguments(function, imageArguments), XpTypeSymbol.User("XPImage"));
        }
        var userConstructors = AstClassConstructorSupport.FindAll(_symbols, typeName);
        if (userConstructors.Length > 0)
        {
            var values = syntax.Arguments.Select(Bind).ToArray();
            var arityMatches = userConstructors.Where(candidate =>
                values.Length >= (candidate.RequiredConstructorArgumentCount ?? candidate.ParameterTypes.Count) &&
                values.Length <= candidate.ParameterTypes.Count).ToArray();
            var compatible = arityMatches.Where(candidate =>
                values.Select((value, i) => candidate.ByRefParameters?[i] == true
                    ? _symbols.ClassifyConversion(value.SemanticType, candidate.SemanticParameterTypes![i]).IsIdentity &&
                        value.Type == candidate.ParameterTypes[i] && IsConstructorReference(value)
                    : _symbols.ClassifyConversion(value.SemanticType, candidate.SemanticParameterTypes![i]).IsImplicit).All(match => match)).ToArray();
            var candidates = compatible.Where(candidate => !candidate.IsPrivateConstructor).ToArray();
            if (candidates.Length == 0 && compatible.Any(candidate => candidate.IsPrivateConstructor))
                return Error(syntax, CompilerDiagnosticCodes.UnknownMember, $"Constructor for '{typeName}' is private.");
            // Preserve precise address/type diagnostics for a single signature.
            var userConstructor = candidates.Length == 1 ? candidates[0]
                : candidates.Length == 0 && arityMatches.Length == 1 ? arityMatches[0] : null;
            if (candidates.Length > 1)
            {
                var best = candidates.Where(candidate => candidates.All(other => ReferenceEquals(candidate, other) ||
                    BetterConstructor(candidate, other, values))).ToArray();
                if (best.Length != 1)
                    return Error(syntax, CompilerDiagnosticCodes.AmbiguousOverload, $"Ambiguous constructor for '{typeName}'.");
                userConstructor = best[0];
            }
            if (userConstructor is null)
                return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, $"No matching constructor for '{typeName}'.");
            if (userConstructor.IsPrivateConstructor)
                return Error(syntax, CompilerDiagnosticCodes.UnknownMember, $"Constructor for '{typeName}' is private.");
            if (values.Length < (userConstructor.RequiredConstructorArgumentCount ?? userConstructor.ParameterTypes.Count) ||
                values.Length > userConstructor.ParameterTypes.Count)
                return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, $"No matching constructor for '{typeName}'.");
            for (var i = 0; i < values.Length; i++)
            {
                var target = userConstructor.SemanticParameterTypes![i];
                if (userConstructor.ByRefParameters?[i] == true)
                {
                    if (ValidateScalarReference(syntax.Arguments[i], values[i], userConstructor.ParameterTypes[i], target,
                        $"constructor argument {i + 1} to '{typeName}'") is { } failure) return failure;
                }
                else if (!_symbols.ClassifyConversion(values[i].SemanticType, target).IsImplicit)
                    return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, $"No matching constructor for '{typeName}'.");
            }
            return new BoundNewExpression(typeof(object), ConvertArguments(userConstructor, values), userConstructor.SemanticReturnType)
                { Constructor = userConstructor };
        }
        if (_allowDynamicMembers && (!_symbols.TryLookup(typeName, out var knownSymbol) || knownSymbol is not TypeSymbol))
        {
            var dynamicArguments = syntax.Arguments.Select(Bind).ToArray();
            return new BoundNewExpression(typeof(object), dynamicArguments, XpTypeSymbol.User(typeName));
        }
        if (!_symbols.TryLookup(typeName, out var typeSymbol) || typeSymbol is not TypeSymbol typeEntry)
        {
            if (_allowDynamicMembers)
            {
                var dynamicArguments = syntax.Arguments.Select(Bind).ToArray();
                return new BoundNewExpression(typeof(object), dynamicArguments, XpTypeSymbol.User(typeName));
            }
            return new BoundNewExpression(typeof(object), [], XpTypeSymbol.User(typeName));
        }
        var type = typeEntry.Type;

        var arguments = syntax.Arguments.Select(Bind).ToArray();
        if (_allowDynamicMembers && type == typeof(object))
            return new BoundNewExpression(typeof(object), arguments, typeEntry.SemanticType ?? XpTypeSymbol.User(typeName));
        var constructors = type.GetConstructors();
        var constructor = constructors.FirstOrDefault(c =>
            c.GetParameters().Length == arguments.Length &&
            c.GetParameters().Select(p => p.ParameterType).SequenceEqual(arguments.Select(a => a.Type)));

        if (constructor is null)
            return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, $"No matching constructor for '{typeName}'.");

        return new BoundNewExpression(type, arguments, typeEntry.SemanticType ?? new XpTypeSymbol(typeEntry.Name, type));
    }

    private static bool IsConstructorReference(BoundExpression value) => value is
        BoundNameExpression { Symbol: LocalSymbol or ParameterSymbol or FieldSymbol } or
        BoundNameExpression { Symbol: VariableSymbol { HasTypedStorage: true } } or
        BoundMemberAccessExpression { IsFieldStorage: true } ||
        value is BoundIndexExpression index && (index.Expression.SemanticType.IsArray || index.Expression.SemanticType.IsList);

    private bool BetterConstructor(FunctionSymbol left, FunctionSymbol right, BoundExpression[] values)
    {
        var better = false;
        for (var i = 0; i < values.Length; i++)
        {
            var a = left.SemanticParameterTypes![i];
            var b = right.SemanticParameterTypes![i];
            var ca = _symbols.ClassifyConversion(values[i].SemanticType, a);
            var cb = _symbols.ClassifyConversion(values[i].SemanticType, b);
            if (ca.IsIdentity != cb.IsIdentity)
            {
                if (!ca.IsIdentity) return false;
                better = true;
            }
            else
            {
                var ab = _symbols.ClassifyConversion(a, b).IsImplicit;
                var ba = _symbols.ClassifyConversion(b, a).IsImplicit;
                if (ba && !ab) return false;
                if (ab && !ba) better = true;
            }
        }
        return better || left.ParameterTypes.Count < right.ParameterTypes.Count;
    }

    private BoundExpression BindIndex(IndexExpressionSyntax syntax)
    {
        var expression = Bind(syntax.Expression);
        var index = Bind(syntax.Index);
        if (AstXmlSupport.IterationType(expression.SemanticType.Name) is { } xmlElement)
            return new BoundIndexExpression(expression, index, xmlElement.RuntimeType, xmlElement);
        if (AstCsvSupport.IndexType(expression.SemanticType.Name) is { } csvElement)
            return new BoundIndexExpression(expression, index, csvElement.RuntimeType, csvElement);
        if (expression.SemanticType is { IsList: true, ElementType: { } element })
            return new BoundIndexExpression(expression, index, element.RuntimeType, element);
        if (_allowDynamicMembers && expression.SemanticType.IsVariant)
            return new BoundIndexExpression(expression, index, typeof(object), XpTypeSymbol.Variant);
        if (index.Type != typeof(long))
            return Error(syntax.Index, CompilerDiagnosticCodes.ArgumentTypeMismatch, "Array index must be an integer.");
        if (expression.SemanticType.IsArray && expression.SemanticType.ElementType is not null)
            return new BoundIndexExpression(
                expression,
                index,
                expression.SemanticType.ElementType.RuntimeType,
                expression.SemanticType.ElementType);
        if (_allowDynamicMembers && (expression.Type == typeof(object) || expression.SemanticType.IsVariant))
            return new BoundIndexExpression(expression, index, typeof(object), XpTypeSymbol.Variant);
        return Error(syntax, CompilerDiagnosticCodes.TypeMismatch, $"Indexing is not defined for {expression.SemanticType.Name}.");
    }

    private BoundExpression BindMemberAccess(MemberAccessExpressionSyntax syntax)
    {
        var receiver = Bind(syntax.Expression);
        if (_symbols.TryLookupMember(receiver.SemanticType, syntax.NameToken.Text, out var symbol))
        {
            if (symbol is PropertySymbol property)
            {
                if (AstSpreadsheetSupport.IsSpreadsheet(receiver.SemanticType.Name) && receiver is BoundNameExpression { Symbol: TypeSymbol })
                    return Error(syntax.NameToken, CompilerDiagnosticCodes.UnknownMember, "Spreadsheet property requires an instance.");
                if (AstHttpSupport.IsHttp(receiver.SemanticType.Name) && receiver is BoundNameExpression { Symbol: TypeSymbol })
                    return Error(syntax.NameToken, CompilerDiagnosticCodes.UnknownMember, "HTTP property requires an instance.");
                if (AstXmlSupport.IsXml(receiver.SemanticType.Name) && receiver is BoundNameExpression { Symbol: TypeSymbol })
                    return Error(syntax.NameToken, CompilerDiagnosticCodes.UnknownMember, $"XML property '{property.Name.Split('.').Last()}' requires an instance.");
                if (AstCsvSupport.IsCsv(receiver.SemanticType.Name) && receiver is BoundNameExpression { Symbol: TypeSymbol })
                    return Error(syntax.NameToken, CompilerDiagnosticCodes.UnknownMember, $"CSV property '{property.Name.Split('.').Last()}' requires an instance.");
                if (AstImageSupport.IsImage(receiver.SemanticType.Name) && receiver is BoundNameExpression { Symbol: TypeSymbol })
                    return Error(syntax.NameToken, CompilerDiagnosticCodes.UnknownMember, $"XPImage property '{property.Name.Split('.').Last()}' requires an image instance.");
                return BindProperty(syntax, receiver, property);
            }
            if (symbol is FunctionSymbol function)
                return new BoundMemberAccessExpression(receiver, function.Name.Split('.').Last(), function.ReturnType, function.SemanticReturnType);
        }
        // CLR object storage also backs declared user types. Only the XPscript
        // Variant semantic type authorizes unresolved dynamic member lookup.
        return _allowDynamicMembers && receiver.SemanticType.IsVariant
            ? new BoundMemberAccessExpression(receiver, syntax.NameToken.Text, typeof(object), XpTypeSymbol.Variant)
            : Error(syntax.NameToken, CompilerDiagnosticCodes.UnknownMember, $"Undefined member '{syntax.NameToken.Text}' on '{receiver.SemanticType.Name}'.");
    }

    private BoundExpression BindArray(ArrayExpressionSyntax syntax)
    {
        if (_symbols.TryLookup(syntax.ArrayIdentifier.Text, out var symbol) && symbol is LocalSymbol { SemanticType.IsList: true } list && syntax.Elements.Count == 1)
        {
            var element = list.SemanticType!.ElementType!;
            return new BoundIndexExpression(new BoundNameExpression(list), Bind(syntax.Elements[0]), element.RuntimeType, element);
        }
        if (syntax.ArrayIdentifier.Text.Equals("Array", StringComparison.OrdinalIgnoreCase))
        {
            var arrayArguments = syntax.Elements.Select(Bind).ToArray();
            var arrayFunction = new FunctionSymbol("Array", typeof(object[]), arrayArguments.Select(argument => argument.Type).ToArray(), XpTypeSymbol.Variant, arrayArguments.Select(_ => XpTypeSymbol.Variant).ToArray());
            return new BoundCallExpression(null, arrayFunction, arrayArguments);
        }
        if (_allowDynamicMembers && (syntax.ArrayIdentifier.Text.Equals("Files", StringComparison.OrdinalIgnoreCase) || syntax.ArrayIdentifier.Text.Equals("Directories", StringComparison.OrdinalIgnoreCase)))
        {
            var fileArguments = syntax.Elements.Select(Bind).ToArray();
            var function = new FunctionSymbol(syntax.ArrayIdentifier.Text, typeof(object), fileArguments.Select(argument => argument.Type).ToArray(), XpTypeSymbol.Variant, fileArguments.Select(_ => XpTypeSymbol.Variant).ToArray());
            return new BoundCallExpression(null, function, fileArguments);
        }
        if (_allowDynamicMembers && syntax.Elements.Count >= 1 &&
            !_symbols.LookupAll(syntax.ArrayIdentifier.Text).OfType<FunctionSymbol>().Any())
        {
            var local = _symbols.TryLookup(syntax.ArrayIdentifier.Text, out var localSymbol) && localSymbol is LocalSymbol found && found.Type == typeof(object)
                ? found
                : new LocalSymbol(syntax.ArrayIdentifier.Text, typeof(object), XpTypeSymbol.Variant);
            var index = Bind(syntax.Elements[0]);
            return new BoundIndexExpression(new BoundNameExpression(local), index, typeof(object), XpTypeSymbol.Variant);
        }
        var functions = _symbols.LookupAll(syntax.ArrayIdentifier.Text).OfType<FunctionSymbol>().ToArray();
        if (functions.Length == 0)
        {
            if (_allowDynamicMembers)
                return new BoundNameExpression(new LocalSymbol(syntax.ArrayIdentifier.Text, typeof(object), XpTypeSymbol.Variant));
            return Error(syntax.ArrayIdentifier, CompilerDiagnosticCodes.UnknownSymbol, $"Undefined function '{syntax.ArrayIdentifier.Text}'.");
        }

        var arguments = syntax.Elements.Select(Bind).ToArray();
        var candidates = functions
            .Where(function => function.ParameterTypes.Count == arguments.Length)
            .Where(function => ParametersMatch(function, arguments))
            .Where(function => ParameterModesMatch(function, arguments, syntax))
            .ToArray();

        if (candidates.Length == 0)
            return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, $"No matching overload for function '{syntax.ArrayIdentifier.Text}'.");
        if (candidates.Length > 1)
            return Error(syntax, CompilerDiagnosticCodes.AmbiguousOverload, $"Call to function '{syntax.ArrayIdentifier.Text}' is ambiguous.");

        return new BoundCallExpression(null, candidates[0], arguments);
    }

    private BoundExpression BindCall(CallExpressionSyntax syntax)
    {
        BoundExpression? target;
        string name;
        if (syntax.Target is NameExpressionSyntax nameSyntax)
        {
            target = null;
            name = nameSyntax.IdentifierToken.Text;
            if (_symbols.TryLookup(name, out var ownIndexed) && ownIndexed is IndexedPropertySymbol indexed && _symbols.TryLookup("Me", out var me))
                return BindIndexedProperty(syntax, new BoundNameExpression(me), indexed);
            if (name.Equals("CsvSave", StringComparison.OrdinalIgnoreCase) || name.Equals("CsvWriteFile", StringComparison.OrdinalIgnoreCase))
                return Error(nameSyntax.IdentifierToken, CompilerDiagnosticCodes.RemovedNativeApi,
                    "CSV file output is available only through XPCsvDocument.Save or XPCsvDocument.SaveFile.");
            if (_symbols.TryLookup(name, out var collectionSymbol) && collectionSymbol is LocalSymbol or ParameterSymbol &&
                new BoundNameExpression(collectionSymbol).SemanticType.IsList && syntax.Arguments.Count != 1)
                return Error(syntax, CompilerDiagnosticCodes.ArgumentCountMismatch, "List access requires exactly one tag.");
            if (_symbols.TryLookup(name, out var listSymbol) && listSymbol is LocalSymbol or ParameterSymbol && syntax.Arguments.Count == 1 &&
                new BoundNameExpression(listSymbol).SemanticType.IsList)
            {
                var receiver = new BoundNameExpression(listSymbol);
                var element = receiver.SemanticType.ElementType!;
                return new BoundIndexExpression(receiver, Bind(syntax.Arguments[0]), element.RuntimeType, element);
            }
            if (_symbols.TryLookup(name, out var indexedSymbol) && indexedSymbol is LocalSymbol local &&
                (local.Type.IsArray || local.SemanticType?.IsArray == true || local.IsDynamicArray || local.ArrayRank > 1) && syntax.Arguments.Count > 0)
            {
                var array = new BoundNameExpression(local);
                var indices = syntax.Arguments.Select(Bind).ToArray();
                if (indices.Any(index => index.Type != typeof(long)))
                    return Error(syntax, CompilerDiagnosticCodes.ArgumentTypeMismatch, "Array index must be an integer.");
                var elementType = local.SemanticType?.ElementType ?? XpTypeSymbol.FromClr(local.Type.GetElementType() ?? typeof(object));
                return new BoundIndexExpression(array, indices, elementType.RuntimeType, elementType);
            }
            if (_symbols.TryLookup(name, out indexedSymbol) && indexedSymbol is LocalSymbol arrayLocal && arrayLocal.Type.IsArray && syntax.Arguments.Count == 0)
                return new BoundNameExpression(arrayLocal);
            if (_allowDynamicMembers && _symbols.TryLookup(name, out var dynamicSymbol) && dynamicSymbol is LocalSymbol dynamicLocal && dynamicLocal.Type == typeof(object) && dynamicLocal.SemanticType?.IsVariant == true && syntax.Arguments.Count == 1)
            {
                var index = Bind(syntax.Arguments[0]);
                return new BoundIndexExpression(new BoundNameExpression(dynamicLocal), index, typeof(object), XpTypeSymbol.Variant);
            }
        }
        else if (syntax.Target is MemberAccessExpressionSyntax or ImplicitMemberAccessExpressionSyntax)
        {
            var memberSyntax = syntax.Target as MemberAccessExpressionSyntax;
            var memberName = memberSyntax?.NameToken ?? ((ImplicitMemberAccessExpressionSyntax)syntax.Target).NameToken;
            var receiver = memberSyntax is null ? implicitReceiver : Bind(memberSyntax.Expression);
            if (receiver is null)
                return Error(memberName, CompilerDiagnosticCodes.UnknownMember, $"Implicit member '{memberName.Text}' requires an active With scope.");
            name = memberName.Text;
            if (receiver.SemanticType.Name.Equals("XPCsvDocument", StringComparison.OrdinalIgnoreCase) && name.Equals("WriteFile", StringComparison.OrdinalIgnoreCase))
                return Error(memberName, CompilerDiagnosticCodes.RemovedNativeApi, "XPCsvDocument.WriteFile was removed. Use Save or SaveFile.");
            if (receiver.SemanticType.Name.Equals("XPCsvDocument", StringComparison.OrdinalIgnoreCase) && name.Equals("Load", StringComparison.OrdinalIgnoreCase) && syntax.Arguments.Count is < 1 or > 4)
                return Error(syntax, CompilerDiagnosticCodes.InvalidNativeArgumentList, "XPCsvDocument.Load requires path and optional encoding, delimiter, and hasHeaders arguments.");
            if (receiver.SemanticType.Name.Equals("XPCsvDocument", StringComparison.OrdinalIgnoreCase) &&
                (name.Equals("Save", StringComparison.OrdinalIgnoreCase) || name.Equals("SaveFile", StringComparison.OrdinalIgnoreCase)) && syntax.Arguments.Count is < 1 or > 2)
                return Error(syntax, CompilerDiagnosticCodes.InvalidNativeArgumentList, "XPCsvDocument.Save requires path and optional encoding arguments.");
            if (receiver.SemanticType.Name.Equals("XPCsvDocument", StringComparison.OrdinalIgnoreCase) && name.Equals("FromBytes", StringComparison.OrdinalIgnoreCase) && syntax.Arguments.Count is < 1 or > 2)
                return Error(syntax, CompilerDiagnosticCodes.InvalidNativeArgumentList, "XPCsvDocument.FromBytes requires bytes and an optional encoding argument.");
            var memberKey = receiver.SemanticType.Name + "." + name;
            var memberSymbols = _symbols.LookupMembers(receiver.SemanticType, name);
            if (memberSymbols.Count == 0 && _allowDynamicMembers && receiver.SemanticType.IsVariant)
            {
                var arguments = syntax.Arguments.Select(Bind).ToArray();
                var dynamicFunction = new FunctionSymbol(name, typeof(object), arguments.Select(argument => argument.Type).ToArray(), XpTypeSymbol.Variant);
                target = new BoundMemberAccessExpression(receiver, name, typeof(object), XpTypeSymbol.Variant);
                return new BoundCallExpression(target, dynamicFunction, arguments);
            }
            if (memberSymbols.Count == 0)
                return Error(memberName, CompilerDiagnosticCodes.UnknownMember, $"Undefined member '{name}' on '{receiver.SemanticType.Name}'.");
            var indexedProperty = memberSymbols.OfType<IndexedPropertySymbol>().LastOrDefault();
            if (indexedProperty is not null)
                return BindIndexedProperty(syntax, receiver, indexedProperty);
            var memberFunctions = memberSymbols.OfType<FunctionSymbol>().ToArray();
            if (AstSpreadsheetSupport.IsSpreadsheet(receiver.SemanticType.Name) || AstHttpSupport.IsHttp(receiver.SemanticType.Name) || AstImageSupport.IsImage(receiver.SemanticType.Name) || AstCsvSupport.IsCsv(receiver.SemanticType.Name) || AstXmlSupport.IsXml(receiver.SemanticType.Name))
                memberFunctions = memberFunctions.Where(f => f.IsNativeStatic == (receiver is BoundNameExpression { Symbol: TypeSymbol })).ToArray();
            if (AstCsvSupport.IsCsv(receiver.SemanticType.Name) && name.Equals("FromBytes", StringComparison.OrdinalIgnoreCase) && receiver is not BoundNameExpression)
                return Error(syntax, CompilerDiagnosticCodes.InvalidSyntax, "XPCsvDocument.FromBytes requires a writable document variable.");
            if (memberFunctions.Length == 0)
                return Error(memberName, CompilerDiagnosticCodes.UnknownMember, $"Member '{name}' on '{receiver.SemanticType.Name}' is not callable.");
            target = new BoundMemberAccessExpression(receiver, memberFunctions[0].Name.Split('.').Last(), memberFunctions[0].ReturnType, memberFunctions[0].SemanticReturnType);
            return BindOverloadSet(syntax, target, memberKey, memberFunctions);
        }
        else
        {
            return Error(syntax, "Unsupported call target.");
        }

        var functions = _symbols.LookupAll(name).OfType<FunctionSymbol>().ToArray();
        if (functions.Length == 0)
        {
            if (_allowDynamicMembers && syntax.Arguments.Count == 0)
                return new BoundNameExpression(new LocalSymbol(name, typeof(object), XpTypeSymbol.Variant));
        if (_allowDynamicMembers && syntax.Arguments.Count >= 1)
        {
            var dynamicLocal = new LocalSymbol(name, typeof(object), XpTypeSymbol.Variant);
            return new BoundIndexExpression(new BoundNameExpression(dynamicLocal), Bind(syntax.Arguments[0]), typeof(object), XpTypeSymbol.Variant);
        }
        return Error(nameSyntax.IdentifierToken, CompilerDiagnosticCodes.UnknownSymbol, $"Undefined function '{name}'.");
        }
        return BindOverloadSet(syntax, target, name, functions);
    }

    private BoundExpression BindProperty(ExpressionSyntax syntax, BoundExpression receiver, PropertySymbol property)
    {
        var writing = ReferenceEquals(syntax, assignmentTarget);
        if (writing ? !property.CanWrite : !property.CanRead)
            return Error(syntax, CompilerDiagnosticCodes.TypeMismatch,
                $"Property '{property.Name.Split('.').Last()}' is {(writing ? "read-only" : "write-only")}.");
        return new BoundMemberAccessExpression(receiver, property.Name.Split('.').Last(), property.PropertyType, property.SemanticType, property.IsFieldStorage, property.AssignmentKind);
    }

    private BoundExpression BindOverloadSet(CallExpressionSyntax syntax, BoundExpression? target, string name, IReadOnlyList<FunctionSymbol> functions)
    {
        var arguments = syntax.Arguments.Select(Bind).ToArray();
        if (target is null && name.Equals("IsElement", StringComparison.OrdinalIgnoreCase) &&
            (arguments.Length != 1 || arguments[0] is not BoundIndexExpression element ||
             !(element.Expression.SemanticType.IsList || element.Expression.SemanticType.IsVariant)))
            return Error(syntax, CompilerDiagnosticCodes.InvalidSyntax, "IsElement requires one keyed List access, for example IsElement(values(tag)).");
        var candidates = functions
            .Where(function => function.ParameterTypes.Count == arguments.Length)
            .Where(function => ParametersMatch(function, arguments))
            .Where(function => ParameterModesMatch(function, arguments, syntax))
            .ToArray();

        if (candidates.Length == 0)
        {
            for (var i = 0; i < arguments.Length; i++)
                if (!ReferenceEquals(syntax, listByRefStatementCall) &&
                    arguments[i] is BoundIndexExpression { Expression.SemanticType.IsList: true } &&
                    functions.Any(function => function.ParameterTypes.Count == arguments.Length && function.ByRefParameters?[i] == true))
                    return Error(syntax.Arguments[i], CompilerDiagnosticCodes.InvalidSyntax,
                        "AST List element ByRef arguments are not implemented; pass a scalar local or use an explicit copy-back pattern.");
            // This set contains declared signatures. Dynamic receivers are
            // handled before overload selection; fabricating a signature here
            // would suppress arity, argument-type and ByRef diagnostics.
            return Error(syntax, CompilerDiagnosticCodes.NoMatchingOverload, $"No matching overload for function '{name}'.");
        }
        if (candidates.Length > 1)
        {
            var explicitCandidates = candidates.Where(candidate => !candidate.IsOptionalForwarding).ToArray();
            if (explicitCandidates.Length == 1)
                candidates = explicitCandidates;
        }
        if (candidates.Length > 1)
            return Error(syntax, CompilerDiagnosticCodes.AmbiguousOverload, $"Call to function '{name}' is ambiguous.");

        return new BoundCallExpression(target, candidates[0], ConvertArguments(candidates[0], arguments));
    }

    private BoundExpression[] ConvertArguments(FunctionSymbol function, IReadOnlyList<BoundExpression> arguments)
    {
        var converted = new BoundExpression[arguments.Count];
        for (var i = 0; i < arguments.Count; i++)
        {
            var targetType = function.SemanticParameterTypes is not null
                ? function.SemanticParameterTypes[i]
                : XpTypeSymbol.FromClr(function.ParameterTypes[i]);
            var conversion = _symbols.ClassifyConversion(arguments[i].SemanticType, targetType);
            converted[i] = conversion.IsIdentity
                ? arguments[i]
                : new BoundConversionExpression(arguments[i], targetType, conversion) { Span = arguments[i].Span };
        }
        return converted;
    }

    private bool ParameterModesMatch(FunctionSymbol function, IReadOnlyList<BoundExpression> arguments, ExpressionSyntax syntax)
    {
        if (function.ByRefParameters is null)
            return true;
        if (function.ByRefParameters.Count != arguments.Count)
            return false;

        for (var i = 0; i < arguments.Count; i++)
        {
            if (!function.ByRefParameters[i])
                continue;
            if ((ReferenceEquals(syntax, listByRefStatementCall) || function.ReturnType != typeof(void)) &&
                syntax is CallExpressionSyntax { Target: NameExpressionSyntax } &&
                (arguments[i] is BoundIndexExpression { Expression.SemanticType.IsList: true } ||
                 arguments[i] is BoundNameExpression { Symbol: LocalSymbol { IsListAlias: true } }) &&
                _symbols.ClassifyConversion(arguments[i].SemanticType,
                    function.SemanticParameterTypes?[i] ?? XpTypeSymbol.FromClr(function.ParameterTypes[i])).IsIdentity)
                continue;
            if (arguments[i] is not BoundNameExpression name ||
                name.Symbol is not (VariableSymbol or LocalSymbol or ParameterSymbol or FieldSymbol))
                return false;
            if (name.Symbol is LocalSymbol { IsListAlias: true } &&
                !((ReferenceEquals(syntax, listByRefStatementCall) || function.ReturnType != typeof(void)) &&
                  syntax is CallExpressionSyntax { Target: NameExpressionSyntax } &&
                  _symbols.ClassifyConversion(arguments[i].SemanticType,
                      function.SemanticParameterTypes?[i] ?? XpTypeSymbol.FromClr(function.ParameterTypes[i])).IsIdentity))
                return false;
        }

        return true;
    }

    private bool ParametersMatch(FunctionSymbol function, IReadOnlyList<BoundExpression> arguments)
    {
        if (function.ParameterTypes.Count != arguments.Count)
            return false;

        for (var i = 0; i < arguments.Count; i++)
        {
            var targetType = function.SemanticParameterTypes is not null
                ? function.SemanticParameterTypes[i]
                : XpTypeSymbol.FromClr(function.ParameterTypes[i]);
            var conversion = _symbols.ClassifyConversion(arguments[i].SemanticType, targetType);
            var isByRef = function.ByRefParameters is not null && function.ByRefParameters[i];
            if (targetType.IsVariant)
                continue;
            if (!conversion.IsImplicit || isByRef && !conversion.IsIdentity)
                return false;
        }

        return true;
    }

    private BoundExpression BindIndexedProperty(CallExpressionSyntax syntax, BoundExpression receiver, IndexedPropertySymbol property)
    {
        var writing = ReferenceEquals(syntax, assignmentTarget);
        if (writing ? !property.CanWrite : !property.CanRead)
            return Error(syntax, CompilerDiagnosticCodes.TypeMismatch,
                $"Property '{property.Name.Split('.').Last()}' is {(writing ? "read-only" : "write-only")}.");
        var arguments = syntax.Arguments.Select(Bind).ToArray();
        if (arguments.Length < property.RequiredArgumentCount || arguments.Length > property.ParameterTypes.Count)
        {
            var expected = property.RequiredArgumentCount == property.ParameterTypes.Count
                ? property.ParameterTypes.Count.ToString()
                : $"{property.RequiredArgumentCount} to {property.ParameterTypes.Count}";
            return Error(syntax, CompilerDiagnosticCodes.ArgumentCountMismatch, $"Indexed property '{property.Name}' expects {expected} argument(s), but received {arguments.Length}.");
        }
        for (var i = 0; i < arguments.Length; i++)
        {
            var targetType = property.SemanticParameterTypes is not null
                ? property.SemanticParameterTypes[i]
                : XpTypeSymbol.FromClr(property.ParameterTypes[i]);
            var conversion = _symbols.ClassifyConversion(arguments[i].SemanticType, targetType);
            if (property.ByRefIndexes?[i] == true)
            {
                if (ValidateScalarReference(syntax.Arguments[i], arguments[i], property.ParameterTypes[i], targetType,
                    $"index {i + 1} to '{property.Name}'") is { } failure) return failure;
            }
            if (!conversion.IsImplicit)
                return Error(syntax.Arguments[i], CompilerDiagnosticCodes.ArgumentTypeMismatch, $"Argument {i + 1} to '{property.Name}' must be {targetType.Name}, not {arguments[i].SemanticType.Name}.");
            if (!conversion.IsIdentity)
                arguments[i] = new BoundConversionExpression(arguments[i], targetType, conversion);
        }
        return new BoundIndexedPropertyExpression(receiver, property, arguments);
    }

    // Indexed accessors and constructors share the same addressable storage
    // contract. Passing a value-converted or accessor-backed temporary is unsafe.
    private BoundExpression? ValidateScalarReference(ExpressionSyntax syntax, BoundExpression argument, Type runtimeType,
        XpTypeSymbol targetType, string description)
    {
        if (argument is BoundNameExpression { Symbol: VariableSymbol { HasTypedStorage: false } })
            return Error(syntax, CompilerDiagnosticCodes.TargetApiUnavailable,
                "ByRef arguments require typed writable storage; initialized or untyped module variables are not implemented yet.");
        if (!_symbols.ClassifyConversion(argument.SemanticType, targetType).IsIdentity || argument.Type != runtimeType)
            return Error(syntax, CompilerDiagnosticCodes.ArgumentTypeMismatch, $"ByRef {description} requires exactly {targetType.Name}.");
        if (argument is BoundIndexExpression location && !location.Expression.SemanticType.IsArray && !location.Expression.SemanticType.IsList
            || argument is BoundMemberAccessExpression { IsFieldStorage: false } or BoundIndexedPropertyExpression)
            return Error(syntax, CompilerDiagnosticCodes.TargetApiUnavailable,
                "Accessor-backed ByRef arguments are not implemented yet; use writable storage.");
        if (argument is not BoundNameExpression { Symbol: LocalSymbol or ParameterSymbol or FieldSymbol }
            and not BoundNameExpression { Symbol: VariableSymbol { HasTypedStorage: true } }
            and not BoundMemberAccessExpression { IsFieldStorage: true }
            and not BoundIndexExpression)
            return Error(syntax, CompilerDiagnosticCodes.TypeMismatch,
                "A ByRef argument requires a writable local, parameter, class field or typed module variable.");
        return null;
    }

    private BoundExpression BindCallTarget(CallExpressionSyntax syntax, BoundExpression? target, FunctionSymbol function)
    {
        var arguments = syntax.Arguments.Select(Bind).ToArray();
        if (arguments.Length != function.ParameterTypes.Count)
            return Error(syntax, CompilerDiagnosticCodes.ArgumentCountMismatch, $"Function '{function.Name}' expects {function.ParameterTypes.Count} argument(s), but received {arguments.Length}.");

        for (var i = 0; i < arguments.Length; i++)
        {
            var targetType = function.SemanticParameterTypes is not null
                ? function.SemanticParameterTypes[i]
                : XpTypeSymbol.FromClr(function.ParameterTypes[i]);
            var conversion = _symbols.ClassifyConversion(arguments[i].SemanticType, targetType);
            if (!conversion.IsImplicit)
                return Error(syntax.Arguments[i], CompilerDiagnosticCodes.ArgumentTypeMismatch, $"Argument {i + 1} to '{function.Name}' must be {targetType.Name}, not {arguments[i].SemanticType.Name}.");
            if (!conversion.IsIdentity)
                arguments[i] = new BoundConversionExpression(arguments[i], targetType, conversion);
        }

        return new BoundCallExpression(target, function, arguments);
    }

    private BoundExpression BindUnary(UnaryExpressionSyntax syntax)
    {
        var operand = Bind(syntax.Operand);
        if (syntax.OperatorToken.Kind == SyntaxKind.NotKeyword && operand.Type == typeof(bool))
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, typeof(bool));
        if (syntax.OperatorToken.Kind == SyntaxKind.NotKeyword && operand.Type == typeof(long))
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, typeof(long));
        if (syntax.OperatorToken.Kind is SyntaxKind.PlusToken or SyntaxKind.MinusToken &&
            (operand.Type == typeof(long) || operand.Type == typeof(double)))
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, operand.Type);
        if (_allowDynamicMembers && operand.SemanticType.IsVariant)
            return new BoundUnaryExpression(syntax.OperatorToken.Kind, operand, typeof(object));
        return Error(syntax, CompilerDiagnosticCodes.TypeMismatch, $"Unary operator {syntax.OperatorToken.Text} is not defined for {operand.SemanticType.Name}.");
    }

    private BoundExpression BindBinary(BinaryExpressionSyntax syntax)
    {
        var left = Bind(syntax.Left);
        var right = Bind(syntax.Right);
        var op = syntax.OperatorToken.Kind;

        if (op is SyntaxKind.AndKeyword or SyntaxKind.OrKeyword && left.Type == typeof(bool) && right.Type == typeof(bool))
            return new BoundBinaryExpression(left, op, right, typeof(bool));
        if (op is SyntaxKind.AndKeyword or SyntaxKind.OrKeyword && left.Type == typeof(long) && right.Type == typeof(long))
            return new BoundBinaryExpression(left, op, right, typeof(long));
        if (op is SyntaxKind.PlusToken or SyntaxKind.MinusToken or SyntaxKind.StarToken or SyntaxKind.SlashToken &&
            left.Type == right.Type && (left.Type == typeof(long) || left.Type == typeof(double)))
            return new BoundBinaryExpression(left, op, right, left.Type);
        if (op == SyntaxKind.AmpersandToken && left.Type == typeof(string) && right.Type == typeof(string))
            return new BoundBinaryExpression(left, op, right, typeof(string));
        if (op == SyntaxKind.LikeKeyword && left.Type == typeof(string) && right.Type == typeof(string))
            return new BoundBinaryExpression(left, op, right, typeof(bool));
        if (_allowDynamicMembers && op == SyntaxKind.PlusToken && (left.Type == typeof(string) || right.Type == typeof(string)))
            return new BoundBinaryExpression(left, op, right, typeof(string));
        if (op is SyntaxKind.LessToken or SyntaxKind.LessOrEqualsToken or SyntaxKind.GreaterToken or SyntaxKind.GreaterOrEqualsToken &&
            left.Type == right.Type && (left.Type == typeof(long) || left.Type == typeof(double)))
            return new BoundBinaryExpression(left, op, right, typeof(bool));
        if (op is SyntaxKind.EqualsToken or SyntaxKind.LessGreaterToken && left.Type == right.Type)
            return new BoundBinaryExpression(left, op, right, typeof(bool));
        if (op == SyntaxKind.IsKeyword && (left.Type == typeof(object) || right.Type == typeof(object)))
            return new BoundBinaryExpression(left, op, right, typeof(bool));
        if (_allowDynamicMembers && (left.SemanticType.IsVariant || right.SemanticType.IsVariant || left.Type == typeof(object) || right.Type == typeof(object)))
            return new BoundBinaryExpression(left, op, right, typeof(object));
        if (_allowDynamicMembers && (left.Type == typeof(long) || left.Type == typeof(double)) && (right.Type == typeof(long) || right.Type == typeof(double)))
            return new BoundBinaryExpression(left, op, right, typeof(object));

        return Error(syntax, CompilerDiagnosticCodes.TypeMismatch, $"Binary operator {syntax.OperatorToken.Text} is not defined for {left.SemanticType.Name} and {right.SemanticType.Name}.");
    }

    private BoundExpression Error(SyntaxNode syntax, string message) =>
        Error(syntax.Span, CompilerDiagnosticCodes.InvalidSyntax, message);

    private BoundExpression Error(SyntaxNode syntax, string code, string message) =>
        Error(syntax.Span, code, message);

    private BoundExpression Error(SyntaxToken token, string code, string message) =>
        Error(token.Span, code, message);

    private BoundExpression Error(TextSpan span, string code, string message)
    {
        _diagnostics.Add(new SyntaxDiagnostic(code, message, span));
        return new BoundLiteralExpression(null, typeof(object));
    }
}
