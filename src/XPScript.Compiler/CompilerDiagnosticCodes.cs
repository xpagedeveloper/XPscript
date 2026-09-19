namespace XPScript.Compiler;

/// <summary>
/// Stable XPScript-owned diagnostic identifiers used by the public machine interface.
/// Codes are part of the compiler result contract and must not be repurposed.
/// </summary>
internal static class CompilerDiagnosticCodes
{
    // XPS1xxx: parser/syntax validation.
    public const string UnescapedStringQuote = "XPS1001";
    public const string InvalidNothingComparison = "XPS1002";
    public const string InvalidIncrementSyntax = "XPS1003";
    public const string InvalidCompoundAssignmentSyntax = "XPS1004";

    // XPS2xxx: type, argument, member and overload validation.
    public const string TypeMismatch = "XPS2001";
    public const string ArgumentCountMismatch = "XPS2002";
    public const string ArgumentTypeMismatch = "XPS2003";
    public const string NoMatchingOverload = "XPS2004";
    public const string AmbiguousOverload = "XPS2005";
    public const string DuplicateOverload = "XPS2006";
    public const string ConflictingClassMember = "XPS2007";
    public const string UnknownSymbol = "XPS2008";
    public const string UnknownMember = "XPS2009";

    // XPS8xxx: project/configuration and compiler invocation.
    public const string SourceFileRequired = "XPS8001";
    public const string SourceExtensionInvalid = "XPS8002";
    public const string SourceFileNotFound = "XPS8003";
    public const string RuntimeIdentifierUnsupported = "XPS8004";
    public const string WebIisEntryFileInvalid = "XPS8005";
    public const string WebIisOutputInsideSource = "XPS8006";

    // XPS9xxx: compiler pipeline failures where a more specific language
    // diagnostic could not be preserved.
    public const string CompilationFailed = "XPS9001";
    public const string InternalCompilationFailed = "XPS9002";
}
