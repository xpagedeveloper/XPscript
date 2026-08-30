namespace XPScript.Compiler;

internal static class NotesViewNavigatorCachePolicyRegression
{
    internal static void Validate(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Require(source, "CreateViewNav() => CreateViewNav(64)", "default cache size");
        Require(source, "Math.Clamp(XPScriptRuntime.CInt(cacheSizeValue), 0, 512)", "factory cache range");
        Require(source, "Math.Clamp(value, 0, 512)", "property cache range");
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
