using System.Diagnostics;
using System.Text.Json;

var repo = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var psi = new ProcessStartInfo("dotnet")
{
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
    WorkingDirectory = repo
};
foreach (var a in new[] { "run", "--project", "src/XPScript.Compiler/XPScript.Compiler.csproj", "-c", "Release", "--no-build", "--", "mcp" })
    psi.ArgumentList.Add(a);

using var process = Process.Start(psi) ?? throw new Exception("Could not start MCP server.");

async Task<JsonElement> CallAsync(object request)
{
    await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request));
    await process.StandardInput.FlushAsync();
    var line = await process.StandardOutput.ReadLineAsync() ?? throw new Exception("MCP server closed stdout.");
    using var document = JsonDocument.Parse(line);
    return document.RootElement.Clone();
}

await CallAsync(new
{
    jsonrpc = "2.0",
    id = 1,
    method = "initialize",
    @params = new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "xpscript-ci", version = "1" } }
});
await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "notifications/initialized" }));
await process.StandardInput.FlushAsync();

var validation = await CallAsync(new
{
    jsonrpc = "2.0",
    id = 2,
    method = "tools/call",
    @params = new
    {
        name = "xpscript_validate",
        arguments = new
        {
            source = "[Anonymous]\n[Get]\nSub Index()\n    Response.Write(\"ok\")\nEnd Sub",
            filename = "web.xps"
        }
    }
});

var result = validation.GetProperty("result");
if (!result.GetProperty("isError").GetBoolean())
    throw new Exception("Web source validated as a console application must be an MCP tool error.");

var errors = result.GetProperty("structuredContent").GetProperty("errors").EnumerateArray().ToArray();
var descriptions = errors.Select(error => error.GetProperty("description").GetString() ?? "").ToArray();
if (!descriptions.Any(description => description.Contains("web application", StringComparison.OrdinalIgnoreCase)))
    throw new Exception("MCP lost the web application diagnostic. Actual: " + string.Join(" || ", descriptions));

process.StandardInput.Close();
if (!process.WaitForExit(5000))
{
    process.Kill(true);
    throw new Exception("MCP server did not stop after stdin closed.");
}
if (process.ExitCode != 0)
    throw new Exception("MCP server exit code " + process.ExitCode + ": " + await process.StandardError.ReadToEndAsync());

Console.WriteLine("MCP web diagnostic probe passed.");
