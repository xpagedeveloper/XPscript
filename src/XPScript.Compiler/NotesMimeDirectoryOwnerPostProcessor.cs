namespace XPScript.Compiler;

internal static class NotesMimeDirectoryOwnerPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source + "\n\n" + Runtime;
    }

    private const string Runtime = """
internal sealed class XPScriptNotesMimeDirectoryOwner : System.IDisposable
{
    private XPScriptNotesNativeApi? _api;
    private nint _directory;

    internal XPScriptNotesMimeDirectoryOwner(XPScriptNotesNativeApi api, uint note)
    {
        _api = api ?? throw new System.ArgumentNullException(nameof(api));
        _directory = api.OpenMimeDirectory(note);
    }

    internal bool IsAlive => _directory != 0;

    internal nint RootEntity
    {
        get
        {
            EnsureAlive();
            return _api!.GetMimeRootEntity(_directory);
        }
    }

    internal nint FirstSubpart(nint entity)
    {
        EnsureAlive();
        return _api!.GetMimeFirstSubpart(_directory, entity);
    }

    internal nint NextSibling(nint entity)
    {
        EnsureAlive();
        return _api!.GetMimeNextSibling(_directory, entity);
    }

    internal nint PrevSibling(nint entity)
    {
        EnsureAlive();
        return _api!.GetMimePrevSibling(_directory, entity);
    }

    internal nint Parent(nint entity)
    {
        EnsureAlive();
        return _api!.GetMimeParent(_directory, entity);
    }

    public void Dispose()
    {
        var directory = _directory;
        _directory = 0;
        var api = _api;
        _api = null;
        if (directory != 0 && api is not null)
            api.FreeMimeDirectory(directory);
    }

    private void EnsureAlive()
    {
        if (_directory == 0 || _api is null)
            throw new System.ObjectDisposedException(nameof(XPScriptNotesMimeDirectoryOwner));
    }
}
""";
}
