namespace XPScript.Compiler;

internal static class NotesRuntimeSourceBuilderFeaturePatchPostProcessor
{
    // Marker type for the MIME/RichText runtime feature split.
    // RuntimeSourceBuilder itself must pass NotesRuntimeFeatures directly to
    // NotesExtendedRuntimePostProcessor before this helper can be removed.
}
