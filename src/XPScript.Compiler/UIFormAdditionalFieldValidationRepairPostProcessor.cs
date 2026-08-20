namespace XPScript.Compiler;

internal sealed class UIFormAdditionalFieldValidationRepairPostProcessor
{
    private const string MethodMarker = "    private void ApplySubmittedValue(XPScriptUIField field, string submitted)";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (!generated.Contains("AddWeekField", StringComparison.Ordinal)) return generated;

        generated = RemoveBroadValidationInsertions(generated);

        var start = generated.IndexOf(MethodMarker, StringComparison.Ordinal);
        if (start < 0) throw new CompilerException("Unable to locate UIForm submit validator for extended field types.");
        var end = generated.IndexOf("\n    private ", start + MethodMarker.Length, StringComparison.Ordinal);
        if (end < 0) throw new CompilerException("Unable to determine UIForm submit validator boundary.");

        var method = generated[start..end];
        method = ExtendNumberCases(method);
        method = ExtendWeekCase(method);
        method = ExtendLookupCases(method);
        return generated[..start] + method + generated[end..];
    }

    private static string RemoveBroadValidationInsertions(string generated)
    {
        generated = generated.Replace(
            "            case \"NumberField\":\n            case \"RangeField\":\n            case \"DecimalField\":\n            case \"CurrencyField\":",
            "            case \"NumberField\":\n            case \"RangeField\":",
            StringComparison.Ordinal);

        const string week = """
            case "WeekField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!XPScriptUIAdditionalFieldRuntime.IsIsoWeek(submitted)) throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid ISO week in yyyy-Www format.");
                _data.Set(field.Name, submitted);
                return;
            case "MonthField":
""";
        generated = generated.Replace(week, "            case \"MonthField\":\n", StringComparison.Ordinal);

        const string lookup = """
            case "LookupField":
            case "AutoCompleteField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!field.Options.Contains(submitted, StringComparer.Ordinal)) throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' contains an unsupported lookup value.");
                _data.Set(field.Name, submitted);
                return;
            case "Select":
            case "ListBox":
            case "RadioGroup":
""";
        generated = generated.Replace(
            lookup,
            "            case \"Select\":\n            case \"ListBox\":\n            case \"RadioGroup\":\n",
            StringComparison.Ordinal);
        return generated;
    }

    private static string ExtendNumberCases(string method)
    {
        const string marker = "            case \"NumberField\":\n            case \"RangeField\":";
        if (!method.Contains(marker, StringComparison.Ordinal)) throw new CompilerException("Unable to extend UIForm numeric submit validation.");
        return method.Replace(
            marker,
            marker + "\n            case \"DecimalField\":\n            case \"CurrencyField\":",
            StringComparison.Ordinal);
    }

    private static string ExtendWeekCase(string method)
    {
        const string marker = "            case \"MonthField\":";
        if (!method.Contains(marker, StringComparison.Ordinal)) throw new CompilerException("Unable to extend UIForm week submit validation.");
        const string replacement = """
            case "WeekField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!XPScriptUIAdditionalFieldRuntime.IsIsoWeek(submitted))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid ISO week in yyyy-Www format.");
                _data.Set(field.Name, submitted);
                return;
            case "MonthField":
""";
        return method.Replace(marker, replacement.TrimEnd('\n'), StringComparison.Ordinal);
    }

    private static string ExtendLookupCases(string method)
    {
        const string marker = "            case \"Select\":\n            case \"ListBox\":\n            case \"RadioGroup\":";
        if (!method.Contains(marker, StringComparison.Ordinal)) throw new CompilerException("Unable to extend UIForm lookup submit validation.");
        const string replacement = """
            case "LookupField":
            case "AutoCompleteField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!field.Options.Contains(submitted, StringComparer.Ordinal))
                    throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' contains an unsupported lookup value.");
                _data.Set(field.Name, submitted);
                return;
            case "Select":
            case "ListBox":
            case "RadioGroup":
""";
        return method.Replace(marker, replacement.TrimEnd('\n'), StringComparison.Ordinal);
    }
}
