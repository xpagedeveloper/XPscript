internal static class XpsScaffolder
{
    public static int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length != 2)
            throw new ArgumentException("Usage: xpscript new <rest|web|desktop|cli> <directory|file.xps>. The target is required; use '.' for the current directory.");

        var kind = args[0].Trim().ToLowerInvariant();
        if (kind is not ("rest" or "web" or "desktop" or "cli"))
            throw new ArgumentException("Project type must be rest, web, desktop or cli.");

        var suppliedTarget = args[1].Trim();
        if (suppliedTarget.Length == 0)
            throw new ArgumentException("A target directory or .xps file is required; use '.' for the current directory.");

        var target = Path.GetFullPath(suppliedTarget);
        var explicitFile = Path.GetExtension(target).Equals(".xps", StringComparison.OrdinalIgnoreCase);
        var targetDirectory = explicitFile
            ? Path.GetDirectoryName(target) ?? Directory.GetCurrentDirectory()
            : target;

        if (!explicitFile && File.Exists(target))
            throw new IOException("Target path is a file, not a directory: " + target);
        if (explicitFile && Directory.Exists(target))
            throw new IOException("Target path is a directory, not a file: " + target);

        Directory.CreateDirectory(targetDirectory);

        var defaultFileName = kind is "rest" or "web" ? "index.xps" : "main.xps";
        var outputPath = explicitFile ? target : Path.Combine(targetDirectory, defaultFileName);
        var (content, nextCommand) = kind switch
        {
            "rest" => (RestTemplate, $"xpscript web {QuoteForDisplay(targetDirectory)}"),
            "web" => (WebTemplate, $"xpscript web {QuoteForDisplay(targetDirectory)}"),
            "desktop" => (DesktopTemplate, $"xpscript run {QuoteForDisplay(outputPath)}"),
            "cli" => (CliTemplate, $"xpscript run {QuoteForDisplay(outputPath)} --Args \"argument1 argument2\""),
            _ => throw new InvalidOperationException("Unsupported scaffold type.")
        };
        if (File.Exists(outputPath))
            throw new IOException("Refusing to overwrite existing file: " + outputPath);

        File.WriteAllText(outputPath, content);

        Console.WriteLine($"Created {kind} scaffold: {outputPath}");
        Console.WriteLine();
        Console.WriteLine("Run:");
        Console.WriteLine("  " + nextCommand);
        return 0;
    }

    private static string QuoteForDisplay(string path) => path.Any(char.IsWhiteSpace) ? "\"" + path + "\"" : path;

    private const string RestTemplate = """
Public Class HealthResponse
    Public Status As String
End Class

[RoutePrefix:/api]
[Anonymous]

[Get:/health]
Sub Health()
    Dim result As New HealthResponse
    result.Status = "ok"
    Response.OK(result)
End Sub
""";

    private const string WebTemplate = """
[Anonymous]
[Get]
Sub Index()
    Response.Write("<h1>Hello from XPscript</h1>")
    Response.Write("<p>Your web server is running.</p>")
End Sub
""";

    private const string CliTemplate = """
Option Declare

Sub Main()
    Dim i As Integer

    For i = 0 To Application.ArgCount - 1
        Print Application.Args(i)
    Next

    Application.ExitCode = 0
End Sub
""";

    private const string DesktopTemplate = """
Sub Main()
    Dim data As New XPJsonObject
    Dim form As New UIForm("XPscript desktop application", 480, 240, True)
    Dim result As String

    Call data.Set("message", "Hello from XPscript")
    Call form.BindData(data)
    Call form.AddTextField("message", "Message")

    result = form.ShowDialog()
    Print "RESULT=" & result
End Sub
""";
}
