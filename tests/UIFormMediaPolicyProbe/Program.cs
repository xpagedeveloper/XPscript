using XPScript.Compiler;

const string source = """
Dim form
Set form = UIForm("MediaPolicy")
form.AddImage "Preview", "assets/test.png"
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

foreach (var generated in new[] { desktop, android, browser })
{
    if (!generated.Contains(httpClause, StringComparison.Ordinal) ||
        !generated.Contains(httpsClause, StringComparison.Ordinal) ||
        !generated.Contains(dataClause, StringComparison.Ordinal) ||
        !generated.Contains(unsupported, StringComparison.Ordinal))
        throw new Exception("Generated UIForm media policy is missing the shared HTTP/HTTPS/data-image scheme guard.");
}

if (!desktop.Contains("true && " + fileClause, StringComparison.Ordinal))
    throw new Exception("Desktop UIForm media policy must permit local file URIs.");
if (!android.Contains("true && " + fileClause, StringComparison.Ordinal))
    throw new Exception("Android UIForm media policy must permit accessible local file URIs.");
if (!browser.Contains("false && " + fileClause, StringComparison.Ordinal))
    throw new Exception("Browser-WASM UIForm media policy must reject local file URIs.");

Console.WriteLine("UIForm media target policy probe passed.");
