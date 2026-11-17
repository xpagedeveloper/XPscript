namespace XPScript.Compiler;

internal static class LegacyCompilationGuard
{
    internal static void Reject(string sourceName) =>
        throw new CompilerException($"{sourceName}: Legacy compilation is disabled. Use the AST compiler.", "XPS1012", "configuration");
}
