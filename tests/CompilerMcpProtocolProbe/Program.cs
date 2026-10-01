using System.Diagnostics;
using System.Text.Json;

var repo = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var psi = new ProcessStartInfo("dotnet") { RedirectStandardInput=true, RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false, WorkingDirectory=repo };
foreach (var a in new[]{"run","--project","src/XPScript.Compiler/XPScript.Compiler.csproj","-c","Release","--no-build","--","mcp"}) psi.ArgumentList.Add(a);
using var p=Process.Start(psi) ?? throw new Exception("Could not start MCP server.");
async Task<JsonElement> CallAsync(object request) { await p.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request)); await p.StandardInput.FlushAsync(); var line=await p.StandardOutput.ReadLineAsync() ?? throw new Exception("MCP server closed stdout."); try { using var d=JsonDocument.Parse(line); return d.RootElement.Clone(); } catch { throw new Exception("Non-JSON data leaked to MCP stdout: "+line); } }
var init=await CallAsync(new {jsonrpc="2.0",id=1,method="initialize",@params=new {protocolVersion="2025-06-18",capabilities=new{},clientInfo=new{name="xpscript-ci",version="1"}}});
if (init.GetProperty("result").GetProperty("protocolVersion").GetString() != "2025-06-18") throw new Exception("Unexpected MCP protocol version.");
await p.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new {jsonrpc="2.0",method="notifications/initialized"})); await p.StandardInput.FlushAsync();
var list=await CallAsync(new {jsonrpc="2.0",id=2,method="tools/list",@params=new{}});
var names=list.GetProperty("result").GetProperty("tools").EnumerateArray().Select(x=>x.GetProperty("name").GetString()).ToHashSet();
foreach(var name in new[]{"xpscript_validate","xpscript_symbols","xpscript_describe","xpscript_explain"}) if(!names.Contains(name)) throw new Exception("Missing MCP tool: "+name);
var diagnosticSource=await File.ReadAllTextAsync(Path.Combine(repo,"samples","null-integer-assignment-error.xps"));
async Task<JsonElement> ValidateMcpAsync(int id, bool debug)
{
    var validation=await CallAsync(new {jsonrpc="2.0",id,method="tools/call",@params=new{name="xpscript_validate",arguments=new{source=diagnosticSource,filename="agent.xps",debug}}});
    var toolResult=validation.GetProperty("result");
    if (!toolResult.GetProperty("isError").GetBoolean()) throw new Exception("Compiler validation failures must be reported as MCP tool errors.");
    var value=toolResult.GetProperty("structuredContent");
    if (value.GetProperty("result").GetString() != "error") throw new Exception("Expected compiler validation error result.");
    return value.Clone();
}
var structured=await ValidateMcpAsync(3,false);
var diagnostics=structured.GetProperty("errors").EnumerateArray().ToArray();
if (diagnostics.Length == 0) throw new Exception("Expected at least one structured compiler diagnostic.");
if (diagnostics.Any(x=>!x.TryGetProperty("diagnosticCode",out var code) || string.IsNullOrWhiteSpace(code.GetString()))) throw new Exception("MCP validation returned a diagnostic without a stable diagnostic code.");
if (diagnostics.Any(x=>x.TryGetProperty("file",out var file) && file.GetString()!="agent.xps")) throw new Exception("MCP validation did not preserve the virtual filename.");
var structuredDebug=await ValidateMcpAsync(30,true);

static string DiagnosticIdentity(JsonElement diagnostic) => string.Join("|",
    diagnostic.TryGetProperty("diagnosticCode",out var code)?code.GetString():"",
    diagnostic.TryGetProperty("file",out var file)?file.GetString():"",
    diagnostic.TryGetProperty("line",out var line)?line.GetInt32():0,
    diagnostic.TryGetProperty("position",out var position)?position.GetInt32():0,
    diagnostic.TryGetProperty("endLine",out var endLine)?endLine.GetInt32():0,
    diagnostic.TryGetProperty("endColumn",out var endColumn)?endColumn.GetInt32():0,
    diagnostic.TryGetProperty("description",out var description)?description.GetString():"");
var mcpNormalIdentity=DiagnosticIdentity(structured.GetProperty("errors")[0]);
var mcpDebugIdentity=DiagnosticIdentity(structuredDebug.GetProperty("errors")[0]);
if (mcpNormalIdentity != mcpDebugIdentity) throw new Exception($"MCP debug changed source diagnostic identity. normal={mcpNormalIdentity} debug={mcpDebugIdentity}");

var cliRoot=Path.Combine(Path.GetTempPath(),"XPScript","mcp-parity",Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(cliRoot);
var cliSource=Path.Combine(cliRoot,"agent.xps");
await File.WriteAllTextAsync(cliSource,diagnosticSource);
async Task<JsonElement> CompileCliAsync(bool debug)
{
    var cliPsi=new ProcessStartInfo("dotnet"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,WorkingDirectory=repo};
    foreach(var arg in new[]{"run","--project","src/XPScript.Compiler/XPScript.Compiler.csproj","-c","Release","--no-build","--","compile",cliSource,"--result-format","json"}) cliPsi.ArgumentList.Add(arg);
    if(debug) cliPsi.ArgumentList.Add("--debug");
    using var cli=Process.Start(cliPsi) ?? throw new Exception("Could not start CLI compiler.");
    var stdout=await cli.StandardOutput.ReadToEndAsync();
    var stderr=await cli.StandardError.ReadToEndAsync();
    await cli.WaitForExitAsync();
    if(cli.ExitCode==0) throw new Exception("Expected CLI compilation to fail for parity fixture.");
    try { using var doc=JsonDocument.Parse(stdout); return doc.RootElement.Clone(); }
    catch(Exception ex) { throw new Exception("CLI did not return JSON diagnostics. stdout="+stdout+" stderr="+stderr,ex); }
}
try
{
    var cliNormal=await CompileCliAsync(false);
    var cliDebug=await CompileCliAsync(true);
    var cliNormalIdentity=DiagnosticIdentity(cliNormal.GetProperty("errors")[0]);
    var cliDebugIdentity=DiagnosticIdentity(cliDebug.GetProperty("errors")[0]);
    if(cliNormalIdentity != cliDebugIdentity) throw new Exception($"CLI debug changed source diagnostic identity. normal={cliNormalIdentity} debug={cliDebugIdentity}");
    if(cliNormalIdentity != mcpNormalIdentity) throw new Exception($"CLI/MCP diagnostic mismatch. cli={cliNormalIdentity} mcp={mcpNormalIdentity}");
}
finally { try { Directory.Delete(cliRoot,true); } catch { } }
var webValidation=await CallAsync(new {jsonrpc="2.0",id=4,method="tools/call",@params=new{name="xpscript_validate",arguments=new{source="[Anonymous]\n[Get]\nSub Index()\n    Response.Write(\"ok\")\nEnd Sub",filename="web.xps"}}});
var webResult=webValidation.GetProperty("result");
if (!webResult.GetProperty("isError").GetBoolean()) throw new Exception("Web source validated as a console application must be an MCP tool error.");
var webStructured=webResult.GetProperty("structuredContent");
if (webStructured.GetProperty("result").GetString() != "error") throw new Exception("Expected structured web/console mismatch error.");
var webDescription=webStructured.GetProperty("errors")[0].GetProperty("description").GetString() ?? "";
if (!webDescription.Contains("web application", StringComparison.OrdinalIgnoreCase)) throw new Exception("MCP did not preserve the web application compilation diagnostic.");

var tool = list.GetProperty("result").GetProperty("tools").EnumerateArray().Single(x=>x.GetProperty("name").GetString()=="xpscript_validate");
var properties = tool.GetProperty("inputSchema").GetProperty("properties");
if (!properties.TryGetProperty("debug", out var debugProperty) || debugProperty.GetProperty("type").GetString()!="boolean") throw new Exception("MCP validate must expose boolean debug mode.");
p.StandardInput.Close(); if(!p.WaitForExit(5000)){p.Kill(true);throw new Exception("MCP server did not stop after stdin closed.");}
if(p.ExitCode!=0) throw new Exception("MCP server exit code "+p.ExitCode+": "+await p.StandardError.ReadToEndAsync());
Console.WriteLine("MCP protocol probe passed.");
