using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-web-env-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var productionInfo = new XpsServerInfo(
        "prod",
        root,
        XpsWebHostingMode.Kestrel,
        DateTimeOffset.UtcNow,
        "test");
    var productionServer = new XpsWebServer(productionInfo);
    if (productionInfo.Environment != XpsWebEnvironment.Production)
        throw new InvalidOperationException("Production must be the default web environment.");
    if (!string.Equals(productionServer.Environment, "Production", StringComparison.Ordinal))
        throw new InvalidOperationException("Server.Environment did not expose the Production default.");

    var developmentInfo = productionInfo with
    {
        SiteId = "dev",
        Environment = XpsWebEnvironment.Development
    };
    var developmentServer = new XpsWebServer(developmentInfo);
    if (!string.Equals(developmentServer.Environment, "Development", StringComparison.Ordinal))
        throw new InvalidOperationException("Server.Environment did not expose Development.");

    Console.WriteLine("WEB_ENVIRONMENT_OK");
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch { }
}
