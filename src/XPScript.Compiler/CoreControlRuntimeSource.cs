namespace XPScript.Compiler;

public static class CoreControlRuntimeSource
{
    public const string Code = """
internal static class LSControlRuntime
{
    internal sealed class ErrorContext
    {
        public int GeneralHandler { get; set; }
        public bool ResumeNext { get; set; }
        public bool InHandler { get; set; }
        public int Statement { get; set; }
        public Dictionary<int, int> SpecificHandlers { get; } = new();
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
    }

    public static void Disable(dynamic contextValue, int errorNumber)
    {
        var context = (ErrorContext)contextValue;
        if (errorNumber == 0)
        {
            context.GeneralHandler = 0;
            context.SpecificHandlers.Clear();
            context.ResumeNext = false;
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
        context.Statement = statement;
        var number = LotusErrorRuntime.Capture(LSExtendedErrorRuntime.Normalize(exception), statement);
        if (context.ResumeNext) return -1;
        var handler = context.SpecificHandlers.TryGetValue(number, out var specific) ? specific : context.GeneralHandler;
        if (handler != 0) context.InHandler = true;
        return handler;
    }

    public static void Clear(dynamic contextValue)
    {
        var context = (ErrorContext)contextValue;
        context.InHandler = false;
        LotusErrorRuntime.Clear();
    }

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
