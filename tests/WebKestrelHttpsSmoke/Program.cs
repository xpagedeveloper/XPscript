using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-kestrel-https-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var certificatePath = Path.Combine(root, "server.pfx");
const string certificatePassword = "test-only-password";
CreateCertificate(certificatePath, certificatePassword);

var options = new XpsKestrelOptions
{
    Address = IPAddress.Loopback,
    Port = 0,
    HttpsCertificatePath = certificatePath,
    HttpsCertificatePassword = certificatePassword,
    Protocols = HttpProtocols.Http1AndHttp2,
    AllowedHosts = ["localhost", "127.0.0.1", "::1"],
    MaxRequestLineSize = 4096,
    MaxRequestHeadersTotalSize = 16 * 1024,
    MinRequestBodyDataRateBytesPerSecond = 321,
    MinRequestBodyDataRateGracePeriod = TimeSpan.FromSeconds(7),
    MinResponseDataRateBytesPerSecond = 322,
    MinResponseDataRateGracePeriod = TimeSpan.FromSeconds(8)
};

var app = XpsKestrelAdapter.Build(
    options,
    new XpsServerInfo("https-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
    new EchoHandler());

try
{
    await app.StartAsync();

    var configured = app.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;
    if (configured.Limits.MaxRequestLineSize != 4096) throw new Exception("MaxRequestLineSize was not applied.");
    if (configured.Limits.MaxRequestHeadersTotalSize != 16 * 1024) throw new Exception("MaxRequestHeadersTotalSize was not applied.");
    AssertRate(configured.Limits.MinRequestBodyDataRate, 321, TimeSpan.FromSeconds(7), "request");
    AssertRate(configured.Limits.MinResponseDataRate, 322, TimeSpan.FromSeconds(8), "response");

    var server = app.Services.GetRequiredService<IServer>();
    var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
        ?? throw new Exception("HTTPS Kestrel did not expose an address.");
    if (!address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Kestrel HTTPS endpoint was not reported as https.");

    using var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
    using var request = new HttpRequestMessage(HttpMethod.Get, "/tls")
    {
        Version = HttpVersion.Version20,
        VersionPolicy = HttpVersionPolicy.RequestVersionExact
    };
    using var response = await client.SendAsync(request);
    if (response.StatusCode != HttpStatusCode.OK) throw new Exception("HTTPS request failed.");
    if (response.Version.Major != 2) throw new Exception("HTTPS endpoint did not negotiate HTTP/2.");
    var body = await response.Content.ReadAsStringAsync();
    if (!body.Contains("SCHEME=https", StringComparison.Ordinal)) throw new Exception("Normalized request scheme was not https.");
    if (!body.Contains("PROTOCOL=HTTP/2", StringComparison.Ordinal)) throw new Exception("Normalized request protocol was not HTTP/2.");

    try
    {
        new XpsKestrelOptions
        {
            HttpsCertificatePath = certificatePath,
            HttpsCertificatePassword = certificatePassword,
            Protocols = HttpProtocols.Http3
        }.Validate();
        throw new Exception("Unsupported HTTP/3 policy was accepted.");
    }
    catch (ArgumentOutOfRangeException)
    {
    }

    try
    {
        new XpsKestrelOptions { HttpsCertificatePath = Path.Combine(root, "missing.pfx") }.Validate();
        throw new Exception("Missing TLS certificate was accepted.");
    }
    catch (FileNotFoundException)
    {
    }

    Console.WriteLine("WEB-KESTREL-HTTPS=OK");
}
finally
{
    await app.StopAsync();
    await app.DisposeAsync();
    Directory.Delete(root, recursive: true);
}

static void AssertRate(MinDataRate? rate, double bytesPerSecond, TimeSpan grace, string name)
{
    if (rate is null || Math.Abs(rate.BytesPerSecond - bytesPerSecond) > 0.001 || rate.GracePeriod != grace)
        throw new Exception($"Explicit {name} minimum data rate was not applied.");
}

static void CreateCertificate(string path, string password)
{
    using var rsa = RSA.Create(2048);
    var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
    var usages = new OidCollection { new("1.3.6.1.5.5.7.3.1") };
    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, false));
    var san = new SubjectAlternativeNameBuilder();
    san.AddDnsName("localhost");
    san.AddIpAddress(IPAddress.Loopback);
    request.CertificateExtensions.Add(san.Build());
    using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
    File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
}

sealed class EchoHandler : IXpsWebRequestHandler
{
    public Task HandleAsync(XpsWebContext context)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.Write($"SCHEME={context.Request.Scheme}\nPROTOCOL={context.Request.Protocol}\n");
        return Task.CompletedTask;
    }
}
