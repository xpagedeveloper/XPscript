namespace XPScript.Compiler;

internal static class HashFunctionsRuntimeSource
{
    public const string Code = """
internal static class XPScriptHashRuntime
{
    public static string MD5(object? value)
        => Hash(System.Security.Cryptography.MD5.HashData(Bytes(value)));

    public static string SHA1(object? value)
        => Hash(System.Security.Cryptography.SHA1.HashData(Bytes(value)));

    public static string SHA256(object? value)
        => Hash(System.Security.Cryptography.SHA256.HashData(Bytes(value)));

    public static string SHA384(object? value)
        => Hash(System.Security.Cryptography.SHA384.HashData(Bytes(value)));

    public static string SHA512(object? value)
        => Hash(System.Security.Cryptography.SHA512.HashData(Bytes(value)));

    public static string HMACSHA256(object? value, object? key)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(Bytes(key));
        return Hash(hmac.ComputeHash(Bytes(value)));
    }

    public static string HMACSHA384(object? value, object? key)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA384(Bytes(key));
        return Hash(hmac.ComputeHash(Bytes(value)));
    }

    public static string HMACSHA512(object? value, object? key)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA512(Bytes(key));
        return Hash(hmac.ComputeHash(Bytes(value)));
    }

    private static byte[] Bytes(object? value)
        => System.Text.Encoding.UTF8.GetBytes(XPScriptRuntime.CStr(value));

    private static string Hash(byte[] bytes)
        => Convert.ToHexString(bytes).ToLowerInvariant();
}
""";
}
