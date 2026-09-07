namespace XPScript.Compiler;

internal readonly record struct NotesRuntimeFeatures(bool RichText, bool Mime)
{
    public static NotesRuntimeFeatures Full { get; } = new(true, true);

    public static NotesRuntimeFeatures Detect(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var code = PreprocessorFeatureGate.CodeOnly(source);

        var mime = PreprocessorFeatureGate.ContainsTypeReference(
                       code,
                       "NotesMIMEEntity", "NotesMIMEHeader") ||
                   PreprocessorFeatureGate.ContainsCall(code, "CreateMIMEEntity", "GetMIMEEntity", "CloseMIMEEntities");

        var richText = PreprocessorFeatureGate.ContainsTypeReference(
                           code,
                           "NotesRichTextItem", "NotesRichTextNavigator", "NotesRichTextParagraphStyle",
                           "NotesRichTextRange", "NotesRichTextSection", "NotesRichTextStyle", "NotesRichTextTab",
                           "NotesRichTextTable", "NotesRichTextDocLink", "NotesEmbeddedObject") ||
                       PreprocessorFeatureGate.ContainsCall(code, "CreateRichTextItem", "GetEmbeddedObject");

        // ConvertMIME and the current NotesItem rich-text/MIME boundary are still
        // emitted by the shared rich-text MIME processor. Keep that infrastructure
        // enabled for MIME callers while tracking MIME independently so the MIME
        // surface itself no longer has to be inferred from a RichText marker.
        return new NotesRuntimeFeatures(richText || mime, mime);
    }
}
