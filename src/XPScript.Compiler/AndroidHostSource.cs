namespace XPScript.Compiler;

internal static class AndroidHostSource
{
    public const string Code = """
using Android.App;
using Android.OS;
using Android.Util;

[Activity(Label = "XPScript", MainLauncher = true, Exported = true)]
sealed class AndroidEntryActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Console.AndroidLog = (text, isError) => Log.WriteLine(isError ? LogPriority.Error : LogPriority.Info, "XPScript", text);
        try
        {
            System.Environment.ExitCode = 0;
            Program.Main(Array.Empty<string>());
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
