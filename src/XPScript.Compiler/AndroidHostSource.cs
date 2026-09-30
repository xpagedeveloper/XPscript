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
            Program.Main(Array.Empty<string>());
            Log.Info("XPScript", "XPSCRIPT-EXIT=0");
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
