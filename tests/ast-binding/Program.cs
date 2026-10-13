using XPScript.Compiler.Binding;
using XPScript.Compiler.Emission;
using XPScript.Compiler.Syntax;
using XPScript.Compiler;

static ExpressionSyntax Parse(string text)
{
    var parser = new ExpressionParser(text);
    var syntax = parser.ParseExpression();
    if (parser.Diagnostics.Count != 0)
        throw new InvalidOperationException($"Focused binder input '{text}' must parse without diagnostics.");
    return syntax;
}

// Type diagnostics run first so semantic type regressions fail before the broader binder probe.
foreach (var text in new[] { "Application.Secrets.Get(\"service\", \"account\")", "Application.Secrets.Set(\"service\", \"account\", \"test value\")" })
{
    var appBinder = new ExpressionBinder(SymbolTable.CreateWithCompilerCatalog(), allowDynamicMembers: true);
    var secretCall = appBinder.Bind(Parse(text));
    var generatedSecretCall = new BoundExpressionEmitter().Emit(secretCall);
    if (appBinder.Diagnostics.Count != 0 || !generatedSecretCall.StartsWith("XPScriptApplicationSecretsEncryptionRuntime.", StringComparison.Ordinal))
        throw new Exception("Secrets bypassed the shared encryption/password guard: " + text);
}
foreach (var text in new[] { "Application.ArgCount", "Application.Args", "Application.Args(0)", "Application.State.Get(\"key\")", "Application.Registry.User.Get(\"path\", \"name\")", "Application.Crypto.GenerateKey()", "Application.Debug.Enabled", "Application.Executable.Product" })
{
    var appBinder = new ExpressionBinder(SymbolTable.CreateWithCompilerCatalog(), allowDynamicMembers: true);
    _ = appBinder.Bind(Parse(text));
    if (appBinder.Diagnostics.Count != 0) throw new Exception("Application binding failed: " + text);
}
foreach (var text in new[] { "Application.UnknownMember", "Application.SetArgs(\"hidden\")", "Application.State.Set(\"missing\")", "New Application", "Application.Debug.UnknownMember" })
{
    var appBinder = new ExpressionBinder(SymbolTable.CreateWithCompilerCatalog(), allowDynamicMembers: true);
    _ = appBinder.Bind(Parse(text));
    if (appBinder.Diagnostics.Count != 1) throw new Exception("Invalid Application escaped binding: " + text);
}
Console.WriteLine("AST_BINDING_APPLICATION_NATIVE_OK");
var sheetSymbols = SymbolTable.CreateWithCompilerCatalog();
sheetSymbols.Declare(new LocalSymbol("book", typeof(object), XpTypeSymbol.User("XPSpreadsheet")));
sheetSymbols.Declare(new LocalSymbol("sheet", typeof(object), XpTypeSymbol.User("XPWorksheet")));
sheetSymbols.Declare(new LocalSymbol("csv", typeof(object), XpTypeSymbol.User("XPCsvDocument")));
foreach (var text in new[] { "New XPSpreadsheet", "New XPSpreadsheet(\"file.xlsx\")", "book.AddWorksheet()", "book.AddWorksheet(\"Data\")",
    "book.Worksheet(1).Cell(2, 3).Value", "sheet.Range(\"A1:C2\").FontSize", "sheet.ToCsv()", "sheet.ToCsv(False)",
    "csv.ToSpreadsheet()", "csv.ToSpreadsheet(\"Data\")", "XPCsvDocument.FromWorksheet(sheet)" })
{
    var binder = new ExpressionBinder(sheetSymbols, allowDynamicMembers: true);
    _ = binder.Bind(Parse(text));
    if (binder.Diagnostics.Count != 0) throw new Exception("Native spreadsheet signature failed: " + text);
}
foreach (var text in new[] { "New XPWorksheet", "New XPCell", "New XPRange", "book.UnknownMember", "book.Worksheet()", "XPSpreadsheet.Worksheet(1)" })
{
    var binder = new ExpressionBinder(sheetSymbols, allowDynamicMembers: true);
    _ = binder.Bind(Parse(text));
    if (binder.Diagnostics.Count != 1) throw new Exception("Invalid spreadsheet signature escaped binding: " + text);
}
foreach (var definition in CompilerSymbolCatalog.All.Where(d => d.Name.StartsWith("XPSpreadsheet") || d.Name.StartsWith("XPWorksheet") || d.Name.StartsWith("XPCell") || d.Name.StartsWith("XPRange")))
    if (CompilerDocumentationCatalog.Find(definition.DocumentationId) is null) throw new Exception("Spreadsheet API documentation missing.");
Console.WriteLine("AST_BINDING_SPREADSHEET_NATIVE_OK");
var httpSymbols = SymbolTable.CreateWithCompilerCatalog();
httpSymbols.Declare(new LocalSymbol("client", typeof(object), XpTypeSymbol.User("XPHttpClient")));
httpSymbols.Declare(new LocalSymbol("response", typeof(object), XpTypeSymbol.User("XPHttpResponse")));
foreach (var text in new[] { "New XPHttpClient", "New XPHttpRequest", "client.Timeout", "client.Get(\"https://example.invalid\")",
    "client.Post(\"https://example.invalid\", \"body\")", "client.Send(\"HEAD\", \"https://example.invalid\")",
    "client.PostJson(\"https://example.invalid\", 1)", "response.Json()", "response.Headers[\"X-Test\"]", "response.StatusCode" })
{
    var binder = new ExpressionBinder(httpSymbols, allowDynamicMembers: true);
    _ = binder.Bind(Parse(text));
    if (binder.Diagnostics.Count != 0) throw new Exception("Native HTTP signature failed: " + text);
}
foreach (var text in new[] { "New XPHttpResponse", "client.Get()", "client.Post(\"url\")", "XPHttpClient.Get(\"url\")", "client.UnknownMember" })
{
    var binder = new ExpressionBinder(httpSymbols, allowDynamicMembers: true);
    _ = binder.Bind(Parse(text));
    if (binder.Diagnostics.Count != 1) throw new Exception("Invalid HTTP signature escaped binding: " + text);
}
Console.WriteLine("AST_BINDING_HTTP_NATIVE_OK");
var xmlSymbols = SymbolTable.CreateWithCompilerCatalog();
xmlSymbols.Declare(new LocalSymbol("document", typeof(object), XpTypeSymbol.User("XPXmlDocument")));
xmlSymbols.Declare(new LocalSymbol("element", typeof(object), XpTypeSymbol.User("XPXmlElement")));
xmlSymbols.Declare(new LocalSymbol("node", typeof(object), XpTypeSymbol.User("XPXmlNode")));
foreach (var text in new[] { "New XPXmlDocument", "New XPXmlDocument(\"<root />\")", "New XPXmlElement(\"root\")",
    "XPXmlDocument.Parse(\"<root />\")", "XmlParse(\"<root />\")", "XmlStringify(document)", "XmlEscape(\"<&>\")",
    "document.AddRoot(\"root\")", "document.AddRoot(\"root\", \"value\")", "element.Parent.Name", "element.Clone()",
    "element.AddElement(\"child\")", "element.AddElement(\"child\", 1)", "document.Root.Attributes[\"id\"].Value",
    "node.GetAttribute(\"id\")", "document.ValidateDTD(\"<!ELEMENT root EMPTY>\").Errors.Get(0).Message" })
{
    var binder = new ExpressionBinder(xmlSymbols, allowDynamicMembers: true);
    _ = binder.Bind(Parse(text));
    if (binder.Diagnostics.Count != 0) throw new InvalidOperationException("Native XML failed to bind: " + text);
}
foreach (var text in new[] { "New XPXmlNode", "New XPXmlElement", "New XPXmlDocument(1, 2)", "document.NoSuchMember",
    "document.LoadXml()", "document.Parse(\"<root />\")", "XPXmlDocument.HasRoot", "XPXmlDocument.AddRoot(\"root\")", "node.UnknownMethod()" })
{
    var binder = new ExpressionBinder(xmlSymbols, allowDynamicMembers: true);
    _ = binder.Bind(Parse(text));
    if (binder.Diagnostics.Count != 1) throw new InvalidOperationException("Invalid XML signature escaped binding: " + text);
}
if (!Conversion.Classify(XpTypeSymbol.User("XPXmlElement"), XpTypeSymbol.User("XPXmlNode")).IsImplicit ||
    Conversion.Classify(XpTypeSymbol.User("XPXmlNode"), XpTypeSymbol.User("XPXmlElement")).IsImplicit)
    throw new InvalidOperationException("XML element/node inheritance conversion lost its direction.");
foreach (var definition in CompilerSymbolCatalog.All.Where(d => d.Name.StartsWith("XPXml", StringComparison.Ordinal) || d.Name.StartsWith("Xml", StringComparison.Ordinal)))
    if (CompilerDocumentationCatalog.Find(definition.DocumentationId) is null) throw new InvalidOperationException("XML API documentation missing: " + definition.Name);
Console.WriteLine("AST_BINDING_XML_NATIVE_OK");
var csvSymbols = SymbolTable.CreateWithCompilerCatalog();
csvSymbols.Declare(new LocalSymbol("data", typeof(object), XpTypeSymbol.User("XPCsvDocument")));
foreach (var text in new[] { "New XPCsvDocument", "New XPCsvDocument(\"a;b\", \";\")", "CsvParse(\"a\")", "data.Headers[0]",
    "data.Rows[0].Columns[1].Value", "data.Rows[0][\"name\"]", "data.Headers.Add(\"name\")", "data.FromBytes(Array(1))",
    "XPCsvDocument.Load(\"data.csv\", \"auto\", \";\", True)", "CsvEscape(\"a;b\", \";\")" })
{
    var binder = new ExpressionBinder(csvSymbols, allowDynamicMembers: true);
    _ = binder.Bind(Parse(text));
    if (binder.Diagnostics.Count != 0) throw new InvalidOperationException("Native CSV failed to bind: " + text);
}
foreach (var text in new[] { "New XPCsvRow", "data.NoSuchMember", "data.Sort()", "XPCsvDocument.AddRow()", "data.Load(\"data.csv\")", "data.Save()" })
{
    var binder = new ExpressionBinder(csvSymbols, allowDynamicMembers: true);
    _ = binder.Bind(Parse(text));
    if (binder.Diagnostics.Count != 1) throw new InvalidOperationException("Invalid CSV signature escaped binding: " + text);
}
foreach (var definition in CompilerSymbolCatalog.All.Where(d => d.Name.StartsWith("XPCsv", StringComparison.Ordinal) || d.Name.StartsWith("Csv", StringComparison.Ordinal)))
    if (CompilerDocumentationCatalog.Find(definition.DocumentationId) is null) throw new InvalidOperationException("CSV API documentation missing: " + definition.Name);
var csvRoute = typeof(CompilerSymbolCatalog).Assembly.GetType("XPScript.Compiler.AstObjectSupportRegistry")!
    .GetMethod("UsesUnsupportedObject")!;
foreach (var (source, legacy) in new[]
{
    ("Dim document As XPCsvDocument\nSet result = document.ToSpreadsheet()", false),
    ("Dim document As XPCsvDocument\nSet document = XPCsvDocument.FromWorksheet(sheet)", false),
    ("Dim document As XPCsvDocument\nPrint document.ToString()", false),
    ("Print \"XPCsvDocument.ToSpreadsheet()\"\nRem XPCsvDocument.FromWorksheet(sheet)", false)
})
{
    object?[] arguments = [source, null];
    if ((bool)csvRoute.Invoke(null, arguments)! != legacy)
        throw new InvalidOperationException("CSV spreadsheet interop compilation route drifted: " + source);
}
Console.WriteLine("AST_BINDING_CSV_NATIVE_OK");
var imageSymbols = SymbolTable.CreateWithCompilerCatalog();
imageSymbols.Declare(new LocalSymbol("image", typeof(object), XpTypeSymbol.User("XPImage")));
foreach (var text in new[] { "New XPImage(2, 3)", "New XPImage(2, 3, \"#123456\")", "image.width", "image.Resize(2)",
    "image.Resize(2, 3)", "image.Resize(2, 3, \"stretch\")", "image.Histogram()", "image.Histogram(1)",
    "XPImage.Load(\"picture.png\")", "xpimage.frombase64(\"data\")", "image.ToBytes(\"png\", 90)" })
{
    var binder = new ExpressionBinder(imageSymbols, allowDynamicMembers: true);
    _ = binder.Bind(Parse(text));
    if (binder.Diagnostics.Count != 0) throw new InvalidOperationException("Native image signature failed to bind: " + text);
}
foreach (var text in new[] { "New XPImage(2)", "New XPImage(\"wrong\", 2)", "image.Resize(1, 2, 3)",
    "image.Missing", "XPImage.Width", "XPImage.Clone()", "image.Load(\"picture.png\")" })
{
    var binder = new ExpressionBinder(imageSymbols, allowDynamicMembers: true);
    _ = binder.Bind(Parse(text));
    if (binder.Diagnostics.Count != 1) throw new InvalidOperationException("Invalid native image signature escaped binding: " + text);
}
foreach (var symbol in CompilerSymbolCatalog.All.Where(s => s.Name.StartsWith("XPImage.", StringComparison.Ordinal)))
    if (CompilerDocumentationCatalog.Find(symbol.DocumentationId) is null)
        throw new InvalidOperationException("Image API documentation is missing: " + symbol.Name);
Console.WriteLine("AST_BINDING_XPIMAGE_NATIVE_OK");
var receiverSymbols = new SymbolTable();
receiverSymbols.Declare(new VariableSymbol("widget", typeof(object), XpTypeSymbol.User("Widget")));
receiverSymbols.Declare(new VariableSymbol("variant", typeof(object), XpTypeSymbol.Variant));
receiverSymbols.Declare(new PropertySymbol("Widget.Title", typeof(string)));
receiverSymbols.Declare(new FunctionSymbol("Widget.Describe", typeof(string), []));
foreach (var text in new[] { "widget.Title", "widget.Describe()" })
{
    var binder = new ExpressionBinder(receiverSymbols, allowDynamicMembers: true);
    var bound = binder.Bind(Parse(text));
    if (binder.Diagnostics.Count != 0 || bound.Type != typeof(string))
        throw new InvalidOperationException("Declared user members must retain their bound types.");
}
foreach (var text in new[] { "widget.Missing", "widget.Missing()" })
{
    var binder = new ExpressionBinder(receiverSymbols, allowDynamicMembers: true);
    _ = binder.Bind(Parse(text));
    if (binder.Diagnostics.Count != 1 || binder.Diagnostics[0].Code != "XPS2009" ||
        binder.Diagnostics[0].Span != new TextSpan(text.IndexOf("Missing", StringComparison.Ordinal), 7))
        throw new InvalidOperationException("Object-backed user types must report unknown members before Roslyn.");
}
foreach (var text in new[] { "variant.Missing", "variant.Missing()" })
{
    var binder = new ExpressionBinder(receiverSymbols, allowDynamicMembers: true);
    _ = binder.Bind(Parse(text));
    if (binder.Diagnostics.Count != 0)
        throw new InvalidOperationException("Variant receivers must retain dynamic member access and calls.");
}
Console.WriteLine("AST_BINDING_RECEIVER_DIAGNOSTICS_OK");

var declaredCallSymbols = new SymbolTable();
declaredCallSymbols.Declare(new FunctionSymbol("Required", typeof(void), [typeof(long)], ByRefParameters: [false]));
var declaredCallBinder = new ExpressionBinder(declaredCallSymbols, allowDynamicMembers: true);
_ = declaredCallBinder.Bind(Parse("Required()"));
if (declaredCallBinder.Diagnostics.Count != 1 || declaredCallBinder.Diagnostics[0].Code != "XPS2004")
    throw new InvalidOperationException("Dynamic member support must not hide a declared procedure arity error.");
declaredCallSymbols.Declare(new FunctionSymbol("RequiredRef", typeof(void), [typeof(long)], ByRefParameters: [true]));
foreach (var text in new[] { "Required(\"wrong\")", "RequiredRef(1)" })
{
    var binder = new ExpressionBinder(declaredCallSymbols, allowDynamicMembers: true);
    var syntax = Parse(text);
    _ = binder.Bind(syntax);
    if (binder.Diagnostics.Count != 1 || binder.Diagnostics[0].Code != "XPS2004" || binder.Diagnostics[0].Span != syntax.Span)
        throw new InvalidOperationException("Declared argument type/mode errors must preserve the call span.");
}
declaredCallSymbols.Declare(new VariableSymbol("dynamicValue", typeof(object), XpTypeSymbol.Variant));
var dynamicCallBinder = new ExpressionBinder(declaredCallSymbols, allowDynamicMembers: true);
var dynamicCall = dynamicCallBinder.Bind(Parse("dynamicValue.Render(1)"));
if (dynamicCallBinder.Diagnostics.Count != 0 || dynamicCall is not BoundCallExpression)
    throw new InvalidOperationException("An unknown member on a Variant must retain dynamic call binding.");
Console.WriteLine("AST_BINDING_DECLARED_CALL_DIAGNOSTICS_OK");

var invalidUnaryBinder = new ExpressionBinder();
_ = invalidUnaryBinder.Bind(Parse("Not 1"));
var invalidUnaryResult = invalidUnaryBinder.Bind(Parse("Not 1"));
if (invalidUnaryBinder.Diagnostics.Count != 0 || invalidUnaryResult.Type != typeof(long))
    throw new InvalidOperationException("Numeric Not must bind as a supported bitwise long operation.");

var invalidBinaryBinder = new ExpressionBinder();
_ = invalidBinaryBinder.Bind(Parse("True + False"));
if (invalidBinaryBinder.Diagnostics.Count != 1 || invalidBinaryBinder.Diagnostics[0].Code != "XPS2001")
    throw new InvalidOperationException("Invalid binary operands must produce exactly one XPS2001 diagnostic.");

var indexSymbols = new SymbolTable();
indexSymbols.Declare(new VariableSymbol("scalar", typeof(long)));
indexSymbols.Declare(new VariableSymbol("values", typeof(string[]), XpTypeSymbol.ArrayOf(XpTypeSymbol.FromClr(typeof(string)))));

var scalarIndexBinder = new ExpressionBinder(indexSymbols);
_ = scalarIndexBinder.Bind(Parse("scalar[0]"));
if (scalarIndexBinder.Diagnostics.Count != 1 || scalarIndexBinder.Diagnostics[0].Code != "XPS2001")
    throw new InvalidOperationException("Indexing a scalar must produce exactly one XPS2001 diagnostic.");

var wrongIndexTypeBinder = new ExpressionBinder(indexSymbols);
_ = wrongIndexTypeBinder.Bind(Parse("values[\"wrong\"]"));
if (wrongIndexTypeBinder.Diagnostics.Count != 1 || wrongIndexTypeBinder.Diagnostics[0].Code != "XPS2003")
    throw new InvalidOperationException("A non-integer array index must produce exactly one XPS2003 diagnostic.");

Console.WriteLine("AST_BINDING_TYPE_DIAGNOSTICS_OK");

// Collection typing runs first because failures here should be isolated before the broader binder probe.
var stringType = XpTypeSymbol.FromClr(typeof(string));
var stringArrayType = XpTypeSymbol.ArrayOf(stringType);
if (!stringArrayType.IsArray || stringArrayType.IsList || stringArrayType.ElementType != stringType || stringArrayType.RuntimeType != typeof(string[]))
    throw new InvalidOperationException("XPscript arrays must retain their explicit array shape and element type.");

var clrStringArrayType = XpTypeSymbol.FromClr(typeof(string[]));
if (!clrStringArrayType.IsArray || clrStringArrayType.ElementType != stringType)
    throw new InvalidOperationException("CLR array types must map to the same XPscript array semantic shape.");

var variantArrayType = XpTypeSymbol.ArrayOf(XpTypeSymbol.Variant);
if (!variantArrayType.IsArray || variantArrayType.IsVariant || variantArrayType.ElementType != XpTypeSymbol.Variant)
    throw new InvalidOperationException("Variant() must be an array of Variant, not a scalar Variant.");

var stringListType = XpTypeSymbol.ListOf(stringType);
if (!stringListType.IsList || stringListType.IsArray || stringListType.ElementType != stringType || stringListType.RuntimeType != typeof(object))
    throw new InvalidOperationException("XPscript List must retain a distinct list shape and element type.");

var variantListType = XpTypeSymbol.ListOf(XpTypeSymbol.Variant);
if (!variantListType.IsList || variantListType.IsVariant || variantListType.ElementType != XpTypeSymbol.Variant)
    throw new InvalidOperationException("List As Variant must remain distinct from scalar Variant and Variant().");

if (stringArrayType == stringListType)
    throw new InvalidOperationException("Array and List semantic types must never collapse to the same XPscript type.");

Console.WriteLine("AST_BINDING_COLLECTION_TYPES_OK");

var unknownBinder = new ExpressionBinder();
_ = unknownBinder.Bind(Parse("missingName"));
if (unknownBinder.Diagnostics.Count != 1)
    throw new InvalidOperationException($"Expected one binder diagnostic, got {unknownBinder.Diagnostics.Count}.");

var unknownDiagnostic = unknownBinder.Diagnostics[0];
if (!string.Equals(unknownDiagnostic.Code, "XPS2008", StringComparison.Ordinal))
    throw new InvalidOperationException($"Expected XPS2008, got {unknownDiagnostic.Code}.");
if (!unknownDiagnostic.Message.Contains("missingName", StringComparison.Ordinal))
    throw new InvalidOperationException($"Unknown-symbol diagnostic did not identify missingName: {unknownDiagnostic.Message}");

Console.WriteLine("AST_BINDING_UNKNOWN_SYMBOL_OK");

var symbols = new SymbolTable();
var widgetType = XpTypeSymbol.User("Widget");
symbols.Declare(new VariableSymbol("widget", typeof(object), widgetType));
symbols.Declare(new PropertySymbol("Widget.Title", typeof(string), XpTypeSymbol.FromClr(typeof(string))));

var memberBinder = new ExpressionBinder(symbols);
var member = memberBinder.Bind(Parse("widget.Title"));
if (memberBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException($"Expected member lookup to succeed, got: {string.Join("; ", memberBinder.Diagnostics.Select(d => d.Message))}.");
if (member is not BoundMemberAccessExpression || member.Type != typeof(string))
    throw new InvalidOperationException("Member lookup did not bind Widget.Title as a string property.");

Console.WriteLine("AST_BINDING_MEMBER_LOOKUP_OK");

var missingMemberBinder = new ExpressionBinder(symbols);
_ = missingMemberBinder.Bind(Parse("widget.Missing"));
if (missingMemberBinder.Diagnostics.Count != 1 || missingMemberBinder.Diagnostics[0].Code != "XPS2009")
    throw new InvalidOperationException("Missing member must produce exactly one XPS2009 diagnostic.");

Console.WriteLine("AST_BINDING_UNKNOWN_MEMBER_OK");

var overloadSymbols = new SymbolTable();
overloadSymbols.Declare(new FunctionSymbol("Pick", typeof(string), [typeof(long)]));
overloadSymbols.Declare(new FunctionSymbol("Pick", typeof(long), [typeof(string)]));

var overloadBinder = new ExpressionBinder(overloadSymbols);
var overloadCall = overloadBinder.Bind(Parse("Pick(1)"));
if (overloadBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException($"Expected exact overload binding to succeed, got: {string.Join("; ", overloadBinder.Diagnostics.Select(d => d.Message))}.");
if (overloadCall is not BoundCallExpression boundCall || boundCall.Function.ParameterTypes.Count != 1 || boundCall.Function.ParameterTypes[0] != typeof(long))
    throw new InvalidOperationException("Call binding did not select the exact Integer/Long overload.");

Console.WriteLine("AST_BINDING_OVERLOAD_OK");

var noMatchBinder = new ExpressionBinder(overloadSymbols);
_ = noMatchBinder.Bind(Parse("Pick(True)"));
if (noMatchBinder.Diagnostics.Count != 1 || noMatchBinder.Diagnostics[0].Code != "XPS2004")
    throw new InvalidOperationException("No matching overload must produce exactly one XPS2004 diagnostic.");

Console.WriteLine("AST_BINDING_NO_MATCHING_OVERLOAD_OK");

var ambiguousSymbols = new SymbolTable();
ambiguousSymbols.Declare(new FunctionSymbol("Choose", typeof(long), [typeof(long)]));
ambiguousSymbols.Declare(new FunctionSymbol("Choose", typeof(string), [typeof(long)]));

var ambiguousBinder = new ExpressionBinder(ambiguousSymbols);
_ = ambiguousBinder.Bind(Parse("Choose(1)"));
if (ambiguousBinder.Diagnostics.Count != 1 || ambiguousBinder.Diagnostics[0].Code != "XPS2005")
    throw new InvalidOperationException("Ambiguous overload must produce exactly one XPS2005 diagnostic.");

Console.WriteLine("AST_BINDING_AMBIGUOUS_OVERLOAD_OK");

var declarationSymbols = new SymbolTable();
if (!declarationSymbols.TryDeclare(new FunctionSymbol("Format", typeof(string), [typeof(long)])))
    throw new InvalidOperationException("First overload declaration must succeed.");
if (!declarationSymbols.TryDeclare(new FunctionSymbol("Format", typeof(string), [typeof(string)])))
    throw new InvalidOperationException("Same-name overload with a different parameter type must succeed.");
if (!declarationSymbols.TryDeclare(new FunctionSymbol("Format", typeof(string), [typeof(long), typeof(string)])))
    throw new InvalidOperationException("Same-name overload with a different parameter count must succeed.");
if (declarationSymbols.TryDeclare(new FunctionSymbol("Format", typeof(long), [typeof(long)]), out var duplicateCode, out _))
    throw new InvalidOperationException("Return type alone must not create a distinct overload.");
if (duplicateCode != "XPS2006")
    throw new InvalidOperationException($"Duplicate parameter signature must produce XPS2006, got {duplicateCode}.");

Console.WriteLine("AST_BINDING_OVERLOAD_DECLARATION_RULES_OK");

var byRefSymbols = new SymbolTable();
byRefSymbols.Declare(new VariableSymbol("value", typeof(long)));
byRefSymbols.Declare(new FunctionSymbol("Touch", typeof(long), [typeof(long)], ByRefParameters: [true]));
byRefSymbols.Declare(new FunctionSymbol("Read", typeof(long), [typeof(long)], ByRefParameters: [false]));

var byRefVariableBinder = new ExpressionBinder(byRefSymbols);
_ = byRefVariableBinder.Bind(Parse("Touch(value)"));
if (byRefVariableBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException("A variable argument must be valid for a ByRef parameter.");

var byRefLiteralBinder = new ExpressionBinder(byRefSymbols);
_ = byRefLiteralBinder.Bind(Parse("Touch(1)"));
if (byRefLiteralBinder.Diagnostics.Count != 1 || byRefLiteralBinder.Diagnostics[0].Code != "XPS2004")
    throw new InvalidOperationException("A literal argument must not match a ByRef overload.");

var byValLiteralBinder = new ExpressionBinder(byRefSymbols);
_ = byValLiteralBinder.Bind(Parse("Read(1)"));
if (byValLiteralBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException("A literal argument must be valid for a ByVal parameter.");

var modeOverloads = new SymbolTable();
if (!modeOverloads.TryDeclare(new FunctionSymbol("Mode", typeof(long), [typeof(long)], ByRefParameters: [true])))
    throw new InvalidOperationException("ByRef overload declaration must succeed.");
if (modeOverloads.TryDeclare(new FunctionSymbol("Mode", typeof(long), [typeof(long)], ByRefParameters: [false]), out var modeDuplicateCode, out _))
    throw new InvalidOperationException("ByRef/ByVal alone must not create a distinct overload.");
if (modeDuplicateCode != "XPS2006")
    throw new InvalidOperationException($"Duplicate overload differing only by ByRef/ByVal must produce XPS2006, got {modeDuplicateCode}.");

var defaultParameter = new ParameterSymbol("defaultByRef", typeof(long));
if (!defaultParameter.IsByRef)
    throw new InvalidOperationException("Parameters must be ByRef by default.");

var explicitByValParameter = new ParameterSymbol("explicitByVal", typeof(long), IsByRef: false);
if (explicitByValParameter.IsByRef)
    throw new InvalidOperationException("Explicit ByVal parameters must not be ByRef.");

Console.WriteLine("AST_BINDING_BYREF_BYVAL_OK");


var assignmentSymbols = new SymbolTable();
assignmentSymbols.Declare(new VariableSymbol("number", typeof(long)));
assignmentSymbols.Declare(new VariableSymbol("text", typeof(string)));

var compatibleAssignmentParser = new StatementParser("number = 1");
var compatibleAssignment = compatibleAssignmentParser.ParseStatement();
if (compatibleAssignmentParser.Diagnostics.Count != 0)
    throw new InvalidOperationException("Compatible assignment fixture must parse without diagnostics.");
var compatibleAssignmentBinder = new StatementBinder(assignmentSymbols);
compatibleAssignmentBinder.Bind(compatibleAssignment);
if (compatibleAssignmentBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException("Assignment with matching types must bind without diagnostics.");

var incompatibleAssignmentParser = new StatementParser("number = \"wrong\"");
var incompatibleAssignment = incompatibleAssignmentParser.ParseStatement();
if (incompatibleAssignmentParser.Diagnostics.Count != 0)
    throw new InvalidOperationException("Incompatible assignment fixture must parse without diagnostics.");
var incompatibleAssignmentBinder = new StatementBinder(assignmentSymbols);
incompatibleAssignmentBinder.Bind(incompatibleAssignment);
if (incompatibleAssignmentBinder.Diagnostics.Count != 1 || incompatibleAssignmentBinder.Diagnostics[0].Code != "XPS2001")
    throw new InvalidOperationException("Assignment with incompatible types must produce exactly one XPS2001 diagnostic.");

Console.WriteLine("AST_BINDING_ASSIGNMENT_COMPATIBILITY_OK");


var customerType = XpTypeSymbol.User("Customer");
var setSymbols = new SymbolTable();
setSymbols.Declare(new VariableSymbol("firstCustomer", typeof(object), customerType));
setSymbols.Declare(new VariableSymbol("secondCustomer", typeof(object), customerType));
setSymbols.Declare(new VariableSymbol("scalarNumber", typeof(long)));

var referenceSetParser = new StatementParser("Set firstCustomer = secondCustomer");
var referenceSet = referenceSetParser.ParseStatement();
if (referenceSetParser.Diagnostics.Count != 0)
    throw new InvalidOperationException("Reference Set fixture must parse without diagnostics.");
var referenceSetBinder = new StatementBinder(setSymbols);
referenceSetBinder.Bind(referenceSet);
if (referenceSetBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException("Set between matching object references must bind without diagnostics.");

var scalarSetParser = new StatementParser("Set scalarNumber = 1");
var scalarSet = scalarSetParser.ParseStatement();
if (scalarSetParser.Diagnostics.Count != 0)
    throw new InvalidOperationException("Scalar Set fixture must parse without diagnostics.");
var scalarSetBinder = new StatementBinder(setSymbols);
scalarSetBinder.Bind(scalarSet);
if (scalarSetBinder.Diagnostics.Count != 1 || scalarSetBinder.Diagnostics[0].Code != "XPS2001")
    throw new InvalidOperationException("Set on a scalar target must produce exactly one XPS2001 diagnostic.");

Console.WriteLine("AST_BINDING_SET_COMPATIBILITY_OK");


var compatibleReturnParser = new StatementParser("Return 1");
var compatibleReturn = compatibleReturnParser.ParseStatement();
if (compatibleReturnParser.Diagnostics.Count != 0)
    throw new InvalidOperationException("Compatible return fixture must parse without diagnostics.");
var compatibleReturnBinder = new StatementBinder(returnType: XpTypeSymbol.FromClr(typeof(long)), allowsReturnValue: true);
compatibleReturnBinder.Bind(compatibleReturn);
if (compatibleReturnBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException("Function return with matching type must bind without diagnostics.");

var incompatibleReturnParser = new StatementParser("Return \"wrong\"");
var incompatibleReturn = incompatibleReturnParser.ParseStatement();
if (incompatibleReturnParser.Diagnostics.Count != 0)
    throw new InvalidOperationException("Incompatible return fixture must parse without diagnostics.");
var incompatibleReturnBinder = new StatementBinder(returnType: XpTypeSymbol.FromClr(typeof(long)), allowsReturnValue: true);
incompatibleReturnBinder.Bind(incompatibleReturn);
if (incompatibleReturnBinder.Diagnostics.Count != 1 || incompatibleReturnBinder.Diagnostics[0].Code != "XPS2001")
    throw new InvalidOperationException("Function return with incompatible type must produce exactly one XPS2001 diagnostic.");

var subReturnParser = new StatementParser("Return 1");
var subReturn = subReturnParser.ParseStatement();
var subReturnBinder = new StatementBinder();
subReturnBinder.Bind(subReturn);
if (subReturnBinder.Diagnostics.Count != 1 || subReturnBinder.Diagnostics[0].Code != "XPS2001")
    throw new InvalidOperationException("Sub returning a value must produce exactly one XPS2001 diagnostic.");

Console.WriteLine("AST_BINDING_RETURN_TYPE_OK");


var controlFlowSymbols = new SymbolTable();
controlFlowSymbols.Declare(new VariableSymbol("number", typeof(long)));

var ifParser = new StatementParser("If True Then\nnumber = 1\nEnd If");
var ifSyntax = ifParser.ParseStatement();
if (ifParser.Diagnostics.Count != 0)
    throw new InvalidOperationException($"Control-flow If fixture must parse without diagnostics: {string.Join("; ", ifParser.Diagnostics.Select(d => d.Message))}.");
var ifBinder = new StatementBinder(controlFlowSymbols);
var boundIf = ifBinder.Bind(ifSyntax);
if (ifBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException($"Boolean If must bind without diagnostics: {string.Join("; ", ifBinder.Diagnostics.Select(d => d.Message))}.");
if (boundIf is not BoundIfStatement typedIf || typedIf.ThenStatements.Count != 1 || typedIf.ThenStatements[0] is not BoundAssignmentStatement)
    throw new InvalidOperationException("If binding must produce a bound If with its bound assignment body.");

var invalidIfParser = new StatementParser("If 1 Then\nnumber = 1\nEnd If");
var invalidIfSyntax = invalidIfParser.ParseStatement();
if (invalidIfParser.Diagnostics.Count != 0)
    throw new InvalidOperationException("Non-Boolean If fixture must parse without diagnostics.");
var invalidIfBinder = new StatementBinder(controlFlowSymbols);
_ = invalidIfBinder.Bind(invalidIfSyntax);
if (invalidIfBinder.Diagnostics.Count != 1 || invalidIfBinder.Diagnostics[0].Code != "XPS2001")
    throw new InvalidOperationException("Non-Boolean If condition must produce exactly one XPS2001 diagnostic.");

var whileParser = new StatementParser("While True\nnumber = 1\nWend");
var whileSyntax = whileParser.ParseStatement();
if (whileParser.Diagnostics.Count != 0)
    throw new InvalidOperationException("While fixture must parse without diagnostics.");
var whileBinder = new StatementBinder(controlFlowSymbols);
var boundWhile = whileBinder.Bind(whileSyntax);
if (whileBinder.Diagnostics.Count != 0 || boundWhile is not BoundWhileStatement)
    throw new InvalidOperationException("Boolean While must produce a bound While statement without diagnostics.");

var selectParser = new StatementParser("Select Case number\nCase 1\nnumber = 1\nEnd Select");
var selectSyntax = selectParser.ParseStatement();
if (selectParser.Diagnostics.Count != 0)
    throw new InvalidOperationException($"Select Case fixture must parse without diagnostics: {string.Join("; ", selectParser.Diagnostics.Select(d => d.Message))}.");
var selectBinder = new StatementBinder(controlFlowSymbols);
var boundSelect = selectBinder.Bind(selectSyntax);
if (selectBinder.Diagnostics.Count != 0 || boundSelect is not BoundSelectStatement typedSelect || typedSelect.Cases.Count != 1)
    throw new InvalidOperationException("Select Case must produce a bound Select statement without diagnostics.");

Console.WriteLine("AST_BINDING_CONTROL_FLOW_OK");


var conversionSymbols = new SymbolTable();
conversionSymbols.Declare(new VariableSymbol("wide", typeof(double)));
conversionSymbols.Declare(new VariableSymbol("narrow", typeof(long)));
conversionSymbols.Declare(new FunctionSymbol("TakeDouble", typeof(double), [typeof(double)], ByRefParameters: [false]));
conversionSymbols.Declare(new FunctionSymbol("TouchDouble", typeof(double), [typeof(double)], ByRefParameters: [true]));

var wideningAssignmentParser = new StatementParser("wide = 1");
var wideningAssignment = wideningAssignmentParser.ParseStatement();
var wideningAssignmentBinder = new StatementBinder(conversionSymbols);
var boundWideningAssignment = wideningAssignmentBinder.Bind(wideningAssignment);
if (wideningAssignmentParser.Diagnostics.Count != 0 || wideningAssignmentBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException("Integer-to-Double widening assignment must bind without diagnostics.");
if (boundWideningAssignment is not BoundAssignmentStatement { Expression: BoundConversionExpression { Conversion.Kind: ConversionKind.NumericWidening } })
    throw new InvalidOperationException("Widening assignment must contain an explicit bound numeric conversion.");

var narrowingAssignmentParser = new StatementParser("narrow = 1.5");
var narrowingAssignment = narrowingAssignmentParser.ParseStatement();
var narrowingAssignmentBinder = new StatementBinder(conversionSymbols);
_ = narrowingAssignmentBinder.Bind(narrowingAssignment);
if (narrowingAssignmentBinder.Diagnostics.Count != 1 || narrowingAssignmentBinder.Diagnostics[0].Code != "XPS2001")
    throw new InvalidOperationException("Double-to-Integer narrowing assignment must remain a type mismatch.");

var wideningReturnParser = new StatementParser("Return 1");
var wideningReturn = wideningReturnParser.ParseStatement();
var wideningReturnBinder = new StatementBinder(returnType: XpTypeSymbol.FromClr(typeof(double)), allowsReturnValue: true);
var boundWideningReturn = wideningReturnBinder.Bind(wideningReturn);
if (wideningReturnBinder.Diagnostics.Count != 0 ||
    boundWideningReturn is not BoundReturnStatement { Expression: BoundConversionExpression { Conversion.Kind: ConversionKind.NumericWidening } })
    throw new InvalidOperationException("Integer-to-Double Function return must bind through an explicit widening conversion.");

var wideningCallBinder = new ExpressionBinder(conversionSymbols);
var wideningCall = wideningCallBinder.Bind(Parse("TakeDouble(1)"));
if (wideningCallBinder.Diagnostics.Count != 0)
    throw new InvalidOperationException("ByVal calls must allow defined implicit widening conversions.");
if (wideningCall is not BoundCallExpression { Arguments.Count: 1 } typedWideningCall ||
    typedWideningCall.Arguments[0] is not BoundConversionExpression { Conversion.Kind: ConversionKind.NumericWidening })
    throw new InvalidOperationException("ByVal widening calls must materialize an explicit bound numeric conversion.");

var byRefWideningBinder = new ExpressionBinder(conversionSymbols);
_ = byRefWideningBinder.Bind(Parse("TouchDouble(narrow)"));
if (byRefWideningBinder.Diagnostics.Count != 1 || byRefWideningBinder.Diagnostics[0].Code != "XPS2004")
    throw new InvalidOperationException("ByRef calls must require identity conversion and reject widening.");

Console.WriteLine("AST_BINDING_CONVERSION_RULES_OK");


var variantType = XpTypeSymbol.Variant;
if (variantType.RuntimeType != typeof(object) || !variantType.IsVariant)
    throw new InvalidOperationException("Variant must be represented explicitly as an object-backed XPscript dynamic type.");
if (XpTypeSymbol.User("Customer").IsVariant)
    throw new InvalidOperationException("Object-backed user types must not be classified as Variant.");

var toVariant = Conversion.Classify(XpTypeSymbol.FromClr(typeof(long)), variantType);
if (!toVariant.IsImplicit || toVariant.Kind != ConversionKind.ToVariant)
    throw new InvalidOperationException("Statically typed values must convert implicitly to Variant.");

var fromVariant = Conversion.Classify(variantType, XpTypeSymbol.FromClr(typeof(double)));
if (!fromVariant.IsImplicit || fromVariant.Kind != ConversionKind.FromVariant)
    throw new InvalidOperationException("Variant-to-static conversion must be represented as a runtime-checked conversion.");

var variantSymbols = new SymbolTable();
variantSymbols.Declare(new VariableSymbol("dynamicValue", typeof(object), variantType));
variantSymbols.Declare(new VariableSymbol("numberValue", typeof(long)));
var toVariantParser = new StatementParser("dynamicValue = 1");
var toVariantSyntax = toVariantParser.ParseStatement();
var toVariantBinder = new StatementBinder(variantSymbols);
var boundToVariant = toVariantBinder.Bind(toVariantSyntax);
if (toVariantBinder.Diagnostics.Count != 0 ||
    boundToVariant is not BoundAssignmentStatement { Expression: BoundConversionExpression { Conversion.Kind: ConversionKind.ToVariant } })
    throw new InvalidOperationException("Assignment to Variant must retain an explicit dynamic conversion in the bound tree.");

var fromVariantParser = new StatementParser("numberValue = dynamicValue");
var fromVariantSyntax = fromVariantParser.ParseStatement();
var fromVariantBinder = new StatementBinder(variantSymbols);
var boundFromVariant = fromVariantBinder.Bind(fromVariantSyntax);
if (fromVariantBinder.Diagnostics.Count != 0 ||
    boundFromVariant is not BoundAssignmentStatement { Expression: BoundConversionExpression { Conversion.Kind: ConversionKind.FromVariant } })
    throw new InvalidOperationException("Assignment from Variant must retain an explicit runtime-checked conversion in the bound tree.");

Console.WriteLine("AST_BINDING_VARIANT_SEMANTICS_OK");


var objectType = XpTypeSymbol.Object;
if (objectType.RuntimeType != typeof(object) || !objectType.IsObject || objectType.IsVariant)
    throw new InvalidOperationException("Object must be an explicit object-reference semantic type distinct from Variant.");
if (XpTypeSymbol.User("Customer").IsObject)
    throw new InvalidOperationException("User-defined class types must remain distinct from the Object type.");

var objectCustomerType = XpTypeSymbol.User("Customer");
var customerToObject = Conversion.Classify(objectCustomerType, objectType);
if (!customerToObject.IsImplicit || customerToObject.Kind != ConversionKind.ToObject)
    throw new InvalidOperationException("User-defined object references must convert implicitly to Object.");
if (Conversion.Classify(XpTypeSymbol.FromClr(typeof(long)), objectType).Exists)
    throw new InvalidOperationException("Scalar values must not convert implicitly to Object.");
if (Conversion.Classify(objectType, objectCustomerType).Exists)
    throw new InvalidOperationException("Object must not use Variant-style implicit conversion to a concrete user type.");

var objectSymbols = new SymbolTable();
objectSymbols.Declare(new VariableSymbol("targetObject", typeof(object), objectType));
objectSymbols.Declare(new VariableSymbol("customer", typeof(object), objectCustomerType));
objectSymbols.Declare(new VariableSymbol("scalar", typeof(long)));
var objectAssignmentParser = new StatementParser("targetObject = customer");
var objectAssignmentSyntax = objectAssignmentParser.ParseStatement();
var objectAssignmentBinder = new StatementBinder(objectSymbols);
var boundObjectAssignment = objectAssignmentBinder.Bind(objectAssignmentSyntax);
if (objectAssignmentBinder.Diagnostics.Count != 0 ||
    boundObjectAssignment is not BoundAssignmentStatement { Expression: BoundConversionExpression { Conversion.Kind: ConversionKind.ToObject } })
    throw new InvalidOperationException("Object assignment from a user-defined reference must retain an explicit ToObject conversion.");

var invalidObjectAssignmentParser = new StatementParser("targetObject = scalar");
var invalidObjectAssignmentSyntax = invalidObjectAssignmentParser.ParseStatement();
var invalidObjectAssignmentBinder = new StatementBinder(objectSymbols);
_ = invalidObjectAssignmentBinder.Bind(invalidObjectAssignmentSyntax);
if (invalidObjectAssignmentBinder.Diagnostics.Count != 1 || invalidObjectAssignmentBinder.Diagnostics[0].Code != "XPS2001")
    throw new InvalidOperationException("Scalar assignment to Object must produce exactly one XPS2001 diagnostic.");

Console.WriteLine("AST_BINDING_OBJECT_SEMANTICS_OK");


var emptyLiteralBinder = new ExpressionBinder();
var boundEmptyLiteral = emptyLiteralBinder.Bind(Parse("Empty"));
if (emptyLiteralBinder.Diagnostics.Count != 0 || boundEmptyLiteral.SemanticType != XpTypeSymbol.Empty || boundEmptyLiteral is not BoundLiteralExpression { Value: null })
    throw new InvalidOperationException("EMPTY must bind as the distinct uninitialized Variant state.");

var nullLiteralBinder = new ExpressionBinder();
var boundNullLiteral = nullLiteralBinder.Bind(Parse("Null"));
if (nullLiteralBinder.Diagnostics.Count != 0 || boundNullLiteral.SemanticType != XpTypeSymbol.Null || boundNullLiteral is not BoundLiteralExpression { Value: DBNull })
    throw new InvalidOperationException("NULL must bind as the distinct explicit Variant NULL state.");

var nothingLiteralBinder = new ExpressionBinder();
var boundNothingLiteral = nothingLiteralBinder.Bind(Parse("Nothing"));
if (nothingLiteralBinder.Diagnostics.Count != 0 || boundNothingLiteral.SemanticType != XpTypeSymbol.Nothing || boundNothingLiteral is not BoundLiteralExpression { Value: null })
    throw new InvalidOperationException("NOTHING must bind as a distinct object-reference state.");

var specialValueSymbols = new SymbolTable();
specialValueSymbols.Declare(new VariableSymbol("variantValue", typeof(object), XpTypeSymbol.Variant));
specialValueSymbols.Declare(new VariableSymbol("objectValue", typeof(object), XpTypeSymbol.Object));
specialValueSymbols.Declare(new VariableSymbol("typedValue", typeof(long)));

foreach (var literal in new[] { "Empty", "Null" })
{
    var parser = new StatementParser($"variantValue = {literal}");
    var syntax = parser.ParseStatement();
    var binder = new StatementBinder(specialValueSymbols);
    var bound = binder.Bind(syntax);
    if (parser.Diagnostics.Count != 0 || binder.Diagnostics.Count != 0 || bound is not BoundAssignmentStatement { Expression: BoundConversionExpression })
        throw new InvalidOperationException($"{literal} must be assignable to Variant through an explicit semantic conversion.");
}

var nothingObjectParser = new StatementParser("Set objectValue = Nothing");
var nothingObjectSyntax = nothingObjectParser.ParseStatement();
var nothingObjectBinder = new StatementBinder(specialValueSymbols);
var boundNothingObject = nothingObjectBinder.Bind(nothingObjectSyntax);
if (nothingObjectParser.Diagnostics.Count != 0 || nothingObjectBinder.Diagnostics.Count != 0 ||
    boundNothingObject is not BoundAssignmentStatement { IsSet: true, Expression: BoundConversionExpression { Conversion.Kind: ConversionKind.NothingToObject } })
    throw new InvalidOperationException("Set Object = Nothing must bind as an explicit object-reference clearing conversion.");

var nothingVariantParser = new StatementParser("variantValue = Nothing");
var nothingVariantSyntax = nothingVariantParser.ParseStatement();
var nothingVariantBinder = new StatementBinder(specialValueSymbols);
_ = nothingVariantBinder.Bind(nothingVariantSyntax);
if (nothingVariantBinder.Diagnostics.Count != 1 || nothingVariantBinder.Diagnostics[0].Code != "XPS2001")
    throw new InvalidOperationException("Variant = Nothing must remain invalid.");

var nullScalarParser = new StatementParser("typedValue = Null");
var nullScalarSyntax = nullScalarParser.ParseStatement();
var nullScalarBinder = new StatementBinder(specialValueSymbols);
_ = nullScalarBinder.Bind(nullScalarSyntax);
if (nullScalarBinder.Diagnostics.Count != 1 || nullScalarBinder.Diagnostics[0].Code != "XPS2001")
    throw new InvalidOperationException("NULL must not be assignable to a typed scalar.");

Console.WriteLine("AST_BINDING_NULL_EMPTY_NOTHING_SEMANTICS_OK");
