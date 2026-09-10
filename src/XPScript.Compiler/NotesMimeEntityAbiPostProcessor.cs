namespace XPScript.Compiler;

// Retained as a compatibility shim for older call sites. New code uses
// NotesMimeStreamAbiPostProcessor, which names the actual C API concern.
internal static class NotesMimeEntityAbiPostProcessor
{
    public static string ApplyBuiltSurface(string source) => NotesMimeStreamAbiPostProcessor.ApplyBuiltSurface(source);
}
