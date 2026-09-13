namespace XPScript.Compiler;

internal readonly record struct NotesRuntimeFeatures(bool RichText, bool Mime, bool Mail)
{
    public static NotesRuntimeFeatures Full { get; } = new(true, true, true);

    public static NotesRuntimeFeatures Detect(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var code = PreprocessorFeatureGate.CodeOnly(source);

        var mail = PreprocessorFeatureGate.ContainsTypeReference(code, "NotesMail") ||
                   PreprocessorFeatureGate.ContainsCall(
                       code,
                       "CreateMail", "SetBodyText", "SetBodyRichText", "SetBodyHTML",
                       "SetMIME", "SetMIMEBody", "AddAttachment", "ClearAttachments");

        var mailMime = PreprocessorFeatureGate.ContainsCall(
            code,
            "SetBodyHTML", "SetMIME", "SetMIMEBody", "AddAttachment");

        var mime = mailMime ||
                   PreprocessorFeatureGate.ContainsTypeReference(
                       code,
                       "NotesMIMEEntity", "NotesMIMEHeader") ||
                   PreprocessorFeatureGate.ContainsCall(code, "CreateMIMEEntity", "GetMIMEEntity", "CloseMIMEEntities");

        var richText = mail ||
                       PreprocessorFeatureGate.ContainsTypeReference(
                           code,
                           "NotesRichTextItem", "NotesRichTextNavigator", "NotesRichTextParagraphStyle",
                           "NotesRichTextRange", "NotesRichTextSection", "NotesRichTextStyle", "NotesRichTextTab",
                           "NotesRichTextTable", "NotesRichTextDocLink", "NotesEmbeddedObject") ||
                       PreprocessorFeatureGate.ContainsCall(code, "CreateRichTextItem", "GetEmbeddedObject");

        return new NotesRuntimeFeatures(richText, mime, mail);
    }
}
