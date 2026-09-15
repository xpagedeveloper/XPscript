namespace XPScript.Compiler;

internal static class SpreadsheetRuntimeSource
{
    public static readonly string Code = SpreadsheetRuntimePart1Source.Code + "\n"
        + SpreadsheetRuntimePart2Source.Code + "\n"
        + SpreadsheetRuntimePart3Source.Code + "\n"
        + SpreadsheetRuntimePart4Source.Code + "\n"
        + SpreadsheetRuntimePart5Source.Code + "\n"
        + NativeCsvRuntimeSource.Code + "\n"
        + SpreadsheetCsvInteropRuntimeSource.Code;
}
