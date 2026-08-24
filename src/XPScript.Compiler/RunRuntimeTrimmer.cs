namespace XPScript.Compiler;

internal static class RunRuntimeTrimmer
{
    public static string Trim(string generatedSource)
    {
        ArgumentNullException.ThrowIfNull(generatedSource);

        var runtimeStart = generatedSource.IndexOf(CoreControlRuntimeSource.Code, StringComparison.Ordinal);
        if (runtimeStart < 0) return generatedSource;

        var script = generatedSource[..runtimeStart];

        var usesEvaluate = ContainsAny(script,
            "XPScriptEvaluateRuntime.",
            "XPScriptEvaluateArgumentRuntime.",
            "Evaluate(",
            "EvaluateByVal(");

        var usesJson = ContainsAny(script,
            "XPScriptJson",
            "JsonDocument",
            "JsonNode",
            "JsonSerializer",
            "ParseJson",
            "ToJson",
            "FromJson");

        var usesHttp = ContainsAny(script,
            "XPScriptHttp",
            "HttpClient",
            "HttpGet",
            "HttpPost",
            "HttpPut",
            "HttpDelete",
            "HttpPatch",
            "HttpRequest");

        var usesHttpDb = ContainsAny(script,
            "XPScriptHttpDb",
            "XPDbHttp",
            "HttpDb");

        var usesFileIo = ContainsAny(script,
            "XPScriptFileIo",
            "TextIo",
            "FileOpen",
            "FileClose",
            "OpenTextFile",
            "CreateTextFile");

        var usesDatabaseUi = ContainsAny(script,
            "XPScriptDatabaseUiDataSource",
            "XPScriptDatabaseAttachment",
            "Attachment");

        // HTTP and HTTP-backed database helpers depend on JSON helpers.
        usesJson |= usesHttp || usesHttpDb;
        usesHttp |= usesHttpDb;

        if (!usesEvaluate)
        {
            generatedSource = RemoveBlock(generatedSource, EvaluateArgumentRuntimeSource.Code);
            generatedSource = RemoveBlock(generatedSource, NormalizeEvaluateRuntimeSource());
        }

        if (!usesJson)
        {
            generatedSource = RemoveBlock(generatedSource, JsonHttpCompatibilityRuntimeSource.Code);
            generatedSource = RemoveBlock(generatedSource, JsonNodesSerializerShimSource.Code);
            generatedSource = RemoveBlock(generatedSource, NativeJsonRuntimeSource.Code);
        }

        if (!usesHttp)
        {
            generatedSource = RemoveBlock(generatedSource, NativeHttpRuntimeSource.Code);
            generatedSource = RemoveBlock(generatedSource, AsyncHttpRuntimeSource.Code);
        }

        if (!usesHttpDb)
            generatedSource = RemoveBlock(generatedSource, HttpDbRuntimeSource.Code);

        if (!usesFileIo)
        {
            generatedSource = RemoveBlock(generatedSource, TextIoCompatibilityRuntimeSource.Code);
            generatedSource = RemoveBlock(generatedSource, FileIoExtensionsRuntimeSource.Code);
        }

        if (!usesDatabaseUi)
        {
            generatedSource = RemoveBlock(generatedSource, DatabaseUiDataSourceRuntimeSource.Build(usesSqlite: false, usesMsSql: false));
            generatedSource = RemoveBlock(generatedSource, DatabaseAttachmentRuntimeV2Source.Build(usesSqlite: false, usesMsSql: false));
            generatedSource = RemoveBlock(generatedSource, DatabaseAttachmentRuntimeV3Source.Code);
        }

        return generatedSource;
    }

    private static bool ContainsAny(string source, params string[] markers)
    {
        foreach (var marker in markers)
            if (source.Contains(marker, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string RemoveBlock(string source, string block)
    {
        if (string.IsNullOrEmpty(block)) return source;
        return source.Replace("\n\n" + block + "\n", "\n", StringComparison.Ordinal);
    }

    private static string NormalizeEvaluateRuntimeSource() => XPScriptEvaluateRuntimeSource.Code
        .Replace("\"isobject\" when args.Count == 1 => XPScriptRuntime.IsObject(Arg(0)),",
            "\"isobject\" when args.Count == 1 => XPScriptNullRuntime.IsObject(Arg(0)),", StringComparison.Ordinal)
        .Replace("\"isscalar\" when args.Count == 1 => Arg(0) is not LSArray && XPScriptRuntime.IsScalar(Arg(0)),",
            "\"isscalar\" when args.Count == 1 => Arg(0) is not LSArray && XPScriptNullRuntime.IsScalar(Arg(0)),", StringComparison.Ordinal);
}
