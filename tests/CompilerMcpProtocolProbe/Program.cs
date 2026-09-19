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
var validation=await CallAsync(new {jsonrpc="2.0",id=3,method="tools/call",@params=new{name="xpscript_validate",arguments=new{source=await File.ReadAllTextAsync(Path.Combine(repo,"samples","null-integer-assignment-error.xps")),filename="agent.xps"}}});
var result=validation.GetProperty("result");
if (result.GetProperty("isError").GetBoolean()) throw new Exception("Compiler diagnostics must not be MCP tool-level errors.");
var structured=result.GetProperty("structuredContent");
if (structured.GetProperty("result").GetString() != "error") throw new Exception("Expected compiler validation error result.");
var diagnostics=structured.GetProperty("errors").EnumerateArray().ToArray();\nif (diagnostics.Length == 0) throw new Exception("Expected at least one structured compiler diagnostic.");\nif (diagnostics.Any(x=>!x.TryGetProperty("diagnosticCode",out var code) || string.IsNullOrWhiteSpace(code.GetString()))) throw new Exception("MCP validation returned a diagnostic without a stable diagnostic code.");\nif (diagnostics.Any(x=>x.TryGetProperty("file",out var file) && file.GetString()!="agent.xps")) throw new Exception("MCP validation did not preserve the virtual filename.");
p.StandardInput.Close(); if(!p.WaitForExit(5000)){p.Kill(true);throw new Exception("MCP server did not stop after stdin closed.");}
if(p.ExitCode!=0) throw new Exception("MCP server exit code "+p.ExitCode+": "+await p.StandardError.ReadToEndAsync());
Console.WriteLine("MCP protocol probe passed.");
