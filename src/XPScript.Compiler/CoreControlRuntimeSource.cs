namespace XPScript.Compiler;

public static class CoreControlRuntimeSource
{
    public const string Code = """
internal static class LSControlRuntime
{
    internal sealed class ErrorContext
    {
        private readonly Stack<int> _resumeStatements = new();

        public int GeneralHandler { get; set; }
        public bool ResumeNext { get; set; }
        public bool InHandler { get; set; }
        public Dictionary<int, int> SpecificHandlers { get; } = new();

        // Reading Statement consumes the innermost resume frame. This keeps
        // Resume/Resume Next paired with the error frame that actually
        // transferred control to the handler, even through nested calls.
        public int Statement
        {
            get => _resumeStatements.Count == 0 ? 0 : _resumeStatements.Pop();
            set
            {
                if (value <= 0) return;
                if (_resumeStatements.Count == 0 || _resumeStatements.Peek() != value)
                    _resumeStatements.Push(value);
            }
        }

        public int ResumeDepth => _resumeStatements.Count;

        public void DiscardResumeFrame()
        {
            if (_resumeStatements.Count > 0) _resumeStatements.Pop();
        }

        public void ClearResumeFrames() => _resumeStatements.Clear();
    }

    [ThreadStatic] private static Dictionary<int, Stack<int>>? _gosubStacks;

    public static ErrorContext CreateErrorContext() => new();

    public static void SetGoto(dynamic contextValue, int handlerId, int errorNumber)
    {
        var context = (ErrorContext)contextValue;
        context.ResumeNext = false;
        if (errorNumber == 0) context.GeneralHandler = handlerId;
        else context.SpecificHandlers[errorNumber] = handlerId;
    }

    public static void SetResumeNext(dynamic contextValue)
    {
        var context = (ErrorContext)contextValue;
        context.GeneralHandler = 0;
        context.SpecificHandlers.Clear();
        context.ResumeNext = true;
        context.ClearResumeFrames();
    }

    public static void Disable(dynamic contextValue, int errorNumber)
    {
        var context = (ErrorContext)contextValue;
        if (errorNumber == 0)
        {
            context.GeneralHandler = 0;
            context.SpecificHandlers.Clear();
            context.ResumeNext = false;
            context.ClearResumeFrames();
        }
        else
        {
            context.SpecificHandlers.Remove(errorNumber);
        }
    }

    public static int Capture(dynamic contextValue, Exception exception, int statement)
    {
        var context = (ErrorContext)contextValue;
        if (context.InHandler) return int.MinValue;

        var sourceLine = XPSourceLineRuntime.Current > 0 ? XPSourceLineRuntime.Current : statement;
        var number = XPScriptErrorRuntime.Capture(LSExtendedErrorRuntime.Normalize(exception), sourceLine);

        // On Error Resume Next continues immediately after the failing
        // statement and therefore must not leave an explicit Resume frame.
        if (context.ResumeNext) return -1;

        var handler = context.SpecificHandlers.TryGetValue(number, out var specific) ? specific : context.GeneralHandler;
        if (handler != 0)
        {
            context.Statement = statement;
            context.InHandler = true;
        }
        return handler;
    }

    public static void Clear(dynamic contextValue)
    {
        var context = (ErrorContext)contextValue;
        context.InHandler = false;
        XPScriptErrorRuntime.Clear();
    }

    public static int ResumeDepth(dynamic contextValue) => ((ErrorContext)contextValue).ResumeDepth;

    public static void PushGosub(int procedureId, int returnId)
    {
        _gosubStacks ??= new Dictionary<int, Stack<int>>();
        if (!_gosubStacks.TryGetValue(procedureId, out var stack))
        {
            stack = new Stack<int>();
            _gosubStacks[procedureId] = stack;
        }
        stack.Push(returnId);
    }

    public static int PopGosub(int procedureId)
    {
        if (_gosubStacks is null || !_gosubStacks.TryGetValue(procedureId, out var stack) || stack.Count == 0)
            return 0;
        return stack.Pop();
    }
}
""";
}
