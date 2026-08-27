internal static class XpsScaffolder
{
    public static int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length != 2)
            throw new ArgumentException("Usage: xpscript new <rest|web|desktop> <directory>. The directory is required; use '.' for the current directory.");

        var kind = args[0].Trim().ToLowerInvariant();
        if (kind is not ("rest" or "web" or "desktop"))
            throw new ArgumentException("Project type must be rest, web or desktop.");

        var suppliedTarget = args[1].Trim();
        if (suppliedTarget.Length == 0)
            throw new ArgumentException("A target directory is required; use '.' for the current directory.");

        var target = Path.GetFullPath(suppliedTarget);
        if (File.Exists(target))
            throw new IOException("Target path is a file, not a directory: " + target);

        Directory.CreateDirectory(target);

        var (fileName, content, nextCommand) = kind switch
        {
            "rest" => ("index.xps", RestTemplate, $"xpscript web {QuoteForDisplay(target)}"),
            "web" => ("index.xps", WebTemplate, $"xpscript web {QuoteForDisplay(target)}"),
            "desktop" => ("main.xps", DesktopTemplate, $"xpscript run {QuoteForDisplay(Path.Combine(target, "main.xps"))}"),
            _ => throw new InvalidOperationException("Unsupported scaffold type.")
        };

        var outputPath = Path.Combine(target, fileName);
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
[RoutePrefix:/api]
[Anonymous]

[Get:/health]
Function Health() As Object
    Dim result As New JsonObject
    Call result.Set("status", "ok")
    Set Health = result
End Function
""";

    private const string WebTemplate = """
[Anonymous]
[Get]
Sub Index()
    Response.Write("<h1>Hello from XPscript</h1>")
    Response.Write("<p>Your web server is running.</p>")
End Sub
""";

    private const string DesktopTemplate = """
Sub Main()
    Dim data As New JsonObject
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
