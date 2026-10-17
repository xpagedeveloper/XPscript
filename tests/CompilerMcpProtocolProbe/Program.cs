using System.Diagnostics;
using System.Text.Json;

var repo = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var psi = new ProcessStartInfo("dotnet") { RedirectStandardInput=true, RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false, WorkingDirectory=repo };
foreach (var a in new[]{"run","--project","src/XPScript.Compiler/XPScript.Compiler.csproj","-c","Release","--no-build","--","mcp"}) psi.ArgumentList.Add(a);
using var p=Process.Start(psi) ?? throw new Exception("Could not start MCP server.");
async Task<JsonElement> CallAsync(object request) { await p.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request)); await p.StandardInput.FlushAsync(); string line; try { line=await p.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)) ?? throw new Exception("MCP server closed stdout."); } catch (TimeoutException ex) { if (!p.HasExited) p.Kill(entireProcessTree: true); throw new Exception("MCP server did not respond within 30 seconds.", ex); } try { using var d=JsonDocument.Parse(line); return d.RootElement.Clone(); } catch { throw new Exception("Non-JSON data leaked to MCP stdout: "+line); } }
var init=await CallAsync(new {jsonrpc="2.0",id=1,method="initialize",@params=new {protocolVersion="2025-06-18",capabilities=new{},clientInfo=new{name="xpscript-ci",version="1"}}});
if (init.GetProperty("result").GetProperty("protocolVersion").GetString() != "2025-06-18") throw new Exception("Unexpected MCP protocol version.");
await p.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new {jsonrpc="2.0",method="notifications/initialized"})); await p.StandardInput.FlushAsync();
var list=await CallAsync(new {jsonrpc="2.0",id=2,method="tools/list",@params=new{}});
var names=list.GetProperty("result").GetProperty("tools").EnumerateArray().Select(x=>x.GetProperty("name").GetString()).ToHashSet();
foreach(var name in new[]{"xpscript_validate","xpscript_ast_validate","xpscript_symbols","xpscript_describe","xpscript_explain"}) if(!names.Contains(name)) throw new Exception("Missing MCP appTool: "+name);
var classMethodsSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe/class-methods-native.xps"));
var arrayBytesSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe/image-array-byte-interop.xps"));
var arrayBytesResult = await CallAsync(new { jsonrpc="2.0",id="image-array-byte-interop",method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source=arrayBytesSource,filename="image-array-byte-interop.xps" } } });
if (arrayBytesResult.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok")
    throw new Exception("Native bounded Byte array validation failed: " + arrayBytesResult);
var invalidArrayBoundsSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe/array-storage-bounds-error.xps"));
var invalidArrayBoundsResult = await CallAsync(new { jsonrpc="2.0",id="array-storage-bounds-error",method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source=invalidArrayBoundsSource,filename="array-storage-bounds-error.xps" } } });
var invalidArrayBoundsError = invalidArrayBoundsResult.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
if (!invalidArrayBoundsResult.GetProperty("result").GetProperty("isError").GetBoolean() || invalidArrayBoundsError.GetProperty("diagnosticCode").GetString() != "XPS1012" || invalidArrayBoundsError.GetProperty("line").GetInt32() != 2)
    throw new Exception("Invalid array bound diagnostic lost: " + invalidArrayBoundsResult);
var arrayBoundsSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe/array-storage-bounds.xps"));
var arrayBoundsResult = await CallAsync(new { jsonrpc="2.0",id="array-storage-bounds",method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source=arrayBoundsSource,filename="array-storage-bounds.xps" } } });
if (arrayBoundsResult.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok")
    throw new Exception("Array storage validation failed: " + arrayBoundsResult);
foreach (var fixture in new[] { "array-parameter-runtime-next", "constructor-array-runtime-next", "method-array-runtime-next", "constructor-array-optional-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Array signature validation failed: " + result);
}
foreach (var (fixture, code, line) in new[] { ("array-storage-bounds-error", "XPS1012", 2), ("constructor-array-rank-error", "XPS1012", 2), ("constructor-array-list-error", "XPS1012", 2) })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (!result.GetProperty("result").GetProperty("isError").GetBoolean() || error.GetProperty("diagnosticCode").GetString() != code || error.GetProperty("line").GetInt32() != line)
        throw new Exception("Array signature diagnostic lost: " + result);
}
foreach (var imageParameterFixture in new[] { "xpimage-parameter-runtime-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", imageParameterFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=imageParameterFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=imageParameterFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("XPImage parameter validation failed: " + result);
}
foreach (var optionalClassMethodFixture in new[] { "class-method-optional-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", optionalClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=optionalClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=optionalClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Optional class-method validation failed: " + result);
}
foreach (var staticClassMethodFixture in new[] { "class-method-static-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", staticClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=staticClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=staticClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Static class-method validation failed: " + result);
}
foreach (var derivedClassMethodFixture in new[] { "class-method-derived-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", derivedClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=derivedClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=derivedClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Derived class-method validation failed: " + result);
}
foreach (var referenceClassMethodFixture in new[] { "class-method-reference-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", referenceClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=referenceClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=referenceClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Reference class-method validation failed: " + result);
}
foreach (var listClassMethodFixture in new[] { "class-method-list-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", listClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=listClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=listClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("List class-method validation failed: " + result);
}
foreach (var staticListClassMethodFixture in new[] { "class-method-static-list-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", staticListClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=staticListClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=staticListClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Static List class-method validation failed: " + result);
}
foreach (var staticArrayClassMethodFixture in new[] { "class-method-static-array-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", staticArrayClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=staticArrayClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=staticArrayClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Static array class-method validation failed: " + result);
}
foreach (var objectClassMethodFixture in new[] { "class-method-object-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", objectClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=objectClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=objectClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Object class-method validation failed: " + result);
}
foreach (var variantClassMethodFixture in new[] { "class-method-variant-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", variantClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=variantClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=variantClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Variant class-method validation failed: " + result);
}
foreach (var jsonClassMethodFixture in new[] { "class-method-json-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", jsonClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=jsonClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=jsonClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("JSON class-method validation failed: " + result);
}
foreach (var jsonArrayClassMethodFixture in new[] { "class-method-json-array-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", jsonArrayClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=jsonArrayClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=jsonArrayClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("JSON array class-method validation failed: " + result);
}
foreach (var jsonDocumentClassMethodFixture in new[] { "class-method-json-document-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", jsonDocumentClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=jsonDocumentClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=jsonDocumentClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("JSON document class-method validation failed: " + result);
}
foreach (var jsonElementClassMethodFixture in new[] { "class-method-json-element-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", jsonElementClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=jsonElementClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=jsonElementClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("JSON element class-method validation failed: " + result);
}
foreach (var xmlClassMethodFixture in new[] { "class-method-xml-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", xmlClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=xmlClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=xmlClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("XML class-method validation failed: " + result);
}
foreach (var xmlDocumentClassMethodFixture in new[] { "class-method-xml-document-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", xmlDocumentClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=xmlDocumentClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=xmlDocumentClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("XML document class-method validation failed: " + result);
}
foreach (var xmlElementClassMethodFixture in new[] { "class-method-xml-element-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", xmlElementClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=xmlElementClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=xmlElementClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("XML element class-method validation failed: " + result);
}
foreach (var csvClassMethodFixture in new[] { "class-method-csv-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", csvClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=csvClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=csvClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("CSV class-method validation failed: " + result);
}
foreach (var spreadsheetClassMethodFixture in new[] { "class-method-spreadsheet-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", spreadsheetClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=spreadsheetClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=spreadsheetClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Spreadsheet class-method validation failed: " + result);
}
foreach (var httpClientClassMethodFixture in new[] { "class-method-http-client-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", httpClientClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=httpClientClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=httpClientClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("HTTP client class-method validation failed: " + result);
}
foreach (var httpRequestClassMethodFixture in new[] { "class-method-http-request-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", httpRequestClassMethodFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=httpRequestClassMethodFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=httpRequestClassMethodFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("HTTP request class-method validation failed: " + result);
}
foreach (var derivedPropertyFixture in new[] { "class-property-derived-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", derivedPropertyFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=derivedPropertyFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=derivedPropertyFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Derived property validation failed: " + result);
}
foreach (var initializedModuleFixture in new[] { "initialized-module-byref-next", "initialized-module-boolean-next", "initialized-module-string-next", "initialized-module-reference-next", "initialized-module-reference-new-next", "initialized-module-reference-args-next", "initialized-module-date-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", initializedModuleFixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=initializedModuleFixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=initializedModuleFixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Initialized module validation failed: " + result);
}
foreach (var fixture in new[] { "class-constructor-list", "constructor-reference-list", "image-constructor-list", "image-constructor-variant", "class-constructor-variant", "class-constructor-native", "constructor-reference-variant", "class-constructor-overloads", "class-constructor-overload-parity", "constructor-refref-module", "image-constructor-optional-refref", "class-constructor-reference-storage", "class-constructor-optional-refref", "constructor-refref-array", "constructor-refref-list", "constructor-refref-forall", "constructor-refref-optional", "class-constructor-optional-reference", "constructor-reference-optional", "image-constructor-optional-reference", "class-constructor-reference-byref", "constructor-reference-byref-simple", "constructor-reference-byref", "constructor-reference-implicit", "image-constructor-reference-byref", "class-constructor-reference", "constructor-reference-byval", "class-constructor-reference-parity", "image-constructor-reference", "constructor-optional-byref-zero", "class-constructor-optional-byref", "constructor-byref-optional", "constructor-optional-byref", "image-constructor-optional-byref", "class-constructor-mixed", "image-constructor-mixed", "class-constructor-byref", "class-constructor-byref-simple", "constructor-byref", "image-constructor-byref", "class-constructor-optional", "class-constructor-optional-parity", "constructor-optional", "image-constructor-optional", "class-constructor-derived", "class-constructor-derived-parity", "constructor-parent-forwarding", "image-constructor-derived", "class-constructor", "class-constructor-parity", "image-constructor", "constructor-xpimage-parameter-next" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Constructor validation failed: " + result);
}
foreach (var (fixture, code, line) in new[] { ("constructor-list-byref-type-error", "XPS2003", 7), ("constructor-list-byval-type-error", "XPS2004", 7), ("constructor-list-default-type-error", "XPS2001", 2), ("constructor-list-default-name-error", "XPS2008", 2), ("constructor-list-reference-type-error", "XPS2003", 11), ("constructor-list-unknown-type-error", "XPS3001", 2), ("constructor-list-method-deferred", "XPS3001", 2), ("constructor-native-byref-type-error", "XPS2003", 7), ("constructor-native-optional-type-error", "XPS2001", 2), ("constructor-variant-byref-type-error", "XPS2003", 7), ("constructor-variant-optional-type-error", "XPS2001", 2), ("constructor-overloads-ambiguous-error", "XPS2005", 8), ("constructor-overloads-name-error", "XPS2006", 2), ("constructor-overloads-no-match-error", "XPS2004", 8), ("constructor-overloads-private-error", "XPS2009", 8), ("constructor-parent-late-error", "XPS1012", 6), ("constructor-parent-duplicate-error", "XPS1012", 6), ("constructor-parent-root-error", "XPS1012", 3), ("constructor-parent-nested-error", "XPS1012", 6), ("constructor-parent-type-error", "XPS2004", 7), ("constructor-parent-private-error", "XPS2009", 7), ("constructor-parent-instance-error", "XPS2008", 8), ("constructor-private-base-field-error", "XPS2008", 6), ("constructor-arity-error", "XPS2004", 6), ("constructor-type-error", "XPS2004", 6), ("constructor-private-error", "XPS2009", 6), ("constructor-body-error", "XPS2008", 3), ("constructor-byref-literal-error", "XPS2001", 6), ("constructor-byref-type-error", "XPS2003", 7), ("constructor-byref-property-error", "XPS3001", 11), ("constructor-byref-indexed-property-error", "XPS3001", 11), ("constructor-byref-module-storage-error", "XPS3001", 7), ("constructor-byref-parent-literal-error", "XPS2001", 7), ("constructor-byref-parent-type-error", "XPS2003", 7), ("constructor-mixed-literal-error", "XPS2001", 6), ("constructor-mixed-type-error", "XPS2003", 7), ("constructor-mixed-default-error", "XPS2008", 2), ("constructor-optional-byref-literal-error", "XPS2001", 6), ("constructor-optional-byref-type-error", "XPS2003", 7), ("constructor-optional-byref-default-error", "XPS2008", 2), ("constructor-optional-byref-default-type-error", "XPS2001", 2), ("constructor-optional-byref-parent-literal-error", "XPS2001", 7), ("constructor-optional-order-error", "XPS1012", 2), ("constructor-optional-marker-error", "XPS1012", 2), ("constructor-optional-type-error", "XPS2001", 2), ("constructor-optional-name-error", "XPS2008", 2), ("constructor-optional-later-error", "XPS2008", 2), ("constructor-optional-self-error", "XPS2008", 2), ("constructor-optional-instance-error", "XPS2008", 3), ("constructor-optional-required-error", "XPS2004", 6), ("constructor-optional-slot-error", "XPS1012", 6), ("constructor-optional-private-error", "XPS2009", 6), ("constructor-refref-element-type-error", "XPS2003", 11), ("constructor-refref-upcast-error", "XPS2003", 11), ("constructor-refref-object-error", "XPS2003", 9), ("constructor-refref-temporary-error", "XPS2001", 8), ("constructor-refref-variant-error", "XPS2003", 7), ("constructor-refref-property-error", "XPS3001", 14), ("constructor-refref-parent-error", "XPS2003", 11), ("constructor-optional-reference-type-error", "XPS2001", 2), ("constructor-optional-reference-name-error", "XPS2008", 4), ("constructor-reference-native-deferred", "XPS3001", 2), ("constructor-reference-unknown-deferred", "XPS3001", 2), ("constructor-reference-scalar-error", "XPS2004", 6), ("constructor-reference-class-error", "XPS2004", 11), ("constructor-reference-downcast-error", "XPS2004", 11), ("constructor-reference-method-deferred", "XPS3001", 6), ("constructor-required-base-deferred", "XPS2004", 5), ("constructor-overloads-duplicate-error", "XPS2006", 4) })
{
    if (fixture == "constructor-reference-native-deferred") continue;
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (!result.GetProperty("result").GetProperty("isError").GetBoolean() || error.GetProperty("diagnosticCode").GetString() != code || error.GetProperty("line").GetInt32() != line)
        throw new Exception("Constructor diagnostic lost: " + result);
}
foreach (var fixture in new[] { "class-destructor-derived", "class-destructor-derived-parity", "class-destructor-derived-signature", "image-destructor-derived", "class-destructor", "class-destructor-parity", "image-destructor" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Destructor validation failed: " + result);
}
foreach (var (fixture, code, line) in new[] { ("class-destructor-parameter-error", "XPS1012", 2), ("class-destructor-duplicate-error", "XPS1012", 4), ("class-destructor-function-error", "XPS1012", 2), ("class-destructor-private-base-error", "XPS2008", 6), ("class-destructor-private-member-error", "XPS2009", 6), ("class-destructor-private-method-error", "XPS2009", 7), ("class-destructor-derived-body-error", "XPS2008", 5), ("class-destructor-derived-method-deferred", "XPS3001", 4), ("class-destructor-body-error", "XPS2008", 3) })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (!result.GetProperty("result").GetProperty("isError").GetBoolean() || error.GetProperty("diagnosticCode").GetString() != code || error.GetProperty("line").GetInt32() != line)
        throw new Exception("Destructor diagnostic lost: " + result);
}
foreach (var fixture in new[] { "image-field-delete", "image-property-set", "image-property-set-byval-signature", "image-property-set-byref-signature", "image-property-get", "image-property-signature-only", "http-reference-properties", "http-property-signature-only", "tabular-reference-properties", "tabular-reference-signatures", "native-reference-properties", "native-reference-signatures" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Native accessor validation failed: " + result);
}
foreach (var (fixture, code, line) in new[] { ("image-property-set-mode-error", "XPS2001", 8), ("image-property-set-type-error", "XPS2001", 8), ("image-property-index-deferred", "XPS3001", 2), ("image-property-readonly-error", "XPS2001", 7), ("image-property-private-error", "XPS2009", 7), ("image-property-body-error", "XPS2008", 3), ("http-property-type-error", "XPS2001", 8), ("http-property-mode-error", "XPS2001", 8), ("http-property-readonly-error", "XPS2001", 7), ("http-property-file-type-error", "XPS2001", 8), ("http-property-index-deferred", "XPS3001", 2), ("tabular-property-type-error", "XPS2001", 8), ("tabular-property-mode-error", "XPS2001", 8), ("tabular-property-frombytes-error", "XPS1012", 7), ("tabular-property-readonly-error", "XPS2001", 7), ("native-property-type-error", "XPS2001", 8), ("native-property-mode-error", "XPS2001", 8) })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (!result.GetProperty("result").GetProperty("isError").GetBoolean() || error.GetProperty("diagnosticCode").GetString() != code || error.GetProperty("line").GetInt32() != line)
        throw new Exception("Native accessor diagnostic lost: " + result);
}
var inheritedSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe/inherited-reference-runtime.xps"));
var inheritedResult = await CallAsync(new { jsonrpc="2.0",id="inherited-reference-runtime",method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source=inheritedSource,filename="inherited-reference-runtime.xps" } } });
if (inheritedResult.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Inherited reference validation failed: " + inheritedResult);
foreach (var (fixture, code, line) in new[] { ("inherited-reference-downcast-error", "XPS2001", 11), ("inherited-reference-sibling-error", "XPS2001", 13), ("inherited-reference-isolation-error", "XPS2001", 11), ("inheritance-cycle-error", "XPS1012", 1), ("inheritance-self-cycle-error", "XPS1012", 1), ("inheritance-unknown-base-error", "XPS2008", 1) })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (!result.GetProperty("result").GetProperty("isError").GetBoolean() || error.GetProperty("diagnosticCode").GetString() != code || error.GetProperty("line").GetInt32() != line)
        throw new Exception("Inherited reference diagnostic lost: " + result);
}
foreach (var fixture in new[] { "indexed-object-runtime", "indexed-object-properties" })
{
    var path = fixture == "indexed-object-properties" ? Path.Combine(repo, "samples", fixture + ".xps") : Path.Combine(repo, "tests/ast-compile-probe", fixture + ".xps");
    var source = await File.ReadAllTextAsync(path);
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Typed/indexed object validation failed: " + result);
}
foreach (var (fixture, line) in new[] { ("object-property-member-mode-error", 7), ("object-property-declaration-error", 2), ("object-property-mode-error", 7), ("object-property-scalar-error", 7), ("object-property-type-error", 11), ("object-property-let-error", 7), ("object-property-call-target-error", 4) })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (!result.GetProperty("result").GetProperty("isError").GetBoolean() || error.GetProperty("diagnosticCode").GetString() != "XPS2001" || error.GetProperty("line").GetInt32() != line)
        throw new Exception("Object accessor diagnostic lost: " + result);
}
var objectPropertySource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe/object-property-set.xps"));
var objectPropertyResult = await CallAsync(new { jsonrpc="2.0",id="object-property-set",method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source=objectPropertySource,filename="object-property-set.xps" } } });
if (objectPropertyResult.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Object Property Set validation failed: " + objectPropertyResult);
foreach (var fixture in new[] { "indexed-optional-simple", "indexed-optional-runtime" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Optional index validation failed: " + result);
}
foreach (var (fixture, code, line) in new[] { ("indexed-optional-order-error", "XPS1012", 2), ("indexed-optional-default-error", "XPS2001", 2), ("indexed-optional-name-error", "XPS2008", 2), ("indexed-optional-later-error", "XPS2008", 2), ("indexed-optional-signature-error", "XPS1012", 5), ("indexed-optional-value-error", "XPS1012", 2), ("indexed-optional-required-error", "XPS2002", 8), ("indexed-optional-marker-error", "XPS1012", 2) })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture + ".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (!result.GetProperty("result").GetProperty("isError").GetBoolean() || error.GetProperty("diagnosticCode").GetString() != code || error.GetProperty("line").GetInt32() != line)
        throw new Exception("Optional index diagnostic lost: " + result);
}
var staticEarlySource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe/static-array.xps"));
var staticEarlyValidation = await CallAsync(new { jsonrpc="2.0",id="static-array-early",method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source=staticEarlySource,filename="static-array.xps" } } });
if (staticEarlyValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST Static array storage failed: " + staticEarlyValidation);
var elementIndexSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe/indexed-byref-elements-runtime.xps"));
var elementIndexResult = await CallAsync(new { jsonrpc="2.0",id="indexed-byref-elements-runtime",method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source=elementIndexSource,filename="indexed-byref-elements-runtime.xps" } } });
if (elementIndexResult.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST element ByRef index validation failed: " + elementIndexResult);
var moduleIndexSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe/indexed-byref-module-runtime.xps"));
var moduleIndexResult = await CallAsync(new { jsonrpc="2.0",id="indexed-byref-module-runtime",method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source=moduleIndexSource,filename="indexed-byref-module-runtime.xps" } } });
if (moduleIndexResult.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST module ByRef index validation failed: " + moduleIndexResult);
var byRefIndexSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe/indexed-byref-runtime.xps"));
var byRefIndexResult = await CallAsync(new { jsonrpc="2.0",id="indexed-byref-runtime",method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source=byRefIndexSource,filename="indexed-byref-runtime.xps" } } });
if (byRefIndexResult.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST ByRef index validation failed: " + byRefIndexResult);
foreach (var (fixture, code, line) in new[] { ("indexed-byref-module-storage-error", "XPS3001", 9), ("indexed-byref-module-type-error", "XPS2003", 9), ("indexed-byref-property-error", "XPS3001", 16), ("indexed-byref-literal-error", "XPS2001", 8), ("indexed-byref-type-error", "XPS2003", 9), ("indexed-byref-mode-error", "XPS1012", 5), ("indexed-byref-complex-error", "XPS2003", 9) })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture+".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (error.GetProperty("diagnosticCode").GetString() != code || error.GetProperty("line").GetInt32() != line) throw new Exception("ByRef index diagnostic lost: " + result);
}
foreach (var indexedFixture in new[] { "indexed-byref-list-lifetime", "list-stable-storage", "indexed-byref-elements-simple", "indexed-byref-module-simple", "indexed-byref-field-capability", "indexed-properties-runtime", "indexed-get-parity" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", indexedFixture+".xps"));
    foreach (var indexedTool in indexedFixture is "indexed-get-parity" or "list-stable-storage" ? new[] { "xpscript_ast_validate", "xpscript_validate" } : new[] { "xpscript_ast_validate" })
    {
        var result = await CallAsync(new { jsonrpc="2.0",id=indexedFixture+indexedTool,method="tools/call",@params=new { name=indexedTool,arguments=new { source,filename=indexedFixture+".xps" } } });
        if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Indexed property validation failed: " + result);
    }
}
foreach (var (fixture, code, line) in new[] { ("indexed-signature-error", "XPS1012", 5), ("indexed-readonly-error", "XPS2001", 8), ("indexed-writeonly-error", "XPS2001", 7), ("indexed-arity-error", "XPS2002", 8), ("indexed-type-error", "XPS2003", 8), ("indexed-private-error", "XPS2009", 8) })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture+".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (error.GetProperty("diagnosticCode").GetString() != code || error.GetProperty("line").GetInt32() != line) throw new Exception("Indexed diagnostic lost: " + result);
}
var propertiesSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe/class-properties-native.xps"));
var propertyCapabilitySource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe/class-properties-capability.xps"));
var propertyCapability = await CallAsync(new { jsonrpc="2.0",id="property-capability",method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source=propertyCapabilitySource,filename="class-properties-capability.xps" } } });
if (propertyCapability.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST property capability failed: " + propertyCapability);
foreach (var propertyTool in new[] { "xpscript_ast_validate", "xpscript_validate" })
{
    var result = await CallAsync(new { jsonrpc="2.0",id="properties-"+propertyTool,method="tools/call",@params=new { name=propertyTool,arguments=new { source=propertiesSource,filename="class-properties-native.xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("Property validation failed: " + result);
}
foreach (var (fixture, code, line) in new[] { ("class-property-readonly-error", "XPS2001", 8), ("class-property-writeonly-error", "XPS2001", 9), ("class-property-private-error", "XPS2009", 8), ("class-property-body-error", "XPS2008", 3), ("class-property-type-error", "XPS1012", 5) })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture+".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source,filename=fixture+".xps" } } });
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (error.GetProperty("diagnosticCode").GetString() != code || error.GetProperty("line").GetInt32() != line) throw new Exception("Property diagnostic lost: " + result);
}
var classCaseSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe/class-method-case.xps"));
var classCaseResult = await CallAsync(new { jsonrpc="2.0",id="class-method-case",method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source=classCaseSource,filename="class-method-case.xps" } } });
if (classCaseResult.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST class case regression: " + classCaseResult);
foreach (var classTool in new[] { "xpscript_ast_validate", "xpscript_validate" })
{
    var result = await CallAsync(new { jsonrpc="2.0",id="class-methods-"+classTool,method="tools/call",@params=new { name=classTool,arguments=new { source=classMethodsSource,filename="class-methods-native.xps" } } });
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("MCP class method compilation failed: " + result);
}
foreach (var (fixture, code, line) in new[] { ("class-method-scope-error", "XPS2008", 6), ("class-method-private-error", "XPS2009", 7), ("class-field-private-error", "XPS2009", 6), ("class-method-body-error", "XPS2008", 3) })
{
    var classSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", fixture+".xps"));
    var result = await CallAsync(new { jsonrpc="2.0",id=fixture,method="tools/call",@params=new { name="xpscript_ast_validate",arguments=new { source=classSource,filename=fixture+".xps" } } });
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (error.GetProperty("diagnosticCode").GetString() != code || error.GetProperty("line").GetInt32() != line) throw new Exception("Class method diagnostic span/code lost: " + result);
}
foreach (var appFixture in new[] { "application-with", "application-native", "application-services", "application-metadata", "application-unused" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", appFixture + ".xps"));
    foreach (var appTool in appFixture == "application-with" ? new[] { "xpscript_ast_validate" } : new[] { "xpscript_ast_validate", "xpscript_validate" })
    {
        var result = await CallAsync(new { jsonrpc="2.0",id=appFixture+appTool,method="tools/call",@params=new { name=appTool,arguments=new { source,filename=appFixture+".xps" } } });
        if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("MCP Application rejected: " + appFixture + appTool + result);
    }
}
foreach (var (appFixture, code) in new[] { ("application-member-error", "XPS2009"), ("application-readonly-error", "XPS2001"), ("application-args-readonly-error", "XPS2001"), ("application-arity-error", "XPS2004"), ("application-constructor-error", "XPS1009") })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests/ast-compile-probe", appFixture + ".xps"));
    foreach (var appTool in appFixture is "application-readonly-error" or "application-args-readonly-error" or "application-constructor-error" ? new[] { "xpscript_ast_validate", "xpscript_validate" } : new[] { "xpscript_ast_validate" })
    {
        var result = await CallAsync(new { jsonrpc="2.0",id=appFixture+appTool,method="tools/call",@params=new { name=appTool,arguments=new { source,filename=appFixture+".xps" } } });
        var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
        if (error.GetProperty("diagnosticCode").GetString() != code || error.GetProperty("line").GetInt32() != 2) throw new Exception("Application MCP code/span lost: " + appFixture + appTool + result);
    }
}
foreach (var sheetFixture in new[] { "spreadsheet-runtime", "spreadsheet-reference", "spreadsheet-unused", "spreadsheet-csv-inferred", "spreadsheet-tocsv-inferred", "spreadsheet-fromworksheet" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", sheetFixture + ".xps"));
    var result = await CallAsync(new {jsonrpc="2.0",id=sheetFixture,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source,filename=sheetFixture+".xps"}}});
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP rejected spreadsheet fixture: " + sheetFixture);
}
foreach (var sheetFixture in new[] { ("spreadsheet-member-error", "XPS2009", 3), ("spreadsheet-constructor-error", "XPS1009", 2),
    ("spreadsheet-readonly-error", "XPS2001", 3), ("spreadsheet-private-setter-error", "XPS2001", 3), ("spreadsheet-arity-error", "XPS2004", 3) })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", sheetFixture.Item1 + ".xps"));
    var result = await CallAsync(new {jsonrpc="2.0",id="sheet-invalid-"+sheetFixture.Item1,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source,filename=sheetFixture.Item1+".xps"}}});
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (error.GetProperty("diagnosticCode").GetString() != sheetFixture.Item2 || error.GetProperty("line").GetInt32() != sheetFixture.Item3) throw new Exception("Spreadsheet diagnostic code/span lost.");
    if (sheetFixture.Item1 == "spreadsheet-constructor-error")
    {
        var normal = await CallAsync(new {jsonrpc="2.0",id="sheet-normal-invalid",method="tools/call",@params=new{name="xpscript_validate",arguments=new{source,filename=sheetFixture.Item1+".xps"}}});
        if (normal.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0].GetProperty("diagnosticCode").GetString() != sheetFixture.Item2) throw new Exception("Normal spreadsheet constructor diagnostic drifted.");
    }
}
foreach (var httpFixture in new[] { "http-runtime", "http-unused" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", httpFixture + ".xps"));
    foreach (var httpTool in new[] { "xpscript_ast_validate", "xpscript_validate" })
    {
        var result = await CallAsync(new {jsonrpc="2.0",id=httpFixture+httpTool,method="tools/call",@params=new{name=httpTool,arguments=new{source,filename=httpFixture+".xps"}}});
        if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("MCP rejected native HTTP fixture: " + httpFixture + httpTool + " " + result.GetProperty("result").GetProperty("structuredContent"));
    }
}
foreach (var httpFixture in new[] { ("http-member-error", "XPS2009", 3), ("http-constructor-error", "XPS1009", 2), ("http-readonly-error", "XPS2001", 3), ("http-arity-error", "XPS2004", 3) })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", httpFixture.Item1 + ".xps"));
    var result = await CallAsync(new {jsonrpc="2.0",id="http-invalid-"+httpFixture.Item1,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source,filename=httpFixture.Item1+".xps"}}});
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (error.GetProperty("diagnosticCode").GetString() != httpFixture.Item2 || error.GetProperty("line").GetInt32() != httpFixture.Item3) throw new Exception("HTTP diagnostic code/span lost.");
    if (httpFixture.Item1 == "http-constructor-error")
    {
        var normal = await CallAsync(new {jsonrpc="2.0",id="http-normal-invalid",method="tools/call",@params=new{name="xpscript_validate",arguments=new{source,filename=httpFixture.Item1+".xps"}}});
        if (normal.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0].GetProperty("diagnosticCode").GetString() != httpFixture.Item2) throw new Exception("Normal HTTP constructor diagnostic drifted.");
    }
}
foreach (var xmlFixture in new[] { "xml-parent", "xml-with-call", "xml-runtime", "xml-reference", "xml-security", "xml-parity", "xml-unused", "xml-escape-only" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", xmlFixture + ".xps"));
    var result = await CallAsync(new {jsonrpc="2.0",id="xml-"+xmlFixture,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source,filename=xmlFixture+".xps"}}});
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok")
        throw new Exception("AST MCP rejected XML fixture: " + xmlFixture);
}
foreach (var xmlFixture in new[] { ("xml-member-error", "XPS2009", 3), ("xml-constructor-error", "XPS1009", 2),
    ("xml-element-constructor-error", "XPS1008", 2), ("xml-readonly-error", "XPS2001", 3),
    ("xml-index-readonly-error", "XPS2001", 3), ("xml-arity-error", "XPS2004", 3), ("xml-static-error", "XPS2009", 2) })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", xmlFixture.Item1 + ".xps"));
    var result = await CallAsync(new {jsonrpc="2.0",id="xml-invalid-"+xmlFixture.Item1,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source,filename=xmlFixture.Item1+".xps"}}});
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (error.GetProperty("diagnosticCode").GetString() != xmlFixture.Item2 || error.GetProperty("line").GetInt32() != xmlFixture.Item3)
        throw new Exception("AST MCP XML diagnostic lost its code or span: " + xmlFixture.Item1);
}
var xmlParitySource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "xml-parity.xps"));
var xmlParityValidation = await CallAsync(new {jsonrpc="2.0",id="xml-normal-parity",method="tools/call",@params=new{name="xpscript_validate",arguments=new{source=xmlParitySource,filename="xml-parity.xps"}}});
if (xmlParityValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok")
    throw new Exception("Normal MCP rejected native XML parity.");
foreach (var xmlFixture in new[] { ("xml-constructor-error", "XPS1009"), ("xml-element-constructor-error", "XPS1008") })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", xmlFixture.Item1 + ".xps"));
    var result = await CallAsync(new {jsonrpc="2.0",id="xml-normal-invalid-"+xmlFixture.Item1,method="tools/call",@params=new{name="xpscript_validate",arguments=new{source,filename=xmlFixture.Item1+".xps"}}});
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (error.GetProperty("diagnosticCode").GetString() != xmlFixture.Item2) throw new Exception("Normal XML constructor diagnostic drifted.");
}
foreach (var csvFixture in new[] { "csv-runtime", "csv-reference", "csv-parity", "csv-unused", "csv-escape-only" })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", csvFixture + ".xps"));
    var result = await CallAsync(new {jsonrpc="2.0",id="csv-"+csvFixture,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source,filename=csvFixture+".xps"}}});
    if (result.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok")
        throw new Exception("AST MCP rejected CSV fixture: " + csvFixture);
}
foreach (var csvFixture in new[] { ("csv-member-error", "XPS2009", 3), ("csv-constructor-error", "XPS1009", 2),
    ("csv-readonly-error", "XPS2001", 3), ("csv-writefile-error", "XPS1011", 3), ("csv-load-arity-error", "XPS1010", 3) })
{
    var source = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", csvFixture.Item1 + ".xps"));
    var result = await CallAsync(new {jsonrpc="2.0",id="csv-invalid-"+csvFixture.Item1,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source,filename=csvFixture.Item1+".xps"}}});
    var error = result.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (error.GetProperty("diagnosticCode").GetString() != csvFixture.Item2 || error.GetProperty("line").GetInt32() != csvFixture.Item3)
        throw new Exception("AST MCP CSV diagnostic lost its code or span: " + csvFixture.Item1);
}
var csvParitySource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "csv-parity.xps"));
var csvParityValidation = await CallAsync(new {jsonrpc="2.0",id="csv-normal-parity",method="tools/call",@params=new{name="xpscript_validate",arguments=new{source=csvParitySource,filename="csv-parity.xps"}}});
if (csvParityValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok")
    throw new Exception("Normal MCP rejected native CSV loader/reference parity.");
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
var listByRefExpressionSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "list-byref-expression-error.xps"));
var listByRefExpressionValidation = await CallAsync(new {jsonrpc="2.0",id=30_1,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=listByRefExpressionSource,filename="list-byref-expression-error.xps"}}});
if (listByRefExpressionValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok")
    throw new Exception("AST MCP rejected List ByRef function expressions.");
var listFunctionsSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "list-functions.xps"));
var listFunctionsValidation = await CallAsync(new {jsonrpc="2.0",id=30_2,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=listFunctionsSource,filename="list-functions.xps"}}});
if (listFunctionsValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP rejected List parameter and return semantics.");
var listOverloadsSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "list-parameter-overloads.xps"));
var listOverloadsValidation = await CallAsync(new {jsonrpc="2.0",id=30_20,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=listOverloadsSource,filename="list-parameter-overloads.xps"}}});
if (listOverloadsValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP rejected typed List parameter overloads.");
var isElementValidation = await CallAsync(new {jsonrpc="2.0",id=30_3,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source="Sub Main()\n    Print IsElement(1)\nEnd Sub",filename="list-iselement-invalid.xps"}}});
var isElementError = isElementValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
if (isElementError.GetProperty("line").GetInt32() != 2 || !isElementError.GetProperty("description").GetString()!.Contains("IsElement requires one keyed List access")) throw new Exception("MCP lost IsElement argument diagnostics.");
var listTypeRequestId = 30_4;
foreach (var listInvalid in new[] { "list-parameter-type-error", "list-initializer-type-error" })
{
    var invalidSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", listInvalid + ".xps"));
    var invalidValidation = await CallAsync(new {jsonrpc="2.0",id=listTypeRequestId++,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=invalidSource,filename=listInvalid + ".xps"}}});
    var invalidResult = invalidValidation.GetProperty("result");
    var invalidErrors = invalidResult.GetProperty("structuredContent").GetProperty("errors");
    var expectedCode = listInvalid == "list-parameter-type-error" ? "XPS2004" : "XPS2001";
    var expectedLine = listInvalid == "list-parameter-type-error" ? 7 : 3;
    if (!invalidResult.GetProperty("isError").GetBoolean() || invalidErrors.GetArrayLength() != 1 ||
        invalidErrors[0].GetProperty("diagnosticCode").GetString() != expectedCode || invalidErrors[0].GetProperty("line").GetInt32() != expectedLine)
        throw new Exception("MCP lost List element type diagnostics: " + listInvalid);
}
var aliasByRefSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "forall-list-byref.xps"));
var aliasByRefValidation = await CallAsync(new {jsonrpc="2.0",id=31,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=aliasByRefSource,filename="forall-list-byref.xps"}}});
if (aliasByRefValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP rejected the ForAll ByRef CLI fixture.");
var aliasMultipleSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "forall-list-byref-multiple-error.xps"));
var aliasMultipleValidation = await CallAsync(new {jsonrpc="2.0",id=32,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=aliasMultipleSource,filename="forall-list-byref-multiple-error.xps"}}});
if (aliasMultipleValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok")
    throw new Exception("AST MCP rejected multiple ForAll List ByRef aliases.");
var gotoNestedSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "goto-nested-for-error.xps"));
var gotoNestedValidation = await CallAsync(new {jsonrpc="2.0",id=33,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=gotoNestedSource,filename="goto-nested-for-error.xps"}}});
var gotoNestedError = gotoNestedValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
if (gotoNestedError.GetProperty("line").GetInt32() != 4 || gotoNestedError.GetProperty("position").GetInt32() != 5)
    throw new Exception("AST MCP GoTo loop diagnostic lost the GoTo source span.");
var gotoIntoForAllSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "goto-into-forall-error.xps"));
var gotoIntoForAllValidation = await CallAsync(new {jsonrpc="2.0",id=35,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=gotoIntoForAllSource,filename="goto-into-forall-error.xps"}}});
var gotoIntoForAllError = gotoIntoForAllValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
if (gotoIntoForAllError.GetProperty("description").GetString() is not { } gotoIntoForAllMessage || !gotoIntoForAllMessage.Contains("same loop scope", StringComparison.OrdinalIgnoreCase) || gotoIntoForAllError.GetProperty("line").GetInt32() != 3)
    throw new Exception("AST MCP did not preserve the ForAll loop-entry diagnostic.");
var gotoDeclarationSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "goto-declaration-error.xps"));
var gotoDeclarationValidation = await CallAsync(new {jsonrpc="2.0",id=34,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=gotoDeclarationSource,filename="goto-declaration-error.xps"}}});
var gotoDeclarationError = gotoDeclarationValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
if (gotoDeclarationError.GetProperty("line").GetInt32() != 2 || gotoDeclarationError.GetProperty("position").GetInt32() != 5)
    throw new Exception("AST MCP GoTo declaration diagnostic lost the GoTo source span.");
var optionBaseSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "option-base.xps"));
var optionBaseValidation = await CallAsync(new {jsonrpc="2.0",id=35,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=optionBaseSource,filename="option-base.xps"}}});
if (optionBaseValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok")
    throw new Exception("AST MCP rejected the Option Base array fixture.");
var errorHandlingSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "on-error-error.xps"));
var errorHandlingValidation = await CallAsync(new {jsonrpc="2.0",id=36,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=errorHandlingSource,filename="on-error-error.xps"}}});
if (errorHandlingValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP rejected On Error Resume Next.");
var resumeSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "resume-error.xps"));
var resumeValidation = await CallAsync(new {jsonrpc="2.0",id=37,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=resumeSource,filename="resume-error.xps"}}});
if (resumeValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP rejected Resume syntax.");
var resumeRuntimeSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "resume-runtime.xps"));
var resumeRuntimeValidation = await CallAsync(new {jsonrpc="2.0",id=37_1,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=resumeRuntimeSource,filename="resume-runtime.xps"}}});
if (resumeRuntimeValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP rejected executable Resume fixture.");
var resumeUnknownValidation = await CallAsync(new {jsonrpc="2.0",id=37_2,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source="Sub Main()\n    On Error GoTo missing\nEnd Sub",filename="resume-unknown-label.xps"}}});
var resumeUnknownError = resumeUnknownValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
if (resumeUnknownError.GetProperty("line").GetInt32() != 2 || !resumeUnknownError.GetProperty("description").GetString()!.Contains("Unknown label 'missing'")) throw new Exception("MCP lost unknown error-handler label diagnostics.");
foreach (var jsonFixture in new[] { "json-runtime", "json-reference" })
{
    var jsonSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", jsonFixture + ".xps"));
    var jsonValidation = await CallAsync(new {jsonrpc="2.0",id=3700 + jsonFixture.Length,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=jsonSource,filename=jsonFixture + ".xps"}}});
    var jsonResult = jsonValidation.GetProperty("result").GetProperty("structuredContent");
    if (jsonResult.GetProperty("result").GetString() != "ok" || jsonResult.GetProperty("compiler").GetString() != "ast") throw new Exception("MCP rejected shared JSON runtime fixture: " + jsonFixture);
}
var sharedRuntimeSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "shared-runtime.xps"));
var sharedRuntimeValidation = await CallAsync(new {jsonrpc="2.0",id=3798,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=sharedRuntimeSource,filename="shared-runtime.xps"}}});
if (sharedRuntimeValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("MCP rejected shared scalar runtime fixture.");
var jsonInvalidSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "json-member-error.xps"));
var jsonInvalidValidation = await CallAsync(new {jsonrpc="2.0",id=3799,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=jsonInvalidSource,filename="json-member-error.xps"}}});
var jsonInvalidError = jsonInvalidValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
if (jsonInvalidError.GetProperty("diagnosticCode").GetString() != "XPS2009" || jsonInvalidError.GetProperty("line").GetInt32() != 3 || jsonInvalidError.GetProperty("position").GetInt32() != 16) throw new Exception("MCP JSON member diagnostic lost its code or source position.");
if (args.Contains("--ast-error-flow-only"))
{
    p.Kill(entireProcessTree: true);
    Console.WriteLine("AST_MCP_ERROR_FLOW_OK");
    return;
}
var withSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "with-error.xps"));
var withValidation = await CallAsync(new {jsonrpc="2.0",id=38,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=withSource,filename="with-error.xps"}}});
if (withValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP rejected the valid With fixture.");
var imageSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "xpimage-error.xps"));
var imageValidation = await CallAsync(new {jsonrpc="2.0",id=39,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=imageSource,filename="xpimage-error.xps"}}});
if (imageValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP rejected native XPImage construction.");
var imageRuntimeSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "xpimage-runtime.xps"));
var imageRuntimeValidation = await CallAsync(new {jsonrpc="2.0",id=3901,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=imageRuntimeSource,filename="xpimage-runtime.xps"}}});
if (imageRuntimeValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP rejected image loaders, effects or ownership.");
var imageVariantSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "xpimage-variant-return.xps"));
var imageVariantValidation = await CallAsync(new {jsonrpc="2.0",id=3902,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=imageVariantSource,filename="xpimage-variant-return.xps"}}});
if (imageVariantValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP rejected image Variant returns.");
foreach (var imageInvalid in new[] { ("xpimage-constructor-error", "XPS2004"), ("xpimage-member-error", "XPS2009"), ("xpimage-readonly-error", "XPS2001") })
{
    var invalidSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", imageInvalid.Item1 + ".xps"));
    var invalidValidation = await CallAsync(new {jsonrpc="2.0",id="image-"+imageInvalid.Item1,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=invalidSource,filename=imageInvalid.Item1+".xps"}}});
    var invalidError = invalidValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
    if (invalidError.GetProperty("diagnosticCode").GetString() != imageInvalid.Item2 || invalidError.GetProperty("line").GetInt32() != 3)
        throw new Exception("MCP image diagnostic lost its code or source line: " + imageInvalid.Item1);
}
var implicitMemberSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "implicit-member-error.xps"));
var implicitMemberValidation = await CallAsync(new {jsonrpc="2.0",id=40,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=implicitMemberSource,filename="implicit-member-error.xps"}}});
var implicitMemberError = implicitMemberValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
if (implicitMemberError.GetProperty("line").GetInt32() != 2 || implicitMemberError.GetProperty("position").GetInt32() != 11 || implicitMemberError.GetProperty("description").GetString() is not { } implicitMessage || !implicitMessage.Contains("requires an active With scope", StringComparison.OrdinalIgnoreCase)) throw new Exception("AST MCP implicit-member diagnostics lost the source span or stable message.");
var goSubSource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "unsupported-gosub.xps"));
var goSubValidation = await CallAsync(new {jsonrpc="2.0",id=41,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=goSubSource,filename="unsupported-gosub.xps"}}});
var goSubError = goSubValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
if (goSubError.GetProperty("line").GetInt32() != 3 || goSubError.GetProperty("position").GetInt32() != 9 || goSubError.GetProperty("description").GetString() is not { } goSubMessage || !goSubMessage.Contains("GoSub is not implemented", StringComparison.OrdinalIgnoreCase)) throw new Exception("AST MCP GoSub diagnostics lost the source span or stable message.");
var staticArraySource = await File.ReadAllTextAsync(Path.Combine(repo, "tests", "ast-compile-probe", "static-array.xps"));
var staticArrayValidation = await CallAsync(new {jsonrpc="2.0",id=42,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=staticArraySource,filename="static-array.xps"}}});
if (staticArrayValidation.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("AST MCP rejected the Static array lifecycle fixture.");
var paritySource = "Sub Main()\n    Print 7\nEnd Sub";
var legacyParity = await CallAsync(new {jsonrpc="2.0",id=43,method="tools/call",@params=new{name="xpscript_validate",arguments=new{source=paritySource,filename="parity.xps"}}});
var astParity = await CallAsync(new {jsonrpc="2.0",id=44,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=paritySource,filename="parity.xps"}}});
if (legacyParity.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok" || astParity.GetProperty("result").GetProperty("structuredContent").GetProperty("result").GetString() != "ok") throw new Exception("CLI and AST MCP parity fixture did not validate through both compiler backends.");
var astDiagnostic=await CallAsync(new {jsonrpc="2.0",id=27,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source="Sub Main(\n    Print 1\nEnd Sub",filename="ast-error.xps"}}});
var astDiagnosticResult=astDiagnostic.GetProperty("result").GetProperty("structuredContent");
var astErrors=astDiagnosticResult.GetProperty("errors");
if (astDiagnosticResult.GetProperty("result").GetString()!="error" || astErrors.GetArrayLength()==0 || astErrors[0].GetProperty("file").GetString()!="ast-error.xps" || string.IsNullOrWhiteSpace(astErrors[0].GetProperty("diagnosticCode").GetString())) throw new Exception("AST MCP diagnostics lost the stable code or source filename.");
var astDiagnosticDebug=await CallAsync(new {jsonrpc="2.0",id=28,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source="Sub Main(\n    Print 1\nEnd Sub",filename="ast-error.xps",debug=true}}});
var astDebugError=astDiagnosticDebug.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
if (astDebugError.GetProperty("diagnosticCode").GetString()!=astErrors[0].GetProperty("diagnosticCode").GetString() || astDebugError.GetProperty("line").GetInt32()!=astErrors[0].GetProperty("line").GetInt32() || astDebugError.GetProperty("position").GetInt32()!=astErrors[0].GetProperty("position").GetInt32()) throw new Exception("AST MCP debug mode changed source diagnostic identity.");
var astSemanticDiagnostic=await CallAsync(new {jsonrpc="2.0",id=29,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source="Sub Main()\n    Print missingValue\nEnd Sub",filename="ast-semantic-error.xps"}}});
var astSemanticError=astSemanticDiagnostic.GetProperty("result").GetProperty("structuredContent").GetProperty("errors")[0];
if (astSemanticError.GetProperty("diagnosticCode").GetString()!="XPS2008" || astSemanticError.GetProperty("category").GetString()!="semantic" || astSemanticError.GetProperty("file").GetString()!="ast-semantic-error.xps" || astSemanticError.GetProperty("line").GetInt32()!=2 || astSemanticError.GetProperty("position").GetInt32()!=11 || astSemanticError.GetProperty("endColumn").GetInt32()<=astSemanticError.GetProperty("position").GetInt32()) throw new Exception("AST MCP semantic diagnostics lost source mapping.");
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
var nonzeroRedimSource=await File.ReadAllTextAsync(Path.Combine(repo,"tests","ast-compile-probe","redim-nonzero-error.xps"));
var nonzeroRedimValidation=await CallAsync(new {jsonrpc="2.0",id=43,method="tools/call",@params=new{name="xpscript_ast_validate",arguments=new{source=nonzeroRedimSource,filename="redim-nonzero-error.xps"}}});
var nonzeroRedimResult=nonzeroRedimValidation.GetProperty("result");
if (nonzeroRedimResult.GetProperty("isError").GetBoolean() || nonzeroRedimResult.GetProperty("structuredContent").GetProperty("result").GetString() != "ok")
    throw new Exception("AST MCP rejected implemented dynamic non-zero ReDim bounds.");
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
