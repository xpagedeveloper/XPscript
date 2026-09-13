using XPScript.Compiler;

var transpiler = new XPScriptTranspiler();

const string source = """
Option Declare
Sub Main()
    Dim book As New xPsPrEaDsHeEt()
    Dim sheet As xPwOrKsHeEt
    Dim cell As xPcElL
    Set sheet = book.aDdWoRkShEeT("Sales")
    Set cell = sheet.cElL("A1")
    cell.vAlUe = "Hello"
    book.sAvEaS("probe.xlsx")
End Sub
""";

var generated = transpiler.Transpile(source, "spreadsheet-probe.xps", "win-x64");
if (!generated.Contains("XPScriptSpreadsheetFactory.Create", StringComparison.Ordinal))
    throw new Exception("XPSpreadsheet constructor was not lowered to the native runtime factory.");
if (!generated.Contains("internal sealed class XPScriptSpreadsheet", StringComparison.Ordinal))
    throw new Exception("XPSpreadsheet runtime was not injected.");
if (!generated.Contains(".AddWorksheet(", StringComparison.Ordinal)
    || !generated.Contains(".Cell(", StringComparison.Ordinal)
    || !generated.Contains(".Value", StringComparison.Ordinal)
    || !generated.Contains(".SaveAs(", StringComparison.Ordinal))
    throw new Exception("XPSpreadsheet members were not normalized case-insensitively.");

const string unrelated = """
Option Declare
Sub Main()
    Print "no spreadsheet"
End Sub
""";

var unrelatedGenerated = transpiler.Transpile(unrelated, "no-spreadsheet.xps", "win-x64");
if (unrelatedGenerated.Contains("internal sealed class XPScriptSpreadsheet", StringComparison.Ordinal))
    throw new Exception("XPSpreadsheet runtime was injected into a program that does not use it.");

try
{
    _ = transpiler.Transpile(source, "spreadsheet-wasm.xps", "browser-wasm");
    throw new Exception("XPSpreadsheet unexpectedly compiled for browser-wasm.");
}
catch (CompilerException ex)
{
    if (!ex.Message.Contains("XPSpreadsheet", StringComparison.Ordinal)
        || !ex.Message.Contains("browser-wasm", StringComparison.OrdinalIgnoreCase))
        throw new Exception("XPSpreadsheet returned the wrong browser-wasm diagnostic: " + ex.Message);
}

Console.WriteLine("XPSPREADSHEET-CAPABILITY-PROBE=OK");
