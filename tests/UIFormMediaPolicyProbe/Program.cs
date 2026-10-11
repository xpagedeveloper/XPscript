using XPScript.Compiler;
using System.IO;

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

Console.WriteLine("UIForm media target policy probe passed.");

var temp = Path.Combine(Path.GetTempPath(), "xpscript-media-assets-probe", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
var sourcePath = Path.Combine(temp, "sample.xps");
var localPath = Path.Combine(temp, "photo.png");
File.WriteAllBytes(localPath, [1, 2, 3, 4]);
File.WriteAllText(sourcePath, "UIForm");
var staged = UIFormAppAssets.StageLocalMediaSource(sourcePath, "photo.png");
if (!staged.StartsWith("assets/", StringComparison.Ordinal) || !File.Exists(Path.Combine(temp, staged.Replace('/', Path.DirectorySeparatorChar))))
    throw new Exception("Local media source was not copied into the application assets directory.");
var stagedFile = Path.Combine(temp, staged.Replace('/', Path.DirectorySeparatorChar));
File.WriteAllBytes(localPath, [1]);
File.SetLastWriteTimeUtc(localPath, DateTime.UtcNow.AddMinutes(-1));
UIFormAppAssets.StageLocalMediaSource(sourcePath, "photo.png");
if (File.ReadAllBytes(stagedFile).Length != 4)
    throw new Exception("An older/smaller local media source incorrectly replaced the staged asset.");
File.WriteAllBytes(localPath, [1, 2, 3, 4, 5]);
File.SetLastWriteTimeUtc(localPath, DateTime.UtcNow.AddMinutes(1));
UIFormAppAssets.StageLocalMediaSource(sourcePath, "photo.png");
if (File.ReadAllBytes(stagedFile).Length != 5)
    throw new Exception("A newer/larger local media source did not replace the staged asset.");
Console.WriteLine("UIForm local media asset staging probe passed.");
