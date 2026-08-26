namespace XPScript.Compiler;

internal static class ApplicationPersistenceRuntimeSource
{
    public const string Code = """
internal static class XPScriptApplicationRegistryRuntime
{
    public static XPScriptApplicationRegistryStore User { get; } = new(false);
    public static XPScriptApplicationRegistryStore System { get; } = new(true);
}

internal sealed class XPScriptApplicationRegistryStore
{
    private readonly bool _system;

    public XPScriptApplicationRegistryStore(bool system) => _system = system;

    public object? Get(object? pathValue, object? nameValue)
    {
        var path = NormalizeRegistryPath(pathValue);
        var name = NormalizeValueName(nameValue);
        if (OperatingSystem.IsWindows()) return GetWindows(path, name);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) return GetPortable(path, name);
        throw new XPScriptRuntimeException(5, "Application.Registry is not supported on this operating system.");
    }

    public void Set(object? pathValue, object? nameValue, object? value) =>
        Set(pathValue, nameValue, value, InferType(value));

    public void Set(object? pathValue, object? nameValue, object? value, object? typeValue)
    {
        var path = NormalizeRegistryPath(pathValue);
        var name = NormalizeValueName(nameValue);
        var type = NormalizeType(typeValue);
        if (OperatingSystem.IsWindows()) { SetWindows(path, name, value, type); return; }
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) { SetPortable(path, name, value, type); return; }
        throw new XPScriptRuntimeException(5, "Application.Registry is not supported on this operating system.");
    }

    private object? GetWindows(string path, string name)
    {
        var hive = _system ? Microsoft.Win32.Registry.LocalMachine : Microsoft.Win32.Registry.CurrentUser;
        try
        {
            using var key = hive.OpenSubKey(path.Replace('/', '\\'), writable: false);
            if (key is null) return null;
            var value = key.GetValue(name, null, Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames);
            return ToXPScriptValue(value);
        }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Application.Registry read failed: " + ex.Message);
        }
    }

    private void SetWindows(string path, string name, object? value, string type)
    {
        var hive = _system ? Microsoft.Win32.Registry.LocalMachine : Microsoft.Win32.Registry.CurrentUser;
        try
        {
            using var key = hive.CreateSubKey(path.Replace('/', '\\'), writable: true)
                ?? throw new InvalidOperationException("Registry key could not be created.");
            var kind = type switch
            {
                "String" => Microsoft.Win32.RegistryValueKind.String,
                "ExpandString" => Microsoft.Win32.RegistryValueKind.ExpandString,
                "Binary" => Microsoft.Win32.RegistryValueKind.Binary,
                "DWord" => Microsoft.Win32.RegistryValueKind.DWord,
                "MultiString" => Microsoft.Win32.RegistryValueKind.MultiString,
                "QWord" => Microsoft.Win32.RegistryValueKind.QWord,
                _ => throw new XPScriptRuntimeException(5, "Unsupported registry value type: " + type)
            };
            key.SetValue(name, ConvertForStorage(value, type), kind);
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Application.Registry write failed: " + ex.Message);
        }
    }

    private object? GetPortable(string path, string name)
    {
        var file = PortableValuePath(path, name);
        if (!File.Exists(file)) return null;
        try
        {
            var lines = File.ReadAllLines(file, System.Text.Encoding.UTF8);
            if (lines.Length == 0) return null;
            var type = NormalizeType(lines[0]);
            var payload = lines.Skip(1).ToArray();
            return type switch
            {
                "String" or "ExpandString" => DecodeString(payload.FirstOrDefault() ?? ""),
                "Binary" => ToByteArray(Convert.FromBase64String(payload.FirstOrDefault() ?? "")),
                "DWord" => int.Parse(payload.FirstOrDefault() ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                "QWord" => long.Parse(payload.FirstOrDefault() ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                "MultiString" => ToStringArray(payload.Select(DecodeString).ToArray()),
                _ => null
            };
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Application.Registry read failed: " + ex.Message);
        }
    }

    private void SetPortable(string path, string name, object? value, string type)
    {
        var file = PortableValuePath(path, name);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var stored = ConvertForStorage(value, type);
            var lines = new List<string> { type };
            switch (type)
            {
                case "String":
                case "ExpandString":
                    lines.Add(EncodeString((string)stored));
                    break;
                case "Binary":
                    lines.Add(Convert.ToBase64String((byte[])stored));
                    break;
                case "DWord":
                    lines.Add(((int)stored).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "QWord":
                    lines.Add(((long)stored).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "MultiString":
                    lines.AddRange(((string[])stored).Select(EncodeString));
                    break;
                default:
                    throw new XPScriptRuntimeException(5, "Unsupported registry value type: " + type);
            }
            File.WriteAllLines(file, lines, new System.Text.UTF8Encoding(false));
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Application.Registry write failed: " + ex.Message);
        }
    }

    private string PortableValuePath(string path, string name)
    {
        var app = SanitizeSegment(Path.GetFileNameWithoutExtension(XPScriptApplicationRuntime.ExecutableFileName));
        if (app.Length == 0) app = "xpscript";
        string root;
        if (OperatingSystem.IsLinux())
        {
            root = _system
                ? "/etc"
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }
        else
        {
            root = _system
                ? "/Library/Preferences"
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Preferences");
        }

        var current = Path.Combine(root, app);
        foreach (var segment in SplitPath(path)) current = Path.Combine(current, EncodeSegment(segment));
        return Path.Combine(current, EncodeSegment(name) + ".xpsreg");
    }

    private static object ConvertForStorage(object? value, string type) => type switch
    {
        "String" or "ExpandString" => XPScriptRuntime.CStr(value),
        "DWord" => value is bool b ? (b ? 1 : 0) : XPScriptRuntime.CInt(value),
        "QWord" => XPScriptRuntime.CLng(value),
        "Binary" => ReadBytes(value),
        "MultiString" => ReadStrings(value),
        _ => throw new XPScriptRuntimeException(5, "Unsupported registry value type: " + type)
    };

    private static object? ToXPScriptValue(object? value) => value switch
    {
        byte[] bytes => ToByteArray(bytes),
        string[] strings => ToStringArray(strings),
        _ => value
    };

    private static LSArray ToByteArray(byte[] values)
    {
        if (values.Length == 0) return new LSArray("Byte", true);
        var array = new LSArray("Byte", true, [0], [values.Length - 1]);
        for (var i = 0; i < values.Length; i++) array.Set(values[i], i);
        return array;
    }

    private static LSArray ToStringArray(string[] values)
    {
        if (values.Length == 0) return new LSArray("String", true);
        var array = new LSArray("String", true, [0], [values.Length - 1]);
        for (var i = 0; i < values.Length; i++) array.Set(values[i], i);
        return array;
    }

    private static byte[] ReadBytes(object? value)
    {
        if (value is byte[] bytes) return bytes;
        if (value is LSArray array)
        {
            if (!array.IsAllocated) return [];
            if (array.Rank != 1) throw new XPScriptRuntimeException(13, "Binary registry values require a one-dimensional Byte array.");
            var result = new byte[array.UBound() - array.LBound() + 1];
            for (var i = array.LBound(); i <= array.UBound(); i++) result[i - array.LBound()] = XPScriptRuntime.CByte(array.Get(i));
            return result;
        }
        throw new XPScriptRuntimeException(13, "Binary registry values require a Byte array.");
    }

    private static string[] ReadStrings(object? value)
    {
        if (value is string[] strings) return strings;
        if (value is LSArray array)
        {
            if (!array.IsAllocated) return [];
            if (array.Rank != 1) throw new XPScriptRuntimeException(13, "MultiString registry values require a one-dimensional String array.");
            var result = new string[array.UBound() - array.LBound() + 1];
            for (var i = array.LBound(); i <= array.UBound(); i++) result[i - array.LBound()] = XPScriptRuntime.CStr(array.Get(i));
            return result;
        }
        throw new XPScriptRuntimeException(13, "MultiString registry values require a String array.");
    }

    private static string InferType(object? value)
    {
        if (value is bool or byte or short or int or uint) return "DWord";
        if (value is long or ulong) return "QWord";
        if (value is byte[]) return "Binary";
        if (value is string[]) return "MultiString";
        if (value is LSArray array)
        {
            if (array.ElementType.Equals("Byte", StringComparison.OrdinalIgnoreCase)) return "Binary";
            if (array.ElementType.Equals("String", StringComparison.OrdinalIgnoreCase)) return "MultiString";
        }
        return "String";
    }

    private static string NormalizeType(object? value)
    {
        var type = XPScriptRuntime.CStr(value).Trim();
        if (type.Equals("String", StringComparison.OrdinalIgnoreCase)) return "String";
        if (type.Equals("ExpandString", StringComparison.OrdinalIgnoreCase)) return "ExpandString";
        if (type.Equals("Binary", StringComparison.OrdinalIgnoreCase)) return "Binary";
        if (type.Equals("DWord", StringComparison.OrdinalIgnoreCase) || type.Equals("DWORD", StringComparison.OrdinalIgnoreCase)) return "DWord";
        if (type.Equals("MultiString", StringComparison.OrdinalIgnoreCase)) return "MultiString";
        if (type.Equals("QWord", StringComparison.OrdinalIgnoreCase) || type.Equals("QWORD", StringComparison.OrdinalIgnoreCase)) return "QWord";
        throw new XPScriptRuntimeException(5, "Unsupported registry value type: " + type);
    }

    private static string NormalizeRegistryPath(object? value)
    {
        var path = XPScriptRuntime.CStr(value).Trim().Replace('\\', '/');
        if (path.Length == 0) throw new XPScriptRuntimeException(5, "Registry path cannot be empty.");
        var parts = SplitPath(path);
        if (parts.Length == 0 || parts.Any(x => x is "." or ".."))
            throw new XPScriptRuntimeException(5, "Registry path is invalid.");
        return string.Join('/', parts);
    }

    private static string NormalizeValueName(object? value)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Registry value name cannot be empty.");
        return name;
    }

    private static string[] SplitPath(string value) => value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static string EncodeSegment(string value) => Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(value));
    private static string SanitizeSegment(string value) => new(value.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.').ToArray());
    private static string EncodeString(string value) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
    private static string DecodeString(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));
}

internal static class XPScriptApplicationSecretsRuntime
{
    public static string Get(object? serviceValue, object? accountValue)
    {
        var service = Required(serviceValue, "service");
        var account = Required(accountValue, "account");
        if (OperatingSystem.IsWindows()) return WindowsGet(service, account);
        if (OperatingSystem.IsMacOS()) return MacGet(service, account);
        if (OperatingSystem.IsLinux()) return LinuxGet(service, account);
        throw new XPScriptRuntimeException(5, "Application.Secrets is not supported on this operating system.");
    }

    public static void Set(object? serviceValue, object? accountValue, object? secretValue)
    {
        var service = Required(serviceValue, "service");
        var account = Required(accountValue, "account");
        var secret = XPScriptRuntime.CStr(secretValue);
        if (OperatingSystem.IsWindows()) { WindowsSet(service, account, secret); return; }
        if (OperatingSystem.IsMacOS()) { MacSet(service, account, secret); return; }
        if (OperatingSystem.IsLinux()) { LinuxSet(service, account, secret); return; }
        throw new XPScriptRuntimeException(5, "Application.Secrets is not supported on this operating system.");
    }

    private static string WindowsGet(string service, string account)
    {
        var target = Target(service, account);
        if (!CredReadW(target, 1, 0, out var pointer))
        {
            var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            if (error == 1168) return "";
            throw new XPScriptRuntimeException(5, "Credential Manager read failed with error " + error + ".");
        }
        try
        {
            var credential = System.Runtime.InteropServices.Marshal.PtrToStructure<XPSCredential>(pointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0) return "";
            var bytes = new byte[credential.CredentialBlobSize];
            System.Runtime.InteropServices.Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        finally { CredFree(pointer); }
    }

    private static void WindowsSet(string service, string account, string secret)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(secret);
        var blob = System.Runtime.InteropServices.Marshal.AllocHGlobal(bytes.Length == 0 ? 1 : bytes.Length);
        try
        {
            if (bytes.Length > 0) System.Runtime.InteropServices.Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new XPSCredential
            {
                Type = 1,
                TargetName = Target(service, account),
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = 2,
                UserName = account
            };
            if (!CredWriteW(ref credential, 0))
                throw new XPScriptRuntimeException(5, "Credential Manager write failed with error " + System.Runtime.InteropServices.Marshal.GetLastWin32Error() + ".");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(blob); }
    }

    private static string MacGet(string service, string account)
    {
        var serviceBytes = System.Text.Encoding.UTF8.GetBytes(service);
        var accountBytes = System.Text.Encoding.UTF8.GetBytes(account);
        var status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)serviceBytes.Length, serviceBytes, (uint)accountBytes.Length, accountBytes, out var length, out var data, out var item);
        if (status == -25300) return "";
        if (status != 0) throw new XPScriptRuntimeException(5, "Keychain read failed with status " + status + ".");
        try
        {
            if (data == IntPtr.Zero || length == 0) return "";
            var bytes = new byte[length];
            System.Runtime.InteropServices.Marshal.Copy(data, bytes, 0, bytes.Length);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            if (data != IntPtr.Zero) SecKeychainItemFreeContent(IntPtr.Zero, data);
            if (item != IntPtr.Zero) CFRelease(item);
        }
    }

    private static void MacSet(string service, string account, string secret)
    {
        var serviceBytes = System.Text.Encoding.UTF8.GetBytes(service);
        var accountBytes = System.Text.Encoding.UTF8.GetBytes(account);
        var secretBytes = System.Text.Encoding.UTF8.GetBytes(secret);
        var lookup = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)serviceBytes.Length, serviceBytes, (uint)accountBytes.Length, accountBytes, out var oldLength, out var oldData, out var item);
        if (oldData != IntPtr.Zero) SecKeychainItemFreeContent(IntPtr.Zero, oldData);
        try
        {
            int status;
            if (lookup == 0 && item != IntPtr.Zero)
                status = SecKeychainItemModifyAttributesAndData(item, IntPtr.Zero, (uint)secretBytes.Length, secretBytes);
            else if (lookup == -25300)
                status = SecKeychainAddGenericPassword(IntPtr.Zero, (uint)serviceBytes.Length, serviceBytes, (uint)accountBytes.Length, accountBytes, (uint)secretBytes.Length, secretBytes, out _);
            else
                status = lookup;
            if (status != 0) throw new XPScriptRuntimeException(5, "Keychain write failed with status " + status + ".");
        }
        finally { if (item != IntPtr.Zero) CFRelease(item); }
    }

    private static string LinuxGet(string service, string account)
    {
        var result = RunSecretTool(["lookup", "xpscript-service", service, "xpscript-account", account], null, allowNotFound: true);
        return result.TrimEnd('\r', '\n');
    }

    private static void LinuxSet(string service, string account, string secret)
    {
        _ = RunSecretTool(["store", "--label=XPscript " + service + " " + account, "xpscript-service", service, "xpscript-account", account], secret + "\n", allowNotFound: false);
    }

    private static string RunSecretTool(string[] args, string? stdin, bool allowNotFound)
    {
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "secret-tool",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        try
        {
            using var process = System.Diagnostics.Process.Start(start)
                ?? throw new InvalidOperationException("secret-tool could not be started.");
            if (stdin is not null) process.StandardInput.Write(stdin);
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode == 0) return output;
            if (allowNotFound && string.IsNullOrWhiteSpace(output) && string.IsNullOrWhiteSpace(error)) return "";
            throw new XPScriptRuntimeException(5, "Linux Secret Service operation failed: " + (string.IsNullOrWhiteSpace(error) ? "secret-tool exit code " + process.ExitCode : error.Trim()));
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Application.Secrets on Linux requires secret-tool/libsecret and an available Secret Service provider: " + ex.Message);
        }
    }

    private static string Target(string service, string account) => "XPscript:" + service + ":" + account;

    private static string Required(object? value, string name)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length == 0) throw new XPScriptRuntimeException(5, "Application.Secrets " + name + " cannot be empty.");
        return text;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct XPSCredential
    {
        public uint Flags;
        public uint Type;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] public string TargetName;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] public string? TargetAlias;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] public string UserName;
    }

    [System.Runtime.InteropServices.DllImport("Advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

    [System.Runtime.InteropServices.DllImport("Advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref XPSCredential credential, uint flags);

    [System.Runtime.InteropServices.DllImport("Advapi32.dll")]
    private static extern void CredFree(IntPtr credential);

    private const string SecurityFramework = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundationFramework = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [System.Runtime.InteropServices.DllImport(SecurityFramework)]
    private static extern int SecKeychainFindGenericPassword(IntPtr keychain, uint serviceNameLength, byte[] serviceName, uint accountNameLength, byte[] accountName, out uint passwordLength, out IntPtr passwordData, out IntPtr itemRef);

    [System.Runtime.InteropServices.DllImport(SecurityFramework)]
    private static extern int SecKeychainAddGenericPassword(IntPtr keychain, uint serviceNameLength, byte[] serviceName, uint accountNameLength, byte[] accountName, uint passwordLength, byte[] passwordData, out IntPtr itemRef);

    [System.Runtime.InteropServices.DllImport(SecurityFramework)]
    private static extern int SecKeychainItemModifyAttributesAndData(IntPtr itemRef, IntPtr attrList, uint length, byte[] data);

    [System.Runtime.InteropServices.DllImport(SecurityFramework)]
    private static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

    [System.Runtime.InteropServices.DllImport(CoreFoundationFramework)]
    private static extern void CFRelease(IntPtr value);
}
""";
}
