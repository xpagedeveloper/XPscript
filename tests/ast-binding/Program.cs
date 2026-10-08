using XPScript.Compiler.Binding;
using XPScript.Compiler.Syntax;

static ExpressionSyntax Parse(string text)
{
    var parser = new ExpressionParser(text);
    var syntax = parser.ParseExpression();
    if (parser.Diagnostics.Count != 0)
        throw new InvalidOperationException($"Focused binder input '{text}' must parse without diagnostics.");
    return syntax;
}

// Type diagnostics run first so semantic type regressions fail before the broader binder probe.
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
