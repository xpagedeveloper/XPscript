namespace XPScript.Compiler;

internal static class NotesNativeApiPasswordSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi
{
    private const ushort EmGetPassword = 73;
    private const uint EmRegBefore = 0x0001;
    private const ushort ErrBsafeExternalPassword = 0x1761;
    private const ushort ErrBsafeUserAbort = 0x1728;

    private uint _passwordRegistration;
    private PasswordEmHandlerDelegate? _passwordHandler;
    private nint _passwordBuffer;
    private int _passwordLength;

    internal void Initialize(string? notesIni, string? idPassword)
    {
        if (string.IsNullOrEmpty(idPassword))
        {
            Initialize(notesIni);
            return;
        }

        RegisterPasswordHook(idPassword);
        try
        {
            Initialize(notesIni);
        }
        catch
        {
            ReleasePasswordHook();
            throw;
        }
    }

    private void RegisterPasswordHook(string idPassword)
    {
        EnsureNotDisposedForPasswordHook();
        if (_passwordRegistration != 0 || _passwordBuffer != 0)
            throw new XPScriptRuntimeException(5, "Notes ID password hook is already registered.");

        _passwordBuffer = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(idPassword);
        _passwordLength = 0;
        while (System.Runtime.InteropServices.Marshal.ReadByte(_passwordBuffer, _passwordLength) != 0) _passwordLength++;
        var handler = new PasswordEmHandlerDelegate(HandlePasswordRequest);
        _passwordHandler = handler;

        try
        {
            var status = Resolve<EMRegisterDelegate>("EMRegister")(
                EmGetPassword,
                EmRegBefore,
                handler,
                0,
                out _passwordRegistration);
            if (status != 0)
                throw new XPScriptRuntimeException(5, "EMRegister(EM_GETPASSWORD) failed with Notes status 0x" + status.ToString("X4", System.Globalization.CultureInfo.InvariantCulture) + ".");
            if (_passwordRegistration == 0)
                throw new XPScriptRuntimeException(5, "EMRegister(EM_GETPASSWORD) did not return a registration handle.");
        }
        catch
        {
            ReleasePasswordSecret();
            _passwordHandler = null;
            _passwordRegistration = 0;
            throw;
        }
    }

    private ushort HandlePasswordRequest(nint record)
    {
        try
        {
            if (record == 0 || _passwordBuffer == 0 || _passwordLength <= 0)
                return ErrBsafeUserAbort;

            var extensionRecord = System.Runtime.InteropServices.Marshal.PtrToStructure<PasswordEmRecord>(record);
            if (extensionRecord.EventId != EmGetPassword || extensionRecord.Arguments == 0)
                return ErrBsafeUserAbort;

            // VARARG_GET consumes native argument slots. DWORD uses the first slot;
            // pointer arguments use the following two slots.
            var slot = IntPtr.Size == 8 ? 8 : 4;
            var arguments = extensionRecord.Arguments;
            var maxPasswordLength = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(arguments, 0));
            var returnLength = System.Runtime.InteropServices.Marshal.ReadIntPtr(arguments, slot);
            var returnPassword = System.Runtime.InteropServices.Marshal.ReadIntPtr(arguments, slot * 2);
            if (maxPasswordLength == 0 || returnLength == 0 || returnPassword == 0)
                return ErrBsafeUserAbort;

            var maximum = checked((int)Math.Min(maxPasswordLength, int.MaxValue));
            if (_passwordLength > maximum)
                return ErrBsafeUserAbort;

            for (var i = 0; i < _passwordLength; i++)
                System.Runtime.InteropServices.Marshal.WriteByte(returnPassword, i, System.Runtime.InteropServices.Marshal.ReadByte(_passwordBuffer, i));
            if (_passwordLength < maximum)
                System.Runtime.InteropServices.Marshal.WriteByte(returnPassword, _passwordLength, 0);
            System.Runtime.InteropServices.Marshal.WriteInt32(returnLength, _passwordLength);
            return ErrBsafeExternalPassword;
        }
        catch
        {
            // Never allow a managed exception to cross the unmanaged callback boundary.
            return ErrBsafeUserAbort;
        }
    }

    internal void ReleasePasswordHook()
    {
        var registration = _passwordRegistration;
        _passwordRegistration = 0;
        if (registration != 0)
        {
            try
            {
                var status = Resolve<EMDeregisterDelegate>("EMDeregister")(registration);
                if (status != 0 && _initialized)
                    throw new XPScriptRuntimeException(5, "EMDeregister(EM_GETPASSWORD) failed with Notes status 0x" + status.ToString("X4", System.Globalization.CultureInfo.InvariantCulture) + ".");
            }
            finally
            {
                _passwordHandler = null;
                ReleasePasswordSecret();
            }
        }
        else
        {
            _passwordHandler = null;
            ReleasePasswordSecret();
        }
    }

    private void ReleasePasswordSecret()
    {
        var buffer = _passwordBuffer;
        var length = _passwordLength;
        _passwordBuffer = 0;
        _passwordLength = 0;
        if (buffer == 0) return;
        try
        {
            for (var i = 0; i <= length; i++) System.Runtime.InteropServices.Marshal.WriteByte(buffer, i, 0);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }
    }

    private void EnsureNotDisposedForPasswordHook()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(XPScriptNotesNativeApi));
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct PasswordEmRecord
    {
        public ushort EventId;
        public ushort NotificationType;
        public ushort Status;
        public nint Arguments;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort PasswordEmHandlerDelegate(nint record);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort EMRegisterDelegate(ushort eventId, uint flags, PasswordEmHandlerDelegate handler, ushort recursionId, out uint registration);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort EMDeregisterDelegate(uint registration);
}
""";
}
