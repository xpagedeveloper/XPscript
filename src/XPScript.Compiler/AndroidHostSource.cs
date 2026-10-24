namespace XPScript.Compiler;

internal static class AndroidHostSource
{
    public const string Code = """
using Android.App;
using Android.Util;

[Activity(Label = "XPScript", MainLauncher = true, Exported = true)]
public sealed class AndroidEntryActivity : Activity
{
    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Program.Console.AndroidLog = (text, isError) => Log.WriteLine(isError ? LogPriority.Error : LogPriority.Info, "XPScript", text);
        if (Intent?.GetBooleanExtra("xpscript.appdebug", false) == true)
            System.Environment.SetEnvironmentVariable("XPSCRIPT_APPDEBUG", "1");
        try
        {
            System.Environment.ExitCode = 0;
            // Legacy generated hosts called Program.Main(Array.Empty<string>()); AST uses its dedicated entrypoint.
            Program.__xps_android_entry(Array.Empty<string>());
            if (System.Environment.ExitCode == 0)
                Log.Info("XPScript", "XPSCRIPT-EXIT=0");
            else
                Log.Error("XPScript", "XPSCRIPT-EXIT=" + System.Environment.ExitCode);
        }
        catch (Exception exception)
        {
            Log.Error("XPScript", exception.ToString());
            Log.Error("XPScript", "XPSCRIPT-EXIT=1");
            throw;
        }
        finally
        {
            Finish();
        }
    }

}
""";
}
