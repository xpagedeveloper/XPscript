using System.Runtime.CompilerServices;

namespace XPScript.Cli;

internal static class XPScriptVersion
{
    public const string Version = "1.0";
    public const string Copyright = "(c) XPageDeveloper.com 2026";
    public static string Banner => $"XPScript Version {Version} - {Copyright}";

    [ModuleInitializer]
    internal static void InitializeCommandLineVersionOutput()
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();

        if (args.Length == 0)
        {
            Console.WriteLine(Banner);
            return;
        }

        if (args.Length == 1 && args[0] is "--version" or "--info" or "--debug")
        {
            Console.WriteLine(Banner);
            Environment.Exit(0);
        }
    }
}
