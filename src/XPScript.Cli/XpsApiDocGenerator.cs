using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class XpsApiDocGenerator
{
    private static readonly Regex PrefixPattern = new(@"^\s*\[RoutePrefix:(.+)\]\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex RoutePattern = new(@"^\s*\[(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS):([^\]]+)\]\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex ProcedurePattern = new(@"^\s*(?:Public\s+|Private\s+)?(Sub|Function)\s+([A-Za-z_]\w*)\s*\((.*)\)\s*(?:As\s+([A-Za-z_]\w*(?:\(\))?))?", RegexOptions.IgnoreCase);
    private static readonly Regex ParamPattern = new(@"^(?:(?:ByVal|ByRef)\s+)?(?:\[(?:FromRoute|FromQuery|FromBody|FromHeader)(?::[^\]]+)?\]\s*)?([A-Za-z_]\w*)\s*(?:As\s+([A-Za-z_]\w*(?:\(\))?))?", RegexOptions.IgnoreCase);

    public static void Generate(string root)
    {
        var endpoints = new List<Endpoint>();
        foreach (var file in Directory.EnumerateFiles(root, "*.xps", SearchOption.AllDirectories)
                     .Where(x => !x.Contains(Path.DirectorySeparatorChar + "apidoc" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
            ParseFile(root, file, endpoints);

        var output = Path.Combine(root, "apidoc");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "openapi.json"), BuildOpenApi(endpoints), Encoding.UTF8);
        File.WriteAllText(Path.Combine(output, "swagger.json"), BuildSwagger(endpoints), Encoding.UTF8);
        File.WriteAllText(Path.Combine(output, "index.html"), BuildHtml(endpoints), Encoding.UTF8);
        File.WriteAllText(Path.Combine(output, "apidoc.css"), Css, Encoding.UTF8);
    }

    private static void ParseFile(string root, string file, List<Endpoint> endpoints)
    {
        var lines = File.ReadAllLines(file);
        var prefix = string.Empty;
        var docs = new List<string>();
        string? method = null;
        string? route = null;
        var tag = ToTitle(Path.GetFileNameWithoutExtension(file));

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            var pm = PrefixPattern.Match(raw);
            if (pm.Success) { prefix = Normalize(pm.Groups[1].Value); continue; }

            if (trimmed.StartsWith("'''", StringComparison.Ordinal))
            {
                docs.Add(trimmed[3..].Trim());
                continue;
            }

            var rm = RoutePattern.Match(raw);
            if (rm.Success)
            {
                method = rm.Groups[1].Value.ToUpperInvariant();
                route = Normalize(rm.Groups[2].Value);
                continue;
            }

            if (method is null || route is null) continue;
            var proc = ProcedurePattern.Match(raw);
            if (!proc.Success)
            {
                if (trimmed.Length > 0 && !trimmed.StartsWith("[", StringComparison.Ordinal) && !trimmed.StartsWith("'", StringComparison.Ordinal))
                { method = null; route = null; docs.Clear(); }
                continue;
            }

            var parsedDocs = ParseDocs(docs, tag);
            var parameters = ParseParameters(proc.Groups[3].Value, route, parsedDocs.ParamDocs);
            var returnType = string.IsNullOrWhiteSpace(proc.Groups[4].Value) ? null : proc.Groups[4].Value;
            endpoints.Add(new Endpoint(method, Combine(prefix, route), proc.Groups[2].Value, parsedDocs.Summary, parsedDocs.Description,
                parsedDocs.Tag, parameters, parsedDocs.Responses, returnType, Path.GetRelativePath(root, file)));
            method = null; route = null; docs.Clear();
        }
    }

    private static ParsedDocs ParseDocs(List<string> lines, string fallbackTag)
    {
        var prose = new List<string>();
        var paramDocs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var responses = new List<ResponseDoc>();
        var tag = fallbackTag;
        foreach (var line in lines)
        {
            if (line.StartsWith("@param ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line[7..].Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0) paramDocs[parts[0]] = parts.Length > 1 ? parts[1] : string.Empty;
            }
            else if (line.StartsWith("@response ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line[10..].Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && int.TryParse(parts[0], out var code)) responses.Add(new ResponseDoc(code, parts.Length > 1 ? parts[1] : string.Empty));
            }
            else if (line.StartsWith("@tag ", StringComparison.OrdinalIgnoreCase)) tag = line[5..].Trim();
            else if (!line.StartsWith("@", StringComparison.Ordinal)) prose.Add(line);
        }
        var nonEmpty = prose.Where(x => x.Length > 0).ToArray();
        return new ParsedDocs(nonEmpty.FirstOrDefault() ?? string.Empty, string.Join(" ", nonEmpty.Skip(1)), tag, paramDocs, responses);
    }

    private static List<Parameter> ParseParameters(string raw, string route, IReadOnlyDictionary<string, string> docs)
    {
        var result = new List<Parameter>();
        foreach (var part in SplitParams(raw))
        {
            var match = ParamPattern.Match(part.Trim());
            if (!match.Success) continue;
            var name = match.Groups[1].Value;
            var type = string.IsNullOrWhiteSpace(match.Groups[2].Value) ? "Variant" : match.Groups[2].Value;
            var location = route.Contains("{" + name + "}", StringComparison.OrdinalIgnoreCase) ? "path" : IsComplex(type) ? "body" : "query";
            result.Add(new Parameter(name, type, location, location == "path", docs.TryGetValue(name, out var d) ? d : string.Empty));
        }
        return result;
    }

    private static IEnumerable<string> SplitParams(string raw)
    {
        var depth = 0; var start = 0;
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '(') depth++;
            else if (raw[i] == ')') depth--;
            else if (raw[i] == ',' && depth == 0) { yield return raw[start..i]; start = i + 1; }
        }
        if (start < raw.Length) yield return raw[start..];
    }

    private static string BuildOpenApi(List<Endpoint> endpoints)
    {
        var paths = new Dictionary<string, object>();
        foreach (var group in endpoints.GroupBy(e => e.Route))
        {
            var methods = new Dictionary<string, object>();
            foreach (var e in group)
            {
                methods[e.Method.ToLowerInvariant()] = new
                {
                    tags = new[] { e.Tag }, summary = e.Summary, description = e.Description,
                    operationId = e.Name,
                    parameters = e.Parameters.Where(p => p.Location != "body").Select(p => new { name = p.Name, @in = p.Location, required = p.Required, description = p.Description, schema = Schema(p.Type) }).ToArray(),
                    requestBody = e.Parameters.FirstOrDefault(p => p.Location == "body") is { } body ? new { required = true, content = new Dictionary<string, object> { ["application/json"] = new { schema = Schema(body.Type) } } } : null,
                    responses = Responses31(e)
                };
            }
            paths[group.Key] = methods;
        }
        return JsonSerializer.Serialize(new { openapi = "3.1.0", info = new { title = "XPscript REST API", version = "1.0.0" }, paths }, JsonIndented);
    }

    private static string BuildSwagger(List<Endpoint> endpoints)
    {
        var paths = new Dictionary<string, object>();
        foreach (var group in endpoints.GroupBy(e => e.Route))
        {
            var methods = new Dictionary<string, object>();
            foreach (var e in group)
            {
                var parameters = e.Parameters.Select(p => p.Location == "body"
                    ? (object)new { name = p.Name, @in = "body", required = true, description = p.Description, schema = Schema20(p.Type) }
                    : new { name = p.Name, @in = p.Location, required = p.Required, description = p.Description, type = PrimitiveType(p.Type) }).ToArray();
                methods[e.Method.ToLowerInvariant()] = new { tags = new[] { e.Tag }, summary = e.Summary, description = e.Description, operationId = e.Name, produces = new[] { "application/json" }, consumes = new[] { "application/json" }, parameters, responses = Responses20(e) };
            }
            paths[group.Key] = methods;
        }
        return JsonSerializer.Serialize(new { swagger = "2.0", info = new { title = "XPscript REST API", version = "1.0.0" }, paths }, JsonIndented);
    }

    private static Dictionary<string, object> Responses31(Endpoint e)
    {
        var docs = e.Responses.Count > 0 ? e.Responses : new List<ResponseDoc> { new(200, "Successful response") };
        return docs.ToDictionary(x => x.Code.ToString(), x => (object)new { description = x.Description, content = e.ReturnType is null ? null : new Dictionary<string, object> { ["application/json"] = new { schema = Schema(e.ReturnType) } } });
    }

    private static Dictionary<string, object> Responses20(Endpoint e)
    {
        var docs = e.Responses.Count > 0 ? e.Responses : new List<ResponseDoc> { new(200, "Successful response") };
        return docs.ToDictionary(x => x.Code.ToString(), x => (object)new { description = x.Description, schema = e.ReturnType is null ? null : Schema20(e.ReturnType) });
    }

    private static object Schema(string type) => type.EndsWith("()", StringComparison.Ordinal)
        ? new { type = "array", items = Schema(type[..^2]) }
        : IsComplex(type) ? new Dictionary<string, object> { ["$ref"] = "#/components/schemas/" + type } : new { type = PrimitiveType(type) };
    private static object Schema20(string type) => type.EndsWith("()", StringComparison.Ordinal)
        ? new { type = "array", items = Schema20(type[..^2]) }
        : IsComplex(type) ? new Dictionary<string, object> { ["$ref"] = "#/definitions/" + type } : new { type = PrimitiveType(type) };
    private static bool IsComplex(string type) => PrimitiveType(type) == "object" && !type.Equals("Variant", StringComparison.OrdinalIgnoreCase) && !type.Equals("Object", StringComparison.OrdinalIgnoreCase);
    private static string PrimitiveType(string type) => type.TrimEnd('(', ')').ToLowerInvariant() switch { "string" => "string", "integer" or "long" or "byte" => "integer", "single" or "double" or "currency" => "number", "boolean" => "boolean", _ => "object" };

    private static string BuildHtml(List<Endpoint> endpoints)
    {
        var nav = string.Join("", endpoints.GroupBy(e => e.Tag).OrderBy(g => g.Key).Select(g => $"<section><h3>{H(g.Key)}</h3>" + string.Join("", g.Select(e => $"<a href=\"#{Slug(e)}\"><span class=\"method {e.Method.ToLowerInvariant()}\">{H(e.Method)}</span><span>{H(e.Route)}</span></a>")) + "</section>"));
        var cards = string.Join("", endpoints.Select(e => $"<article id=\"{Slug(e)}\"><div class=\"eyebrow\">{H(e.Tag)}</div><h2>{H(e.Summary.Length > 0 ? e.Summary : e.Name)}</h2><div class=\"route\"><span class=\"method {e.Method.ToLowerInvariant()}\">{H(e.Method)}</span><code>{H(e.Route)}</code></div>{(e.Description.Length > 0 ? $"<p class=\"lead\">{H(e.Description)}</p>" : "")}<h3>Parameters</h3>{ParameterTable(e)}<h3>Responses</h3>{ResponseTable(e)}<div class=\"source\">Source: {H(e.Source)}</div></article>"));
        return $"<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>API documentation</title><link rel=\"stylesheet\" href=\"apidoc.css\"></head><body><aside><div class=\"brand\"><strong>API documentation</strong><span>XPscript REST API</span></div><input id=\"search\" type=\"search\" placeholder=\"Filter endpoints\" aria-label=\"Filter endpoints\"><nav>{nav}</nav><footer><a href=\"openapi.json\">OpenAPI 3.1</a><a href=\"swagger.json\">Swagger 2.0</a></footer></aside><main><header><div><div class=\"eyebrow\">REFERENCE</div><h1>REST API</h1><p>Generated from XPscript routes and documentation blocks.</p></div><div class=\"specs\"><a href=\"openapi.json\">OpenAPI 3.1</a><a href=\"swagger.json\">Swagger 2.0</a></div></header>{cards}</main><script>const q=document.querySelector('#search');q.addEventListener('input',()=>{{const v=q.value.toLowerCase();document.querySelectorAll('nav a').forEach(a=>a.hidden=!a.textContent.toLowerCase().includes(v));}});</script></body></html>";
    }

    private static string ParameterTable(Endpoint e) => e.Parameters.Count == 0 ? "<p class=\"muted\">No parameters.</p>" : "<div class=\"table\"><div class=\"tr th\"><span>Name</span><span>Location</span><span>Type</span><span>Description</span></div>" + string.Join("", e.Parameters.Select(p => $"<div class=\"tr\"><code>{H(p.Name)}</code><span>{H(p.Location)}{(p.Required ? " · required" : "")}</span><code>{H(p.Type)}</code><span>{H(p.Description)}</span></div>")) + "</div>";
    private static string ResponseTable(Endpoint e) { var r = e.Responses.Count > 0 ? e.Responses : new List<ResponseDoc> { new(200, "Successful response") }; return "<div class=\"responses\">" + string.Join("", r.Select(x => $"<div><code>{x.Code}</code><span>{H(x.Description)}</span>{(e.ReturnType is null ? "" : $"<code>{H(e.ReturnType)}</code>")}</div>")) + "</div>"; }
    private static string Slug(Endpoint e) => Regex.Replace((e.Method + "-" + e.Route).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
    private static string H(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
    private static string Normalize(string route) { route = route.Trim(); if (!route.StartsWith('/')) route = "/" + route; return route.Length > 1 ? route.TrimEnd('/') : route; }
    private static string Combine(string prefix, string route) => string.IsNullOrEmpty(prefix) ? route : Normalize(prefix + "/" + route.TrimStart('/'));
    private static string ToTitle(string value) => value.Length == 0 ? "General" : char.ToUpperInvariant(value[0]) + value[1..];

    private static readonly JsonSerializerOptions JsonIndented = new() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
    private sealed record Endpoint(string Method, string Route, string Name, string Summary, string Description, string Tag, List<Parameter> Parameters, List<ResponseDoc> Responses, string? ReturnType, string Source);
    private sealed record Parameter(string Name, string Type, string Location, bool Required, string Description);
    private sealed record ResponseDoc(int Code, string Description);
    private sealed record ParsedDocs(string Summary, string Description, string Tag, Dictionary<string, string> ParamDocs, List<ResponseDoc> Responses);

    private const string Css = """
:root{font-family:Inter,ui-sans-serif,system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif;color:#172033;background:#f7f8fa;line-height:1.55}*{box-sizing:border-box}body{margin:0}aside{position:fixed;inset:0 auto 0 0;width:300px;background:#fff;border-right:1px solid #e6e9ef;padding:28px 20px;display:flex;flex-direction:column;overflow:auto}.brand{display:grid;gap:2px;margin:0 8px 24px}.brand strong{font-size:18px}.brand span,.muted,.source{color:#697386;font-size:13px}input{width:100%;border:1px solid #d9dee8;border-radius:9px;padding:10px 12px;font:inherit;outline:none}input:focus{border-color:#697386;box-shadow:0 0 0 3px #eef0f4}nav{margin-top:24px;flex:1}nav section{margin-bottom:24px}nav h3{margin:0 8px 8px;color:#697386;font-size:11px;text-transform:uppercase;letter-spacing:.08em}nav a{display:grid;grid-template-columns:54px 1fr;align-items:center;gap:8px;padding:8px;border-radius:8px;color:#364152;text-decoration:none;font-size:13px}nav a:hover{background:#f3f5f8}footer,.specs{display:flex;gap:14px}footer{padding:16px 8px 0;border-top:1px solid #eef0f4}footer a,.specs a{color:#405cf5;text-decoration:none;font-size:13px}main{margin-left:300px;max-width:1100px;padding:56px 72px 120px}header{display:flex;justify-content:space-between;gap:32px;align-items:flex-start;margin-bottom:56px}h1{font-size:36px;letter-spacing:-.03em;margin:4px 0 8px}h2{font-size:26px;letter-spacing:-.02em;margin:4px 0 18px}h3{font-size:15px;margin:28px 0 12px}.eyebrow{font-size:11px;font-weight:700;letter-spacing:.1em;color:#697386}.lead{max-width:760px;color:#4b5565}.route{display:flex;align-items:center;gap:12px;padding:13px 15px;background:#f7f8fa;border:1px solid #e6e9ef;border-radius:9px}.method{font-size:10px;font-weight:800;letter-spacing:.04em}.get{color:#067647}.post{color:#175cd3}.put,.patch{color:#b54708}.delete{color:#b42318}article{background:#fff;border:1px solid #e6e9ef;border-radius:14px;padding:32px;margin-bottom:28px;box-shadow:0 1px 2px rgba(16,24,40,.03)}code{font-family:"SFMono-Regular",Consolas,monospace;font-size:12px}.table{border:1px solid #e6e9ef;border-radius:9px;overflow:hidden}.tr{display:grid;grid-template-columns:1fr 1fr 1fr 2fr;gap:12px;padding:11px 14px;border-top:1px solid #eef0f4;font-size:13px}.tr:first-child{border-top:0}.th{background:#fafbfc;color:#697386;font-size:11px;font-weight:700}.responses{border-top:1px solid #e6e9ef}.responses>div{display:grid;grid-template-columns:60px 1fr 160px;gap:16px;padding:12px 0;border-bottom:1px solid #eef0f4;font-size:13px}.source{margin-top:28px}@media(max-width:820px){aside{position:static;width:auto;height:auto;border-right:0;border-bottom:1px solid #e6e9ef}main{margin:0;padding:32px 20px 80px}header{display:block}.specs{margin-top:16px}.tr{grid-template-columns:1fr 1fr}.responses>div{grid-template-columns:50px 1fr}nav{max-height:320px;overflow:auto}}
""";
}
