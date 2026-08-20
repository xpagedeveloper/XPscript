using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class HashFunctionsPreprocessor
{
    private static readonly string[] Functions =
    [
        "HMACSHA512",
        "HMACSHA384",
        "HMACSHA256",
        "SHA512",
        "SHA384",
        "SHA256",
        "SHA1",
        "MD5"
    ];

    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var function in Functions)
        {
            source = Regex.Replace(
                source,
                $@"(?<![A-Za-z0-9_\.]){Regex.Escape(function)}\s*\(",
                $"XPScriptHashRuntime.{function}(",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return source;
    }
}
