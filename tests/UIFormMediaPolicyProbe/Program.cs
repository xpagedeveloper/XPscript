using XPScript.Compiler;

const string source = """
Sub Main()
    Dim form As New UIForm("MediaPolicy")
    Dim runtimeImage As XPImage
    Set runtimeImage = New XPImage(8, 8, "#336699")
    form.BootImage = runtimeImage
    Call form.AddImage("Preview", runtimeImage)
End Sub
""";

static string Compile(string source, string target)
    => new XPScriptTranspiler().Transpile(source, "media-policy.xps", target);

var desktop = Compile(source, "linux-x64");
var android = Compile(source, "android-arm64");
var browser = Compile(source, "browser-wasm");

const string fileClause = "uri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)";
const string webGuard = "EnsureWebSafeMediaSource";
const string webFileError = "source cannot expose a local filesystem path through server-web rendering.";
const string imageWebGuard = "EnsureWebSafeMediaSource(field.ImageSource, \"image\")";
const string bootImageWebGuard = "EnsureWebSafeMediaSource(_bootImage, \"boot image\")";

foreach (var (target, generated) in new[] { ("desktop", desktop), ("android", android), ("browser", browser) })
{
    if (!generated.Contains(webGuard, StringComparison.Ordinal) ||
        !generated.Contains(imageWebGuard, StringComparison.Ordinal) ||
        !generated.Contains(bootImageWebGuard, StringComparison.Ordinal))
        throw new Exception($"Generated UIForm media policy must guard Image and BootImage server-web rendering for {target}: web={generated.Contains(webGuard, StringComparison.Ordinal)}, fileError={generated.Contains(webFileError, StringComparison.Ordinal)}, image={generated.Contains(imageWebGuard, StringComparison.Ordinal)}, boot={generated.Contains(bootImageWebGuard, StringComparison.Ordinal)}.");
}

if (!desktop.Contains("true && " + fileClause, StringComparison.Ordinal))
    throw new Exception("Desktop UIForm media policy must permit local file URIs.");
if (!android.Contains("true && " + fileClause, StringComparison.Ordinal))
    throw new Exception("Android UIForm media policy must permit accessible local file URIs.");
if (!browser.Contains("false && " + fileClause, StringComparison.Ordinal))
    throw new Exception("Browser-WASM UIForm media policy must reject local file URIs.");

Console.WriteLine("UIForm media target policy probe passed.");
