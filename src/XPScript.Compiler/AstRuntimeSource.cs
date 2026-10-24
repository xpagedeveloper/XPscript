namespace XPScript.Compiler;

/// <summary>The scalar runtime and coercion rules shared with normal compilation.</summary>
internal static class AstRuntimeSource
{
    public static string Code => XPScriptRuntimeSource.Code + TypeCoercionRuntimeSource.Code + TextFileRuntimeSource.Code + RuntimeDebugTraceSource.Code;
}
