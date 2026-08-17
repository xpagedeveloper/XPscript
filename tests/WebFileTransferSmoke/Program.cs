using System.Text;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-file-transfer-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var scriptPath = Path.Combine(root, "index.xps");
await File.WriteAllTextAsync(scriptPath, """
[Anonymous]
[Post]
Sub Upload()
    Response.SendFile(Request.FileFirst("upload"))
End Sub
""");

try
{
    await using var dispatcher = new XpsWebDispatcher(root);
    var boundary = "xpsBoundary7MA4YWxkTrZu0gW";
    var payload = Encoding.UTF8.GetBytes("hello-file-åäö");
    var body = BuildMultipart(boundary, "caption", "demo", "upload", "C:\\fakepath\\rapport.txt", "text/plain", payload);

    var request = new XpsWebRequest(
        "POST",
        "/index/Upload",
        string.Empty,
        string.Empty,
        new Dictionary<string, IReadOnlyList<string>>(),
        "multipart/form-data; boundary=" + boundary,
        body.Length,
        body,
        "localhost",
        "http",
        "127.0.0.1",
        "HTTP/1.1",
        new Dictionary<string, string>());

    if (request.FormFirst("caption") != "demo")
        throw new Exception("Multipart text field was not parsed.");
    var uploaded = request.FileFirst("upload") ?? throw new Exception("Multipart file was not parsed.");
    if (uploaded.FileName != "rapport.txt")
        throw new Exception("Client-side upload path was not stripped from the file name.");
    if (uploaded.ContentType != "text/plain")
        throw new Exception("Uploaded file content type was not preserved.");
    if (!uploaded.Bytes().SequenceEqual(payload))
        throw new Exception("Uploaded file content changed during parsing.");

    var response = new XpsWebResponse();
    var context = new XpsWebContext(
        request,
        response,
        new XpsServerInfo("file-transfer-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        new XpsWebPrincipal(false),
        new XpsApplicationState());
    await dispatcher.HandleAsync(context);

    if (response.StatusCode != 200 || !response.Completed)
        throw new Exception($"Upload route failed with HTTP {response.StatusCode}.");
    if (response.ContentType != "text/plain")
        throw new Exception("File response content type mismatch.");
    if (!response.Body.Span.SequenceEqual(payload))
        throw new Exception("File response body mismatch.");
    if (!response.Headers.TryGetValue("Content-Disposition", out var dispositions) ||
        dispositions.Count != 1 ||
        !dispositions[0].StartsWith("attachment;", StringComparison.Ordinal) ||
        !dispositions[0].Contains("rapport.txt", StringComparison.Ordinal))
        throw new Exception("File response Content-Disposition mismatch.");

    var duplicateCookie = new XpsWebResponse();
    duplicateCookie.SetCookie("XPSID", "old", new XpsCookieOptions(Path: "/", HttpOnly: true));
    duplicateCookie.SetCookie("XPSID", "new", new XpsCookieOptions(Path: "/", HttpOnly: true));
    if (!duplicateCookie.Headers.TryGetValue("Set-Cookie", out var cookieHeaders) ||
        cookieHeaders.Count != 1 ||
        !cookieHeaders[0].StartsWith("XPSID=new;", StringComparison.Ordinal))
        throw new Exception("Equivalent session cookies were not replaced by the newest value.");

    var direct = new XpsWebResponse();
    direct.SendFile(Encoding.UTF8.GetBytes("generated"), "résumé.txt", "text/plain", inline: true);
    if (!direct.Headers.TryGetValue("Content-Disposition", out var inlineHeaders) ||
        !inlineHeaders.Single().StartsWith("inline;", StringComparison.Ordinal) ||
        !inlineHeaders.Single().Contains("filename*=UTF-8''", StringComparison.Ordinal))
        throw new Exception("Unicode file response did not emit RFC 5987 filename metadata.");

    ExpectFailure(() => request.Files(maxBytes: body.Length - 1), "Multipart total-size limit was not enforced.");
    ExpectFailure(() => request.Files(maxFileBytes: payload.Length - 1), "Multipart per-file limit was not enforced.");
    ExpectFailure(() => request.Files(maxFiles: 0), "Multipart file-count validation was not enforced.");

    Console.WriteLine("WEB-FILE-TRANSFER-SMOKE=OK");
}
finally
{
    Directory.Delete(root, recursive: true);
}

static byte[] BuildMultipart(
    string boundary,
    string fieldName,
    string fieldValue,
    string fileField,
    string fileName,
    string contentType,
    byte[] payload)
{
    using var stream = new MemoryStream();
    void Write(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        stream.Write(bytes);
    }

    Write($"--{boundary}\r\n");
    Write($"Content-Disposition: form-data; name=\"{fieldName}\"\r\n\r\n");
    Write(fieldValue + "\r\n");
    Write($"--{boundary}\r\n");
    Write($"Content-Disposition: form-data; name=\"{fileField}\"; filename=\"{fileName}\"\r\n");
    Write($"Content-Type: {contentType}\r\n\r\n");
    stream.Write(payload);
    Write("\r\n");
    Write($"--{boundary}--\r\n");
    return stream.ToArray();
}

static void ExpectFailure(Action action, string message)
{
    try
    {
        action();
        throw new Exception(message);
    }
    catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
    {
    }
}
