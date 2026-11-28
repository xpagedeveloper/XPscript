using System.Text.RegularExpressions;

namespace XPScript.Compiler;

public sealed partial class XPScriptTranspiler
{
    public static string ApplyWebUiFormModelExtensions(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (!generated.Contains("class XPScriptUIForm", StringComparison.Ordinal)) return generated;
        var hasComponentBase = generated.Contains("internal sealed partial class XPScriptUIForm : XPScriptUIComponent\n{", StringComparison.Ordinal) ||
                               generated.Contains("internal sealed class XPScriptUIForm : XPScriptUIComponent\n{", StringComparison.Ordinal);
        if (hasComponentBase)
        {
            generated = generated
                .Replace("internal sealed partial class XPScriptUIForm : XPScriptUIComponent\n{", "internal sealed partial class XPScriptUIForm\n{", StringComparison.Ordinal)
                .Replace("internal sealed class XPScriptUIForm : XPScriptUIComponent\n{", "internal sealed class XPScriptUIForm\n{", StringComparison.Ordinal);
        }
        generated = new UIFormWindowLifecyclePostProcessor().Transform(generated);
        generated = new UIFormLayoutReactivePostProcessor().TransformModelOnly(generated);
        generated = new UIFormActionModelPostProcessor().Transform(generated);
        generated = new UIFormCallbackModelPostProcessor().Transform(generated);
        if (!generated.Contains("internal sealed class XPScriptJsonSchema", StringComparison.Ordinal))
            generated += Environment.NewLine + JsonSchemaRuntimeSource.Code;
        if (!generated.Contains("public bool IsDataValid => ValidateData().Valid", StringComparison.Ordinal))
            generated += Environment.NewLine + UIFormValidationRuntimeSource.Code;
        if (!generated.Contains("public string ValidationError { get; internal set; }", StringComparison.Ordinal))
            generated = generated.Replace("    public List<string> Options { get; } = [];\n", "    public List<string> Options { get; } = [];\n    public string ValidationError { get; internal set; } = string.Empty;\n", StringComparison.Ordinal);
        generated = new UIFormEventDispatcherPostProcessor().Transform(generated);
        if (hasComponentBase)
        {
            generated = generated
                .Replace("internal sealed partial class XPScriptUIForm\n{", "internal sealed partial class XPScriptUIForm : XPScriptUIComponent\n{", StringComparison.Ordinal)
                .Replace("internal sealed class XPScriptUIForm\n{", "internal sealed class XPScriptUIForm : XPScriptUIComponent\n{", StringComparison.Ordinal);
        }
        return generated;
    }

    public string Transpile(string source, string sourceName) =>
        throw LegacyCompilationGuard.CreateError(sourceName);

    public string Transpile(string source) =>
        throw LegacyCompilationGuard.CreateError("input.xps");

    public string Transpile(string source, string sourceName, string runtimeIdentifier) =>
        throw LegacyCompilationGuard.CreateError(sourceName);

    public string TranspileRestricted(string source, string sourceName, string runtimeIdentifier, IEnumerable<string> allowedSourceRoots)
    {
        ArgumentNullException.ThrowIfNull(allowedSourceRoots);
        throw LegacyCompilationGuard.CreateError(sourceName);
    }
}