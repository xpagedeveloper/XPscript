from pathlib import Path


def patch(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text()
    if new in text:
        return
    if old not in text:
        raise SystemExit(f"Expected pattern not found: {path}")
    p.write_text(text.replace(old, new, 1))


patch(
    "src/XPScript.Compiler/AiRuntimeSource.cs",
    "_handler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false };",
    """_handler = new System.Net.Http.HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli
        };""",
)

patch(
    "src/XPScript.Compiler/JsonHttpCompatibilityRuntimeSource.cs",
    """            AllowAutoRedirect = MaxRedirects > 0,
            MaxAutomaticRedirections = Math.Max(1, MaxRedirects)
""",
    """            AllowAutoRedirect = MaxRedirects > 0,
            MaxAutomaticRedirections = Math.Max(1, MaxRedirects),
            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli
""",
)

for path in (
    "src/XPScript.Compiler/DatabaseAttachmentRuntimeV2Source.cs",
    "src/XPScript.Compiler/DatabaseAttachmentRuntimeSource.cs",
):
    patch(
        path,
        "new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false }",
        """new System.Net.Http.HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli
        }""",
    )

patch(
    "src/XPScript.UI.Desktop/DesktopImageHost.cs",
    "private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };",
    """private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                 System.Net.DecompressionMethods.Deflate |
                                 System.Net.DecompressionMethods.Brotli
    })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };""",
)

for path in (
    "src/XPScript.Compiler/NativeHttpRuntimeSource.cs",
    "src/XPScript.Compiler/AiRuntimeSource.cs",
    "src/XPScript.Compiler/JsonHttpCompatibilityRuntimeSource.cs",
    "src/XPScript.Compiler/DatabaseAttachmentRuntimeV2Source.cs",
    "src/XPScript.Compiler/DatabaseAttachmentRuntimeSource.cs",
    "src/XPScript.UI.Desktop/DesktopImageHost.cs",
):
    text = Path(path).read_text()
    for token in (
        "DecompressionMethods.GZip",
        "DecompressionMethods.Deflate",
        "DecompressionMethods.Brotli",
    ):
        if token not in text:
            raise SystemExit(f"{path} missing {token}")

print("HTTP decompression coverage verified")
