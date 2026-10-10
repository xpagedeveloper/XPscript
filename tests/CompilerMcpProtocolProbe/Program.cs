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
foreach(var name in new[]{"xpscript_validate","xpscript_ast_validate","xpscript_symbols","xpscript_describe","xpscript_explain"}) if(!names.Contains(name)) throw new Exception("Missing MCP tool: "+name);
var astValidation=await CallAsync(new {jsonrpc="2.0",id=25,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source="Sub Main()\n    Print \"AST_MCP_OK\"\nEnd Sub",filename="ast-agent.xps"}}});
var astResult=astValidation.GetProperty("result").GetProperty("structuredContent");
if (astResult.GetProperty("result").GetString()!="ok" || astResult.GetProperty("compiler").GetString()!="ast") throw new Exception("MCP AST validation did not use the experimental AST compiler.");
var staticSource="Sub Accumulate()\n    Static value As Long\n    value = value + 1\n    Print value\nEnd Sub\n\nSub Main()\n    Accumulate()\n    Accumulate()\nEnd Sub";
var staticValidation=await CallAsync(new {jsonrpc="2.0",id=26,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=staticSource,filename="static-local-parity.xps"}}});
var staticResult=staticValidation.GetProperty("result").GetProperty("structuredContent");
if (staticResult.GetProperty("result").GetString()!="ok") throw new Exception("AST MCP validation rejected the Static lifetime parity fixture.");
var listCopybackSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "list-byref-copyback.xps"));
var listCopybackValidation = await CallAsync(new {jsonrpc="2.0",id=29,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=listCopybackSource,filename="list-byref-copyback.xps"}}});
if (listCopybackValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP validation rejected the List ByRef copy-back CLI fixture.");
var listMultipleSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "list-byref-multiple-error.xps"));
var listMultipleValidation = await CallAsync(new {jsonrpc="2.0",id=30,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=listMultipleSource,filename="list-byref-multiple-error.xps"}}});
if (listMultipleValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok")
    throw new Exception("AST MCP rejected multiple List ByRef arguments.");
var aliasByRefSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "forall-list-byref.xps"));
var aliasByRefValidation = await CallAsync(new {jsonrpc="2.0",id=31,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=aliasByRefSource,filename="forall-list-byref.xps"}}});
if (aliasByRefValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP rejected the ForAll ByRef CLI fixture.");
var aliasMultipleSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "forall-list-byref-multiple-error.xps"));
var aliasMultipleValidation = await CallAsync(new {jsonrpc="2.0",id=32,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=aliasMultipleSource,filename="forall-list-byref-multiple-error.xps"}}});
if (aliasMultipleValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok")
    throw new Exception("AST MCP rejected multiple ForAll List ByRef aliases.");
var astDiagnostic=await CallAsync(new {jsonrpc="2.0",id=27,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source="Sub Main(\n    Print 1\nEnd Sub",filename="ast-error.xps"}}});
var astDiagnosticResult=astDiagnostic.GetProperty("result").GetProperty("structuredContent");
var astErrors=astDiagnosticResult.GetProperty("errors");
if (astDiagnosticResult.GetProperty("result").GetString()!="error" || astErrors.GetArrayLength()==0 || astErrors[0].GetProperty("file").GetString()!="ast-error.xps" || string.IsNullOrWhiteSpace(astErrors[0].GetProperty("diagnosticCode").GetString())) throw new Exception("AST MCP diagnostics lost the stable code or source filename.");
var astDiagnosticDebug=await CallAsync(new {jsonrpc="2.0",id=28,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source="Sub Main(\n    Print 1\nEnd Sub",filename="ast-error.xps",debug=true}}});
var astDebugError=astDiagnosticDebug.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
if (astDebugError.GetProperty("diagnosticCode").GetString()!=astErrors[0].GetProperty("diagnosticCode").GetString() || astDebugError.GetProperty("line").GetInt32()!=astErrors[0].GetProperty("line").GetInt32() || astDebugError.GetProperty("position").GetInt32()!=astErrors[0].GetProperty("position").GetInt32()) throw new Exception("AST MCP debug mode changed source diagnostic identity.");
var astParityRoot=Path.Combine(Path.GetTempPath(),"XPScript","ast-parity",Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(astParityRoot);
try
{
    var astParitySource=Path.Combine(astParityRoot,"ast-error.xps");
    await File.WriteAllTextAsync(astParitySource,"Sub Main(\n    Print 1\nEnd Sub");
    var astCli=new ProcessStartInfo("dotnet"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,WorkingDirectory=repo};
    foreach(var arg in new[]{"run","--project","src/XPScript.Compiler/XPScript.Compiler.csproj","-c","Release","--no-build","--","ast-compile",astParitySource,"-o",Path.Combine(astParityRoot,"output")}) astCli.ArgumentList.Add(arg);
    using var astProcess=Process.Start(astCli) ?? throw new Exception("Could not start AST CLI parity process.");
    var astCliError=await astProcess.StandardError.ReadToEndAsync();
    await astProcess.StandardOutput.ReadToEndAsync();
    await astProcess.WaitForExitAsync();
    if(astProcess.ExitCode!=2 || !astCliError.Contains("ast-error.xps:3:8: XPS1012:",StringComparison.Ordinal)) throw new Exception("AST CLI diagnostic parity fixture did not preserve its mapped identity.");
    var mcpDiagnostic=astErrors[0];
    if(mcpDiagnostic.GetProperty("diagnosticCode").GetString()!="XPS1012" || mcpDiagnostic.GetProperty("line").GetInt32()!=3 || mcpDiagnostic.GetProperty("position").GetInt32()!=8) throw new Exception("AST MCP diagnostic identity does not match AST CLI.");
}
finally { try { Directory.Delete(astParityRoot,true); } catch { } }
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
var webDescriptions=webStructured.GetProperty("errors").EnumerateArray().Select(error=>error.GetProperty("description").GetString() ?? "").ToArray();
if (!webDescriptions.Any(description=>description.Contains("web application", StringComparison.OrdinalIgnoreCase))) throw new Exception("MCP did not preserve the web application compilation diagnostic. Actual: "+string.Join(" || ",webDescriptions));

var tool = list.GetProperty("result").GetProperty("tools").EnumerateArray().Single(x=>x.GetProperty("name").GetString()=="xpscript_validate");
var properties = tool.GetProperty("inputSchema").GetProperty("properties");
if (!properties.TryGetProperty("debug", out var debugProperty) || debugProperty.GetProperty("type").GetString()!="boolean") throw new Exception("MCP validate must expose boolean debug mode.");
p.StandardInput.Close(); if(!p.WaitForExit(5000)){p.Kill(true);throw new Exception("MCP server did not stop after stdin closed.");}
if(p.ExitCode!=0) throw new Exception("MCP server exit code "+p.ExitCode+": "+await p.StandardError.ReadToEndAsync());
Console.WriteLine("MCP protocol probe passed.");
