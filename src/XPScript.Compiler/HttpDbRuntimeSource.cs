namespace XPScript.Compiler;

internal static class HttpDbRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptHttpDbSupabase
{
    private readonly XPScriptHttpClient _http = new();
    private readonly string _baseUrl;
    private readonly string _restUrl;
    private string _apiKey;
    private string _bearerToken = "";
    private string _schema = "public";
    private string _sqlEndpoint = "";
    private string _sqlToken = "";

    public XPScriptHttpDbSupabase(object? baseUrl, object? apiKey)
    {
        _baseUrl = NormalizeBaseUrl(baseUrl);
        _restUrl = _baseUrl.EndsWith("/rest/v1", StringComparison.OrdinalIgnoreCase)
            ? _baseUrl
            : _baseUrl + "/rest/v1";
        _apiKey = RequiredText(apiKey, "Supabase API key");
    }

    public double Timeout { get => _http.Timeout; set => _http.Timeout = value; }
    public string BaseUrl => _baseUrl;
    public string Schema => _schema;

    public void SetApiKey(object? apiKey) => _apiKey = RequiredText(apiKey, "Supabase API key");
    public void SetBearerToken(object? token) => _bearerToken = XPScriptRuntime.CStr(token).Trim();

    public void SetSchema(object? schema)
    {
        var value = RequiredIdentifier(schema, "Supabase schema");
        _schema = value;
    }

    public void ConfigureCloudManagement(object? projectRef, object? accessToken)
    {
        var project = RequiredSimpleSegment(projectRef, "Supabase project ref");
        _sqlEndpoint = "https://api.supabase.com/v1/projects/" + Uri.EscapeDataString(project) + "/database/query";
        _sqlToken = RequiredText(accessToken, "Supabase management access token");
    }

    public void ConfigureSqlEndpoint(object? endpoint, object? adminToken)
    {
        _sqlEndpoint = NormalizeAbsoluteUrl(endpoint, "Supabase SQL endpoint");
        _sqlToken = RequiredText(adminToken, "Supabase SQL admin token");
    }

    public XPScriptJsonDocument Select(object? table) => Select(table, "select=*");
    public XPScriptJsonDocument Select(object? table, object? query)
    {
        var response = SendData("GET", table, query, null, "");
        return ParseRequiredJson(response, "Supabase SELECT");
    }

    public XPScriptJsonDocument Insert(object? table, object? data)
    {
        var response = SendData("POST", table, null, data, "return=representation");
        return ParseRequiredJson(response, "Supabase INSERT");
    }

    public XPScriptJsonDocument Upsert(object? table, object? data)
    {
        var response = SendData("POST", table, null, data, "resolution=merge-duplicates,return=representation");
        return ParseRequiredJson(response, "Supabase UPSERT");
    }

    public XPScriptJsonDocument Update(object? table, object? filter, object? data)
    {
        var filterText = RequiredFilter(filter);
        var response = SendData("PATCH", table, filterText, data, "return=representation");
        return ParseRequiredJson(response, "Supabase UPDATE");
    }

    public XPScriptJsonDocument Delete(object? table, object? filter)
    {
        var filterText = RequiredFilter(filter);
        var response = SendData("DELETE", table, filterText, null, "return=representation");
        return ParseRequiredJson(response, "Supabase DELETE");
    }

    public XPScriptJsonDocument Rpc(object? functionName, object? args)
    {
        var name = RequiredSimpleSegment(functionName, "Supabase RPC function");
        ConfigureDataHeaders("return=representation", contentType: true);
        var response = _http.Post(_restUrl + "/rpc/" + Uri.EscapeDataString(name), XPScriptNativeJson.Stringify(args));
        EnsureSuccess(response, "Supabase RPC");
        return ParseJsonOrEmpty(response);
    }

    public XPScriptJsonDocument ExecuteSql(object? sql)
    {
        if (_sqlEndpoint.Length == 0)
            throw new XPScriptRuntimeException(5, "Supabase SQL endpoint is not configured. Use ConfigureCloudManagement or ConfigureSqlEndpoint first.");
        var query = RequiredText(sql, "SQL query");
        var body = new System.Text.Json.Nodes.JsonObject { ["query"] = query };
        _http.ClearHeaders();
        _http.SetHeader("Authorization", "Bearer " + _sqlToken);
        _http.SetHeader("Content-Type", "application/json; charset=utf-8");
        _http.SetHeader("Accept", "application/json");
        var response = _http.Post(_sqlEndpoint, body.ToJsonString());
        EnsureSuccess(response, "Supabase SQL");
        return ParseJsonOrEmpty(response);
    }

    public XPScriptJsonDocument CreateTable(object? sql) => ExecuteSql(sql);
    public XPScriptJsonDocument AlterTable(object? sql) => ExecuteSql(sql);
    public XPScriptJsonDocument CreateView(object? sql) => ExecuteSql(sql);
    public XPScriptJsonDocument AlterView(object? sql) => ExecuteSql(sql);

    public string Eq(object? column, object? value)
        => Uri.EscapeDataString(RequiredIdentifier(column, "Supabase filter column")) + "=eq." + Uri.EscapeDataString(XPScriptRuntime.CStr(value));

    private XPScriptHttpResponse SendData(string method, object? table, object? query, object? data, string prefer)
    {
        var tableName = RequiredSimpleSegment(table, "Supabase table or view");
        var url = _restUrl + "/" + Uri.EscapeDataString(tableName);
        var queryText = query is null ? "" : XPScriptRuntime.CStr(query).Trim().TrimStart('?');
        ValidateQuery(queryText);
        if (queryText.Length > 0) url += "?" + queryText;

        ConfigureDataHeaders(prefer, contentType: data is not null);
        XPScriptHttpResponse response = method switch
        {
            "GET" => _http.Get(url),
            "POST" => _http.Post(url, XPScriptNativeJson.Stringify(data)),
            "PATCH" => _http.Patch(url, XPScriptNativeJson.Stringify(data)),
            "DELETE" => _http.Delete(url),
            _ => throw new XPScriptRuntimeException(5, "Unsupported Supabase HTTP operation.")
        };
        EnsureSuccess(response, "Supabase " + method);
        return response;
    }

    private void ConfigureDataHeaders(string prefer, bool contentType)
    {
        _http.ClearHeaders();
        _http.SetHeader("apikey", _apiKey);
        if (_bearerToken.Length > 0) _http.SetHeader("Authorization", "Bearer " + _bearerToken);
        _http.SetHeader("Accept", "application/json");
        _http.SetHeader("Accept-Profile", _schema);
        if (contentType)
        {
            _http.SetHeader("Content-Type", "application/json; charset=utf-8");
            _http.SetHeader("Content-Profile", _schema);
        }
        if (prefer.Length > 0) _http.SetHeader("Prefer", prefer);
    }

    private static string RequiredFilter(object? filter)
    {
        var value = RequiredText(filter, "Supabase filter").TrimStart('?');
        ValidateQuery(value);
        return value;
    }

    private static void ValidateQuery(string value)
    {
        if (value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new XPScriptRuntimeException(5, "Supabase query/filter contains an invalid control character.");
    }

    private static string NormalizeBaseUrl(object? value)
    {
        var url = NormalizeAbsoluteUrl(value, "Supabase base URL").TrimEnd('/');
        return url;
    }

    private static string NormalizeAbsoluteUrl(object? value, string label)
    {
        var text = RequiredText(value, label).TrimEnd('/');
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new XPScriptRuntimeException(5, label + " must be an absolute http:// or https:// URL.");
        return text;
    }

    private static string RequiredText(object? value, string label)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length == 0) throw new XPScriptRuntimeException(5, label + " cannot be empty.");
        if (text.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new XPScriptRuntimeException(5, label + " contains an invalid control character.");
        return text;
    }

    private static string RequiredIdentifier(object? value, string label)
    {
        var text = RequiredText(value, label);
        if (!text.All(c => char.IsLetterOrDigit(c) || c == '_'))
            throw new XPScriptRuntimeException(5, label + " contains an invalid character.");
        return text;
    }

    private static string RequiredSimpleSegment(object? value, string label)
    {
        var text = RequiredText(value, label);
        if (text.Contains('/') || text.Contains('?') || text.Contains('#'))
            throw new XPScriptRuntimeException(5, label + " must be a single URL segment.");
        return text;
    }

    private static void EnsureSuccess(XPScriptHttpResponse response, string operation)
    {
        if (response.IsSuccess) return;
        throw new XPScriptRuntimeException(5, operation + " failed with HTTP status " + response.StatusCode + " " + response.StatusText + ".");
    }

    private static XPScriptJsonDocument ParseRequiredJson(XPScriptHttpResponse response, string operation)
    {
        if (string.IsNullOrWhiteSpace(response.Body))
            throw new XPScriptRuntimeException(5, operation + " returned an empty response where JSON was expected.");
        return XPScriptNativeJson.Parse(response.Body);
    }

    private static XPScriptJsonDocument ParseJsonOrEmpty(XPScriptHttpResponse response)
        => string.IsNullOrWhiteSpace(response.Body) ? XPScriptNativeJson.Parse("{}") : XPScriptNativeJson.Parse(response.Body);
}

internal sealed class XPScriptHttpDbDominoRest
{
    private readonly XPScriptHttpClient _http = new();
    private readonly string _serverBaseUrl;
    private readonly string _apiBaseUrl;
    private readonly string _setupBaseUrl;
    private string _bearerToken;
    private string _dataSource;

    public XPScriptHttpDbDominoRest(object? baseUrl, object? bearerToken, object? dataSource)
    {
        _serverBaseUrl = NormalizeServerBaseUrl(baseUrl);
        _apiBaseUrl = _serverBaseUrl + "/api/v1";
        _setupBaseUrl = _serverBaseUrl + "/api/setup-v1";
        _bearerToken = XPScriptRuntime.CStr(bearerToken).Trim();
        _dataSource = RequiredText(dataSource, "Domino dataSource");
    }

    public double Timeout { get => _http.Timeout; set => _http.Timeout = value; }
    public string BaseUrl => _serverBaseUrl;
    public string DataSource => _dataSource;
    public string BearerToken => _bearerToken;

    public void SetBearerToken(object? token) => _bearerToken = RequiredText(token, "Domino bearer token");
    public void SetDataSource(object? dataSource) => _dataSource = RequiredText(dataSource, "Domino dataSource");

    public string Login(object? username, object? password)
    {
        var loginBody = new System.Text.Json.Nodes.JsonObject
        {
            ["username"] = RequiredText(username, "Domino username"),
            ["password"] = RequiredText(password, "Domino password")
        };
        _http.ClearHeaders();
        _http.SetHeader("Content-Type", "application/json; charset=utf-8");
        _http.SetHeader("Accept", "application/json");
        var response = _http.Post(_apiBaseUrl + "/auth", loginBody.ToJsonString());
        EnsureSuccess(response, "Domino login");
        var document = XPScriptNativeJson.Parse(response.Body);
        var root = document.Root.AsObject();
        var bearer = root is null ? "" : XPScriptRuntime.CStr(root.Get("bearer"));
        if (string.IsNullOrWhiteSpace(bearer))
            throw new XPScriptRuntimeException(5, "Domino login response did not contain a bearer token.");
        _bearerToken = bearer.Trim();
        return _bearerToken;
    }

    public void Logout()
    {
        EnsureBearer();
        ConfigureHeaders(contentType: true);
        var body = new System.Text.Json.Nodes.JsonObject { ["logout"] = "Yes" };
        var response = _http.Post(_apiBaseUrl + "/auth/logout", body.ToJsonString());
        EnsureSuccess(response, "Domino logout");
        _bearerToken = "";
    }

    public XPScriptJsonDocument CreateDocument(object? data)
    {
        ConfigureHeaders(contentType: true);
        var response = _http.Post(DataUrl("/document"), XPScriptNativeJson.Stringify(data));
        EnsureSuccess(response, "Domino create document");
        return ParseJsonOrEmpty(response);
    }

    public XPScriptJsonDocument GetDocument(object? unid)
    {
        ConfigureHeaders(contentType: false);
        var response = _http.Get(DataUrl("/document/" + Uri.EscapeDataString(RequiredUnid(unid))));
        EnsureSuccess(response, "Domino get document");
        return ParseRequiredJson(response, "Domino get document");
    }

    public XPScriptJsonDocument UpdateDocument(object? unid, object? data)
    {
        ConfigureHeaders(contentType: true);
        var response = _http.Put(DataUrl("/document/" + Uri.EscapeDataString(RequiredUnid(unid))), XPScriptNativeJson.Stringify(data));
        EnsureSuccess(response, "Domino update document");
        return ParseJsonOrEmpty(response);
    }

    public XPScriptJsonDocument PatchDocument(object? unid, object? data)
    {
        ConfigureHeaders(contentType: true);
        var response = _http.Patch(DataUrl("/document/" + Uri.EscapeDataString(RequiredUnid(unid))), XPScriptNativeJson.Stringify(data));
        EnsureSuccess(response, "Domino patch document");
        return ParseJsonOrEmpty(response);
    }

    public bool DeleteDocument(object? unid)
    {
        ConfigureHeaders(contentType: false);
        var response = _http.Delete(DataUrl("/document/" + Uri.EscapeDataString(RequiredUnid(unid))));
        EnsureSuccess(response, "Domino delete document");
        return true;
    }

    public XPScriptJsonDocument ListViews()
    {
        ConfigureHeaders(contentType: false);
        var response = _http.Get(DataUrl("/lists"));
        EnsureSuccess(response, "Domino list views");
        return ParseRequiredJson(response, "Domino list views");
    }

    public XPScriptJsonDocument GetView(object? viewName)
        => GetView(viewName, "");

    public XPScriptJsonDocument GetView(object? viewName, object? query)
    {
        var name = RequiredText(viewName, "Domino view name");
        var url = DataUrl("/lists/" + Uri.EscapeDataString(name));
        var extra = XPScriptRuntime.CStr(query).Trim().TrimStart('?');
        ValidateQuery(extra);
        if (extra.Length > 0) url += "&" + extra;
        ConfigureHeaders(contentType: false);
        var response = _http.Get(url);
        EnsureSuccess(response, "Domino get view");
        return ParseRequiredJson(response, "Domino get view");
    }

    public XPScriptJsonDocument Query(object? queryText)
    {
        var body = new System.Text.Json.Nodes.JsonObject
        {
            ["query"] = RequiredText(queryText, "Domino query"),
            ["viewRefresh"] = true,
            ["noViews"] = false
        };
        return Query(body);
    }

    public XPScriptJsonDocument Query(object? queryPayload)
    {
        ConfigureHeaders(contentType: true);
        var url = DataUrl("/query") + "&action=execute";
        var response = _http.Post(url, XPScriptNativeJson.Stringify(queryPayload));
        EnsureSuccess(response, "Domino query");
        return ParseRequiredJson(response, "Domino query");
    }

    public XPScriptJsonDocument ListForms()
    {
        ConfigureHeaders(contentType: false);
        var response = _http.Get(_setupBaseUrl + "/design/forms?dataSource=" + Uri.EscapeDataString(_dataSource));
        EnsureSuccess(response, "Domino list forms");
        return ParseRequiredJson(response, "Domino list forms");
    }

    private string DataUrl(string path)
        => _apiBaseUrl + path + "?dataSource=" + Uri.EscapeDataString(_dataSource);

    private void ConfigureHeaders(bool contentType)
    {
        EnsureBearer();
        _http.ClearHeaders();
        _http.SetHeader("Authorization", "Bearer " + _bearerToken);
        _http.SetHeader("Accept", "application/json");
        if (contentType) _http.SetHeader("Content-Type", "application/json; charset=utf-8");
    }

    private void EnsureBearer()
    {
        if (string.IsNullOrWhiteSpace(_bearerToken))
            throw new XPScriptRuntimeException(5, "Domino REST API bearer token is not set. Call Login or SetBearerToken first.");
    }

    private static string NormalizeServerBaseUrl(object? value)
    {
        var text = RequiredText(value, "Domino REST API base URL").TrimEnd('/');
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new XPScriptRuntimeException(5, "Domino REST API base URL must be an absolute http:// or https:// URL.");
        if (text.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase)) text = text[..^7];
        if (text.EndsWith("/api/setup-v1", StringComparison.OrdinalIgnoreCase)) text = text[..^13];
        return text.TrimEnd('/');
    }

    private static string RequiredUnid(object? value)
    {
        var unid = RequiredText(value, "Domino UNID");
        if (unid.Length != 32 || !unid.All(Uri.IsHexDigit))
            throw new XPScriptRuntimeException(5, "Domino UNID must be exactly 32 hexadecimal characters.");
        return unid;
    }

    private static string RequiredText(object? value, string label)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length == 0) throw new XPScriptRuntimeException(5, label + " cannot be empty.");
        if (text.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new XPScriptRuntimeException(5, label + " contains an invalid control character.");
        return text;
    }

    private static void ValidateQuery(string value)
    {
        if (value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new XPScriptRuntimeException(5, "Domino query string contains an invalid control character.");
    }

    private static void EnsureSuccess(XPScriptHttpResponse response, string operation)
    {
        if (response.IsSuccess) return;
        throw new XPScriptRuntimeException(5, operation + " failed with HTTP status " + response.StatusCode + " " + response.StatusText + ".");
    }

    private static XPScriptJsonDocument ParseRequiredJson(XPScriptHttpResponse response, string operation)
    {
        if (string.IsNullOrWhiteSpace(response.Body))
            throw new XPScriptRuntimeException(5, operation + " returned an empty response where JSON was expected.");
        return XPScriptNativeJson.Parse(response.Body);
    }

    private static XPScriptJsonDocument ParseJsonOrEmpty(XPScriptHttpResponse response)
        => string.IsNullOrWhiteSpace(response.Body) ? XPScriptNativeJson.Parse("{}") : XPScriptNativeJson.Parse(response.Body);
}
""";
}
