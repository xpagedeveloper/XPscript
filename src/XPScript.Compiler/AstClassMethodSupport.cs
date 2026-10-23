using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;

namespace XPScript.Compiler;

/// <summary>Instance procedure bodies use the same binder/flow validation and emitter as module procedures.</summary>
internal static class AstClassMethodSupport
{
    // Managed native families share references; images use their explicit snapshot ownership contract.
    // Other families retain their deferral until their accessor contract is covered.
    private static bool UnsupportedNativeAccessorType(string? name) =>
        name is not null && name.Trim().ToUpperInvariant() is not ("BYTE" or "BOOLEAN" or "STRING" or "INTEGER" or "LONG" or "SINGLE" or "DOUBLE" or "CURRENCY" or "DATE") &&
        BoundExpressionEmitter.RuntimeObjectType(name ?? "") is not null &&
        !SupportedNativeClassMethodType(name);

    private static bool SupportedNativeClassMethodType(string? name) =>
        string.Equals(name, "Object", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Variant", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Date", StringComparison.OrdinalIgnoreCase) ||
        AstImageSupport.IsImage(name) || AstXmlSupport.IsXml(name) || AstCsvSupport.IsCsv(name) ||
        AstSpreadsheetSupport.IsSpreadsheet(name) || AstHttpSupport.IsHttp(name) ||
        new[] { "XPDBSQLite", "XPDbMySql", "XPDbMsSql", "XPDbSupabase", "XPSupabaseDB" }
            .Contains(name, StringComparer.OrdinalIgnoreCase) ||
        new[] { "SystemInventory", "SystemInventorySnapshot", "SystemInventorySystemInfo", "SystemInventoryCpuInfo",
            "SystemInventoryMemoryInfo", "SystemInventoryPhysicalDiskInfo", "SystemInventoryVolumeInfo",
            "SystemInventoryGraphicsAdapterInfo", "SystemInventoryNetworkAdapterInfo", "InstalledSoftwareInfo" }
            .Contains(name, StringComparer.OrdinalIgnoreCase) ||
        new[] { "NetworkTools", "NetworkPingResult", "NetworkTraceHop", "NetworkDnsResult", "NetworkPortResult",
            "NetworkUdpResult", "NetworkHttpResult", "NetworkTlsResult", "NetworkInterfaceInfo", "NetworkEndpointInfo",
            "NetworkHostResult", "NetworkSubnetInfo", "NetworkTlsVersionResult", "NetworkCertificateResult",
            "NetworkHttpHeadersResult", "NetworkHttpSecurityHeadersResult", "NetworkRedirectResult", "NetworkBannerResult",
            "NetworkPortExposureInfo" }.Contains(name, StringComparer.OrdinalIgnoreCase) ||
        string.Equals(name, "XPAttachmentCollection", StringComparison.OrdinalIgnoreCase) ||
        new[] { "XPShellResult", "XPPathValue", "XPFileInfoValue" }.Contains(name, StringComparer.OrdinalIgnoreCase) ||
        new[] { "XPHttpDbSupabase", "XPHttpDbDominoRest" }.Contains(name, StringComparer.OrdinalIgnoreCase) ||
        string.Equals(name, "XPJsonSchema", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "XPJsonValidationResult", StringComparison.OrdinalIgnoreCase) ||
        new[] { "XPAi", "XPAiResponse", "XPAITool", "XPAIToolFunction", "XPAIToolCall" }
            .Contains(name, StringComparer.OrdinalIgnoreCase) ||
        new[] { "XPJsonDocument", "XPJsonObject", "XPJsonArray", "XPJsonElement" }
            .Contains(name, StringComparer.OrdinalIgnoreCase) ||
        new[] { "NotesHTTPRequest", "XPJSONNavigator", "XPJSONObject", "XPJSONArray", "XPJSONElement" }
            .Contains(name, StringComparer.OrdinalIgnoreCase);

    internal static XpTypeSymbol DeclaredType(TypeSyntax? syntax, SymbolTable symbols)
    {
        var name = syntax?.Identifier.Text;
        if (string.IsNullOrEmpty(name) || name.Equals("Variant", StringComparison.OrdinalIgnoreCase)) return XpTypeSymbol.Variant;
        if (name.Equals("XPNotesHTTPRequest", StringComparison.OrdinalIgnoreCase)) return XpTypeSymbol.User("NotesHTTPRequest");
        if (AstNotesJsonSupport.IsCompatibilityType(name)) return XpTypeSymbol.User(name);
        if (AstClassConstructorSupport.Find(symbols, name)?.SemanticReturnType is { } userType) return userType;
        if (symbols.TryLookup(name, out var symbol) && symbol is TypeSymbol { SemanticType: not null } type) return type.SemanticType;
        var runtime = AstExperimentalCompiler.ResolveRuntimeType(name);
        return runtime == typeof(object) && !name.Equals("Object", StringComparison.OrdinalIgnoreCase)
            ? XpTypeSymbol.User(name) : XpTypeSymbol.FromClr(runtime);
    }

    internal static IEnumerable<Symbol> Properties(ClassDeclarationSyntax declaration, bool external, SymbolTable symbols)
    {
        foreach (var group in declaration.Members.OfType<PropertyDeclarationSyntax>().GroupBy(p => p.Identifier.Text, StringComparer.OrdinalIgnoreCase))
        {
            var getter = group.FirstOrDefault(p => p.IsGetter);
            var setter = group.FirstOrDefault(p => p.IsSetter);
            var readable = getter is not null && (!external || getter.Visibility?.Kind != SyntaxKind.PrivateKeyword);
            var writable = setter is not null && (!external || setter.Visibility?.Kind != SyntaxKind.PrivateKeyword);
            if (!readable && !writable) continue;
            var semanticType = DeclaredType(getter?.Type ?? setter?.Type ?? setter?.Parameters.LastOrDefault()?.Type, symbols);
            var type = semanticType.RuntimeType;
            var assignmentKind = setter is null ? PropertyAssignmentKind.Unspecified
                : setter.AccessorKeyword.Kind == SyntaxKind.SetKeyword ? PropertyAssignmentKind.Set : PropertyAssignmentKind.Let;
            var name = group.First().Identifier.Text;
            var indexes = getter?.Parameters ?? setter!.Parameters.Take(Math.Max(0, setter.Parameters.Count - 1)).ToArray();
            if (indexes.Count > 0)
                yield return new IndexedPropertySymbol(declaration.Identifier.Text + "." + name, type,
                    indexes.Select(p => AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text)).ToArray(),
                    semanticType, indexes.Select(p => DeclaredType(p.Type, symbols)).ToArray(), readable, writable,
                    "__xps_index_get_" + name, "__xps_index_let_" + name,
                    indexes.Select(p => p.IsExplicitByRef).ToArray())
                    { RequiredArgumentCount = indexes.TakeWhile(p => !p.IsOptional).Count(), AssignmentKind = assignmentKind };
            else yield return new PropertySymbol(declaration.Identifier.Text + "." + name,
                type, semanticType, readable, writable) { AssignmentKind = assignmentKind };
        }
    }

    internal static string ModuleSource(string source)
    {
        // Existing module-field scanning must never turn class-private fields into globals.
        var unit = new DeclarationParser(source).ParseCompilationUnit();
        var text = source.ToCharArray();
        foreach (var declaration in unit.Declarations.OfType<ClassDeclarationSyntax>())
            for (var i = declaration.Span.Start; i < declaration.Span.End && i < text.Length; i++)
                if (text[i] is not ('\r' or '\n')) text[i] = ' ';
        return new string(text);
    }

    private static bool SameMethodSignature(SubDeclarationSyntax? leftSub, FunctionDeclarationSyntax? leftFunction,
        SubDeclarationSyntax? rightSub, FunctionDeclarationSyntax? rightFunction)
    {
        var leftName = leftSub?.Identifier.Text ?? leftFunction?.Identifier.Text;
        var rightName = rightSub?.Identifier.Text ?? rightFunction?.Identifier.Text;
        var left = leftSub?.Parameters ?? leftFunction?.Parameters ?? [];
        var right = rightSub?.Parameters ?? rightFunction?.Parameters ?? [];
        return string.Equals(leftName, rightName, StringComparison.OrdinalIgnoreCase) && left.Count == right.Count &&
            left.Zip(right, (a, b) => a.IsByRef == b.IsByRef && string.Equals(a.Type?.Identifier.Text, b.Type?.Identifier.Text, StringComparison.OrdinalIgnoreCase)).All(match => match);
    }

    private static bool HasBaseMethod(ClassDeclarationSyntax declaration, SubDeclarationSyntax? sub, FunctionDeclarationSyntax? function,
        IReadOnlyDictionary<string, ClassDeclarationSyntax> classes)
    {
        for (var owner = declaration.BaseType is { } baseType ? classes[baseType.Identifier.Text] : null;
             owner is not null;
             owner = owner.BaseType is { } parent ? classes[parent.Identifier.Text] : null)
            if (owner.Members.Any(member => member is SubDeclarationSyntax or FunctionDeclarationSyntax &&
                SameMethodSignature(sub, function, member as SubDeclarationSyntax, member as FunctionDeclarationSyntax)))
                return true;
        return false;
    }

    private static bool HasBaseProperty(ClassDeclarationSyntax declaration, string name,
        IReadOnlyDictionary<string, ClassDeclarationSyntax> classes)
    {
        for (var owner = declaration.BaseType is { } baseType ? classes[baseType.Identifier.Text] : null;
             owner is not null;
             owner = owner.BaseType is { } parent ? classes[parent.Identifier.Text] : null)
            if (owner.Members.OfType<PropertyDeclarationSyntax>().Any(property =>
                    property.Identifier.Text.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return true;
        return false;
    }

    private static string CSharpDeclaredType(TypeSyntax? syntax, SymbolTable symbols)
    {
        var name = syntax?.Identifier.Text ?? string.Empty;
        return BoundExpressionEmitter.RuntimeObjectType(name) ?? BoundMethodEmitter.CSharpType(DeclaredType(syntax, symbols));
    }
    internal static string Emit(ClassDeclarationSyntax declaration, SymbolTable globals, string sourcePath, string source,
        IReadOnlyDictionary<string, ClassDeclarationSyntax> classes)
    {
        var output = new System.Text.StringBuilder();
        var destructorSeen = false;
        // The compilation unit already validated this hierarchy. Declare ancestors
        // first so the current class's fields and private members keep precedence.
        var lineage = new Stack<ClassDeclarationSyntax>();
        for (var owner = declaration; owner is not null; owner = owner.BaseType is { } parent ? classes[parent.Identifier.Text] : null)
            lineage.Push(owner);
        foreach (var group in declaration.Members.OfType<PropertyDeclarationSyntax>().GroupBy(p => p.Identifier.Text, StringComparer.OrdinalIgnoreCase))
        {
            var getter = group.FirstOrDefault(p => p.IsGetter);
            var setter = group.FirstOrDefault(p => p.IsSetter);
            if (group.Count(p => p.IsGetter) > 1 || group.Count(p => p.IsSetter) > 1)
                throw AstExperimentalCompiler.MappedDiagnostic(group.Last().Span, sourcePath, source,
                    $"Property '{group.Key}' has duplicate accessors.", "XPS1012", "semantic");
            if (getter is not null && setter is not null && getter.Parameters.Count != Math.Max(0, setter.Parameters.Count - 1))
                throw AstExperimentalCompiler.MappedDiagnostic(setter.Span, sourcePath, source,
                    $"Property '{group.Key}' accessors must use compatible index parameter signatures.", "XPS1012", "semantic");
            if (getter is not null && setter is not null && getter.Parameters.Where((p, i) => p.IsExplicitByRef != setter.Parameters[i].IsExplicitByRef).Any())
                throw AstExperimentalCompiler.MappedDiagnostic(setter.Span, sourcePath, source,
                    $"Property '{group.Key}' accessors must use compatible index passing modes.", "XPS1012", "semantic");
            if (getter is not null && setter is not null && getter.Parameters.Where((p, i) =>
                    AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text) != AstExperimentalCompiler.ResolveRuntimeType(setter.Parameters[i].Type?.Identifier.Text) ||
                    p.IsOptional != setter.Parameters[i].IsOptional).Any())
                throw AstExperimentalCompiler.MappedDiagnostic(setter.Span, sourcePath, source,
                    $"Property '{group.Key}' accessors must use compatible index types and Optional signatures.", "XPS1012", "semantic");
            if (getter is not null && setter is not null &&
                !string.Equals(getter.Type?.Identifier.Text, setter.Type?.Identifier.Text ?? setter.Parameters.LastOrDefault()?.Type?.Identifier.Text, StringComparison.OrdinalIgnoreCase))
                throw AstExperimentalCompiler.MappedDiagnostic(setter.Span, sourcePath, source,
                    $"Property '{group.Key}' accessors must use compatible value types.", "XPS1012", "semantic");
        }
        foreach (var member in declaration.Members.Where(m => m is SubDeclarationSyntax or FunctionDeclarationSyntax or PropertyDeclarationSyntax or DestructorDeclarationSyntax or ConstructorDeclarationSyntax))
        {
            var sub = member as SubDeclarationSyntax;
            var function = member as FunctionDeclarationSyntax;
            var property = member as PropertyDeclarationSyntax;
            var destructor = member as DestructorDeclarationSyntax;
            var constructor = member as ConstructorDeclarationSyntax;
            var name = sub?.Identifier.Text ?? function?.Identifier.Text ?? property?.Identifier.Text ?? destructor?.Identifier.Text ?? constructor!.NewKeyword.Text;
            var isDestructor = name.Equals("Delete", StringComparison.OrdinalIgnoreCase);
            if (isDestructor)
            {
                if (destructor is null || destructorSeen)
                    throw AstExperimentalCompiler.MappedDiagnostic(member.Span, sourcePath, source,
                        "A class destructor must be a single parameterless Sub Delete.", "XPS1012", "semantic");
                destructorSeen = true;
            }
            var syntaxParameters = sub?.Parameters ?? function?.Parameters ?? property?.Parameters ?? constructor?.Parameters ?? Array.Empty<ParameterSyntax>();
            var statements = sub?.Statements ?? function?.Statements ?? property?.Statements ?? destructor?.Statements ?? constructor!.Statements;
            if (constructor is not null)
                statements = AstClassConstructorSupport.Body(declaration, constructor, sourcePath, source);
            var isObjectSetter = property?.AccessorKeyword.Kind == SyntaxKind.SetKeyword;
            if (property?.IsSetter == true && syntaxParameters.Count == 0)
                syntaxParameters = new[] { new ParameterSyntax(new SyntaxToken(SyntaxKind.ByValKeyword, "ByVal", null, property.Identifier.Span),
                    property.Identifier, property.AsKeyword, property.Type) };
            var resultTypeName = function?.ReturnType?.Identifier.Text ?? (property?.IsGetter == true ? property.Type?.Identifier.Text : null);
            if (isObjectSetter && DeclaredType(property!.Type ?? syntaxParameters.Last().Type, globals).RuntimeType != typeof(object))
                throw AstExperimentalCompiler.MappedDiagnostic(member.Span, sourcePath, source,
                    "Property Set requires an object/reference value type.", "XPS2001", "semantic");
            var hasUnsupportedObjectParameter = constructor is null && syntaxParameters.Where((p, i) => !isObjectSetter || i < syntaxParameters.Count - 1)
                .Any(p => !p.IsArray && AstExperimentalCompiler.ResolveRuntimeType(p.Type?.Identifier.Text) == typeof(object) &&
                    !SupportedNativeClassMethodType(p.Type?.Identifier.Text) &&
                    !classes.ContainsKey(p.Type?.Identifier.Text ?? ""));
            var hasUnsupportedObjectResult = function is not null && AstExperimentalCompiler.ResolveRuntimeType(resultTypeName) == typeof(object) &&
                !SupportedNativeClassMethodType(resultTypeName) && !classes.ContainsKey(resultTypeName ?? "");
            if (hasUnsupportedObjectParameter || hasUnsupportedObjectResult ||
                property is not null && (UnsupportedNativeAccessorType(resultTypeName) ||
                    isObjectSetter && UnsupportedNativeAccessorType(syntaxParameters.Last().Type?.Identifier.Text)))
                throw AstExperimentalCompiler.MappedDiagnostic(member.Span, sourcePath, source,
                    "This AST class-method increment supports scalar, Object/Variant, concrete user-class and XPImage parameters/results; other object/array signatures are not implemented yet.", "XPS3001", "ast");
            if (property is not null)
            {
                var indexParameters = property.IsGetter ? syntaxParameters : syntaxParameters.Take(syntaxParameters.Count - 1).ToArray();
                AstExperimentalCompiler.ValidateOptionalDeclarations(indexParameters, sourcePath, source);
                if (property.IsSetter && (syntaxParameters[^1].IsOptional || syntaxParameters[^1].DefaultValue is not null))
                    throw AstExperimentalCompiler.MappedDiagnostic(syntaxParameters[^1].Span, sourcePath, source,
                        "A Property Let value parameter must be required and cannot have a default value.", "XPS1012", "semantic");
            }
            var classScope = globals.CreateChildScope();
            foreach (var ownConstructor in AstClassConstructorSupport.FindAll(globals, declaration.Identifier.Text))
                classScope.Declare(ownConstructor with { IsPrivateConstructor = false });
            classScope.Declare(new VariableSymbol("Me", typeof(object), XpTypeSymbol.User(declaration.Identifier.Text)));
            if (declaration.BaseType is { } baseType)
            {
                classScope.Declare(new VariableSymbol("Parent", typeof(object), XpTypeSymbol.User(baseType.Identifier.Text)));
            }
            foreach (var owner in lineage)
            {
                var inherited = owner != declaration;
                foreach (var ownProperty in Properties(owner, inherited, globals))
                {
                    var propertyName = ownProperty.Name.Split('.').Last();
                    classScope.Declare(ownProperty with { Name = declaration.Identifier.Text + "." + propertyName });
                    classScope.Declare(ownProperty with { Name = propertyName });
                }
                foreach (var field in owner.Members.OfType<FieldDeclarationSyntax>().Where(f => !inherited || f.Visibility?.Kind != SyntaxKind.PrivateKeyword))
                {
                    var type = DeclaredType(field.Type, globals);
                    classScope.Declare(new FieldSymbol(field.Identifier.Text, type.RuntimeType, type));
                    classScope.Declare(new PropertySymbol(declaration.Identifier.Text + "." + field.Identifier.Text, type.RuntimeType, type, IsFieldStorage: true));
                }
                foreach (var own in owner.Members.Where(m => m is SubDeclarationSyntax or FunctionDeclarationSyntax))
                {
                    var ownSub = own as SubDeclarationSyntax; var ownFunction = own as FunctionDeclarationSyntax;
                    if (inherited && (ownSub?.Visibility ?? ownFunction?.Visibility)?.Kind == SyntaxKind.PrivateKeyword) continue;
                    var methodName = ownSub?.Identifier.Text ?? ownFunction!.Identifier.Text;
                    var ps = ownSub?.Parameters ?? ownFunction!.Parameters;
                    if (inherited && declaration.Members
                        .Where(member => member is SubDeclarationSyntax or FunctionDeclarationSyntax)
                        .Any(member =>
                        {
                            var ownParameters = member is SubDeclarationSyntax ownSubDeclaration
                                ? ownSubDeclaration.Parameters
                                : ((FunctionDeclarationSyntax)member).Parameters;
                            var ownName = member is SubDeclarationSyntax ownSubDeclarationName
                                ? ownSubDeclarationName.Identifier.Text
                                : ((FunctionDeclarationSyntax)member).Identifier.Text;
                            return ownName.Equals(methodName, StringComparison.OrdinalIgnoreCase) &&
                                ownParameters.Count == ps.Count &&
                                ownParameters.Zip(ps, (left, right) => left.IsByRef == right.IsByRef &&
                                    string.Equals(left.Type?.Identifier.Text, right.Type?.Identifier.Text, StringComparison.OrdinalIgnoreCase)).All(match => match);
                        }))
                        continue;
                    var rt = ownFunction is null ? typeof(void) : AstExperimentalCompiler.ResolveRuntimeType(ownFunction.ReturnType?.Identifier.Text);
                    var parameterTypes = ps.Select(p => AstExperimentalCompiler.ResolveParameterType(p, globals)).ToArray();
                    var symbol = new FunctionSymbol(methodName, rt, parameterTypes.Select((p, i) => ps[i].IsArray ? typeof(Array) : p.RuntimeType).ToArray(),
                        XpTypeSymbol.FromClr(rt), parameterTypes, ps.Select(p => p.IsByRef).ToArray());
                    classScope.Declare(symbol);
                    classScope.Declare(symbol with { Name = declaration.Identifier.Text + "." + methodName });
                }
            }
            var scope = classScope.CreateChildScope();
            var parameters = syntaxParameters.Select((p, i) =>
            {
                var semanticType = AstExperimentalCompiler.ResolveParameterType(p, globals);
                return new ParameterSymbol(p.Identifier.Text, p.IsArray ? typeof(Array) : semanticType.RuntimeType, p.IsByRef, semanticType)
            {
                IsBorrowedImageSnapshot = isObjectSetter && i == syntaxParameters.Count - 1 && p.IsByRef && AstImageSupport.IsImage(p.Type?.Identifier.Text)
            };
            }).ToArray();
            // Bind defaults in the instance declaration scope, exposing only earlier
            // indexes. Accessor locals and the incoming setter value are unavailable.
            var defaults = new BoundExpression?[parameters.Length];
            var defaultScope = classScope.CreateChildScope();
            for (var i = 0; i < parameters.Length; i++)
            {
                if (syntaxParameters[i].IsOptional && constructor is null)
                {
                    var binder = new ExpressionBinder(defaultScope);
                    var type = parameters[i].SemanticType ?? XpTypeSymbol.FromClr(parameters[i].Type);
                    var value = syntaxParameters[i].DefaultValue is { } syntax ? binder.Bind(syntax)
                        : new BoundLiteralExpression(type.IsArray || type.IsList ? null : parameters[i].Type == typeof(string) ? "" : Activator.CreateInstance(parameters[i].Type), parameters[i].Type, type);
                    if (binder.Diagnostics.Count > 0)
                        throw AstExperimentalCompiler.SyntaxDiagnosticException(binder.Diagnostics[0], sourcePath, source);
                    var conversion = defaultScope.ClassifyConversion(value.SemanticType, type);
                    if (!conversion.IsImplicit)
                        throw AstExperimentalCompiler.MappedDiagnostic(syntaxParameters[i].Span, sourcePath, source,
                            $"Default value for Optional parameter '{parameters[i].Name}' is incompatible with its type.", "XPS2001", "semantic");
                    defaults[i] = conversion.IsIdentity ? value : new BoundConversionExpression(value, type, conversion) { Span = value.Span };
                }
                defaultScope.Declare(parameters[i]);
            }
            foreach (var parameter in parameters) scope.Declare(parameter);
            var hasResult = function is not null || property?.IsGetter == true;
            var returnType = hasResult ? AstExperimentalCompiler.ResolveRuntimeType(resultTypeName) : typeof(void);
            var semanticResult = property?.IsGetter == true ? DeclaredType(property.Type, globals) :
                resultTypeName is not null && globals.LookupAll(resultTypeName).OfType<TypeSymbol>().Any()
                    ? XpTypeSymbol.User(resultTypeName) : XpTypeSymbol.FromClr(returnType);
            var result = hasResult ? new LocalSymbol("__xps_class_result", returnType, semanticResult) : null;
            var bound = AstExperimentalCompiler.BindProcedure(statements, scope, parameters,
                hasResult ? semanticResult : null, hasResult ? name : null, result,
                declaration.Identifier.Text + "." + name, sourcePath, source, source, allowDynamicConversions: false);
            var emittedName = constructor is not null ? "__xps_constructor_body" + AstClassConstructorSupport.Suffix(declaration, constructor) : isDestructor ? "__xps_destructor_body" : property is null ? name : "__xps_property_" + (property.IsGetter ? "get_" : "let_") + name;
            string body;
            try { body = new BoundMethodEmitter().Emit(emittedName, returnType, parameters, bound, false, source, sourcePath, semanticResult); }
            catch (BoundEmissionException ex) { throw AstExperimentalCompiler.MappedDiagnostic(ex.Span ?? member.Span, sourcePath, source, ex.Message); }
            foreach (var staticDeclaration in BoundStatementTraversal.Descendants(bound).OfType<BoundVariableDeclarationStatement>()
                         .Where(item => item.Local.StaticStorageName is not null))
            {
                var storageType = BoundMethodEmitter.SemanticStorageType(staticDeclaration.Local,
                    BoundMethodEmitter.CSharpType(staticDeclaration.Local.Type));
                var initialValue = staticDeclaration.Local.IsDynamicArray || staticDeclaration.Local.Type.IsArray
                    ? BoundArrayAllocation.Emit(staticDeclaration.Local, staticDeclaration.EmptyArray, staticDeclaration.ArrayLength)
                    : staticDeclaration.Local.SemanticType?.IsList == true ? $"new {storageType}()"
                    : staticDeclaration.Local.Type == typeof(string) ? "string.Empty" : $"default({storageType})";
                output.Append("private static ").Append(storageType).Append(' ').Append(staticDeclaration.Local.StaticStorageName)
                    .Append(" = ").Append(initialValue).Append(";\n");
                if (staticDeclaration.Initializer is not null)
                {
                    output.Append("private static int ").Append(staticDeclaration.Local.StaticStorageName).Append("_initState;\n");
                    output.Append("private static readonly object ").Append(staticDeclaration.Local.StaticStorageName).Append("_initGate = new object();\n");
                }
            }
            if (constructor is not null || isDestructor || property is not null || (sub?.Visibility ?? function?.Visibility)?.Kind == SyntaxKind.PrivateKeyword)
                body = "private " + body["public ".Length..];
            else if (property is null && constructor is null && !isDestructor)
            {
                var overrides = HasBaseMethod(declaration, sub, function, classes);
                body = overrides ? "public override " + body["public ".Length..] : "public virtual " + body["public ".Length..];
            }
            output.Append(body);
            if (property is null && constructor is null && !isDestructor)
            {
                for (var count = parameters.Length - 1; count >= 0 && syntaxParameters[count].IsOptional; count--)
                {
                    if (declaration.Members.Any(other => !ReferenceEquals(other, member) &&
                        other is FunctionDeclarationSyntax otherFunction && function is not null &&
                        otherFunction.Identifier.Text.Equals(function.Identifier.Text, StringComparison.OrdinalIgnoreCase) &&
                        SameParameterSignature(otherFunction.Parameters, syntaxParameters.Take(count))))
                        continue;
                    if (declaration.Members.Any(other => !ReferenceEquals(other, member) &&
                        other is SubDeclarationSyntax otherSub && sub is not null &&
                        otherSub.Identifier.Text.Equals(sub.Identifier.Text, StringComparison.OrdinalIgnoreCase) &&
                        SameParameterSignature(otherSub.Parameters, syntaxParameters.Take(count))))
                        continue;
                    var forward = OptionalCallLowering.Forward(emittedName, returnType, parameters, defaults, count,
                        semanticReturnType: semanticResult);
                    var forwardBody = new BoundMethodEmitter().Emit(emittedName, returnType, forward.Parameters, forward.Statements,
                        false, source, sourcePath, semanticResult, cloneImageParameters: false);
                    var forwardModifier = sub?.Visibility?.Kind == SyntaxKind.PrivateKeyword || function?.Visibility?.Kind == SyntaxKind.PrivateKeyword
                        ? "private "
                        : HasBaseMethod(declaration, sub, function, classes) ? "public override " : "public virtual ";
                    output.Append(forwardModifier)
                        .Append(forwardBody["public ".Length..]);
                }
            }
            if (property is not null)
            {
                var end = parameters.Length - (property.IsSetter ? 1 : 0);
                for (var count = end - 1; count >= 0 && syntaxParameters[count].IsOptional; count--)
                {
                    var forward = OptionalCallLowering.Forward(emittedName, returnType, parameters, defaults, count, property.IsSetter ? 1 : 0);
                    output.Append("private ").Append(new BoundMethodEmitter().Emit(emittedName, returnType,
                        forward.Parameters, forward.Statements, false, source, sourcePath, semanticResult, cloneImageParameters: false)["public ".Length..]);
                }
            }
        }
        foreach (var group in declaration.Members.OfType<PropertyDeclarationSyntax>().GroupBy(p => p.Identifier.Text, StringComparer.OrdinalIgnoreCase))
        {
            var getter = group.FirstOrDefault(p => p.IsGetter);
            var setter = group.FirstOrDefault(p => p.IsSetter);
            var name = group.First().Identifier.Text;
            var type = DeclaredType(getter?.Type ?? setter?.Type ?? setter?.Parameters.LastOrDefault()?.Type, globals);
            var indexes = getter?.Parameters ?? setter!.Parameters.Take(Math.Max(0, setter.Parameters.Count - 1)).ToArray();
            if (indexes.Count > 0)
            {
                foreach (var accessor in group)
                {
                    var required = indexes.TakeWhile(p => !p.IsOptional).Count();
                    for (var count = indexes.Count; count >= required; count--)
                    {
                        var supplied = accessor.Parameters.Take(count).Concat(accessor.IsSetter ? accessor.Parameters.TakeLast(1) : []).ToArray();
                        var arguments = string.Join(", ", supplied.Select(p => (p.IsByRef ? "ref " : "") + p.Identifier.Text));
                        var accessorModifier = accessor.Visibility?.Kind == SyntaxKind.PrivateKeyword ? "private "
                            : HasBaseProperty(declaration, name, classes) ? "public override " : "public virtual ";
                        output.Append(accessorModifier)
                            .Append(accessor.IsGetter ? BoundExpressionEmitter.RuntimeObjectType(type.Name) ?? BoundMethodEmitter.CSharpType(type) : "void")
                            .Append(accessor.IsGetter ? " __xps_index_get_" : " __xps_index_let_").Append(name).Append('(')
                            .Append(string.Join(", ", supplied.Select((p, i) => (i < count && p.IsExplicitByRef ? "ref " : "") + CSharpDeclaredType(p.Type, globals) + " " + p.Identifier.Text)))
                            .Append(") => __xps_property_").Append(accessor.IsGetter ? "get_" : "let_").Append(accessor.Identifier.Text)
                            .Append('(').Append(arguments).Append(");\n");
                    }
                }
                continue;
            }
            var publicProperty = group.Any(p => p.Visibility?.Kind != SyntaxKind.PrivateKeyword);
            var getterVisibility = publicProperty && getter?.Visibility?.Kind == SyntaxKind.PrivateKeyword ? "private " : "";
            var setterVisibility = publicProperty && setter?.Visibility?.Kind == SyntaxKind.PrivateKeyword ? "private " : "";
            var propertyModifier = publicProperty
                ? HasBaseProperty(declaration, name, classes) ? "public override " : "public virtual "
                : "private ";
            output.Append(propertyModifier).Append(BoundMethodEmitter.CSharpType(type))
                .Append(' ').Append(name).Append(" { ");
            if (getter is not null) output.Append(getterVisibility).Append("get => __xps_property_get_").Append(getter.Identifier.Text).Append("(); ");
            if (setter is not null) output.Append(setterVisibility).Append("set => __xps_property_let_").Append(setter.Identifier.Text)
                .Append(setter.Parameters.FirstOrDefault()?.IsByRef == true ? "(ref value); " : "(value); ");
            output.Append("}\n");
        }
        return output.ToString();
    }

    private static bool SameParameterSignature(IReadOnlyList<ParameterSyntax> left, IEnumerable<ParameterSyntax> right)
    {
        var values = right.ToArray();
        return left.Count == values.Length && left.Zip(values, (a, b) =>
            a.IsByRef == b.IsByRef && a.IsArray == b.IsArray && a.IsList == b.IsList &&
            string.Equals(a.Type?.Identifier.Text, b.Type?.Identifier.Text, StringComparison.OrdinalIgnoreCase)).All(match => match);
    }
}
