using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using XPScript.Compiler;

internal static class IncludeDebuggerProbe
{
    [ModuleInitializer]
    public static void Run()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "xpscript-debug-include-" + Guid.NewGuid().ToString("N"));
        var libDirectory = Path.Combine(tempRoot, "lib");
        Directory.CreateDirectory(libDirectory);

        try
        {
            var rootPath = Path.Combine(tempRoot, "root.xps");
            var helperPath = Path.Combine(libDirectory, "helper.xps");
            var rootSource = """
Include "lib/helper.xps"
Use "lib/helper"

Sub Main()
    Dim value As Integer
    value = HelperValue()
    Print CStr(value)
End Sub
""";
            var helperSource = """
Function HelperValue() As Integer
    Dim result As Integer
    result = 42
    HelperValue = result
End Function
""";

            File.WriteAllText(rootPath, rootSource);
            File.WriteAllText(helperPath, helperSource);

            var compilerAssembly = typeof(XPScriptTranspiler).Assembly;
            var includeType = compilerAssembly.GetType("XPScript.Compiler.IncludeSourcePreprocessor", throwOnError: true)!;
            var include = Activator.CreateInstance(includeType, nonPublic: true)!;
            var includeTransform = includeType.GetMethod("Transform", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new Exception("Include source preprocessor Transform method was not found.");
            var includeResult = includeTransform.Invoke(include, [rootSource, rootPath])
                ?? throw new Exception("Include source preprocessor returned null.");
            var resultType = includeResult.GetType();
            var expandedSource = (string)(resultType.GetProperty("Source")?.GetValue(includeResult)
                ?? throw new Exception("Expanded include source was not returned."));
            var sourceMap = resultType.GetProperty("Map")?.GetValue(includeResult)
                ?? throw new Exception("Include source map was not returned.");

            var markerType = compilerAssembly.GetType("XPScript.Compiler.SourceLineMarkerPreprocessor", throwOnError: true)!;
            var marker = Activator.CreateInstance(markerType, nonPublic: true)!;
            var markerTransform = markerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(method => method.Name == "Transform" && method.GetParameters().Length == 3);
            var marked = (string)(markerTransform.Invoke(marker, [expandedSource, sourceMap, rootPath])
                ?? throw new Exception("Source-line marker preprocessor returned null."));

            var helperSourceId = "lib/helper.xps";
            var helperHex = Convert.ToHexString(Encoding.UTF8.GetBytes(helperSourceId));
            var rootSourceId = "root.xps";
            var rootHex = Convert.ToHexString(Encoding.UTF8.GetBytes(rootSourceId));

            if (!marked.Contains("__XPSOURCE_3_" + helperHex + "()", StringComparison.Ordinal))
                throw new Exception("Debugger include mapping did not preserve helper source id and local line.");
            if (!marked.Contains("__XPSOURCE_5_" + rootHex + "()", StringComparison.Ordinal))
                throw new Exception("Debugger include mapping did not restore the root source after included code.");
            if (expandedSource.Split("Function HelperValue", StringSplitOptions.None).Length != 2)
                throw new Exception("Use must deduplicate a file already included through Include.");

            var useRootPath = Path.Combine(tempRoot, "use-root.xps");
            var useRootSource = "Use \"lib/helper\"\nSub Main()\nEnd Sub\n";
            File.WriteAllText(useRootPath, useRootSource);
            var useResult = includeTransform.Invoke(include, [useRootSource, useRootPath])
                ?? throw new Exception("Use source preprocessor returned null.");
            var useExpandedSource = (string)(useResult.GetType().GetProperty("Source")?.GetValue(useResult)
                ?? throw new Exception("Expanded Use source was not returned."));
            if (!useExpandedSource.Contains("Function HelperValue", StringComparison.Ordinal))
                throw new Exception("Use did not infer the .xps extension.");

            var invalidUsePath = Path.Combine(tempRoot, "invalid-use.xps");
            File.WriteAllText(invalidUsePath, "Use \"..\\lib\\helper\"\n");
            try
            {
                includeTransform.Invoke(include, [File.ReadAllText(invalidUsePath), invalidUsePath]);
                throw new Exception("Use unexpectedly allowed parent-directory traversal.");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is CompilerException)
            {
            }

            foreach (var invalidUse in new[] { $"Use \"{helperPath}\"\n", "Use \"lib/helper.xps\"\n" })
            {
                try
                {
                    includeTransform.Invoke(include, [invalidUse, invalidUsePath]);
                    throw new Exception("Use unexpectedly accepted an absolute path or explicit .xps suffix.");
                }
                catch (TargetInvocationException ex) when (ex.InnerException is CompilerException)
                {
                }
            }

            var generated = new XPScriptTranspiler().Transpile(rootSource.Replace("Include \"lib/helper.xps\"", string.Empty, StringComparison.Ordinal), rootPath, CompilerDriver.CurrentRuntimeIdentifier());
            if (!generated.Contains("\"into\" => true", StringComparison.Ordinal))
                throw new Exception("Debugger Step Into no longer stops at the next XPscript statement.");
            if (!generated.Contains("\"over\" => depth <= _stepDepth", StringComparison.Ordinal))
                throw new Exception("Debugger Step Over no longer skips deeper include/function frames.");
            if (!generated.Contains("\"out\" => depth < _stepDepth", StringComparison.Ordinal))
                throw new Exception("Debugger Step Out no longer waits for the caller frame.");
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }
}
