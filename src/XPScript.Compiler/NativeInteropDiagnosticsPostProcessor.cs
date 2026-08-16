using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NativeInteropDiagnosticsPostProcessor
{
    private static readonly Regex DeclarationPattern = new(
        """(?<indent>^[ \t]*)\[System\.Runtime\.InteropServices\.DllImport\("(?<library>[^"]+)", EntryPoint = "(?<entry>[^"]+)", CharSet = System\.Runtime\.InteropServices\.CharSet\.(?<charset>\w+)\)\]\r?\n[ \t]*private static extern (?<return>[^\r\n]+?) (?<name>[A-Za-z_]\w*)\((?<parameters>[^\r\n]*)\);""",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    public string Transform(string generated)
    {
        var ordinal = 0;
        return DeclarationPattern.Replace(generated, match => Rewrite(match, ++ordinal));
    }

    private static string Rewrite(Match match, int ordinal)
    {
        var indent = match.Groups["indent"].Value;
        var markedLibrary = match.Groups["library"].Value;
        var markerIndex = markedLibrary.IndexOf(
            NativeLibraryPlatformPreprocessor.ApplicationLocalMarker,
            StringComparison.Ordinal);
        var applicationLocal = markerIndex >= 0;
        var library = applicationLocal
            ? markedLibrary[(markerIndex + NativeLibraryPlatformPreprocessor.ApplicationLocalMarker.Length)..]
            : markedLibrary;
        var entry = match.Groups["entry"].Value;
        var charset = match.Groups["charset"].Value;
        var returnType = match.Groups["return"].Value.Trim();
        var name = match.Groups["name"].Value;
        var parameters = match.Groups["parameters"].Value.Trim();
        var internalName = $"__ls_native_{ordinal}_{name}";
        var argumentNames = ExtractArgumentNames(parameters);
        var call = internalName + "(" + string.Join(", ", argumentNames) + ")";

        var builder = new StringBuilder();
        builder.Append(indent).Append("[System.Runtime.InteropServices.DllImport(\"").Append(Escape(library))
            .Append("\", EntryPoint = \"").Append(Escape(entry))
            .Append("\", CharSet = System.Runtime.InteropServices.CharSet.").Append(charset).AppendLine(")]" );
        builder.Append(indent).Append("private static extern ").Append(returnType).Append(' ').Append(internalName)
            .Append('(').Append(parameters).AppendLine(");");
        builder.Append(indent).Append("private static ").Append(returnType).Append(' ').Append(name)
            .Append('(').Append(parameters).AppendLine(")");
        builder.Append(indent).AppendLine("{");
        builder.Append(indent).AppendLine("    try");
        builder.Append(indent).AppendLine("    {");
        if (applicationLocal)
        {
            builder.Append(indent).Append("        XPNativeInteropRuntime.EnsureApplicationLocalLibrary(\"")
                .Append(Escape(library)).AppendLine("\");");
        }
        builder.Append(indent).Append("        ");
        if (!returnType.Equals("void", StringComparison.Ordinal)) builder.Append("return ");
        builder.Append(call).AppendLine(";");
        builder.Append(indent).AppendLine("    }");
        builder.Append(indent).Append("    catch (DllNotFoundException ex) { throw XPNativeInteropRuntime.LibraryNotFound(\"")
            .Append(Escape(library)).Append("\", \"").Append(Escape(entry)).AppendLine("\", ex); }");
        builder.Append(indent).Append("    catch (EntryPointNotFoundException ex) { throw XPNativeInteropRuntime.EntryPointNotFound(\"")
            .Append(Escape(library)).Append("\", \"").Append(Escape(entry)).AppendLine("\", ex); }");
        builder.Append(indent).Append("    catch (BadImageFormatException ex) { throw XPNativeInteropRuntime.WrongArchitecture(\"")
            .Append(Escape(library)).Append("\", \"").Append(Escape(entry)).AppendLine("\", ex); }");
        builder.Append(indent).AppendLine("}");
        return builder.ToString().TrimEnd('\r', '\n');
    }

    private static IReadOnlyList<string> ExtractArgumentNames(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters)) return Array.Empty<string>();
        var result = new List<string>();
        foreach (var parameter in SplitParameters(parameters))
        {
            var match = Regex.Match(parameter.Trim(), @"(?<name>[A-Za-z_]\w*)\s*$");
            if (!match.Success)
                throw new CompilerException("Unable to generate native interop wrapper for parameter: " + parameter.Trim());
            result.Add(match.Groups["name"].Value);
        }
        return result;
    }

    private static IReadOnlyList<string> SplitParameters(string value)
    {
        var result = new List<string>();
        var start = 0;
        var angleDepth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '<') angleDepth++;
            else if (value[i] == '>') angleDepth--;
            else if (value[i] == ',' && angleDepth == 0)
            {
                result.Add(value[start..i]);
                start = i + 1;
            }
        }
        result.Add(value[start..]);
        return result;
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
