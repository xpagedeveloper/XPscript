namespace XPScript.Compiler;

internal static class SpreadsheetRuntimeSource
{
    public static readonly string Code = SpreadsheetRuntimeCoreSource.Code + "\n" + SpreadsheetRuntimeObjectsSource.Code;
}
