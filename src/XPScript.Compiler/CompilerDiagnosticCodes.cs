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
    public const string EmptyDimDeclaration = "XPS1005";
    public const string UnterminatedStringLiteral = "XPS1006";
    public const string InvalidDateComparison = "XPS1007";
    public const string MissingConstructorArgument = "XPS1008";
    public const string InvalidNativeConstructor = "XPS1009";
    public const string InvalidNativeArgumentList = "XPS1010";
    public const string RemovedNativeApi = "XPS1011";

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
    public const string InvalidCallbackName = "XPS2010";
    public const string CallbackNotFound = "XPS2011";
    public const string CallbackArityMismatch = "XPS2012";

    // XPS3xxx: target/platform and execution-context restrictions.
    public const string TargetApiUnavailable = "XPS3001";
    public const string ServerSideContextRequired = "XPS3002";

    // XPS7xxx: application security and dependency audit.
    public const string DependencyAuditUnavailable = "XPS7001";
    public const string DependencyVulnerability = "XPS7002";
    public const string RestrictedSourcePath = "XPS7003";
    public const string UnsafeDependencyPath = "XPS7004";
    public const string SecureStagingFailed = "XPS7005";
    public const string TemporaryWorkspaceSecurityFailed = "XPS7006";

    // XPS8xxx: project/configuration and compiler invocation.
    public const string SourceFileRequired = "XPS8001";
    public const string SourceExtensionInvalid = "XPS8002";
    public const string SourceFileNotFound = "XPS8003";
    public const string RuntimeIdentifierUnsupported = "XPS8004";
    public const string WebIisEntryFileInvalid = "XPS8005";
    public const string WebIisOutputInsideSource = "XPS8006";
    public const string InvalidSourcePreprocessor = "XPS8007";
    public const string SourcePreprocessorFailed = "XPS8008";
    public const string SourceTooLarge = "XPS8009";

    // XPS9xxx: compiler pipeline failures where a more specific language
    // diagnostic could not be preserved.
    public const string CompilationFailed = "XPS9001";
    public const string InternalCompilationFailed = "XPS9002";
    public const string ValidationBuildTimedOut = "XPS9003";
}
