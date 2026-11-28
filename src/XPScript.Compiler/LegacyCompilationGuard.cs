namespace XPScript.Compiler;

internal static class LegacyCompilationGuard
{
    internal static CompilerException CreateError(string sourceName) =>
        new($"{sourceName}: Legacy compilation is disabled. Use the AST compiler.", "XPS1012", "configuration");

    internal static void Reject(string sourceName) =>
        throw CreateError(sourceName);
}
