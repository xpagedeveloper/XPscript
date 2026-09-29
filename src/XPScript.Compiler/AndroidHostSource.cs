namespace XPScript.Compiler;

internal static class AndroidHostSource
{
    public const string Code = """
using Android.App;
using Android.OS;
using Android.Util;
using System.Text;

[Activity(Label = "XPScript", MainLauncher = true, Exported = true)]
sealed class AndroidEntryActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Console.SetOut(new AndroidLogWriter(LogPriority.Info));
        Console.SetError(new AndroidLogWriter(LogPriority.Error));
        try
        {
            Program.Main(Array.Empty<string>());
        }
        catch (Exception exception)
        {
            Log.Error("XPScript", exception.ToString());
            throw;
        }
        finally
        {
            Finish();
        }
    }

    private sealed class AndroidLogWriter : TextWriter
    {
        private readonly LogPriority _priority;
        private readonly StringBuilder _buffer = new();

        public AndroidLogWriter(LogPriority priority) => _priority = priority;
        override Encoding Encoding => Encoding.UTF8;

        override void Write(char value)
        {
            if (value == '\n')
            {
                Flush();
                return;
            }
            if (value != '\r') _buffer.Append(value);
        }

        override void Flush()
        {
            if (_buffer.Length == 0) return;
            Log.WriteLine(_priority, "XPScript", _buffer.ToString());
            _buffer.Clear();
        }
    }
}
""";
}
