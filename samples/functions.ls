Option Declare

Function AddNumbers(a As Long, b As Long) As Long
    AddNumbers = a + b
End Function

Sub Main()
    Dim answer As Long
    answer = AddNumbers(20, 22)

    If answer = 42 Then
        MsgBox("The answer is 42")
    End If
End Sub
