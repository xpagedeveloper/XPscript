namespace XPScript.Compiler;

/// <summary>Composes the shared JSON and object-reference runtime for the AST host.</summary>
internal static class AstJsonRuntimeSource
{
    public static string Code => XPScriptObjectRuntimeSource.Code + NativeJsonRuntimeSource.Code
        .Replace("internal sealed class XPScriptJson", "public sealed class XPScriptJson", StringComparison.Ordinal)
        .Replace("XPScriptRuntime.", "Program.XPScriptRuntime.", StringComparison.Ordinal)
        .Replace("XPScriptNullRuntime.IsNull", "Program.XPScriptNullRuntime.IsNull", StringComparison.Ordinal)
        .Replace("var index = Program.XPScriptRuntime.CInt(indexValue);", "var index = Convert.ToInt32(Program.XPScriptRuntime.CInt(indexValue));", StringComparison.Ordinal);
}
