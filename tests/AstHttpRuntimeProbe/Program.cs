using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

var repo = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
var output = Path.Combine(repo, "out", "http-ast-probe", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(output);
using var cancellation = new CancellationTokenSource();
using var valid = Certificate(false);
using var expired = Certificate(true);
using var plain = new TcpListener(IPAddress.Loopback, 0);
using var tls = new TcpListener(IPAddress.Loopback, 0);
using var oldTls = new TcpListener(IPAddress.Loopback, 0);
plain.Start(); tls.Start(); oldTls.Start();
var handlers = new System.Collections.Concurrent.ConcurrentBag<Task>();
var aiRequests = 0;
var tasks = new[] { Serve(plain, null), Serve(tls, valid), Serve(oldTls, expired) };
var arguments = new[] { $"http://127.0.0.1:{Port(plain)}", $"https://localhost:{Port(tls)}/get",
    $"https://127.0.0.1:{Port(tls)}/get", $"https://localhost:{Port(oldTls)}/get" };
try
{
    var compiler = Environment.GetEnvironmentVariable("XPSCRIPT_COMPILER_PATH")
        ?? Path.Combine(repo, "src/XPScript.Compiler/bin/Release/net10.0/xpscriptc.dll");
    if (args.Contains("--stream-only"))
    {
        var streamOutput = Path.Combine(output, "ai-stream");
        await Run("dotnet", [compiler, "ast-compile", Path.Combine(repo, "tests/ast-compile-probe/xpai-stream-next.xps"), "-o", streamOutput]);
        if ((await Run("dotnet", [Path.Combine(streamOutput, "Generated.dll")])).Trim() != "XPAI_STREAM_OK")
            throw new Exception("XPAi streaming regression failed.");
        Console.WriteLine("AST_XPAI_STREAM_OK");
        return;
    }
    if (args.Contains("--ai-only"))
    {
        var isolatedAiOutput = Path.Combine(output, "ai");
        await Run("dotnet", [compiler, "ast-compile", Path.Combine(repo, "tests/ast-compile-probe/xpai-request-success-next.xps"), "-o", isolatedAiOutput]);
        if ((await Run("dotnet", [Path.Combine(isolatedAiOutput, "Generated.dll")])).Trim() != "XPAI_REQUEST_SUCCESS_OK")
            throw new Exception("XPAi provider-success regression failed.");
        var isolatedAiToolOutput = Path.Combine(output, "ai-tool-loop");
        await Run("dotnet", [compiler, "ast-compile", Path.Combine(repo, "tests/ast-compile-probe/xpai-tool-loop-next.xps"), "-o", isolatedAiToolOutput]);
        if ((await Run("dotnet", [Path.Combine(isolatedAiToolOutput, "Generated.dll")])).Trim() != "XPAI_TOOL_LOOP_OK")
            throw new Exception("XPAi tool-loop regression failed.");
        var isolatedAiTimeoutOutput = Path.Combine(output, "ai-tool-timeout");
        await Run("dotnet", [compiler, "ast-compile", Path.Combine(repo, "tests/ast-compile-probe/xpai-tool-timeout-next.xps"), "-o", isolatedAiTimeoutOutput]);
        if ((await Run("dotnet", [Path.Combine(isolatedAiTimeoutOutput, "Generated.dll")])).Trim() != "XPAI_TOOL_TIMEOUT_OK")
            throw new Exception("XPAi timeout regression failed.");
        Console.WriteLine("AST_XPAI_PROVIDER_TOOL_OK");
        return;
    }
    if (args.Contains("--notes-only"))
    {
        var notesOutput = Path.Combine(output, "notes-http-request");
        await Run("dotnet", [compiler, "ast-compile", Path.Combine(repo, "tests/ast-compile-probe/notes-http-request-next.xps"), "-o", notesOutput]);
        if ((await Run("dotnet", [Path.Combine(notesOutput, "Generated.dll")])).Trim() != "NOTES_HTTP_REQUEST_OK")
            throw new Exception("NotesHTTPRequest regression failed.");
        Console.WriteLine("AST_NOTES_HTTP_REQUEST_OK");
        return;
    }
    if (!args.Contains("--accessors-only"))
    {
        var streamProbeOutput = Path.Combine(output, "ai-stream");
        await Run("dotnet", [compiler, "ast-compile", Path.Combine(repo, "tests/ast-compile-probe/xpai-stream-next.xps"), "-o", streamProbeOutput]);
        if ((await Run("dotnet", [Path.Combine(streamProbeOutput, "Generated.dll")])).Trim() != "XPAI_STREAM_OK")
            throw new Exception("XPAi streaming regression failed.");
        Console.WriteLine("AST_XPAI_STREAM_OK");
    }
    var accessors = Path.Combine(output, "accessors");
    await Run("dotnet", [compiler, "ast-compile", Path.Combine(repo, "tests/ast-compile-probe/http-reference-properties.xps"), "-o", accessors]);
    if ((await Run("dotnet", [Path.Combine(accessors, "Generated.dll"), ..arguments])).Trim() != "HTTP_REFERENCE_PROPERTIES_OK")
        throw new Exception("HTTP accessor reference/security regression failed.");
    Console.WriteLine("AST_HTTP_ACCESSORS_OK");
    if (args.Contains("--accessors-only")) return;
    var source = Path.Combine(repo, "tests/ast-compile-probe/http-runtime.xps");
    await Run("dotnet", [compiler, "ast-compile", source, "-o", Path.Combine(output, "ast")]);
    var assembly = System.Reflection.Assembly.LoadFile(Path.Combine(output, "ast", "Generated.dll"));
    if (assembly.GetType("XPScriptHttpClient") is null || assembly.GetType("XPScriptTlsValidationState") is null)
        throw new Exception("Native HTTP/TLS runtime missing.");
    var result = await Run("dotnet", [Path.Combine(output, "ast", "Generated.dll"), ..arguments]);
    if (result.Trim() != "HTTP_AST_OK") throw new Exception("HTTP/TLS fixture output mismatch: " + result);
    var normal = Path.Combine(output, OperatingSystem.IsWindows() ? "normal.exe" : "normal");
    await Run("dotnet", [compiler, "legacy-compile", source, "-o", normal, "--single-file=false", "--runtime=false"]);
    if ((await Run(normal, arguments)).Trim() != result.Trim()) throw new Exception("Normal/AST HTTP/TLS execution differs.");
    var unused = Path.Combine(output, "unused");
    await Run("dotnet", [compiler, "ast-compile", Path.Combine(repo, "tests/ast-compile-probe/http-unused.xps"), "-o", unused]);
    var unusedAssembly = System.Reflection.Assembly.LoadFile(Path.Combine(unused, "Generated.dll"));
    if (unusedAssembly.GetType("XPScriptNativeHttp") is not null || unusedAssembly.GetType("XPScriptTlsValidationState") is not null)
        throw new Exception("Unused HTTP/TLS runtime leaked into output.");
    if (assembly.GetReferencedAssemblies().Any(a => a.Name!.StartsWith("ClosedXML") || a.Name.StartsWith("Magick.NET") || a.Name.StartsWith("Microsoft.Data.Sqlite")))
        throw new Exception("HTTP acquired unrelated addon dependencies.");
    if ((await Run("dotnet", [Path.Combine(unused, "Generated.dll")])).Trim() != "XPHttpClient XPHttpRequest XPHttpResponse")
        throw new Exception("Ordinary http variable was replaced by a stub.");
    Console.WriteLine("AST_HTTP_TLS_RUNTIME_OK");
    var aiOutput = Path.Combine(output, "ai");
    await Run("dotnet", [compiler, "ast-compile", Path.Combine(repo, "tests/ast-compile-probe/xpai-request-success-next.xps"), "-o", aiOutput]);
    if ((await Run("dotnet", [Path.Combine(aiOutput, "Generated.dll")])).Trim() != "XPAI_REQUEST_SUCCESS_OK")
        throw new Exception("XPAi request success regression failed.");
    Console.WriteLine("AST_XPAI_REQUEST_SUCCESS_OK");
    var aiToolOutput = Path.Combine(output, "ai-tool-loop");
    await Run("dotnet", [compiler, "ast-compile", Path.Combine(repo, "tests/ast-compile-probe/xpai-tool-loop-next.xps"), "-o", aiToolOutput]);
    if ((await Run("dotnet", [Path.Combine(aiToolOutput, "Generated.dll")])).Trim() != "XPAI_TOOL_LOOP_OK")
        throw new Exception("XPAi tool-loop regression failed.");
    Console.WriteLine("AST_XPAI_TOOL_LOOP_OK");
    var aiTimeoutOutput = Path.Combine(output, "ai-tool-timeout");
    await Run("dotnet", [compiler, "ast-compile", Path.Combine(repo, "tests/ast-compile-probe/xpai-tool-timeout-next.xps"), "-o", aiTimeoutOutput]);
    if ((await Run("dotnet", [Path.Combine(aiTimeoutOutput, "Generated.dll")] )).Trim() != "XPAI_TOOL_TIMEOUT_OK")
        throw new Exception("XPAi tool-timeout regression failed.");
    Console.WriteLine("AST_XPAI_TOOL_TIMEOUT_OK");
    Console.WriteLine("AST_XPAI_PROVIDER_TOOL_OK");
    var notesHttpOutput = Path.Combine(output, "notes-http-request");
    await Run("dotnet", [compiler, "ast-compile", Path.Combine(repo, "tests/ast-compile-probe/notes-http-request-next.xps"), "-o", notesHttpOutput]);
    if ((await Run("dotnet", [Path.Combine(notesHttpOutput, "Generated.dll")] )).Trim() != "NOTES_HTTP_REQUEST_OK")
        throw new Exception("NotesHTTPRequest request regression failed.");
    Console.WriteLine("AST_NOTES_HTTP_REQUEST_OK");
}
finally
{
    cancellation.Cancel(); plain.Stop(); tls.Stop(); oldTls.Stop();
    try { await Task.WhenAll(tasks); } catch (OperationCanceledException) { }
    await Task.WhenAll(handlers);
}

int Port(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;
X509Certificate2 Certificate(bool old)
{
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var san = new SubjectAlternativeNameBuilder(); san.AddDnsName("localhost"); request.CertificateExtensions.Add(san.Build());
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
    using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(old ? -1 : 1));
    return X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), (string?)null,
        X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
}
async Task Serve(TcpListener listener, X509Certificate2? certificate)
{
    while (!cancellation.IsCancellationRequested)
    {
        TcpClient connection;
        try { connection = await listener.AcceptTcpClientAsync(cancellation.Token); }
        catch (OperationCanceledException) { break; }
        handlers.Add(Handle(connection, certificate));
    }
}
async Task Handle(TcpClient connection, X509Certificate2? certificate)
{
    using (connection)
    {
        try
        {
            using Stream stream = certificate is null ? connection.GetStream() : new SslStream(connection.GetStream());
            if (stream is SslStream ssl) await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, cancellation.Token);
            var headerBytes = new List<byte>();
            var single = new byte[1];
            while (true)
            {
                await stream.ReadExactlyAsync(single, cancellation.Token); headerBytes.Add(single[0]);
                if (headerBytes.Count > 8192) throw new Exception("Test header limit exceeded.");
                if (headerBytes.Count >= 4 && headerBytes.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 })) break;
            }
            var lines = Encoding.ASCII.GetString(headerBytes.ToArray()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            var request = lines[0].Split(' ');
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines.Skip(1))
            {
                var colon = line.IndexOf(':'); headers[line[..colon]] = line[(colon + 1)..].Trim();
            }
            var body = "";
            if (headers.TryGetValue("Content-Length", out var length))
            {
                // Requests above the native limit must never reach this listener.
                if (int.Parse(length) > 8 * 1024 * 1024) throw new Exception("Oversized request reached test server.");
                var buffer = new byte[int.Parse(length)];
                await stream.ReadExactlyAsync(buffer, cancellation.Token); body = Encoding.UTF8.GetString(buffer);
            }
            if (request[1] == "/slow") await Task.Delay(600, cancellation.Token);
            var status = request[1] == "/fail" ? "418 Teapot" : request[1] == "/redirect" ? "302 Found" : "200 OK";
            var payload = request[1] == "/notes" ? "notes-http-ok" : request[1] == "/notes-json" ? "{\"name\":\"Fredrik\"}" : request[1] == "/ai-timeout" ? "{\"model\":\"test-model\",\"choices\":[{\"message\":{\"role\":\"assistant\",\"tool_calls\":[{\"id\":\"call-timeout\",\"type\":\"function\",\"function\":{\"name\":\"slow_call\",\"arguments\":\"{}\"}}]}}]}" : request[1] == "/ai-stream" ? "data: {\"model\":\"test-model\",\"choices\":[{\"delta\":{\"content\":\"hello \"}}]}\r\n\r\ndata: {\"model\":\"test-model\",\"choices\":[{\"delta\":{\"content\":\"world\"}}]}\r\n\r\ndata: [DONE]\r\n\r\n" : request[1] == "/ai" ? AiPayload(body) : request[1] == "/json" ? body : request[1] == "/redirect" ? "redirect-not-followed" :
                request[0] + "|" + headers.GetValueOrDefault("X-XPScript-Test", "") + (body.Length > 0 ? "|" + body : "");
            var contentType = request[1] == "/ai-stream" ? "text/event-stream" : request[1] is "/json" or "/ai" or "/notes-json" ? "application/json" : "text/plain; charset=utf-8";
            if (request[1] == "/multipart")
            {
                contentType = "multipart/mixed; boundary=xps-accessor-boundary";
                payload = "--xps-accessor-boundary\r\nContent-Disposition: form-data; name=\"note\"\r\nContent-Type: text/plain\r\n\r\nshared\r\n" +
                    "--xps-accessor-boundary\r\nContent-Disposition: attachment; name=\"upload\"; filename=\"report.txt\"\r\nContent-Type: text/plain\r\n\r\nfile-body\r\n--xps-accessor-boundary--\r\n";
            }
            var data = Encoding.UTF8.GetBytes(payload);
            var size = request[1] == "/declared-oversized" ? 64 * 1024 * 1024 + 1 : data.Length;
            var response = Encoding.ASCII.GetBytes($"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\nX-Regression: native-http\r\nLocation: /get\r\nContent-Length: {size}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response, cancellation.Token); await stream.WriteAsync(data, cancellation.Token);
        }
        catch (Exception ex) when (ex is IOException or AuthenticationException or OperationCanceledException) { }
    }
}

string AiPayload(string requestBody)
{
    var requestNumber = Interlocked.Increment(ref aiRequests);
    if (requestNumber == 2 && requestBody.Contains("get_weather", StringComparison.Ordinal))
        return "{\"model\":\"test-model\",\"choices\":[{\"message\":{\"role\":\"assistant\",\"tool_calls\":[{\"id\":\"call-1\",\"type\":\"function\",\"function\":{\"name\":\"get_weather\",\"arguments\":\"{\\\"city\\\":\\\"Linkoping\\\"}\"}}]}}]}";
    return requestNumber == 1
        ? "{\"model\":\"test-model\",\"choices\":[{\"message\":{\"content\":\"hello from provider\"}}],\"usage\":{\"total_tokens\":3}}"
        : "{\"model\":\"test-model\",\"choices\":[{\"message\":{\"content\":\"tool complete\"}}],\"usage\":{\"total_tokens\":3}}";
}
async Task<string> Run(string file, string[] commandArguments)
{
    var start = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = repo };
    foreach (var (name, value) in new[] { ("XPSCRIPT_HTTP_TEST_HTTP", arguments[0]), ("XPSCRIPT_HTTP_TEST_TLS", arguments[1]), ("XPSCRIPT_AI_STREAM_TEST_AI", arguments[0] + "/ai-stream"),
        ("XPSCRIPT_HTTP_TEST_MISMATCH", arguments[2]), ("XPSCRIPT_HTTP_TEST_EXPIRED", arguments[3]),
        ("XPSCRIPT_AI_TEST_AI", arguments[0] + "/ai"), ("XPSCRIPT_AI_TIMEOUT_TEST_AI", arguments[0] + "/ai-timeout"), ("XPSCRIPT_NOTES_HTTP_TEST_URL", arguments[0] + "/notes"), ("XPSCRIPT_NOTES_JSON_TEST_URL", arguments[0] + "/notes-json"), ("XPSCRIPT_NOTES_REDIRECT_TEST_URL", arguments[0] + "/redirect") }) start.Environment[name] = value;
    foreach (var value in commandArguments) start.ArgumentList.Add(value);
    using var process = Process.Start(start)!;
    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
    try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90)); }
    catch { process.Kill(true); throw; }
    var text = await stdout;
    if (process.ExitCode != 0) throw new Exception($"Process failed ({process.ExitCode}): {text}{await stderr}");
    return text;
}
