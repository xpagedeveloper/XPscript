Option Declare

Sub Main()
    Dim parser As NotesSAXParser
    Dim shellResult As Long
    Dim calc As Variant
    Dim answer As Long
    Dim ole As Variant

    If Len(Environ("PATH")) > 0 Then
        Print "ENV=OK"
    End If

    If Len(Format(0.125, "Percent")) > 0 Then
        Print "FORMAT=OK"
    End If
    If Len(Format$(12.5, "Fixed")) > 0 Then
        Print "FORMATDOLLAR=OK"
    End If
    If Len(FormatNumber(12.5, 2)) > 0 Then
        Print "FORMATNUMBER=OK"
    End If
    If Len(FormatPercent(0.125, 1)) > 0 Then
        Print "FORMATPERCENT=OK"
    End If

    Print "ERROR53=" & Error$(53)

    If False Then
        Print InputBox("prompt", "title", "default")
        ole = GetObject("", "Scripting.Dictionary")
        Beep
        Stop
    End If

    On Error GoTo MissingHandler
    Open "__lslite_missing_runtime_file__.txt" For Input As #1
    Print "FILEERR=RESUMED"
    GoTo MissingDone
MissingHandler:
    Print "FILEERR=" & CStr(Err) & ":" & Error$ & ":" & CStr(Erl)
    Resume Next
MissingDone:
    On Error GoTo 0

    Sleep 0
    shellResult = Shell("cmd.exe /c exit 0")
    Print "SHELL=" & CStr(shellResult)

    calc = Evaluate("1+2*3")
    Print "EVALUATE=" & CStr(calc)

    answer = MessageBox("CONSOLE-MSG", 0, "LS Lite")
    Print "MESSAGEBOX=" & CStr(answer)

    Set parser = New NotesSAXParser("<root id=""7""><child>text</child></root>")
    On Event SAX_StartDocument From parser Call SAXStartDocument
    On Event SAX_StartElement From parser Call SAXStartElement
    On Event SAX_Characters From parser Call SAXCharacters
    On Event SAX_EndElement From parser Call SAXEndElement
    On Event SAX_EndDocument From parser Call SAXEndDocument
    parser.Process
End Sub

Sub SAXStartDocument(Source As NotesSAXParser)
    Print "SAX=STARTDOC"
End Sub

Sub SAXStartElement(Source As NotesSAXParser, ByVal ElementName As String, Attributes As NotesSAXAttributeList)
    Print "SAX=START:" & ElementName & ":" & CStr(Attributes.Length)
    If Attributes.Length > 0 Then
        Print "SAX=ATTR:" & Attributes.GetName(1) & "=" & Attributes.GetValue(1) & ":" & Attributes.GetType(1)
    End If
End Sub

Sub SAXCharacters(Source As NotesSAXParser, ByVal Characters As String, Count As Long)
    Print "SAX=TEXT:" & Characters & ":" & CStr(Count)
End Sub

Sub SAXEndElement(Source As NotesSAXParser, ByVal ElementName As String)
    Print "SAX=END:" & ElementName
End Sub

Sub SAXEndDocument(Source As NotesSAXParser)
    Print "SAX=ENDDOC"
End Sub
