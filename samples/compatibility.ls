Option Declare

Sub Main()
    Dim f As Integer
    Dim lineText As String
    Dim d As Date
    Dim n As Double

    Print UCase("ls lite")
    Print FullTrim("  a   b  ")
    Print Hex(255)
    Print Bin(10)
    Print CStr(Sgn(-4))

    d = DateNumber(2026, 8, 11)
    Print CStr(Year(d))
    Print CStr(Month(d))
    Print CStr(Day(d))
    Print CStr(DateDiff("d", d, DateAdd("d", 5, d)))

    n = Val("123.5xyz")
    Print CStr(n)

    f = FreeFile()
    Open "lslite-compat.txt" For Output As #f
    Print #f, "FILE-OK"
    Close #f

    f = FreeFile()
    Open "lslite-compat.txt" For Input As #f
    Line Input #f, lineText
    Close #f
    Print lineText
    Print CStr(FileLen("lslite-compat.txt"))
    Kill "lslite-compat.txt"
End Sub
