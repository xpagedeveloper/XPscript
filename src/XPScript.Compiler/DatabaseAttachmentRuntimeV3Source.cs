namespace XPScript.Compiler;

internal static class DatabaseAttachmentRuntimeV3Source
{
    public const string Code = """
internal sealed class XPScriptAttachmentCollectionV3
{
    private readonly XPScriptAttachmentCollection _inner;

    internal XPScriptAttachmentCollectionV3(XPScriptAttachmentCollection inner)
    {
        _inner = inner;
    }

    public XPScriptJsonArray List() => Sanitize(_inner.List());
    public XPScriptJsonArray GetMetadata() => Sanitize(_inner.GetMetadata());
    public XPScriptJsonObject GetMetadata(object? attachmentId) => Sanitize(_inner.GetMetadata(attachmentId));
    public XPScriptJsonArray FindByName(object? originalName) => Sanitize(_inner.FindByName(originalName));

    public XPScriptJsonObject Save(object? sourcePath, object? createdBy)
    {
        _inner.SetActor(RequiredCreatedBy(createdBy));
        return Sanitize(_inner.Save(sourcePath));
    }

    public XPScriptJsonObject SaveAs(object? sourcePath, object? originalName, object? createdBy)
    {
        _inner.SetActor(RequiredCreatedBy(createdBy));
        return Sanitize(_inner.SaveAs(sourcePath, originalName));
    }

    public bool Get(object? attachmentId, object? targetPath)
        => SaveToDisk(attachmentId, targetPath);

    public bool SaveToDisk(object? attachmentId, object? targetPath)
    {
        if (OperatingSystem.IsBrowser())
            throw new XPScriptRuntimeException(5, "SaveToDisk is not available in browser-wasm. Use SendToBrowser instead.");

        var id = XPScriptAttachmentRuntimeHelpers.RequiredAttachmentId(attachmentId);
        var bytes = ReadBytes(id);
        if (TryGetWebContext(out var webContext))
        {
            var fullPath = ResolvePrivateWebExportPath(webContext!, targetPath);
            XPScriptAttachmentFileRuntime.WriteAllBytes(fullPath, bytes);
            return true;
        }

        XPScriptAttachmentFileRuntime.WriteAllBytes(targetPath, bytes);
        return true;
    }

    public XPScriptJsonArray GetAll(object? targetFolder)
    {
        if (OperatingSystem.IsBrowser())
            throw new XPScriptRuntimeException(5, "GetAll disk export is not available in browser-wasm. Use SendToBrowser for individual attachments.");

        var result = new System.Text.Json.Nodes.JsonArray();
        var metadata = GetMetadata();
        foreach (var node in metadata.Node)
        {
            if (node is not System.Text.Json.Nodes.JsonObject item) continue;
            var id = XPScriptAttachmentRuntimeHelpers.RequiredAttachmentId(item["attachmentId"]?.GetValue<string>());
            var name = XPScriptAttachmentFileRuntime.RequiredAttachmentName(item["originalName"]?.GetValue<string>());
            var relativeName = id + "_" + name;
            string localPath;
            string exposedPath;
            if (TryGetWebContext(out var webContext))
            {
                var requestedFolder = NormalizePrivateRelativePath(targetFolder, allowFileName: false);
                var relativePath = requestedFolder + "/" + relativeName;
                localPath = ResolvePrivateWebExportPath(webContext!, relativePath);
                exposedPath = relativePath;
            }
            else
            {
                var folder = XPScriptAttachmentFileRuntime.RequiredTargetFolder(targetFolder);
                localPath = Path.Combine(folder, relativeName);
                exposedPath = localPath;
            }
            XPScriptAttachmentFileRuntime.WriteAllBytes(localPath, ReadBytes(id));
            var copy = (System.Text.Json.Nodes.JsonObject)item.DeepClone();
            copy["localPath"] = exposedPath;
            result.Add(copy);
        }
        return new XPScriptJsonArray(result);
    }

    public bool SendToBrowser(object? attachmentId)
        => SendToBrowser(attachmentId, null);

    public bool SendToBrowser(object? attachmentId, object? downloadName)
    {
        var id = XPScriptAttachmentRuntimeHelpers.RequiredAttachmentId(attachmentId);
        var metadata = GetMetadata(id);
        var originalName = XPScriptAttachmentFileRuntime.RequiredAttachmentName(metadata.Get("originalName"));
        var requestedName = downloadName is null || string.IsNullOrWhiteSpace(XPScriptRuntime.CStr(downloadName))
            ? originalName
            : XPScriptAttachmentFileRuntime.RequiredAttachmentName(downloadName);
        var contentType = XPScriptRuntime.CStr(metadata.Get("contentType")).Trim();
        if (contentType.Length == 0) contentType = "application/octet-stream";
        var bytes = ReadBytes(id);

        if (OperatingSystem.IsBrowser())
        {
            var browserHost = ResolveType("XPScript.UI.Browser.BrowserFormHost", "XPScript.UI.Browser");
            var method = browserHost?.GetMethod("DownloadFile", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method is null)
                throw new XPScriptRuntimeException(5, "Browser attachment download backend is unavailable.");
            method.Invoke(null, [Convert.ToBase64String(bytes), requestedName, contentType]);
            return true;
        }

        if (!TryGetWebContext(out var context))
            throw new XPScriptRuntimeException(5, "SendToBrowser requires an active web request or browser-wasm runtime.");

        var response = context!.GetType().GetProperty("Response")?.GetValue(context)
            ?? throw new XPScriptRuntimeException(5, "Active web response is unavailable.");
        var sendFile = response.GetType().GetMethod("SendFile", [typeof(byte[]), typeof(string), typeof(string), typeof(bool)]);
        if (sendFile is null)
            throw new XPScriptRuntimeException(5, "Web response file streaming backend is unavailable.");
        sendFile.Invoke(response, [bytes, requestedName, contentType, false]);
        return true;
    }

    public bool Delete(object? attachmentId)
        => _inner.Delete(attachmentId);

    private byte[] ReadBytes(string attachmentId)
    {
        var field = typeof(XPScriptAttachmentCollection).GetField("_get", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new XPScriptRuntimeException(5, "Attachment binary backend is unavailable.");
        if (field.GetValue(_inner) is not Func<string, byte[]> getter)
            throw new XPScriptRuntimeException(5, "Attachment binary backend is invalid.");
        return getter(attachmentId);
    }

    private static string RequiredCreatedBy(object? value)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length is < 1 or > 512 || text.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw new XPScriptRuntimeException(5, "Attachment createdBy must contain 1 to 512 characters without line breaks.");
        return text;
    }

    private static XPScriptJsonObject Sanitize(XPScriptJsonObject value)
    {
        var node = (System.Text.Json.Nodes.JsonObject)value.Node.DeepClone();
        node.Remove("modified");
        node.Remove("modifiedBy");
        return new XPScriptJsonObject(node);
    }

    private static XPScriptJsonArray Sanitize(XPScriptJsonArray value)
    {
        var result = new System.Text.Json.Nodes.JsonArray();
        foreach (var node in value.Node)
        {
            if (node is System.Text.Json.Nodes.JsonObject item)
            {
                var copy = (System.Text.Json.Nodes.JsonObject)item.DeepClone();
                copy.Remove("modified");
                copy.Remove("modifiedBy");
                result.Add(copy);
            }
        }
        return new XPScriptJsonArray(result);
    }

    private static bool TryGetWebContext(out object? context)
    {
        context = null;
        var accessor = ResolveType("XPScript.Web.Runtime.XpsWebContextAccessor", "XPScript.Web.Runtime");
        if (accessor is null) return false;
        try
        {
            context = accessor.GetProperty("Current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
            return context is not null;
        }
        catch (System.Reflection.TargetInvocationException)
        {
            return false;
        }
    }

    private static string ResolvePrivateWebExportPath(object context, object? targetPath)
    {
        var server = context.GetType().GetProperty("Server")?.GetValue(context)
            ?? throw new XPScriptRuntimeException(5, "Web server information is unavailable.");
        var root = Convert.ToString(server.GetType().GetProperty("RootPath")?.GetValue(server), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        if (root.Length == 0) throw new XPScriptRuntimeException(5, "Web root information is unavailable.");

        var requested = NormalizePrivateRelativePath(targetPath, allowFileName: true);
        var siteHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(root))))[..24].ToLowerInvariant();
        var sandbox = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "xpscript-private-attachments", siteHash));
        Directory.CreateDirectory(sandbox);
        EnsureNoReparsePoint(sandbox);

        var full = Path.GetFullPath(Path.Combine(sandbox, requested.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = sandbox.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(5, "Web attachment export escaped the private sandbox.");

        var directory = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(directory);
        EnsureSandboxPathHasNoReparsePoints(sandbox, directory);
        if (File.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new XPScriptRuntimeException(5, "Web attachment export refuses symbolic-link or reparse-point file targets.");
        return full;
    }

    private static string NormalizePrivateRelativePath(object? value, bool allowFileName)
    {
        var requested = XPScriptRuntime.CStr(value).Trim().Replace('\\', '/');
        if (requested.Length == 0 || requested.Length > 1024 || Path.IsPathRooted(requested) || requested.StartsWith('/', StringComparison.Ordinal))
            throw new XPScriptRuntimeException(5, "Web attachment export path must be a relative private path.");
        var parts = requested.Split('/', StringSplitOptions.None);
        if (parts.Any(part => part.Length == 0 || part == "." || part == ".." || part.Any(char.IsControl)))
            throw new XPScriptRuntimeException(5, "Web attachment export path contains an invalid segment.");
        if (!allowFileName && parts.Length > 32)
            throw new XPScriptRuntimeException(5, "Web attachment export folder is too deeply nested.");
        if (parts.Length > 32)
            throw new XPScriptRuntimeException(5, "Web attachment export path is too deeply nested.");
        return string.Join('/', parts);
    }

    private static void EnsureSandboxPathHasNoReparsePoints(string sandbox, string directory)
    {
        EnsureNoReparsePoint(sandbox);
        var relative = Path.GetRelativePath(sandbox, directory);
        if (relative == ".") return;
        var current = sandbox;
        foreach (var part in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            EnsureNoReparsePoint(current);
        }
    }

    private static void EnsureNoReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new XPScriptRuntimeException(5, "Web attachment export refuses symbolic-link or reparse-point paths.");
    }

    private static Type? ResolveType(string typeName, string assemblyName)
    {
        var direct = Type.GetType(typeName + ", " + assemblyName, throwOnError: false, ignoreCase: false);
        if (direct is not null) return direct;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var candidate = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
            if (candidate is not null) return candidate;
        }
        return null;
    }
}

internal static class XPScriptDatabaseAttachmentApi
{
    public static XPScriptAttachmentCollectionV3 ForSqlite(XPScriptDbSqlite db, object? table, object? keyColumn, object? keyValue)
        => new(XPScriptDatabaseAttachmentRuntime.ForSqlite(db, table, keyColumn, keyValue));

    public static XPScriptAttachmentCollectionV3 ForMsSql(XPScriptDbMsSql db, object? table, object? keyColumn, object? keyValue)
        => new(XPScriptDatabaseAttachmentRuntime.ForMsSql(db, table, keyColumn, keyValue));

    public static XPScriptAttachmentCollectionV3 ForSupabase(XPScriptHttpDbSupabase db, object? table, object? keyColumn, object? keyValue)
        => new(XPScriptDatabaseAttachmentRuntime.ForSupabase(db, table, keyColumn, keyValue));

    public static XPScriptAttachmentCollectionV3 ForDomino(XPScriptHttpDbDominoRest db, object? unid)
        => new(XPScriptDatabaseAttachmentRuntime.ForDomino(db, unid));

    public static XPScriptAttachmentCollectionV3 ForDomino(XPScriptHttpDbDominoRest db, object? unid, object? fieldName)
        => new(XPScriptDatabaseAttachmentRuntime.ForDomino(db, unid, fieldName));

    public static void SetSupabaseBucket(XPScriptHttpDbSupabase db, object? bucket)
        => XPScriptDatabaseAttachmentRuntime.SetSupabaseBucket(db, bucket);
}
""";
}
