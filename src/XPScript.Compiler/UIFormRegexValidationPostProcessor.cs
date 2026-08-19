namespace XPScript.Compiler;

internal sealed class UIFormRegexValidationPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains("public string RegexPattern { get; set; } = string.Empty;", StringComparison.Ordinal))
            return generated;

        generated = ReplaceRequired(generated,
            "    public List<string> Options { get; } = [];\n",
            """
    public string RegexPattern { get; set; } = string.Empty;
    public List<string> Options { get; } = [];
""",
            "field-metadata");

        generated = ReplaceRequired(generated,
            "public object? GetFieldValue(object? name)",
            """
public void SetRegexValidation(object? name, object? pattern)
    {
        var field = FindField(name);
        if (field.Type is not ("TextField" or "TextArea" or "PasswordField" or "EmailField" or "UrlField"))
            throw new XPScriptRuntimeException(5, "UIForm regex validation is only supported for text-entry fields.");

        var value = XPScriptRuntime.CStr(pattern);
        if (value.Length > 1024)
            throw new XPScriptRuntimeException(5, "UIForm regex pattern must contain at most 1024 characters.");
        if (value.Any(char.IsControl))
            throw new XPScriptRuntimeException(5, "UIForm regex pattern contains a control character.");
        if (value.Length > 0)
        {
            try
            {
                _ = new System.Text.RegularExpressions.Regex(
                    value,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(250));
            }
            catch (ArgumentException ex)
            {
                throw new XPScriptRuntimeException(5, "UIForm regex pattern is invalid: " + ex.Message);
            }
        }
        field.RegexPattern = value;
    }

    public object? GetFieldValue(object? name)
""",
            "api");

        generated = ReplaceRequired(generated,
            """
            if (field.MaxLength.HasValue && submitted.Length > field.MaxLength.Value)
                throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must contain at most {field.MaxLength.Value} characters.");
        }

        switch (field.Type)
""",
            """
            if (field.MaxLength.HasValue && submitted.Length > field.MaxLength.Value)
                throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must contain at most {field.MaxLength.Value} characters.");
            if (submitted.Length > 0 && field.RegexPattern.Length > 0 &&
                !System.Text.RegularExpressions.Regex.IsMatch(
                    submitted,
                    field.RegexPattern,
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(250)))
                throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' does not match the required format.");
        }

        switch (field.Type)
""",
            "server-validation");

        generated = ReplaceRequired(generated,
            """
            var length = (field.MinLength.HasValue ? $" minlength=\"{field.MinLength.Value}\"" : string.Empty)
                + (field.MaxLength.HasValue ? $" maxlength=\"{field.MaxLength.Value}\"" : string.Empty);
            var range =
""",
            """
            var length = (field.MinLength.HasValue ? $" minlength=\"{field.MinLength.Value}\"" : string.Empty)
                + (field.MaxLength.HasValue ? $" maxlength=\"{field.MaxLength.Value}\"" : string.Empty);
            var regexPattern = field.RegexPattern.Length > 0
                ? " pattern=\"" + System.Net.WebUtility.HtmlEncode(field.RegexPattern) + "\""
                : string.Empty;
            var range =
""",
            "web-pattern-state");

        generated = generated.Replace(
            ".Append(required).Append(length).Append(placeholder).Append(\">\")",
            ".Append(required).Append(length).Append(regexPattern).Append(placeholder).Append(\">\")",
            StringComparison.Ordinal);
        generated = generated.Replace(
            ".Append(required).Append(length).Append(\">\")",
            ".Append(required).Append(length).Append(regexPattern).Append(\">\")",
            StringComparison.Ordinal);

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to install UIForm regex validation runtime ({stage}).");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
