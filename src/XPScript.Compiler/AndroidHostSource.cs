namespace XPScript.Compiler;

internal static class AndroidHostSource
{
    public const string Code = """
[global::Android.App.Activity(Label = "XPScript", MainLauncher = true, Exported = true)]
public sealed class AndroidEntryActivity : global::Android.App.Activity
{
    protected override void OnCreate(global::Android.OS.Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Program.Console.AndroidLog = (System.Action<string, bool>)((text, isError) => global::Android.Util.Log.WriteLine(isError ? global::Android.Util.LogPriority.Error : global::Android.Util.LogPriority.Info, "XPScript", text));
        if (Intent?.GetBooleanExtra("xpscript.appdebug", false) == true)
            System.Environment.SetEnvironmentVariable("XPSCRIPT_APPDEBUG", "1");
        try
        {
            System.Environment.ExitCode = 0;
            Program.__xps_android_entry(Array.Empty<string>());
            if (System.Environment.ExitCode == 0)
                global::Android.Util.Log.Info("XPScript", "XPSCRIPT-EXIT=0");
            else
                global::Android.Util.Log.Error("XPScript", "XPSCRIPT-EXIT=" + System.Environment.ExitCode);
        }
        catch (Exception exception)
        {
            global::Android.Util.Log.Error("XPScript", exception.ToString());
            global::Android.Util.Log.Error("XPScript", "XPSCRIPT-EXIT=1");
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
