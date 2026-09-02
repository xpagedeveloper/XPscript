namespace XPScript.Compiler;

internal readonly record struct NotesRuntimeFeatures(bool RichText)
{
    public static NotesRuntimeFeatures Full { get; } = new(true);

    public static NotesRuntimeFeatures Detect(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var code = PreprocessorFeatureGate.CodeOnly(source);

        var richText = PreprocessorFeatureGate.ContainsTypeReference(
                           code,
                           "NotesRichTextItem", "NotesRichTextNavigator", "NotesRichTextParagraphStyle",
                           "NotesRichTextRange", "NotesRichTextSection", "NotesRichTextStyle", "NotesRichTextTab",
                           "NotesRichTextTable", "NotesRichTextDocLink", "NotesEmbeddedObject") ||
                       PreprocessorFeatureGate.ContainsCall(code, "CreateRichTextItem", "GetEmbeddedObject");
        return new NotesRuntimeFeatures(richText);
    }
}
