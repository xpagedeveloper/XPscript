namespace XPScript.Web.FastCgi;

internal static class StringComparisonExtensions
{
    internal static bool StartsWith(this string value, char prefix, StringComparison comparison) =>
        value.StartsWith(prefix.ToString(), comparison);
}
