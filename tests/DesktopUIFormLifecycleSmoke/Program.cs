using System.Text.Json;
using XPScript.Compiler;
using XPScript.UI.Desktop;

var parseDetachedRequest = typeof(DesktopFormLifecycleHost).GetMethod(
    "ParseDetachedRequest",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
    ?? throw new InvalidOperationException("Desktop modeless UI request detachment helper is missing.");

var detachedArguments = new object?[]
{
    "{\"instanceId\":\"detached-smoke\",\"title\":\"Detached request\",\"fields\":[]}",
    null
};

var detachedRoot = (JsonElement)(parseDetachedRequest.Invoke(null, detachedArguments)
    ?? throw new InvalidOperationException("Desktop modeless UI request was not parsed."));

if (!string.Equals(detachedArguments[1] as string, "detached-smoke", StringComparison.Ordinal))
    throw new InvalidOperationException("Desktop modeless UI request instance id was not preserved.");
if (detachedRoot.GetProperty("title").GetString() != "Detached request")
    throw new InvalidOperationException("Desktop modeless UI request remained tied to a disposed JsonDocument.");

var modalChildSource = """
Option Declare

Sub Main()
    Dim first As New UIForm("First form", 480, 320, True)
    Call first.AddTextField("name", "Name")
    Call first.AddButtonCallback("hello", "Klicka här", "OpenSecondForm")
    Call first.Show(True)
End Sub

Sub OpenSecondForm(evt As Variant)
    Dim secondForm As New UIForm("Second form", 480, 320, True)
    Call secondForm.AddTextField("value", "Value")
    Call secondForm.AddButtonCallback("close", "Close", "CloseSecondForm")
    Call secondForm.Show(False)
End Sub

Sub CloseSecondForm(evt As Variant)
    Call evt.Form.Close()
End Sub
""";

var generated = new XPScriptTranspiler().Transpile(modalChildSource, "uiform-modal-child-smoke.xps", "win-x64");
foreach (var expected in new[]
{
    "public void Show(object? modalValue)",
    "XPScriptCallbackRuntime.Invoke(",
    "XPScriptUIDesktopAdapter.Show(this, _fields, _data, ApplyDesktopValue, ApplyDesktopValues);",
    "new XPScriptUIFormEvent(this, \"button\""
})
{
    if (!generated.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException("Generated modal-parent/modeless-child callback path is missing: " + expected);
}

Console.WriteLine("DESKTOP_UIFORM_DETACHED_REQUEST_OK");
Console.WriteLine("DESKTOP_UIFORM_MODAL_CHILD_TRANSPILE_OK");
