from pathlib import Path


def read(path): return Path(path).read_text()
def write(path, text): Path(path).write_text(text)
def replace_once(path, old, new):
    s=read(path)
    if new in s: return
    if old not in s: raise SystemExit(f'pattern not found: {path}: {old[:80]!r}')
    write(path,s.replace(old,new,1))

# Shared TLS policy is part of the always-injected core runtime.
core='src/XPScript.Compiler/CoreCompatibilityRuntimeSource.cs'
marker='internal sealed class XPScriptRuntimeException : Exception\n'
helper=r'''internal sealed class XPScriptTlsValidationState
{
    public const int CertificateErrorNumber = 1201;
    private string _mode = "Strict";
    public string LastError { get; private set; } = string.Empty;

    public string Mode
    {
        get => _mode;
        set
        {
            var mode = (value ?? string.Empty).Trim();
            if (mode.Equals("Strict", StringComparison.OrdinalIgnoreCase)) _mode = "Strict";
            else if (mode.Equals("AllowSelfSigned", StringComparison.OrdinalIgnoreCase)) _mode = "AllowSelfSigned";
            else if (mode.Equals("Insecure", StringComparison.OrdinalIgnoreCase)) _mode = "Insecure";
            else throw new XPScriptRuntimeException(5, "CertificateValidation must be Strict, AllowSelfSigned, or Insecure.");
        }
    }

    public void Reset() => LastError = string.Empty;

    public bool Validate(System.Net.Http.HttpRequestMessage request,
        System.Security.Cryptography.X509Certificates.X509Certificate2? certificate,
        System.Security.Cryptography.X509Certificates.X509Chain? chain,
        System.Net.Security.SslPolicyErrors errors)
    {
        if (errors == System.Net.Security.SslPolicyErrors.None)
        {
            LastError = string.Empty;
            return true;
        }

        var details = new List<string>();
        if ((errors & System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable) != 0)
            details.Add("the server did not provide a certificate");
        if ((errors & System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
            details.Add("the certificate hostname does not match '" + (request.RequestUri?.Host ?? "the requested host") + "'");

        var statuses = chain?.ChainStatus ?? [];
        foreach (var status in statuses)
        {
            var text = status.Status switch
            {
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot => "the certificate root is not trusted (self-signed certificate or private CA)",
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.PartialChain => "the certificate chain is incomplete or its issuer is not trusted",
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NotTimeValid => "the certificate is expired or not yet valid",
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.Revoked => "the certificate has been revoked",
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.RevocationStatusUnknown => "the certificate revocation status could not be verified",
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NotSignatureValid => "the certificate signature is invalid",
                System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.InvalidBasicConstraints => "the certificate has invalid basic constraints",
                _ => status.StatusInformation.Trim().Length > 0 ? status.StatusInformation.Trim() : status.Status.ToString()
            };
            if (text.Length > 0) details.Add(text);
        }
        if (details.Count == 0) details.Add("the certificate chain is not trusted");
        LastError = string.Join("; ", details.Distinct(StringComparer.OrdinalIgnoreCase));

        if (_mode == "Insecure") return true;
        if (_mode == "AllowSelfSigned")
        {
            if ((errors & (System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch | System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
                return false;
            return statuses.Length > 0 && statuses.All(s =>
                s.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot ||
                s.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.PartialChain ||
                s.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NoError);
        }
        return false;
    }

    public XPScriptRuntimeException Failure(string operation)
        => new(CertificateErrorNumber, operation + " TLS certificate validation failed: " +
            (LastError.Length > 0 ? LastError : "the server certificate was rejected by the operating system trust policy") + ".");
}

'''
s=read(core)
if 'internal sealed class XPScriptTlsValidationState' not in s:
    if marker not in s: raise SystemExit('core runtime marker missing')
    write(core,s.replace(marker,helper+marker,1))

# XPHttpClient
p='src/XPScript.Compiler/NativeHttpRuntimeSource.cs'
replace_once(p,
'    private readonly System.Net.Http.HttpClient _client;\n',
'    private readonly System.Net.Http.HttpClient _client;\n    private readonly XPScriptTlsValidationState _tls = new();\n')
replace_once(p,
'''            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli
''',
'''            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli,
            ServerCertificateCustomValidationCallback = _tls.Validate
''')
replace_once(p,
'    public bool AllowPrivateNetwork\n',
'''    public string CertificateValidation
    {
        get => _tls.Mode;
        set => _tls.Mode = value;
    }

    public bool AllowPrivateNetwork
''')
replace_once(p,
'        EnsureNotDisposed();\n        var url = XPScriptRuntime.CStr(urlValue).Trim();\n',
'        EnsureNotDisposed();\n        _tls.Reset();\n        var url = XPScriptRuntime.CStr(urlValue).Trim();\n')
replace_once(p,
'''        catch (System.Net.Http.HttpRequestException)
        {
            throw new XPScriptRuntimeException(5, "HTTP request failed.");
        }
''',
'''        catch (System.Net.Http.HttpRequestException)
        {
            if (_tls.LastError.Length > 0) throw _tls.Failure("HTTP request");
            throw new XPScriptRuntimeException(5, "HTTP request failed.");
        }
''')

# XPAi
p='src/XPScript.Compiler/AiRuntimeSource.cs'
replace_once(p,'    private readonly System.Net.Http.HttpClient _client;\n','    private readonly System.Net.Http.HttpClient _client;\n    private readonly XPScriptTlsValidationState _tls = new();\n')
replace_once(p,
'''            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli
''',
'''            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli,
            ServerCertificateCustomValidationCallback = _tls.Validate
''')
replace_once(p,'    public string Endpoint => _endpoint.ToString();\n','    public string Endpoint => _endpoint.ToString();\n    public string CertificateValidation { get => _tls.Mode; set => _tls.Mode = value; }\n')
replace_once(p,'        var cancellation = BeginRequest();\n','        _tls.Reset();\n        var cancellation = BeginRequest();\n')
replace_once(p,
'''        catch (System.Net.Http.HttpRequestException)
        {
            throw new XPScriptRuntimeException(5, "XPAi HTTP request failed.");
        }
''',
'''        catch (System.Net.Http.HttpRequestException)
        {
            if (_tls.LastError.Length > 0) throw _tls.Failure("XPAi request");
            throw new XPScriptRuntimeException(5, "XPAi HTTP request failed.");
        }
''')

# NotesHTTPRequest
p='src/XPScript.Compiler/JsonHttpCompatibilityRuntimeSource.cs'
replace_once(p,'    private string[] _responseHeaders = [];\n','    private string[] _responseHeaders = [];\n    private readonly XPScriptTlsValidationState _tls = new();\n')
replace_once(p,'    public bool PreferJSONNavigator { get; set; }\n','    public bool PreferJSONNavigator { get; set; }\n    public string CertificateValidation { get => _tls.Mode; set => _tls.Mode = value; }\n')
replace_once(p,'        var url = XPScriptRuntime.CStr(rawUrl).Trim();\n','        _tls.Reset();\n        var url = XPScriptRuntime.CStr(rawUrl).Trim();\n')
replace_once(p,
'''            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli
''',
'''            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli,
            ServerCertificateCustomValidationCallback = _tls.Validate
''')
replace_once(p,
'''        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "HTTP request failed: " + ex.Message);
        }
''',
'''        catch (System.Net.Http.HttpRequestException ex)
        {
            if (_tls.LastError.Length > 0) throw _tls.Failure("NotesHTTPRequest");
            throw new XPScriptRuntimeException(5, "HTTP request failed: " + ex.Message);
        }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "HTTP request failed: " + ex.Message);
        }
''')

# Expose on compatibility member whitelist.
p='src/XPScript.Compiler/ExtendedCompatibilityTranspiler.cs'
s=read(p)
old='            "PreferUTF8", "PreferJSONNavigator"\n'
new='            "PreferUTF8", "PreferJSONNavigator", "CertificateValidation"\n'
if old in s: write(p,s.replace(old,new,1))
elif new not in s: raise SystemExit('compat member marker missing')

# HTTP DB clients inherit the policy from their private XPHttpClient.
p='src/XPScript.Compiler/HttpDbRuntimeSource.cs'
s=read(p)
needle='    public double Timeout { get => _http.Timeout; set => _http.Timeout = value; }\n'
# occurs twice
if s.count(needle) < 2: raise SystemExit('http db Timeout markers missing')
s=s.replace(needle,needle+'    public string CertificateValidation { get => _http.CertificateValidation; set => _http.CertificateValidation = value; }\n',2)
write(p,s)

# XPDbSupabase REST wrapper.
p='src/XPScript.Compiler/SupabaseDbRuntimeSource.cs'
replace_once(p,
'    public string Schema => _rest?.Schema ?? "";\n',
'''    public string Schema => _rest?.Schema ?? "";
    public string CertificateValidation
    {
        get => _rest?.CertificateValidation ?? "Strict";
        set => RequireRest().CertificateValidation = value;
    }
''')

# Attachment HTTP runtime accepts a per-call TLS mode and emits runtime error 1201.
for p in ['src/XPScript.Compiler/DatabaseAttachmentRuntimeV2Source.cs','src/XPScript.Compiler/DatabaseAttachmentRuntimeSource.cs']:
    s=read(p)
    sig='public static byte[] Send(System.Net.Http.HttpMethod method, string url, IReadOnlyDictionary<string, string> headers, byte[]? body, string contentType, double timeoutSeconds)'
    if sig in s:
        s=s.replace(sig,sig[:-1]+', string certificateValidation = "Strict")',1)
    handler='''        using var handler = new System.Net.Http.HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli
        };
'''
    replacement='''        var tls = new XPScriptTlsValidationState { Mode = certificateValidation };
        using var handler = new System.Net.Http.HttpClientHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli,
            ServerCertificateCustomValidationCallback = tls.Validate
        };
'''
    if handler in s: s=s.replace(handler,replacement,1)
    old='catch (System.Net.Http.HttpRequestException) { throw new XPScriptRuntimeException(5, "Attachment HTTP operation failed."); }'
    new='catch (System.Net.Http.HttpRequestException) { if (tls.LastError.Length > 0) throw tls.Failure("Attachment HTTP operation"); throw new XPScriptRuntimeException(5, "Attachment HTTP operation failed."); }'
    if old in s: s=s.replace(old,new,1)
    write(p,s)

# Active V2 attachment provider: thread db policy through Supabase helpers and direct Domino sends.
p='src/XPScript.Compiler/DatabaseAttachmentRuntimeV2Source.cs'
s=read(p)
# add mode to top-level closures
s=s.replace('Headers(),db.Timeout)', 'Headers(),db.Timeout,db.CertificateValidation)')
s=s.replace('Headers(),db.Timeout),', 'Headers(),db.Timeout,db.CertificateValidation),')
# helper signatures and internal calls, targeted to Supabase section
repls={
'Dictionary<string,string> headers,double timeout)':'Dictionary<string,string> headers,double timeout,string certificateValidation)',
'headers,JsonBytes(request),"application/json",timeout)':'headers,JsonBytes(request),"application/json",timeout,certificateValidation)',
'headers,null,string.Empty,timeout)':'headers,null,string.Empty,timeout,certificateValidation)',
'headers,timeout,false)':'headers,timeout,certificateValidation,false)',
'headers,timeout,true)':'headers,timeout,certificateValidation,true)',
'headers,timeout).Node':'headers,timeout,certificateValidation).Node',
'headers,double timeout,bool upsert)':'headers,double timeout,string certificateValidation,bool upsert)',
'h,bytes,type,timeout)':'h,bytes,type,timeout,certificateValidation)',
'headers,null,string.Empty,timeout);':'headers,null,string.Empty,timeout,certificateValidation);',
'headers,null,string.Empty,timeout);_=':'headers,null,string.Empty,timeout,certificateValidation);_=',
}
for a,b in repls.items(): s=s.replace(a,b)
# Domino calls have db available.
s=s.replace('multipart,multipartType,db.Timeout)', 'multipart,multipartType,db.Timeout,db.CertificateValidation)')
s=s.replace('headers,null,string.Empty,db.Timeout)', 'headers,null,string.Empty,db.Timeout,db.CertificateValidation)')
write(p,s)

# Desktop UI image: policy is per image field.
p='src/XPScript.Compiler/UIFormMediaButtonsPostProcessor.cs'
replace_once(p,
'    public string ImageAltText { get; set; } = string.Empty;\n',
'    public string ImageAltText { get; set; } = string.Empty;\n    public string ImageCertificateValidation { get; set; } = "Strict";\n')
# method to configure one field
replace_once(p,
'''    public void SetImageAltText(object? name, object? altText)
    {
        var field = FindField(name);
        if (field.Type != "Image") throw new XPScriptRuntimeException(5, "UIForm.SetImageAltText requires an Image field.");
        field.ImageAltText = NormalizeMediaText(altText, "image alt text", 1024);
    }

''',
'''    public void SetImageAltText(object? name, object? altText)
    {
        var field = FindField(name);
        if (field.Type != "Image") throw new XPScriptRuntimeException(5, "UIForm.SetImageAltText requires an Image field.");
        field.ImageAltText = NormalizeMediaText(altText, "image alt text", 1024);
    }
    public void SetImageCertificateValidation(object? name, object? mode)
    {
        var field = FindField(name);
        if (field.Type != "Image") throw new XPScriptRuntimeException(5, "UIForm.SetImageCertificateValidation requires an Image field.");
        var value = XPScriptRuntime.CStr(mode).Trim();
        if (!(value.Equals("Strict", StringComparison.OrdinalIgnoreCase) || value.Equals("AllowSelfSigned", StringComparison.OrdinalIgnoreCase) || value.Equals("Insecure", StringComparison.OrdinalIgnoreCase)))
            throw new XPScriptRuntimeException(5, "Image certificate validation must be Strict, AllowSelfSigned, or Insecure.");
        field.ImageCertificateValidation = value;
    }

''')

p='src/XPScript.Compiler/UIFormDesktopLayoutMetadataPostProcessor.cs'
s=read(p)
s=s.replace('                imageAltText = field.ImageAltText,\n','                imageAltText = field.ImageAltText,\n                imageCertificateValidation = field.ImageCertificateValidation,\n')
write(p,s)

p='src/XPScript.UI.Desktop/DesktopBridgeContracts.cs'
replace_once(p,
'    public string ImageAltText { get; init; } = string.Empty;\n',
'    public string ImageAltText { get; init; } = string.Empty;\n    public string ImageCertificateValidation { get; init; } = "Strict";\n')
s=read(p)
s=s.replace('DesktopImageHost.ToWebSource(field.ImageSource)', 'DesktopImageHost.ToWebSource(field.ImageSource, field.ImageCertificateValidation)')
write(p,s)

p='src/XPScript.UI.Desktop/DesktopImageHost.cs'
s=read(p)
# Replace static client with factory and stateful detailed validation local to desktop project.
old='''    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                 System.Net.DecompressionMethods.Deflate |
                                 System.Net.DecompressionMethods.Brotli
    })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };
'''
new='''    private static HttpClient CreateHttpClient(string certificateValidation)
    {
        var mode = string.IsNullOrWhiteSpace(certificateValidation) ? "Strict" : certificateValidation.Trim();
        if (!(mode.Equals("Strict", StringComparison.OrdinalIgnoreCase) || mode.Equals("AllowSelfSigned", StringComparison.OrdinalIgnoreCase) || mode.Equals("Insecure", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Image certificate validation must be Strict, AllowSelfSigned, or Insecure.");
        string tlsError = string.Empty;
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                                     System.Net.DecompressionMethods.Deflate |
                                     System.Net.DecompressionMethods.Brotli,
            ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) =>
            {
                if (errors == System.Net.Security.SslPolicyErrors.None) return true;
                var details = new List<string>();
                if ((errors & System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch) != 0) details.Add("certificate hostname mismatch");
                if ((errors & System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable) != 0) details.Add("server certificate missing");
                var statuses = chain?.ChainStatus ?? [];
                foreach (var status in statuses) details.Add(status.StatusInformation.Trim().Length > 0 ? status.StatusInformation.Trim() : status.Status.ToString());
                tlsError = details.Count > 0 ? string.Join("; ", details) : "certificate is not trusted";
                if (mode.Equals("Insecure", StringComparison.OrdinalIgnoreCase)) return true;
                if (mode.Equals("AllowSelfSigned", StringComparison.OrdinalIgnoreCase))
                    return (errors & (System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch | System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable)) == 0 &&
                           statuses.Length > 0 && statuses.All(x => x.Status is System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot or System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.PartialChain or System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NoError);
                return false;
            }
        };
        var client = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-XPScript-Tls-Mode", mode); // internal marker removed before request below
        return client;
    }
'''
if old not in s: raise SystemExit('desktop client marker missing')
s=s.replace(old,new,1)
s=s.replace('public static string ToWebSource(string source)', 'public static string ToWebSource(string source, string certificateValidation = "Strict")')
s=s.replace('var bytes = ReadImageBytes(path);', 'var bytes = ReadImageBytes(path);',1)
# remote image path in ToWebSource currently returns URL directly, so for desktop image WebView it never fetches. Force data conversion for HTTP URLs.
s=s.replace('        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https") return value;\n\n        var path = ResolveLocalPath(value);',
'''        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            using var http = CreateHttpClient(certificateValidation);
            try
            {
                var bytes = http.GetByteArrayAsync(uri).GetAwaiter().GetResult();
                ValidateSize(bytes.LongLength);
                var mime = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant() switch
                {
                    ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".webp" => "image/webp", ".bmp" => "image/bmp", ".svg" => "image/svg+xml", _ => "application/octet-stream"
                };
                return "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
            }
            catch (HttpRequestException ex) { throw new InvalidOperationException("TLS/HTTP image request failed: " + ex.Message, ex); }
        }

        var path = ResolveLocalPath(value);''')
# Create() old remote ReadImageBytes path cannot select policy; leave strict via default helper not static Http. replace Http usage.
s=s.replace('            var bytes = Http.GetByteArrayAsync(uri).GetAwaiter().GetResult();', '            using var http = CreateHttpClient("Strict");\n            var bytes = http.GetByteArrayAsync(uri).GetAwaiter().GetResult();')
# Remove invalid internal marker header idea; no need.
s=s.replace('        client.DefaultRequestHeaders.TryAddWithoutValidation("X-XPScript-Tls-Mode", mode); // internal marker removed before request below\n','')
write(p,s)

# Basic source assertions.
checks={
 'src/XPScript.Compiler/NativeHttpRuntimeSource.cs':['CertificateValidation','ServerCertificateCustomValidationCallback','Failure("HTTP request")'],
 'src/XPScript.Compiler/AiRuntimeSource.cs':['CertificateValidation','ServerCertificateCustomValidationCallback','Failure("XPAi request")'],
 'src/XPScript.Compiler/JsonHttpCompatibilityRuntimeSource.cs':['CertificateValidation','ServerCertificateCustomValidationCallback','Failure("NotesHTTPRequest")'],
 'src/XPScript.Compiler/HttpDbRuntimeSource.cs':['CertificateValidation'],
 'src/XPScript.Compiler/CoreCompatibilityRuntimeSource.cs':['CertificateErrorNumber = 1201','AllowSelfSigned','Insecure'],
}
for path,tokens in checks.items():
    text=read(path)
    for token in tokens:
        if token not in text: raise SystemExit(f'{path} missing {token}')
print('per-client TLS patch applied')
