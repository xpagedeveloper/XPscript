Option Declare

Sub Main()
    Dim json As New NotesJSONNavigator("")
    Dim parsed As New NotesJSONNavigator("{""name"":""Fredrik"",""items"":[1,2,3]}")
    Dim address As NotesJSONObject
    Dim roles As NotesJSONArray
    Dim element As NotesJSONElement
    Dim secondElement As NotesJSONElement
    Dim copied As NotesJSONNavigator
    Dim copiedObject As NotesJSONObject
    Dim http As New NotesHTTPRequest
    Dim response As Variant
    Dim responseJson As NotesJSONNavigator
    Dim headers As Variant

    Call json.AppendElement("Fredrik", "name")
    Call json.AppendElement(40, "age")
    Call json.AppendElement(True, "active")

    Set address = json.AppendObject("address")
    Call address.AppendElement("Linkoping", "city")
    Call address.AppendElement("Sweden", "country")

    Set roles = json.AppendArray("roles")
    Call roles.AppendElement("admin")
    Call roles.AppendElement("developer")

    Print "JSON=" & json.Stringify()
    Print "OBJECTSIZE=" & CStr(address.Size)
    Print "ARRAYSIZE=" & CStr(roles.Size)

    Set element = parsed.GetElementByName("name")
    Print "ELEMENT=" & element.Name & ":" & CStr(element.Type) & ":" & CStr(element.Value)

    Set element = parsed.GetElementByPointer("/items/1")
    Print "POINTER=" & CStr(element.Value)

    Set element = roles.GetNthElement(2)
    Print "NTH=" & CStr(element.Value)

    Set element = roles.GetFirstElement()
    Set secondElement = roles.GetNextElement()
    Print "ITER=" & CStr(element.Value) & ":" & CStr(secondElement.Value)

    Set copied = New NotesJSONNavigator("")
    Set copiedObject = copied.AppendObject("copy")
    Call copiedObject.Copy(address)
    Print "COPY=" & copied.Stringify()

    If Jsonelem_type_object = 1 And Jsonelem_type_array = 2 And Jsonelem_type_string = 3 And Jsonelem_type_number = 4 And Jsonelem_type_boolean = 5 And Jsonelem_type_utf8_bytearray = 6 And Jsonelem_type_empty = 64 Then
        Print "JSONCONSTANTS=OK"
    End If

    http.TimeoutSec = 10
    http.MaxRedirects = 2
    http.PreferStrings = True
    Call http.SetHeaderField("Accept", "application/json")
    Call http.SetHeaderField("Authorization", "Bearer abc123")

    response = http.Get("http://127.0.0.1:18999/get")
    Print "GETCODE=" & CStr(http.ResponseCode)
    Print "GET=" & response

    response = http.Post("http://127.0.0.1:18999/post", json.Stringify())
    Print "POSTCODE=" & CStr(http.ResponseCode)
    Print "POST=" & response

    response = http.Put("http://127.0.0.1:18999/put", "{""value"":1}")
    Print "PUTCODE=" & CStr(http.ResponseCode)

    response = http.Patch("http://127.0.0.1:18999/patch", "{""value"":2}")
    Print "PATCHCODE=" & CStr(http.ResponseCode)

    response = http.DeleteResource("http://127.0.0.1:18999/delete")
    Print "DELETECODE=" & CStr(http.ResponseCode)

    headers = http.GetResponseHeaders()
    If IsArray(headers) Then
        Print "HEADERS=OK"
    End If

    Call http.ResetHeaders()
    Call http.SetProxy("127.0.0.1", 8080)
    Call http.SetProxyUser("user", "password")
    Call http.ResetProxy()
    Print "PROXY=OK"

    http.PreferStrings = False
    http.PreferJSONNavigator = True
    Set responseJson = http.Post("http://127.0.0.1:18999/json", "{""hello"":""world""}")
    Set element = responseJson.GetElementByName("method")
    Print "HTTPJSON=" & CStr(element.Value)
End Sub
