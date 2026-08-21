namespace XPScript.Compiler;

internal sealed class UIFormNavigationCompatibilityPostProcessor
{
    private const string OldValidation = """
        if (path.Length is < 5 or > 512 || path.StartsWith('/') || path.Contains("..", StringComparison.Ordinal) ||
            !path.EndsWith(".xps", StringComparison.OrdinalIgnoreCase) || Uri.TryCreate(path, UriKind.Absolute, out _))
            throw new XPScriptRuntimeException(5, "UIForm navigation target must be a relative local .xps path.");
""";

    private const string NewValidation = """
        var extension = System.IO.Path.GetExtension(path);
        if (path.Length is < 1 or > 512 || path.StartsWith('/') || path.Contains("..", StringComparison.Ordinal) ||
            Uri.TryCreate(path, UriKind.Absolute, out _) ||
            (extension.Length > 0 && !extension.Equals(".xps", StringComparison.OrdinalIgnoreCase)))
            throw new XPScriptRuntimeException(5, "UIForm navigation target must be a relative local XPS module path with an optional .xps extension.");
""";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains(NewValidation, StringComparison.Ordinal)) return generated;
        if (!generated.Contains(OldValidation, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm compiled-navigation compatibility.");
        return generated.Replace(OldValidation, NewValidation, StringComparison.Ordinal);
    }
}
