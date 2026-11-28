using XPScript.Compiler;

var workspace = Path.GetFullPath(Path.Combine("out", "spreadsheet-capability-probe", Guid.NewGuid().ToString("N")));
Directory.CreateDirectory(workspace);

async Task<string> GenerateAsync(string sourceText, string filename)
{
    var path = Path.Combine(workspace, filename);
    await File.WriteAllTextAsync(path, sourceText);
    return await AstCompiler.GenerateSourceAsync(path, workspace);
}

const string source = """
Option Declare
Sub Main()
    Dim book As New xPsPrEaDsHeEt()
    Dim sheet As xPwOrKsHeEt
    Dim cell As xPcElL
    Dim area As xPrAnGe
    Set sheet = book.aDdWoRkShEeT("Sales")
    Set cell = sheet.cElL("A1")
    cell.vAlUe = "Hello"
    cell.bAcKgRoUnDcOlOr = "#FFFF00"
    cell.bOlD = True
    cell.iTaLiC = True
    cell.fOnTcOlOr = "blue"
    cell.fOnTsIzE = 12
    cell.fOnTnAmE = "Arial"
    cell.nUmBeRfOrMaT = "#,##0.00"
    cell.hOrIzOnTaLaLiGnMeNt = "center"
    cell.vErTiCaLaLiGnMeNt = "center"
    cell.wRaPtExT = True
    cell.bOrDeRbOtToM = "thin"
    cell.bOrDeRcOlOr = "black"
    Set area = sheet.rAnGe("A1:C3")
    area.bOlD = True
    area.cOlUmNwIdTh = 20
    area.rOwHeIgHt = 24
    area.aUtOfIt()
    sheet.rAnGe("A1:C3").bAcKgRoUnDcOlOr = "yellow"
    Dim values As Variant
    Set area = sheet.rAnGe("A1:A3")
    values = area.vAlUeS
    sheet.aUtOfIlTeR("A1:C3")
    sheet.cLeArAuToFiLtEr()
    sheet.cElL("B1").vAlUe = "Direct"
    sheet.cElL("C1").fOrMuLa = "=1+1"
    Print CStr(book.cReAtEdByXpScRiPt)
    Print CStr(book.xPsCrIpTfOrMaTvErSiOn)
    Print CStr(book.cAnUpDaTe)
    Dim data As Variant
    data = book.tObYtEs()
    book.fRoMbYtEs(data)
    book.sAvEaS("probe.xlsx")
End Sub
""";

var generated = await GenerateAsync(source, "spreadsheet-probe.xps");
if (!generated.Contains("XPScriptSpreadsheetFactory.Create", StringComparison.Ordinal))
    throw new Exception("XPSpreadsheet constructor was not lowered to the native runtime factory.");
if (!generated.Contains("internal sealed class XPScriptSpreadsheet", StringComparison.Ordinal)
    || !generated.Contains("internal sealed class XPScriptSpreadsheetRange", StringComparison.Ordinal))
    throw new Exception("XPSpreadsheet or XPRange runtime was not injected.");
if (!generated.Contains(".AddWorksheet(", StringComparison.Ordinal)
    || !generated.Contains(".Cell(", StringComparison.Ordinal)
    || !generated.Contains(".Range(", StringComparison.Ordinal)
    || !generated.Contains(".Values", StringComparison.Ordinal)
    || !generated.Contains(".FontColor", StringComparison.Ordinal)
    || !generated.Contains(".FontSize", StringComparison.Ordinal)
    || !generated.Contains(".FontName", StringComparison.Ordinal)
    || !generated.Contains(".NumberFormat", StringComparison.Ordinal)
    || !generated.Contains(".HorizontalAlignment", StringComparison.Ordinal)
    || !generated.Contains(".VerticalAlignment", StringComparison.Ordinal)
    || !generated.Contains(".WrapText", StringComparison.Ordinal)
    || !generated.Contains(".BorderBottom", StringComparison.Ordinal)
    || !generated.Contains(".BorderColor", StringComparison.Ordinal)
    || !generated.Contains(".ColumnWidth", StringComparison.Ordinal)
    || !generated.Contains(".RowHeight", StringComparison.Ordinal)
    || !generated.Contains(".AutoFit(", StringComparison.Ordinal)
    || !generated.Contains(".AutoFilter(", StringComparison.Ordinal)
    || !generated.Contains(".ClearAutoFilter(", StringComparison.Ordinal)
    || !generated.Contains(".Value =", StringComparison.Ordinal)
    || !generated.Contains(".BackgroundColor =", StringComparison.Ordinal)
    || !generated.Contains(".ToBytes(", StringComparison.Ordinal)
    || !generated.Contains(".FromBytes(", StringComparison.Ordinal))
    throw new Exception("XPSpreadsheet range, formatting, filter, or direct-assignment members were not lowered correctly.");

if (!generated.Contains("XPScriptWorkbookVersion", StringComparison.Ordinal)
    || !generated.Contains("CurrentFormatVersion = 2", StringComparison.Ordinal)
    || !generated.Contains("xl/styles.xml", StringComparison.Ordinal)
    || !generated.Contains("SaveAsSimple", StringComparison.Ordinal))
    throw new Exception("XPSpreadsheet marker, styles, or safe conversion support was not emitted.");

const string csvSource = """
Option Declare
Sub Main()
    Dim csv As New XPCsvDocument
    csv.Encoding = "utf-8"
    Dim data As Variant
    data = csv.ToBytes()
    csv.FromBytes(data)
    csv.FromBytes(data, "utf-8")
End Sub
""";

var csvGenerated = await GenerateAsync(csvSource, "csv-bytes-probe.xps");
if (!csvGenerated.Contains(".ToBytes()", StringComparison.Ordinal)
    || !csvGenerated.Contains("XPScriptNativeCsv.ParseBytes", StringComparison.Ordinal)
    || csvGenerated.Contains(".FromBytes(", StringComparison.OrdinalIgnoreCase))
    throw new Exception("XPCsvDocument byte methods were not lowered correctly.");

const string unrelated = """
Option Declare
Sub Main()
    Print "no spreadsheet"
End Sub
""";

var unrelatedGenerated = await GenerateAsync(unrelated, "no-spreadsheet.xps");
if (unrelatedGenerated.Contains("internal sealed class XPScriptSpreadsheet", StringComparison.Ordinal))
    throw new Exception("XPSpreadsheet runtime was injected into a program that does not use it.");

if (!generated.Contains("System.OperatingSystem.IsBrowser()", StringComparison.Ordinal) ||
    !generated.Contains("XPSpreadsheet file operations are not available in browser-wasm.", StringComparison.Ordinal))
    throw new Exception("XPSpreadsheet did not emit the browser file-operation guard.");

Console.WriteLine("XPSPREADSHEET-CAPABILITY-PROBE=OK");
