using XPScript.Compiler;

const string source = """
Sub Main()
    Dim form As New UIForm("MediaPolicy")
    Dim runtimeImage As XPImage
    Set runtimeImage = New XPImage(8, 8, "#336699")
    form.BootImage = runtimeImage
    Call form.AddImage("Preview", runtimeImage)
    Call form.AddVideo("Intro", "Intro video")
End Sub
""";

static string Compile(string source, string target)
    => new XPScriptTranspiler().Transpile(source, "media-policy.xps", target);

var desktop = Compile(source, "linux-x64");
var android = Compile(source, "android-arm64");
var browser = Compile(source, "browser-wasm");

const string fileClause = "uri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase)";
const string httpClause = "uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)";
const string httpsClause = "uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)";
const string dataClause = "uri.Scheme.Equals(\"data\", StringComparison.OrdinalIgnoreCase)";
const string unsupported = "source uses an unsupported URI scheme";
const string webGuard = "EnsureWebSafeMediaSource";
const string webFileError = "source cannot expose a local filesystem path through server-web rendering.";
const string imageWebGuard = "EnsureWebSafeMediaSource(field.ImageSource, \"image\")";
const string bootImageWebGuard = "EnsureWebSafeMediaSource(_bootImage, \"boot image\")";
const string webDataImageCheck = "!text.StartsWith(\"data:image/\", StringComparison.OrdinalIgnoreCase)";
const string webDataImageError = "UIForm server-web data URI must use an image media type.";
const string rootedPathCheck = "System.IO.Path.IsPathRooted(text)";
const string windowsRootedPathCheck = "text.Length >= 3 && char.IsLetter(text[0]) && text[1] == ':'";
const string rootedPathReturn = "if (allowLocalPaths && isRootedPath) return text;";
const string xpImageCheck = "value.GetType().Name.Equals(\"XPImage\", StringComparison.Ordinal)";
const string xpImageDataImage = "return \"data:image/png;base64,\" + Convert.ToBase64String(bytes);";

foreach (var (target, generated) in new[] { ("desktop", desktop), ("android", android), ("browser", browser) })
{
    if (!generated.Contains(httpClause, StringComparison.Ordinal) ||
        !generated.Contains(httpsClause, StringComparison.Ordinal) ||
        !generated.Contains(dataClause, StringComparison.Ordinal) ||
        !generated.Contains(unsupported, StringComparison.Ordinal))
        throw new Exception($"Generated UIForm media policy is missing the shared HTTP/HTTPS/data-image scheme guard for {target}: http={generated.Contains(httpClause, StringComparison.Ordinal)}, https={generated.Contains(httpsClause, StringComparison.Ordinal)}, data={generated.Contains(dataClause, StringComparison.Ordinal)}, unsupported={generated.Contains(unsupported, StringComparison.Ordinal)}.");
    if (!generated.Contains(webGuard, StringComparison.Ordinal) ||
        !generated.Contains(webFileError, StringComparison.Ordinal) ||
        !generated.Contains(imageWebGuard, StringComparison.Ordinal) ||
        !generated.Contains(bootImageWebGuard, StringComparison.Ordinal))
        throw new Exception("Generated UIForm media policy must guard Image and BootImage server-web rendering from local filesystem sources.");
    if (!generated.Contains(webDataImageCheck, StringComparison.Ordinal) ||
        !generated.Contains(webDataImageError, StringComparison.Ordinal))
        throw new Exception("Generated UIForm server-web media policy must reject non-image data URIs.");
    if (!generated.Contains(rootedPathCheck, StringComparison.Ordinal) ||
        !generated.Contains(windowsRootedPathCheck, StringComparison.Ordinal) ||
        !generated.Contains(rootedPathReturn, StringComparison.Ordinal))
        throw new Exception("Generated UIForm media policy must explicitly handle rooted local filesystem image paths.");
    if (!generated.Contains(xpImageCheck, StringComparison.Ordinal) ||
        !generated.Contains(xpImageDataImage, StringComparison.Ordinal))
        throw new Exception("Generated UIForm media policy must normalize XPImage sources to data-image content on every target.");
}

if (!desktop.Contains("true && " + fileClause, StringComparison.Ordinal))
    throw new Exception("Desktop UIForm media policy must permit local file URIs.");
if (!android.Contains("true && " + fileClause, StringComparison.Ordinal))
    throw new Exception("Android UIForm media policy must permit accessible local file URIs.");
if (!browser.Contains("false && " + fileClause, StringComparison.Ordinal))
    throw new Exception("Browser-WASM UIForm media policy must reject local file URIs.");

Console.WriteLine("UIForm media target policy probe passed.");
