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

    private const string ParameterOverload = """

    public void Navigate(object? target, object? parameterName, object? parameterValue)
        => SetNavigation(target, XPScriptRuntime.CStr(parameterName), XPScriptRuntime.CStr(parameterValue));
""";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        if (!generated.Contains(NewValidation, StringComparison.Ordinal))
        {
            if (!generated.Contains(OldValidation, StringComparison.Ordinal))
                throw new CompilerException("Unable to install UIForm compiled-navigation compatibility.");
            generated = generated.Replace(OldValidation, NewValidation, StringComparison.Ordinal);
        }

        if (generated.Contains(ParameterOverload, StringComparison.Ordinal))
            generated = generated.Replace(ParameterOverload, string.Empty, StringComparison.Ordinal);

        return generated;
    }
}
