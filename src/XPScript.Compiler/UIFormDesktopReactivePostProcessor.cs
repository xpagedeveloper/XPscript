using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIFormDesktopReactivePostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        if (!generated.Contains("types: [typeof(string), typeof(Func<string, string, string>)]", StringComparison.Ordinal))
        {
            generated = ReplaceRequiredRegex(
                generated,
                """
                var\s+method\s*=\s*type\.GetMethod\(\s*"ShowDialog"\s*,\s*System\.Reflection\.BindingFlags\.Public\s*\|\s*System\.Reflection\.BindingFlags\.Static\s*\)\s*
                \?\?\s*throw\s+new\s+XPScriptRuntimeException\(5,\s*"XPScript desktop UI bridge is incomplete\."\)\s*;
                """,
                """
        var method = type.GetMethod(
                "ShowDialog",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                binder: null,
                types: [typeof(string), typeof(Func<string, string, string>)],
                modifiers: null)
            ?? type.GetMethod("ShowDialog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript desktop UI bridge is incomplete.");
""",
                "method-lookup");
        }

        if (!generated.Contains("new Func<string, string, string>(form.DispatchRegisteredEvent)", StringComparison.Ordinal))
        {
            generated = ReplaceRequiredRegex(
                generated,
                """
                resultJson\s*=\s*Convert\.ToString\(\s*method\.Invoke\(null,\s*\[requestJson\]\)\s*,\s*System\.Globalization\.CultureInfo\.InvariantCulture\s*\)\s*
                \?\?\s*string\.Empty\s*;
                """,
                """
            var invokeArgs = method.GetParameters().Length == 2
                ? new object?[] { requestJson, new Func<string, string, string>(form.DispatchRegisteredEvent) }
                : new object?[] { requestJson };
            resultJson = Convert.ToString(method.Invoke(null, invokeArgs), System.Globalization.CultureInfo.InvariantCulture)
                ?? string.Empty;
""",
                "invoke");
        }

        return generated;
    }

    private static string ReplaceRequiredRegex(string source, string pattern, string replacement, string stage)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace | RegexOptions.Singleline);
        if (!regex.IsMatch(source))
            throw new CompilerException($"Unable to install UIForm desktop event runtime ({stage}).");
        return regex.Replace(source, replacement, 1);
    }
}
