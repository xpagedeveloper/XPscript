namespace XPScript.Compiler.Binding;

public sealed class SymbolTable
{
    public SymbolTable(SymbolTable? parent = null)
    {
        Parent = parent;
    }

    public SymbolTable? Parent { get; }

    public SymbolTable CreateChildScope() => new(this);

    private readonly Dictionary<string, string?> _userClassBases = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a class edge after compilation-unit hierarchy validation.</summary>
    public void DeclareUserClassInheritance(string className, string? baseClassName) => _userClassBases[className] = baseClassName;

    private bool TryLookupUserClassBase(string name, out string? baseName)
    {
        if (_userClassBases.TryGetValue(name, out baseName)) return true;
        if (Parent is not null) return Parent.TryLookupUserClassBase(name, out baseName);
        return false;
    }

    public Conversion ClassifyConversion(XpTypeSymbol source, XpTypeSymbol target)
    {
        var conversion = Conversion.Classify(source, target);
        if (conversion.Exists || source.Shape != XpTypeShape.Scalar || target.Shape != XpTypeShape.Scalar ||
            source.RuntimeType != typeof(object) || target.RuntimeType != typeof(object) ||
            !TryLookupUserClassBase(target.Name, out _)) return conversion;

        // Only declared ancestry authorizes a user-reference upcast. The visited
        // set also bounds this public helper if handed a malformed registry.
        var current = source.Name;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (visited.Add(current) && TryLookupUserClassBase(current, out var parent) && parent is not null)
        {
            if (parent.Equals(target.Name, StringComparison.OrdinalIgnoreCase)) return new Conversion(ConversionKind.ReferenceUpcast);
            current = parent;
        }
        return conversion;
    }

    public static SymbolTable CreateWithCompilerCatalog()
    {
        var table = new SymbolTable();
        table.ImportCompilerCatalog();
        return table;
    }

    public void ImportCompilerCatalog()
    {
        foreach (var definition in CompilerSymbolCatalog.All)
        {
            if (AstApplicationSupport.OwnsSymbol(definition.Name)) continue;
            if (AstSpreadsheetSupport.Names.Any(n => definition.Name.Equals(n, StringComparison.OrdinalIgnoreCase) || definition.Name.StartsWith(n + ".", StringComparison.OrdinalIgnoreCase)) || AstHttpSupport.OwnsSymbol(definition.Name) || AstSystemInventorySupport.Names.Any(n => definition.Name.Equals(n, StringComparison.OrdinalIgnoreCase) || definition.Name.StartsWith(n + ".", StringComparison.OrdinalIgnoreCase)) || AstSqliteSupport.OwnsSymbol(definition.Name) || AstMySqlSupport.OwnsSymbol(definition.Name) || AstMsSqlSupport.OwnsSymbol(definition.Name) || AstSupabaseSupport.OwnsSymbol(definition.Name) || definition.Name.StartsWith("XPCsv", StringComparison.OrdinalIgnoreCase) || definition.Name.StartsWith("Csv", StringComparison.OrdinalIgnoreCase) ||
                definition.Name.StartsWith("XPXml", StringComparison.OrdinalIgnoreCase) || definition.Name.StartsWith("Xml", StringComparison.OrdinalIgnoreCase) || AstHttpDatabaseSupport.OwnsSymbol(definition.Name) || AstJsonValidationSupport.OwnsSymbol(definition.Name) || XpsShellResultAstSupport.OwnsSymbol(definition.Name) || AstPathValueSupport.OwnsSymbol(definition.Name))
            {
                if (definition.Kind == "class") Declare(new TypeSymbol(definition.Name, typeof(object), XpTypeSymbol.User(definition.Name)));
                continue;
            }
            if (definition.Name.StartsWith("XPImage.", StringComparison.OrdinalIgnoreCase))
                continue; // Import every native overload from the shared runtime below.
            if (definition.Kind.Equals("class", StringComparison.OrdinalIgnoreCase))
            {
                Declare(new TypeSymbol(definition.Name, typeof(object), XpTypeSymbol.User(definition.Name)));
                continue;
            }

            if (definition.Kind.Equals("property", StringComparison.OrdinalIgnoreCase))
            {
                var propertyType = definition.ReturnType ?? "Variant";
                Declare(new PropertySymbol(definition.Name, ResolveRuntimeType(propertyType), ResolveSemanticType(propertyType)));
                continue;
            }
            if (!definition.Kind.Equals("method", StringComparison.OrdinalIgnoreCase))
                continue;

            var parameterTypes = definition.Parameters.Select(parameter => ResolveRuntimeType(parameter.Type)).ToArray();
            var semanticParameterTypes = definition.Parameters.Select(parameter => ResolveSemanticType(parameter.Type)).ToArray();
            var returnType = ResolveRuntimeType(definition.ReturnType);
            var semanticReturnType = definition.ReturnType is null ? null : ResolveSemanticType(definition.ReturnType);
            Declare(new FunctionSymbol(definition.Name, returnType, parameterTypes, semanticReturnType, semanticParameterTypes));
        }
        AstImageSupport.Import(this);
        AstCsvSupport.Import(this);
        AstXmlSupport.Import(this);
        AstHttpSupport.Import(this);
        AstSpreadsheetSupport.Import(this);
        AstApplicationSupport.Import(this);
        AstNotesJsonSupport.Import(this);
        AstSystemInventorySupport.Import(this);
        AstSqliteSupport.Import(this);
        AstMySqlSupport.Import(this);
        AstMsSqlSupport.Import(this);
        AstSupabaseSupport.Import(this);
        AstHttpDatabaseSupport.Import(this);
        AstJsonValidationSupport.Import(this);
        XpsShellResultAstSupport.Import(this);
        AstPathValueSupport.Import(this);
        AstAttachmentSupport.Import(this);
        // NotesEmbeddedObject is emitted by the Notes runtime source, but its
        // surface is conditional and therefore is not present in the shared
        // compiler catalog. Keep the AST binder aligned with that generated
        // runtime surface.
        Declare(new PropertySymbol("NotesEmbeddedObject.FileCreated", typeof(object), XpTypeSymbol.User("NotesDateTime")));
        Declare(new VariableSymbol("XPScriptNotesConst.SEARCH_DEPTH", typeof(long), XpTypeSymbol.Variant));
        ImportNotesMimeOverloads();
        ImportXPAiOverloads();
    }

    private void ImportNotesMimeOverloads()
    {
        var entity = XpTypeSymbol.User("NotesMIMEEntity");
        var header = XpTypeSymbol.User("NotesMIMEHeader");
        Declare(new TypeSymbol("NotesMIMEEntity", typeof(object), entity));
        Declare(new TypeSymbol("NotesMIMEHeader", typeof(object), header));

        foreach (var property in new[]
        {
            "BoundaryStart", "BoundaryEnd", "Charset", "ContentAsText", "ContentID",
            "ContentLocation", "ContentSubType", "ContentType", "Encoding", "HeaderObjects",
            "Headers", "InputStream", "IsDiscretePart", "IsMessagePart", "IsMultipart",
            "Parent", "Preamble", "Reader"
        })
            Declare(new PropertySymbol("NotesMIMEEntity." + property, property is "BoundaryStart" or "BoundaryEnd" or "Charset" or "ContentAsText" or "ContentID" or "ContentLocation" or "ContentSubType" or "ContentType" or "Headers" or "Preamble" ? typeof(string) : typeof(object), property is "BoundaryStart" or "BoundaryEnd" or "Charset" or "ContentAsText" or "ContentID" or "ContentLocation" or "ContentSubType" or "ContentType" or "Headers" or "Preamble" ? XpTypeSymbol.FromClr(typeof(string)) : XpTypeSymbol.Variant));

        var entityMethods = new (string Name, int Arity)[]
        {
            ("AppendChildEntity", 1), ("CreateChildEntity", 0), ("CreateChildEntity", 1),
            ("CreateHeader", 1), ("CreateHeader", 2), ("DecodeContent", 0), ("EncodeContent", 1),
            ("GetChildren", 0), ("GetContentAsBytes", 1), ("GetContentAsText", 1),
            ("GetContentAsText", 2), ("GetEntityAsText", 1), ("GetFirstChildEntity", 0),
            ("GetHeaders", 0), ("GetHeaders", 1), ("GetInputStream", 0), ("GetInputStream", 1),
            ("GetNextEntity", 0), ("GetNextEntity", 1), ("GetNextSibling", 0),
            ("GetNthHeader", 1), ("GetNthHeader", 2), ("GetParentEntity", 0),
            ("GetPrevSibling", 0), ("GetSomeHeaders", 1), ("Remove", 0),
            ("RemoveChildEntity", 1), ("RemoveHeaders", 1), ("SetContentFromBytes", 3),
            ("SetContentFromText", 3)
        };
        foreach (var (method, arity) in entityMethods)
            Declare(new FunctionSymbol("NotesMIMEEntity." + method, method is "GetSomeHeaders" ? typeof(string) : typeof(object), Enumerable.Repeat(typeof(object), arity).ToArray(), method is "GetSomeHeaders" ? XpTypeSymbol.FromClr(typeof(string)) : XpTypeSymbol.Variant, Enumerable.Repeat(XpTypeSymbol.Variant, arity).ToArray()));

        foreach (var property in new[] { "HeaderName", "Parent" })
            Declare(new PropertySymbol("NotesMIMEHeader." + property, property == "HeaderName" ? typeof(string) : typeof(object), property == "HeaderName" ? XpTypeSymbol.FromClr(typeof(string)) : XpTypeSymbol.Variant));

        var headerMethods = new (string Name, int Arity)[]
        {
            ("AddValText", 1), ("GetHeaderVal", 0), ("GetHeaderValAndParams", 0),
            ("GetParamVal", 1), ("Remove", 0), ("SetHeaderVal", 1),
            ("SetHeaderValAndParams", 1), ("SetParamVal", 2)
        };
        foreach (var (method, arity) in headerMethods)
            Declare(new FunctionSymbol("NotesMIMEHeader." + method, method is "GetHeaderVal" or "GetHeaderValAndParams" or "GetParamVal" ? typeof(string) : typeof(object), Enumerable.Repeat(typeof(object), arity).ToArray(), method is "GetHeaderVal" or "GetHeaderValAndParams" or "GetParamVal" ? XpTypeSymbol.FromClr(typeof(string)) : XpTypeSymbol.Variant, Enumerable.Repeat(XpTypeSymbol.Variant, arity).ToArray()));
    }

    private void ImportXPAiOverloads()
    {
        var response = XpTypeSymbol.User("XPAiResponse");
        var messages = XpTypeSymbol.User("XPJsonDocument");
        var stringType = XpTypeSymbol.FromClr(typeof(string));
        Declare(new FunctionSymbol("XPAi.Complete", typeof(object), [typeof(object), typeof(string)], response, [messages, stringType]));
        Declare(new FunctionSymbol("XPAi.Stream", typeof(object), [typeof(object), typeof(string), typeof(string)], response, [messages, stringType, stringType]));
        Declare(new FunctionSymbol("XPAi.StreamCallback", typeof(object), [typeof(string), typeof(object)], response, [stringType, XpTypeSymbol.Variant]));
    }

    private static Type ResolveRuntimeType(string? name) => name?.Trim().ToUpperInvariant() switch
    {
        "BOOLEAN" => typeof(bool),
        "STRING" => typeof(string),
        "INTEGER" or "LONG" => typeof(long),
        "SINGLE" or "DOUBLE" or "CURRENCY" => typeof(double),
        _ => typeof(object)
    };

    private static XpTypeSymbol ResolveSemanticType(string name)
    {
        var runtimeType = ResolveRuntimeType(name);
        return runtimeType == typeof(object) ? XpTypeSymbol.User(name) : XpTypeSymbol.FromClr(runtimeType);
    }

    private readonly Dictionary<string, List<Symbol>> _symbols = new(StringComparer.OrdinalIgnoreCase);

    public bool TryDeclare(Symbol symbol) =>
        TryDeclare(symbol, out _, out _);

    public bool TryDeclare(Symbol symbol, out string? diagnosticCode, out string? diagnosticMessage)
    {
        if (_symbols.TryGetValue(symbol.Name, out var existing) && existing.Count > 0)
        {
            var duplicate = symbol is LocalSymbol
                ? existing.OfType<LocalSymbol>().Any()
                : symbol is FunctionSymbol function
                ? existing.OfType<FunctionSymbol>().Any(candidate => SameParameterSignature(candidate, function))
                : true;

            if (duplicate)
            {
                diagnosticCode = CompilerDiagnosticCodes.DuplicateOverload;
                diagnosticMessage = $"Symbol '{symbol.Name}' is already declared with the same parameter signature in this scope.";
                return false;
            }
        }

        Declare(symbol);
        diagnosticCode = null;
        diagnosticMessage = null;
        return true;
    }

    private static bool SameParameterSignature(FunctionSymbol left, FunctionSymbol right)
    {
        if (!left.ParameterTypes.SequenceEqual(right.ParameterTypes))
            return false;

        if (left.SemanticParameterTypes is null || right.SemanticParameterTypes is null)
            return true;

        if (left.SemanticParameterTypes.Count != right.SemanticParameterTypes.Count)
            return false;

        return left.SemanticParameterTypes
            .Select(type => type.Name)
            .SequenceEqual(right.SemanticParameterTypes.Select(type => type.Name), StringComparer.OrdinalIgnoreCase);
    }

    public void Declare(Symbol symbol)
    {
        if (!_symbols.TryGetValue(symbol.Name, out var symbols))
        {
            symbols = [];
            _symbols[symbol.Name] = symbols;
        }

        if (symbol is FunctionSymbol)
            symbols.Add(symbol);
        else
        {
            // A local/field may legally shadow a callable name in XPscript
            // (for example `Dim files As Variant` alongside `Files(...)`).
            // Keep overload symbols available for call binding while the
            // ordinary name lookup still resolves to the newest value symbol.
            symbols.RemoveAll(existing => existing is not FunctionSymbol);
            symbols.Add(symbol);
        }
    }

    public bool TryLookup(string name, out Symbol symbol)
    {
        if (_symbols.TryGetValue(name, out var symbols) && symbols.Count > 0)
        {
            symbol = symbols[^1];
            return true;
        }

        if (Parent is not null)
            return Parent.TryLookup(name, out symbol);

        symbol = null!;
        return false;
    }

    public IReadOnlyList<Symbol> LookupAll(string name)
    {
        if (_symbols.TryGetValue(name, out var symbols) && symbols.Count > 0)
            return symbols;
        return Parent?.LookupAll(name) ?? [];
    }

    public IReadOnlyList<Symbol> LookupMembers(XpTypeSymbol receiverType, string memberName) =>
        LookupAll(MemberKey(receiverType.Name, memberName));

    public bool TryLookupMember(XpTypeSymbol receiverType, string memberName, out Symbol symbol)
    {
        var members = LookupMembers(receiverType, memberName);
        if (members.Count > 0)
        {
            symbol = members[^1];
            return true;
        }

        symbol = null!;
        return false;
    }

    private static string MemberKey(string typeName, string memberName) =>
        typeName + "." + memberName;
}
