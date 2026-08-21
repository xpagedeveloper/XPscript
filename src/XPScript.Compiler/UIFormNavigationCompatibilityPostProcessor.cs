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

    private const string ParameterFields = """
    private string _navigationParameterName = string.Empty;
    private string _navigationParameterValue = string.Empty;
""";

    private const string ParameterOverload = """

    public void Navigate(object? target, object? parameterName, object? parameterValue)
        => SetNavigation(target, XPScriptRuntime.CStr(parameterName), XPScriptRuntime.CStr(parameterValue));
""";

    private const string OldNavigate = """
    public void Navigate(object? target)
        => SetNavigation(target, string.Empty, string.Empty);
""";

    private const string NewNavigate = """
    public void Navigate(object? target)
        => SetNavigation(target);
""";

    private const string OldSetNavigationSignature = "    private void SetNavigation(object? target, string parameterName, string parameterValue)";
    private const string NewSetNavigationSignature = "    private void SetNavigation(object? target)";

    private const string ParameterValidation = """
        if (parameterName.Length > 0 && (parameterName.Length > 128 || !parameterName.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-')))
            throw new XPScriptRuntimeException(5, "UIForm navigation parameter name is invalid.");
""";

    private const string ParameterAssignments = """
        _navigationParameterName = parameterName;
        _navigationParameterValue = parameterValue;
""";

    private const string NavigationAssignment = "        _navigationTarget = path;";
    private const string StartupToken = "        XPScriptRuntime.SetArgs(args);";
    private const string StartupWithRestore = "        XPScriptRuntime.SetArgs(args);\n        XPScriptBrowserNavigationStateRuntime.Restore();";

    private const string BrowserNavigationAssignment = """
        _navigationTarget = path;
        if (OperatingSystem.IsBrowser())
        {
            XPScriptBrowserNavigationStateRuntime.Stage();
            var browserHost = Type.GetType("XPScript.UI.Browser.BrowserFormHost, XPScript.UI.Browser", throwOnError: false, ignoreCase: false);
            var navigateMethod = browserHost?.GetMethod("Navigate", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (navigateMethod is null)
                throw new XPScriptRuntimeException(5, "Browser UI navigation backend is unavailable.");
            navigateMethod.Invoke(null, [path]);
        }
        else
        {
            var webRuntime = Type.GetType("XPScript.Web.Runtime.XpsWebRuntimeObjects, XPScript.Web.Runtime", throwOnError: false, ignoreCase: false);
            var stageMethod = webRuntime?.GetMethod("TryStageRequestStateForNavigation", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            stageMethod?.Invoke(null, null);
        }
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

        generated = generated.Replace(ParameterFields, string.Empty, StringComparison.Ordinal);
        generated = generated.Replace(ParameterOverload, string.Empty, StringComparison.Ordinal);
        generated = generated.Replace(OldNavigate, NewNavigate, StringComparison.Ordinal);
        generated = generated.Replace(OldSetNavigationSignature, NewSetNavigationSignature, StringComparison.Ordinal);
        generated = generated.Replace(ParameterValidation, string.Empty, StringComparison.Ordinal);
        generated = generated.Replace(ParameterAssignments, string.Empty, StringComparison.Ordinal);

        if (!generated.Contains(BrowserNavigationAssignment, StringComparison.Ordinal))
        {
            if (!generated.Contains(NavigationAssignment, StringComparison.Ordinal))
                throw new CompilerException("Unable to install browser UIForm navigation compatibility.");
            generated = generated.Replace(NavigationAssignment, BrowserNavigationAssignment, StringComparison.Ordinal);
        }

        if (!generated.Contains("internal static class XPScriptBrowserNavigationStateRuntime", StringComparison.Ordinal))
            generated += "\n" + BrowserNavigationStateRuntimeSource.Code + "\n";

        if (!generated.Contains(StartupWithRestore, StringComparison.Ordinal))
        {
            if (!generated.Contains(StartupToken, StringComparison.Ordinal))
                throw new CompilerException("Unable to install browser Request.State navigation restore hook.");
            generated = generated.Replace(StartupToken, StartupWithRestore, StringComparison.Ordinal);
        }

        if (generated.Contains("_navigationParameterName", StringComparison.Ordinal) ||
            generated.Contains("_navigationParameterValue", StringComparison.Ordinal) ||
            generated.Contains("SetNavigation(target, string.Empty, string.Empty)", StringComparison.Ordinal))
            throw new CompilerException("UIForm navigation parameter compatibility cleanup was incomplete.");

        return generated;
    }
}
