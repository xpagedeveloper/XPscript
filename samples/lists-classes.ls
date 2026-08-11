Option Declare

Class Person
    Private mName As String
    Private mRole As String

    Sub New(name As String, role As String)
        Me.mName = name
        Me.mRole = role
    End Sub

    Public Property Get Name As String
        Name = Me.mName
    End Property

    Public Property Set Name As String
        Me.mName = Name
    End Property

    Public Function Describe() As String
        Describe = Me.mName & ":" & Me.mRole
    End Function

    Sub Delete()
        Me.mRole = "deleted"
    End Sub
End Class

Sub Main()
    Dim users List As String
    Dim p As Person
    Dim q As Person

    users("admin") = "Fredrik"
    users("guest") = "Guest"

    If IsElement(users("admin")) Then
        Print "ADMIN=" & users("admin")
    End If

    ForAll value In users
        Print ListTag(value) & ":" & value
    End ForAll

    Erase users("guest")
    If Not IsElement(users("guest")) Then
        Print "GUEST-ERASED"
    End If

    Set p = New Person("Fredrik", "Admin")
    Set q = p
    Print p.Name
    p.Name = "Fno"
    Print q.Describe()

    Delete p
    If q Is Nothing Then
        Print "OBJECT-DELETED"
    End If
End Sub
