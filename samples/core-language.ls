Option Declare
Option Base 1
DefInt A-C

Declare Function GetTickCount Lib "kernel32.dll" Alias "GetTickCount" () As Long
Declare Sub Sleep Lib "kernel32.dll" Alias "Sleep" (ByVal milliseconds As Long)

Class Person
    Public Name As String
End Class

Sub Increment(ByRef value As Long)
    value = value + 1
End Sub

Sub TouchArray(values() As Long)
    values(1) = 99
End Sub

Function Counter() As Long
    Static count As Long
    count = count + 1
    Counter = count
End Function

Static Function ProcedureCounter() As Long
    Dim count As Long
    count = count + 1
    ProcedureCounter = count
End Function

Sub Main()
    Dim aValue As Long
    Dim autoArray(2) As Long
    Dim matrix(1 To 2, 0 To 1) As Long
    Dim passed(1 To 2) As Long
    Dim names() As String
    Dim p As Person
    Dim f As Integer
    Dim binaryValue As Long
    Dim randomValue As Long
    Dim divisor As Long
    Dim apple

    aValue = 10
    Call Increment(aValue)
    Print "BYREF=" & CStr(aValue)

    autoArray(1) = 7
    autoArray(2) = 8
    Print "AUTO=" & CStr(LBound(autoArray)) & ":" & CStr(UBound(autoArray)) & ":" & CStr(autoArray(2))

    matrix(1, 0) = 12
    matrix(2, 1) = 24
    Print "MATRIX=" & CStr(LBound(matrix, 1)) & ":" & CStr(UBound(matrix, 2)) & ":" & CStr(matrix(2, 1))

    passed(1) = 1
    Call TouchArray(passed)
    Print "ARRAYREF=" & CStr(passed(1))

    ReDim names(1 To 2) As String
    names(1) = "A"
    names(2) = "B"
    ReDim Preserve names(1 To 3)
    names(3) = "C"
    Print "REDIM=" & names(1) & names(2) & names(3)

    Select Case aValue
    Case 1
        Print "SELECT=ONE"
    Case 2 To 10
        Print "SELECT=RANGE"
    Case Is > 10
        Print "SELECT=HIGH"
    Case Else
        Print "SELECT=ELSE"
    End Select

    Set p = New Person()
    With p
        .Name = "WITH-OK"
        Print .Name
    End With

    Print "STATIC=" & CStr(Counter()) & ":" & CStr(Counter())
    Print "STATICPROC=" & CStr(ProcedureCounter()) & ":" & CStr(ProcedureCounter())

    apple = 42
    Print "DEFTYPE=" & TypeName(apple) & ":" & CStr(apple)

    Call Sleep(0)
    aValue = GetTickCount()
    If aValue >= 0 Then
        Print "DECLARE=OK"
    End If

    GoSub Worker
    Print "GOSUB=RETURNED"
    GoTo AfterWorker
Worker:
    Print "GOSUB=CALLED"
    Return
AfterWorker:

    GoTo SkipLine
    Print "GOTO=BAD"
SkipLine:
    Print "GOTO=OK"

    Print "ERRORFN=" & Error(53)

    On Error GoTo ErrorHandler
    Error 123, "expected-error"
    Print "RESUME=OK"
    GoTo ErrorDone
ErrorHandler:
    Print "ERR=" & CStr(Err) & ":" & Error()
    Resume Next
ErrorDone:
    On Error GoTo 0

    divisor = 0
    On Error GoTo RetryHandler
    aValue = 10 / divisor
    Print "RESUME-CURRENT=" & CStr(aValue)
    GoTo RetryDone
RetryHandler:
    divisor = 2
    Resume
RetryDone:
    On Error GoTo 0

    On Error GoTo LabelHandler
    Error 126, "resume-label-error"
    Print "RESUME-LABEL=BAD"
ResumeTarget:
    Print "RESUME-LABEL=OK"
    GoTo LabelDone
LabelHandler:
    Resume ResumeTarget
LabelDone:
    On Error GoTo 0

    On Error Resume Next
    Error 124, "resume-next-error"
    Print "RESUME-NEXT=" & CStr(Err)
    On Error GoTo 0

    f = FreeFile()
    Open "lslite-core.bin" For Binary As #f
    binaryValue = 123456
    Put #f, 1, binaryValue
    binaryValue = 0
    Get #f, 1, binaryValue
    Print "BINARY=" & CStr(binaryValue) & ":" & CStr(Loc(f))
    Close #f
    Kill "lslite-core.bin"

    f = FreeFile()
    Open "lslite-random.bin" For Random As #f Len = 16
    randomValue = 77
    Put #f, 2, randomValue
    randomValue = 0
    Get #f, 2, randomValue
    Print "RANDOM=" & CStr(randomValue) & ":" & CStr(Loc(f))
    Close #f
    Kill "lslite-random.bin"
End Sub
