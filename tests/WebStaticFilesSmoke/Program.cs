using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

var parent = Path.Combine(Path.GetTempPath(), "xps-static-smoke-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(Path.Combine(root, "assets"));
await File.WriteAllTextAsync(Path.Combine(root, "assets", "site.css"), "body{margin:0}");
await File.WriteAllTextAsync(Path.Combine(root, "assets", "app.js"), "console.log('xps');");
await File.WriteAllTextAsync(Path.Combine(root, "secret.xps"), "Response.Write(\"SECRET-SOURCE\")");
await File.WriteAllTextAsync(Path.Combine(root, ".hidden.css"), "hidden");
await File.WriteAllTextAsync(Path.Combine(root, "assets", "data.bin"), "binary");
await File.WriteAllTextAsync(Path.Combine(root, "assets", "large.css"), new string('x', 129));

var options = new XpsKestrelOptions
{
    Port = 0,
    AllowedHosts = ["localhost", "127.0.0.1", "::1"],
    EnableStaticFiles = true,
    MaxStaticFileBytes = 128,
    StaticCacheControl = "public, max-age=60"
};
var serverInfo = new XpsServerInfo("static-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");
var app = XpsKestrelAdapter.Build(options, serverInfo, new FallbackHandler());

try
{
    await app.StartAsync();
    var server = app.Services.GetRequiredService<IServer>();
    var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
        ?? throw new Exception("Kestrel did not expose a listen address.");
    using var client = new HttpClient { BaseAddress = new Uri(address) };

    using (var css = await client.GetAsync("/assets/site.css"))
    {
        if (css.StatusCode != HttpStatusCode.OK) throw new Exception("Allowed CSS was not served.");
        if (await css.Content.ReadAsStringAsync() != "body{margin:0}") throw new Exception("CSS body mismatch.");
        if (!string.Equals(css.Content.Headers.ContentType?.MediaType, "text/css", StringComparison.OrdinalIgnoreCase)) throw new Exception("CSS content type mismatch.");
        if (!css.Headers.TryGetValues("X-Content-Type-Options", out var values) || values.Single() != "nosniff") throw new Exception("nosniff header missing.");
        if (!css.Headers.CacheControl?.ToString().Contains("max-age=60", StringComparison.OrdinalIgnoreCase) ?? true) throw new Exception("Static cache policy missing.");
    }

    using (var head = new HttpRequestMessage(HttpMethod.Head, "/assets/app.js"))
    using (var response = await client.SendAsync(head))
    {
        if (response.StatusCode != HttpStatusCode.OK) throw new Exception("Static HEAD request failed.");
        if ((response.Content.Headers.ContentLength ?? -1) != new FileInfo(Path.Combine(root, "assets", "app.js")).Length) throw new Exception("HEAD content length mismatch.");
        if ((await response.Content.ReadAsByteArrayAsync()).Length != 0) throw new Exception("HEAD returned a response body.");
    }

    using (var wasmAsset = await client.GetAsync("/secret.xps/main.js"))
    {
        var body = await wasmAsset.Content.ReadAsStringAsync();
        if (body != "NOT-STATIC") throw new Exception("A .xps child asset was intercepted by static-file middleware instead of reaching the XPscript dispatcher.");
    }

    using (var wasmFrameworkAsset = await client.GetAsync("/secret.xps/_framework/dotnet.js"))
    {
        var body = await wasmFrameworkAsset.Content.ReadAsStringAsync();
        if (body != "NOT-STATIC") throw new Exception("A browser-WASM framework asset was intercepted by static-file middleware instead of reaching the XPscript dispatcher.");
    }

    using (var source = await client.GetAsync("/secret.xps"))
    {
        var body = await source.Content.ReadAsStringAsync();
        if (body.Contains("SECRET-SOURCE", StringComparison.Ordinal)) throw new Exception("XPScript source was exposed as a static file.");
    }

    using (var hidden = await client.GetAsync("/.hidden.css"))
    {
        var body = await hidden.Content.ReadAsStringAsync();
        if (body == "hidden") throw new Exception("Dotfile was exposed as a static file.");
    }

    using (var unknown = await client.GetAsync("/assets/data.bin"))
    {
        var body = await unknown.Content.ReadAsStringAsync();
        if (body == "binary") throw new Exception("Unknown extension was exposed as a static file.");
    }

    using (var large = await client.GetAsync("/assets/large.css"))
    {
        if (large.StatusCode != HttpStatusCode.NotFound) throw new Exception("Oversized static file should fail closed with 404.");
    }

    Console.WriteLine("WEB-STATIC-FILES-SMOKE=OK");
}
finally
{
    await app.StopAsync();
    await app.DisposeAsync();
    Directory.Delete(parent, recursive: true);
}

sealed class FallbackHandler : IXpsWebRequestHandler
{
    public Task HandleAsync(XpsWebContext context)
    {
        context.Response.StatusCode = 404;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.Write("NOT-STATIC");
        return Task.CompletedTask;
    }
}
