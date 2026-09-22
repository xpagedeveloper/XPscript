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
            source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, function, $"XPScriptHashRuntime.{function}");
        }

        return source;
    }
}
