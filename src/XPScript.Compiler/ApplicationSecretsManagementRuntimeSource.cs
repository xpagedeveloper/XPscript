namespace XPScript.Compiler;

internal static class ApplicationSecretsManagementRuntimeSource
{
    public const string Code = """
internal static class XPScriptApplicationSecretsManagementRuntime
{
    private static readonly object IndexSync = new();

    public static bool Exists(object? serviceValue, object? accountValue)
    {
        var applicationId = RequireApplicationId();
        var service = Required(serviceValue, "service");
        var account = Required(accountValue, "account");

        // Reuse the existing access path so Application.Id binding rules remain identical
        // to Get/Set/Backup/Restore. Get also ensures the public identifier is tracked.
        _ = XPScriptApplicationSecretsBackupRuntime.Get(service, account);

        var exists = NativeExists(ScopedService(applicationId, service), account);
        if (!exists) Untrack(service, account);
        return exists;
    }

    public static bool Delete(object? serviceValue, object? accountValue)
    {
        var applicationId = RequireApplicationId();
        var service = Required(serviceValue, "service");
        var account = Required(accountValue, "account");

        // Enforce the same immutable Application.Id binding used by the existing Secrets API.
        _ = XPScriptApplicationSecretsBackupRuntime.Get(service, account);

        var deleted = NativeDelete(ScopedService(applicationId, service), account);
        Untrack(service, account);
        return deleted;
    }

    public static LSArray List()
    {
        var applicationId = RequireApplicationId();
        var entries = ReadEntries();

        // If the application already has indexed credentials, route through the existing
        // runtime once so a changed Application.Id is rejected exactly as for Get/Set.
        if (entries.Count > 0)
            _ = XPScriptApplicationSecretsBackupRuntime.Get(entries[0].Service, entries[0].Account);

        var live = new System.Collections.Generic.List<SecretRef>();
        foreach (var entry in entries)
        {
            if (NativeExists(ScopedService(applicationId, entry.Service), entry.Account))
                live.Add(entry);
        }

        RewriteIndex(live);

        if (live.Count == 0) return new LSArray("String", true);
        var result = new LSArray("String", true, [0, 0], [live.Count - 1, 1]);
        for (var i = 0; i < live.Count; i++)
        {
            result.Set(live[i].Service, i, 0);
            result.Set(live[i].Account, i, 1);
        }
        return result;
    }

    private static bool NativeExists(string service, string account)
    {
        if (OperatingSystem.IsWindows()) return WindowsExists(service, account);
        if (OperatingSystem.IsMacOS()) return MacExists(service, account);
        if (OperatingSystem.IsLinux()) return LinuxExists(service, account);
        throw new XPScriptRuntimeException(5, "Application.Secrets is not supported on this operating system.");
    }

    private static bool NativeDelete(string service, string account)
    {
        if (OperatingSystem.IsWindows()) return WindowsDelete(service, account);
        if (OperatingSystem.IsMacOS()) return MacDelete(service, account);
        if (OperatingSystem.IsLinux()) return LinuxDelete(service, account);
        throw new XPScriptRuntimeException(5, "Application.Secrets is not supported on this operating system.");
    }

    private static bool WindowsExists(string service, string account)
    {
        if (CredReadW(Target(service, account), 1, 0, out var pointer))
        {
            CredFree(pointer);
            return true;
        }
        var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        if (error == 1168) return false;
        throw new XPScriptRuntimeException(5, "Credential Manager existence check failed with error " + error + ".");
    }

    private static bool WindowsDelete(string service, string account)
    {
        if (CredDeleteW(Target(service, account), 1, 0)) return true;
        var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        if (error == 1168) return false;
        throw new XPScriptRuntimeException(5, "Credential Manager delete failed with error " + error + ".");
    }

    private static bool MacExists(string service, string account)
    {
        var status = FindMac(service, account, out var data, out var item);
        try
        {
            if (status == -25300) return false;
            if (status != 0) throw new XPScriptRuntimeException(5, "Keychain existence check failed with status " + status + ".");
            return true;
        }
        finally
        {
            if (data != IntPtr.Zero) SecKeychainItemFreeContent(IntPtr.Zero, data);
            if (item != IntPtr.Zero) CFRelease(item);
        }
    }

    private static bool MacDelete(string service, string account)
    {
        var status = FindMac(service, account, out var data, out var item);
        try
        {
            if (status == -25300) return false;
            if (status != 0) throw new XPScriptRuntimeException(5, "Keychain lookup before delete failed with status " + status + ".");
            var deleteStatus = SecKeychainItemDelete(item);
            if (deleteStatus != 0) throw new XPScriptRuntimeException(5, "Keychain delete failed with status " + deleteStatus + ".");
            return true;
        }
        finally
        {
            if (data != IntPtr.Zero) SecKeychainItemFreeContent(IntPtr.Zero, data);
            if (item != IntPtr.Zero) CFRelease(item);
        }
    }

    private static int FindMac(string service, string account, out IntPtr data, out IntPtr item)
    {
        var serviceBytes = System.Text.Encoding.UTF8.GetBytes(service);
        var accountBytes = System.Text.Encoding.UTF8.GetBytes(account);
        return SecKeychainFindGenericPassword(IntPtr.Zero, (uint)serviceBytes.Length, serviceBytes, (uint)accountBytes.Length, accountBytes, out _, out data, out item);
    }

    private static bool LinuxExists(string service, string account)
    {
        var exitCode = RunSecretTool(["lookup", "xpscript-service", service, "xpscript-account", account], null, out _, out var error);
        if (exitCode == 0) return true;
        if (string.IsNullOrWhiteSpace(error)) return false;
        throw new XPScriptRuntimeException(5, "Linux Secret Service existence check failed: " + error.Trim());
    }

    private static bool LinuxDelete(string service, string account)
    {
        if (!LinuxExists(service, account)) return false;
        var exitCode = RunSecretTool(["clear", "xpscript-service", service, "xpscript-account", account], null, out _, out var error);
        if (exitCode == 0) return true;
        throw new XPScriptRuntimeException(5, "Linux Secret Service delete failed: " + (string.IsNullOrWhiteSpace(error) ? "secret-tool exit code " + exitCode : error.Trim()));
    }

    private static int RunSecretTool(string[] args, string? stdin, out string output, out string error)
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
            output = process.StandardOutput.ReadToEnd();
            error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Application.Secrets on Linux requires secret-tool/libsecret and an available Secret Service provider: " + ex.Message);
        }
    }

    private static System.Collections.Generic.List<SecretRef> ReadEntries()
    {
        lock (IndexSync)
        {
            var result = new System.Collections.Generic.List<SecretRef>();
            foreach (var token in ReadIndexTokens())
            {
                var separator = token.IndexOf('.');
                if (separator <= 0 || separator >= token.Length - 1) continue;
                try
                {
                    result.Add(new SecretRef(Decode(token[..separator]), Decode(token[(separator + 1)..])));
                }
                catch (FormatException) { }
            }
            result.Sort((left, right) =>
            {
                var serviceCompare = StringComparer.Ordinal.Compare(left.Service, right.Service);
                return serviceCompare != 0 ? serviceCompare : StringComparer.Ordinal.Compare(left.Account, right.Account);
            });
            return result;
        }
    }

    private static System.Collections.Generic.List<string> ReadIndexTokens()
    {
        var value = XPScriptApplicationRegistryRuntime.User.Get(IndexPath(), "Entries");
        var result = new System.Collections.Generic.List<string>();
        if (value is not LSArray array || !array.IsAllocated || array.Rank != 1) return result;
        for (var i = array.LBound(); i <= array.UBound(); i++)
        {
            var token = XPScriptRuntime.CStr(array.Get(i));
            if (token.Length > 0) result.Add(token);
        }
        return result;
    }

    private static void Untrack(string service, string account)
    {
        var token = Encode(service) + "." + Encode(account);
        lock (IndexSync)
        {
            var tokens = ReadIndexTokens();
            if (tokens.RemoveAll(x => string.Equals(x, token, StringComparison.Ordinal)) == 0) return;
            tokens.Sort(StringComparer.Ordinal);
            XPScriptApplicationRegistryRuntime.User.Set(IndexPath(), "Entries", tokens.ToArray(), "MultiString");
        }
    }

    private static void RewriteIndex(System.Collections.Generic.List<SecretRef> entries)
    {
        lock (IndexSync)
        {
            var tokens = entries.Select(x => Encode(x.Service) + "." + Encode(x.Account)).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            XPScriptApplicationRegistryRuntime.User.Set(IndexPath(), "Entries", tokens, "MultiString");
        }
    }

    private static string IndexPath()
    {
        var encoded = System.Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(RequireApplicationId()));
        return "Software/XPscript/Applications/" + encoded + "/Secrets";
    }

    private static string ScopedService(string applicationId, string service)
    {
        var app = System.Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(applicationId));
        return "app:" + app + ":" + service;
    }

    private static string RequireApplicationId()
    {
        var value = XPScriptApplicationRuntime.State.Get("__xps_application_id");
        var applicationId = XPScriptRuntime.CStr(value).Trim();
        if (applicationId.Length == 0)
            throw new XPScriptRuntimeException(5, "Application.Secrets requires Application.Id to be set before the credential store can be used.");
        if (applicationId.Length > 256)
            throw new XPScriptRuntimeException(5, "Application.Id cannot exceed 256 characters when used with Application.Secrets.");
        if (applicationId.IndexOf('\0') >= 0)
            throw new XPScriptRuntimeException(5, "Application.Id cannot contain NUL.");
        return applicationId;
    }

    private static string Required(object? value, string name)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length == 0) throw new XPScriptRuntimeException(5, "Application.Secrets " + name + " cannot be empty.");
        return text;
    }

    private static string Target(string service, string account) => "XPscript:" + service + ":" + account;
    private static string Encode(string value) => System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
    private static string Decode(string value) => System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(value));
    private sealed record SecretRef(string Service, string Account);

    [System.Runtime.InteropServices.DllImport("Advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);

    [System.Runtime.InteropServices.DllImport("Advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [System.Runtime.InteropServices.DllImport("Advapi32.dll")]
    private static extern void CredFree(IntPtr credential);

    private const string SecurityFramework = "/System/Library/Frameworks/Security.framework/Security";
    private const string CoreFoundationFramework = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    [System.Runtime.InteropServices.DllImport(SecurityFramework)]
    private static extern int SecKeychainFindGenericPassword(IntPtr keychain, uint serviceNameLength, byte[] serviceName, uint accountNameLength, byte[] accountName, out uint passwordLength, out IntPtr passwordData, out IntPtr itemRef);

    [System.Runtime.InteropServices.DllImport(SecurityFramework)]
    private static extern int SecKeychainItemDelete(IntPtr itemRef);

    [System.Runtime.InteropServices.DllImport(SecurityFramework)]
    private static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

    [System.Runtime.InteropServices.DllImport(CoreFoundationFramework)]
    private static extern void CFRelease(IntPtr value);
}
""";
}
