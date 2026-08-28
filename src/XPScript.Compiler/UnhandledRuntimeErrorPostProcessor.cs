using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class UnhandledRuntimeErrorPostProcessor
{
    private static readonly Regex MainBodyPattern = new(
        @"(?ms)(?<header>public\s+static\s+void\s+Main\s*\(\s*string\[\]\s+args\s*\)\s*\{\s*)(?<body>.*?)(?<footer>^\s{4}\})",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        var match = MainBodyPattern.Match(generated);
        if (!match.Success)
            throw new CompilerException("Unable to install XPscript runtime error boundary in generated program.");

        var body = Indent(match.Groups["body"].Value.Trim(), 8);
        var replacement =
            match.Groups["header"].Value + Environment.NewLine +
            "        try" + Environment.NewLine +
            "        {" + Environment.NewLine +
            body + Environment.NewLine +
            "        }" + Environment.NewLine +
            "        catch (Exception ex)" + Environment.NewLine +
            "        {" + Environment.NewLine +
            "            Console.Error.WriteLine(XPScriptUnhandledRuntimeError.Format(ex));" + Environment.NewLine +
            "            Environment.ExitCode = 1;" + Environment.NewLine +
            "        }" + Environment.NewLine +
            match.Groups["footer"].Value;

        generated = generated[..match.Index] + replacement + generated[(match.Index + match.Length)..];
        return generated + Environment.NewLine + Environment.NewLine + RuntimeHelper;
    }

    private static string Indent(string value, int spaces)
    {
        var prefix = new string(' ', spaces);
        return string.Join(
            Environment.NewLine,
            value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n')
                .Select(line => prefix + line.TrimStart()));
    }

    private const string RuntimeHelper = """
internal static class XPScriptUnhandledRuntimeError
{
    public static string Format(Exception exception)
    {
        var ex = Unwrap(exception);
        if (string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_DEBUG_RUNTIME_ERRORS"), "1", StringComparison.Ordinal))
            return ex.ToString();

        var description = ex switch
        {
            XPScriptRuntimeException runtime => runtime.Message,
            DivideByZeroException => "Division by zero.",
            NullReferenceException => "Object reference is Nothing.",
            InvalidCastException => "Invalid type conversion.",
            IndexOutOfRangeException => "Index out of range.",
            ArgumentOutOfRangeException => "Index or argument is out of range.",
            _ when string.Equals(ex.GetType().FullName, "Microsoft.CSharp.RuntimeBinder.RuntimeBinderException", StringComparison.Ordinal) =>
                "Invalid object or member access.",
            _ => "An unexpected runtime error occurred."
        };

        var location = TryGetSourceLocation(ex);
        return location.Length == 0
            ? "XPscript runtime error: " + description
            : "XPscript runtime error at " + location + ": " + description;
    }

    private static Exception Unwrap(Exception exception)
    {
        var current = exception;
        while (current is System.Reflection.TargetInvocationException && current.InnerException is not null)
            current = current.InnerException;
        return current;
    }

    private static string TryGetSourceLocation(Exception exception)
    {
        var stack = exception.StackTrace ?? string.Empty;
        var match = System.Text.RegularExpressions.Regex.Match(
            stack,
            @"(?<file>[A-Za-z0-9_.-]+\.xps):line\s+(?<line>\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["file"].Value + ":" + match.Groups["line"].Value : string.Empty;
    }
}
""";
}
