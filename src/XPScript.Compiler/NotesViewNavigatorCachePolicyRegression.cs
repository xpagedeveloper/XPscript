namespace XPScript.Compiler;

internal static class NotesViewNavigatorCachePolicyRegression
{
    internal static void Validate(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Require(source, "CreateViewNav() => CreateViewNav(64)", "default cache size");
        Require(source, "var cacheSize = XPScriptRuntime.CInt(cacheSizeValue)", "factory BufferMaxEntries value");
        Require(source, "public int BufferMaxEntries", "BufferMaxEntries property");
        Require(source, "EffectiveBufferMaxEntries => Math.Clamp(_bufferMaxEntries, 0, 512)", "effective BufferMaxEntries range");
        Require(source, "_cacheSize = Math.Clamp(value, 0, 512)", "compatibility cache range");
        Require(source, "if (_view.AutoUpdate)", "AutoUpdate cache guard");
        Require(source, "_viewGeneration = _view.NavigationGeneration", "Refresh generation tracking");
        Require(source, "MaxRetainedHistory = 2048", "bounded retained history");
        Require(source, "TrimHistory()", "history trimming");
    }

    private static void Require(string source, string value, string description)
    {
        if (!source.Contains(value, StringComparison.Ordinal))
            throw new CompilerException("NotesView navigator cache policy regression: missing " + description + ".");
    }
}
