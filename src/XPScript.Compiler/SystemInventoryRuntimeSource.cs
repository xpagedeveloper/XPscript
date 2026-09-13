namespace XPScript.Compiler;

internal static class SystemInventoryRuntimeSource
{
    public const string Code = """
internal static class XPScriptSystemInventoryFactory
{
    public static object Create() => new XPScriptSystemInventory();
}

internal sealed class XPScriptSystemInventory
{
    public XPScriptSystemInventorySnapshot GetSnapshot() => new(
        GetSystemInfo(), GetCpuInfo(), GetMemoryInfo(), GetPhysicalDisks(), GetVolumes(),
        GetGraphicsAdapters(), GetNetworkAdapters(), GetInstalledApplications(), GetInstalledPackages());

    public XPScriptSystemInventorySystemInfo GetSystemInfo()
    {
        var manufacturer = "";
        var model = "";
        var serial = "";
        var biosVendor = "";
        var biosVersion = "";

        if (System.OperatingSystem.IsWindows())
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
            manufacturer = RegistryText(key, "SystemManufacturer");
            model = RegistryText(key, "SystemProductName");
            serial = RegistryText(key, "SystemSerialNumber");
            biosVendor = RegistryText(key, "BIOSVendor");
            biosVersion = RegistryText(key, "BIOSVersion");
        }
        else if (System.OperatingSystem.IsLinux())
        {
            manufacturer = ReadTextFile("/sys/class/dmi/id/sys_vendor");
            model = ReadTextFile("/sys/class/dmi/id/product_name");
            serial = ReadTextFile("/sys/class/dmi/id/product_serial");
            biosVendor = ReadTextFile("/sys/class/dmi/id/bios_vendor");
            biosVersion = ReadTextFile("/sys/class/dmi/id/bios_version");
        }
        else if (System.OperatingSystem.IsMacOS())
        {
            manufacturer = "Apple Inc.";
            model = MacSysctlString("hw.model");
        }

        return new XPScriptSystemInventorySystemInfo(
            Environment.MachineName,
            System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            Environment.OSVersion.VersionString,
            System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            manufacturer, model, serial, biosVendor, biosVersion,
            Environment.Is64BitOperatingSystem, Environment.Is64BitProcess,
            Environment.ProcessorCount);
    }

    public XPScriptSystemInventoryCpuInfo GetCpuInfo()
    {
        var name = "";
        var manufacturer = "";
        var physicalCores = 0;
        var maxClockMHz = 0L;

        if (System.OperatingSystem.IsWindows())
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            name = RegistryText(key, "ProcessorNameString");
            manufacturer = RegistryText(key, "VendorIdentifier");
            maxClockMHz = RegistryLong(key, "~MHz");
        }
        else if (System.OperatingSystem.IsLinux())
        {
            try
            {
                var text = System.IO.File.ReadAllText("/proc/cpuinfo");
                var blocks = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
                var coreIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var block in blocks)
                {
                    var map = ParseColonBlock(block);
                    if (name.Length == 0) name = GetMap(map, "model name", GetMap(map, "Processor", ""));
                    if (manufacturer.Length == 0) manufacturer = GetMap(map, "vendor_id", GetMap(map, "CPU implementer", ""));
                    var physical = GetMap(map, "physical id", "0");
                    var core = GetMap(map, "core id", GetMap(map, "processor", ""));
                    if (core.Length != 0) coreIds.Add(physical + ":" + core);
                    if (maxClockMHz == 0 && double.TryParse(GetMap(map, "cpu MHz", ""), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var mhz))
                        maxClockMHz = (long)Math.Round(mhz);
                }
                physicalCores = coreIds.Count;
            }
            catch { }
        }
        else if (System.OperatingSystem.IsMacOS())
        {
            name = MacSysctlString("machdep.cpu.brand_string");
            manufacturer = name.Contains("Apple", StringComparison.OrdinalIgnoreCase) ? "Apple" : "";
            physicalCores = checked((int)MacSysctlUInt64("hw.physicalcpu"));
            var hz = MacSysctlUInt64("hw.cpufrequency_max");
            maxClockMHz = checked((long)(hz / 1_000_000UL));
        }

        return new XPScriptSystemInventoryCpuInfo(
            name, manufacturer,
            System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
            physicalCores, Environment.ProcessorCount, maxClockMHz);
    }

    public XPScriptSystemInventoryMemoryInfo GetMemoryInfo()
    {
        long total = 0;
        long available = 0;
        long totalSwap = 0;
        long availableSwap = 0;

        if (System.OperatingSystem.IsWindows())
        {
            var status = new XPScriptMemoryStatusEx();
            if (XPScriptSystemInventoryNative.GlobalMemoryStatusEx(status))
            {
                total = checked((long)status.ullTotalPhys);
                available = checked((long)status.ullAvailPhys);
                totalSwap = checked((long)status.ullTotalPageFile);
                availableSwap = checked((long)status.ullAvailPageFile);
            }
        }
        else if (System.OperatingSystem.IsLinux())
        {
            try
            {
                var values = ParseColonBlock(System.IO.File.ReadAllText("/proc/meminfo"));
                total = ParseKb(GetMap(values, "MemTotal", ""));
                available = ParseKb(GetMap(values, "MemAvailable", GetMap(values, "MemFree", "")));
                totalSwap = ParseKb(GetMap(values, "SwapTotal", ""));
                availableSwap = ParseKb(GetMap(values, "SwapFree", ""));
            }
            catch { }
        }
        else if (System.OperatingSystem.IsMacOS())
        {
            total = checked((long)MacSysctlUInt64("hw.memsize"));
        }

        if (total == 0)
        {
            var gc = GC.GetGCMemoryInfo();
            total = gc.TotalAvailableMemoryBytes > 0 ? gc.TotalAvailableMemoryBytes : 0;
        }

        return new XPScriptSystemInventoryMemoryInfo(total, available, totalSwap, availableSwap);
    }

    public LSArray GetPhysicalDisks()
    {
        var result = new List<object?>();
        if (System.OperatingSystem.IsLinux() && System.IO.Directory.Exists("/sys/block"))
        {
            foreach (var path in System.IO.Directory.EnumerateDirectories("/sys/block").OrderBy(x => x, StringComparer.Ordinal))
            {
                var name = System.IO.Path.GetFileName(path);
                if (name.StartsWith("loop", StringComparison.Ordinal) || name.StartsWith("ram", StringComparison.Ordinal)) continue;
                var model = ReadTextFile(System.IO.Path.Combine(path, "device/model"));
                var vendor = ReadTextFile(System.IO.Path.Combine(path, "device/vendor"));
                var serial = ReadTextFile(System.IO.Path.Combine(path, "device/serial"));
                var removable = ReadTextFile(System.IO.Path.Combine(path, "removable")) == "1";
                long size = 0;
                if (long.TryParse(ReadTextFile(System.IO.Path.Combine(path, "size")), out var sectors)) size = checked(sectors * 512L);
                result.Add(new XPScriptSystemInventoryPhysicalDiskInfo(name, model, vendor, serial, size, removable, "sysfs"));
            }
        }
        return Pack(result);
    }

    public LSArray GetVolumes()
    {
        var result = new List<object?>();
        foreach (var drive in System.IO.DriveInfo.GetDrives())
        {
            try
            {
                result.Add(new XPScriptSystemInventoryVolumeInfo(
                    drive.Name, drive.RootDirectory.FullName, drive.DriveType.ToString(), drive.IsReady ? drive.DriveFormat : "",
                    drive.IsReady ? drive.TotalSize : 0, drive.IsReady ? drive.AvailableFreeSpace : 0));
            }
            catch
            {
                result.Add(new XPScriptSystemInventoryVolumeInfo(drive.Name, drive.Name, drive.DriveType.ToString(), "", 0, 0));
            }
        }
        return Pack(result);
    }

    public LSArray GetGraphicsAdapters()
    {
        var result = new List<object?>();
        if (System.OperatingSystem.IsWindows())
        {
            try
            {
                using var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Video");
                if (root is not null)
                {
                    foreach (var childName in root.GetSubKeyNames())
                    {
                        using var child = root.OpenSubKey(childName + @"\0000");
                        if (child is null) continue;
                        var name = RegistryText(child, "DriverDesc");
                        if (name.Length == 0) name = RegistryText(child, "AdapterString");
                        if (name.Length == 0) continue;
                        result.Add(new XPScriptSystemInventoryGraphicsAdapterInfo(name, RegistryText(child, "ProviderName"), RegistryText(child, "DriverVersion"), RegistryText(child, "MatchingDeviceId"), "WindowsRegistry"));
                    }
                }
            }
            catch { }
        }
        else if (System.OperatingSystem.IsLinux() && System.IO.Directory.Exists("/sys/class/drm"))
        {
            foreach (var card in System.IO.Directory.EnumerateDirectories("/sys/class/drm", "card*"))
            {
                var fileName = System.IO.Path.GetFileName(card);
                if (fileName.Contains('-', StringComparison.Ordinal)) continue;
                var device = System.IO.Path.Combine(card, "device");
                if (!System.IO.Directory.Exists(device)) continue;
                result.Add(new XPScriptSystemInventoryGraphicsAdapterInfo(
                    fileName, ReadTextFile(System.IO.Path.Combine(device, "vendor")), "",
                    ReadTextFile(System.IO.Path.Combine(device, "device")), "sysfs"));
            }
        }
        return Pack(result.GroupBy(x => x is XPScriptSystemInventoryGraphicsAdapterInfo g ? g.Name + "|" + g.DeviceId : Guid.NewGuid().ToString(), StringComparer.OrdinalIgnoreCase).Select(g => g.First()));
    }

    public LSArray GetNetworkAdapters()
    {
        var result = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces().Select(item =>
        {
            var properties = item.GetIPProperties();
            return (object?)new XPScriptSystemInventoryNetworkAdapterInfo(
                item.Name, item.Description, item.Id, item.NetworkInterfaceType.ToString(), item.OperationalStatus.ToString(),
                item.Speed, item.GetPhysicalAddress().ToString(),
                XPScriptNetworkArray.PackStrings(properties.UnicastAddresses.Select(x => x.Address.ToString())));
        });
        return Pack(result);
    }

    public LSArray GetInstalledApplications() => Pack(EnumerateInstalledApplications().Cast<object?>());
    public LSArray GetInstalledPackages() => Pack(EnumerateInstalledPackages().Cast<object?>());

    public LSArray FindInstalledSoftware(object? nameValue)
    {
        var name = RequiredText(nameValue, "name");
        return Pack(EnumerateAllSoftware()
            .Where(x => x.Name.Contains(name, StringComparison.OrdinalIgnoreCase) || x.PackageId.Contains(name, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.Name + "|" + x.Version + "|" + x.PackageManager + "|" + x.Scope, StringComparer.OrdinalIgnoreCase)
            .Select(x => (object?)x.First()));
    }

    public bool IsSoftwareInstalled(object? nameValue)
    {
        var name = RequiredText(nameValue, "name");
        return EnumerateAllSoftware().Any(x =>
            x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || x.PackageId.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<XPScriptInstalledSoftwareInfo> EnumerateAllSoftware() =>
        EnumerateInstalledApplications().Concat(EnumerateInstalledPackages());

    private static IEnumerable<XPScriptInstalledSoftwareInfo> EnumerateInstalledApplications()
    {
        if (System.OperatingSystem.IsWindows()) return EnumerateWindowsSoftware();
        if (System.OperatingSystem.IsLinux()) return EnumerateLinuxDesktopApplications();
        if (System.OperatingSystem.IsMacOS()) return EnumerateMacApplications();
        return [];
    }

    private static IEnumerable<XPScriptInstalledSoftwareInfo> EnumerateInstalledPackages()
    {
        if (System.OperatingSystem.IsWindows()) return EnumerateWindowsSoftware();
        if (System.OperatingSystem.IsLinux()) return EnumerateDpkgPackages().Concat(EnumerateSnapPackages()).Concat(EnumerateFlatpakPackages());
        if (System.OperatingSystem.IsMacOS()) return EnumerateHomebrewPackages();
        return [];
    }

    private static IEnumerable<XPScriptInstalledSoftwareInfo> EnumerateWindowsSoftware()
    {
        var result = new List<XPScriptInstalledSoftwareInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hive in new[] { Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryHive.CurrentUser })
        foreach (var view in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, view);
                using var root = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (root is null) continue;
                foreach (var subName in root.GetSubKeyNames())
                {
                    using var item = root.OpenSubKey(subName);
                    if (item is null) continue;
                    var name = RegistryText(item, "DisplayName");
                    if (name.Length == 0 || RegistryLong(item, "SystemComponent") == 1) continue;
                    var version = RegistryText(item, "DisplayVersion");
                    var scope = hive == Microsoft.Win32.RegistryHive.CurrentUser ? "User" : "Machine";
                    var key = name + "|" + version + "|" + scope;
                    if (!seen.Add(key)) continue;
                    result.Add(new XPScriptInstalledSoftwareInfo(
                        name, version, RegistryText(item, "Publisher"), "", RegistryText(item, "InstallDate"),
                        RegistryText(item, "InstallLocation"), subName, "WindowsRegistry", scope,
                        hive + " " + view));
                }
            }
            catch { }
        }
        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<XPScriptInstalledSoftwareInfo> EnumerateLinuxDesktopApplications()
    {
        var result = new List<XPScriptInstalledSoftwareInfo>();
        var roots = new List<(string Path, string Scope)> { ("/usr/share/applications", "Machine"), ("/usr/local/share/applications", "Machine") };
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length != 0) roots.Add((System.IO.Path.Combine(home, ".local/share/applications"), "User"));
        foreach (var root in roots)
        {
            if (!System.IO.Directory.Exists(root.Path)) continue;
            foreach (var file in System.IO.Directory.EnumerateFiles(root.Path, "*.desktop"))
            {
                try
                {
                    var values = ParseEqualsFile(file);
                    var name = GetMap(values, "Name", "");
                    if (name.Length == 0 || GetMap(values, "NoDisplay", "").Equals("true", StringComparison.OrdinalIgnoreCase)) continue;
                    result.Add(new XPScriptInstalledSoftwareInfo(name, GetMap(values, "X-AppImage-Version", ""), "", "", "", "", System.IO.Path.GetFileNameWithoutExtension(file), "DesktopEntry", root.Scope, file));
                }
                catch { }
            }
        }
        return result.GroupBy(x => x.Name + "|" + x.Scope, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<XPScriptInstalledSoftwareInfo> EnumerateDpkgPackages()
    {
        const string statusFile = "/var/lib/dpkg/status";
        if (!System.IO.File.Exists(statusFile)) return [];
        var result = new List<XPScriptInstalledSoftwareInfo>();
        try
        {
            var text = System.IO.File.ReadAllText(statusFile);
            foreach (var block in text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var values = ParseColonBlock(block);
                if (!GetMap(values, "Status", "").Equals("install ok installed", StringComparison.OrdinalIgnoreCase)) continue;
                var name = GetMap(values, "Package", "");
                if (name.Length == 0) continue;
                result.Add(new XPScriptInstalledSoftwareInfo(name, GetMap(values, "Version", ""), GetMap(values, "Maintainer", ""), GetMap(values, "Architecture", ""), "", "", name, "dpkg", "Machine", statusFile));
            }
        }
        catch { }
        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<XPScriptInstalledSoftwareInfo> EnumerateSnapPackages()
    {
        const string root = "/snap";
        if (!System.IO.Directory.Exists(root)) return [];
        var result = new List<XPScriptInstalledSoftwareInfo>();
        foreach (var path in System.IO.Directory.EnumerateDirectories(root))
        {
            var name = System.IO.Path.GetFileName(path);
            if (name.Equals("bin", StringComparison.OrdinalIgnoreCase)) continue;
            var current = System.IO.Path.Combine(path, "current");
            var version = "";
            try { if (System.IO.Directory.Exists(current)) version = new System.IO.DirectoryInfo(current).LinkTarget ?? ""; } catch { }
            result.Add(new XPScriptInstalledSoftwareInfo(name, version, "", "", "", path, name, "Snap", "Machine", path));
        }
        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<XPScriptInstalledSoftwareInfo> EnumerateFlatpakPackages()
    {
        var result = new List<XPScriptInstalledSoftwareInfo>();
        foreach (var root in new[] { "/var/lib/flatpak/app", System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/flatpak/app") })
        {
            if (!System.IO.Directory.Exists(root)) continue;
            var scope = root.StartsWith("/var/", StringComparison.Ordinal) ? "Machine" : "User";
            foreach (var path in System.IO.Directory.EnumerateDirectories(root))
            {
                var name = System.IO.Path.GetFileName(path);
                result.Add(new XPScriptInstalledSoftwareInfo(name, "", "", "", "", path, name, "Flatpak", scope, path));
            }
        }
        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<XPScriptInstalledSoftwareInfo> EnumerateMacApplications()
    {
        var result = new List<XPScriptInstalledSoftwareInfo>();
        var roots = new List<(string Path, string Scope)> { ("/Applications", "Machine"), ("/System/Applications", "System") };
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (home.Length != 0) roots.Add((System.IO.Path.Combine(home, "Applications"), "User"));
        foreach (var root in roots)
        {
            if (!System.IO.Directory.Exists(root.Path)) continue;
            foreach (var app in System.IO.Directory.EnumerateDirectories(root.Path, "*.app", System.IO.SearchOption.TopDirectoryOnly))
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(app);
                var version = "";
                var packageId = "";
                var plist = System.IO.Path.Combine(app, "Contents/Info.plist");
                try
                {
                    if (System.IO.File.Exists(plist))
                    {
                        var text = System.IO.File.ReadAllText(plist);
                        if (text.TrimStart().StartsWith("<?xml", StringComparison.Ordinal) || text.Contains("<plist", StringComparison.Ordinal))
                        {
                            var doc = System.Xml.Linq.XDocument.Parse(text);
                            var dict = doc.Descendants("dict").FirstOrDefault();
                            if (dict is not null)
                            {
                                var elements = dict.Elements().ToArray();
                                for (var i = 0; i + 1 < elements.Length; i++)
                                {
                                    if (elements[i].Name.LocalName != "key") continue;
                                    var key = elements[i].Value;
                                    var value = elements[i + 1].Value;
                                    if (key == "CFBundleDisplayName" && value.Length != 0) name = value;
                                    else if (key == "CFBundleShortVersionString") version = value;
                                    else if (key == "CFBundleIdentifier") packageId = value;
                                }
                            }
                        }
                    }
                }
                catch { }
                result.Add(new XPScriptInstalledSoftwareInfo(name, version, "", "", "", app, packageId, "MacAppBundle", root.Scope, app));
            }
        }
        return result.GroupBy(x => x.Name + "|" + x.Scope, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<XPScriptInstalledSoftwareInfo> EnumerateHomebrewPackages()
    {
        var result = new List<XPScriptInstalledSoftwareInfo>();
        foreach (var root in new[] { "/opt/homebrew/Cellar", "/usr/local/Cellar" })
        {
            if (!System.IO.Directory.Exists(root)) continue;
            foreach (var formula in System.IO.Directory.EnumerateDirectories(root))
            {
                var name = System.IO.Path.GetFileName(formula);
                string version = "";
                try { version = System.IO.Directory.EnumerateDirectories(formula).Select(System.IO.Path.GetFileName).Where(x => x is not null).Cast<string>().OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? ""; } catch { }
                result.Add(new XPScriptInstalledSoftwareInfo(name, version, "", "", "", formula, name, "Homebrew", "Machine", root));
            }
        }
        return result.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ParseColonBlock(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? currentKey = null;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            if ((raw.StartsWith(' ') || raw.StartsWith('\t')) && currentKey is not null)
            {
                result[currentKey] = result[currentKey] + " " + raw.Trim();
                continue;
            }
            var colon = raw.IndexOf(':');
            if (colon <= 0) continue;
            currentKey = raw[..colon].Trim();
            result[currentKey] = raw[(colon + 1)..].Trim();
        }
        return result;
    }

    private static Dictionary<string, string> ParseEqualsFile(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in System.IO.File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('[')) continue;
            var equals = line.IndexOf('=');
            if (equals <= 0) continue;
            result[line[..equals].Trim()] = line[(equals + 1)..].Trim();
        }
        return result;
    }

    private static long ParseKb(string value)
    {
        var token = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "0";
        return long.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var kb) ? checked(kb * 1024L) : 0;
    }

    private static string GetMap(Dictionary<string, string> values, string key, string fallback) => values.TryGetValue(key, out var value) ? value : fallback;
    private static string ReadTextFile(string path) { try { return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path).Trim() : ""; } catch { return ""; } }
    private static string RegistryText(Microsoft.Win32.RegistryKey? key, string name) { try { return Convert.ToString(key?.GetValue(name), System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? ""; } catch { return ""; } }
    private static long RegistryLong(Microsoft.Win32.RegistryKey? key, string name) { try { return Convert.ToInt64(key?.GetValue(name) ?? 0, System.Globalization.CultureInfo.InvariantCulture); } catch { return 0; } }

    private static string RequiredText(object? value, string name)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length == 0) throw new XPScriptRuntimeException(5, "SystemInventory " + name + " cannot be empty.");
        return text;
    }

    private static string MacSysctlString(string name)
    {
        if (!System.OperatingSystem.IsMacOS()) return "";
        try
        {
            nuint length = 0;
            if (XPScriptSystemInventoryNative.sysctlbyname(name, IntPtr.Zero, ref length, IntPtr.Zero, 0) != 0 || length == 0) return "";
            var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(checked((int)length));
            try
            {
                if (XPScriptSystemInventoryNative.sysctlbyname(name, pointer, ref length, IntPtr.Zero, 0) != 0) return "";
                return (System.Runtime.InteropServices.Marshal.PtrToStringUTF8(pointer, checked((int)length)) ?? "").TrimEnd('\0');
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
        }
        catch { return ""; }
    }

    private static ulong MacSysctlUInt64(string name)
    {
        if (!System.OperatingSystem.IsMacOS()) return 0;
        try
        {
            ulong value = 0;
            nuint length = (nuint)sizeof(ulong);
            unsafe
            {
                if (XPScriptSystemInventoryNative.sysctlbyname(name, (IntPtr)(&value), ref length, IntPtr.Zero, 0) == 0) return value;
            }
        }
        catch { }
        return 0;
    }

    private static LSArray Pack(IEnumerable<object?> values)
    {
        var items = values.ToArray();
        if (items.Length == 0) return new LSArray("Variant", true);
        var result = new LSArray("Variant", true, [0], [items.Length - 1]);
        for (var i = 0; i < items.Length; i++) result.Set(items[i], i);
        return result;
    }
}

internal static class XPScriptNetworkArray
{
    public static LSArray PackStrings(IEnumerable<string> values)
    {
        var items = values.ToArray();
        if (items.Length == 0) return new LSArray("String", true);
        var result = new LSArray("String", true, [0], [items.Length - 1]);
        for (var i = 0; i < items.Length; i++) result.Set(items[i], i);
        return result;
    }
}

internal sealed record XPScriptSystemInventorySystemInfo(
    string MachineName, string OperatingSystem, string OsVersion, string OsArchitecture, string ProcessArchitecture,
    string RuntimeVersion, string Manufacturer, string Model, string SerialNumber, string BiosVendor, string BiosVersion,
    bool Is64BitOperatingSystem, bool Is64BitProcess, int LogicalProcessorCount);

internal sealed record XPScriptSystemInventoryCpuInfo(
    string Name, string Manufacturer, string Architecture, int PhysicalCores, int LogicalProcessors, long MaxClockMHz);

internal sealed record XPScriptSystemInventoryMemoryInfo(long TotalPhysicalMemory, long AvailablePhysicalMemory, long TotalSwap, long AvailableSwap);
internal sealed record XPScriptSystemInventoryPhysicalDiskInfo(string Name, string Model, string Manufacturer, string SerialNumber, long Size, bool IsRemovable, string Source);
internal sealed record XPScriptSystemInventoryVolumeInfo(string Name, string MountPoint, string DriveType, string FileSystem, long TotalSize, long FreeSpace);
internal sealed record XPScriptSystemInventoryGraphicsAdapterInfo(string Name, string Vendor, string DriverVersion, string DeviceId, string Source);
internal sealed record XPScriptSystemInventoryNetworkAdapterInfo(string Name, string Description, string Id, string Type, string Status, long Speed, string MacAddress, LSArray Addresses);
internal sealed record XPScriptInstalledSoftwareInfo(string Name, string Version, string Publisher, string Architecture, string InstallDate, string InstallLocation, string PackageId, string PackageManager, string Scope, string Source);
internal sealed record XPScriptSystemInventorySnapshot(
    XPScriptSystemInventorySystemInfo System, XPScriptSystemInventoryCpuInfo Cpu, XPScriptSystemInventoryMemoryInfo Memory,
    LSArray PhysicalDisks, LSArray Volumes, LSArray GraphicsAdapters, LSArray NetworkAdapters,
    LSArray InstalledApplications, LSArray InstalledPackages);

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
internal sealed class XPScriptMemoryStatusEx
{
    public uint dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<XPScriptMemoryStatusEx>();
    public uint dwMemoryLoad;
    public ulong ullTotalPhys;
    public ulong ullAvailPhys;
    public ulong ullTotalPageFile;
    public ulong ullAvailPageFile;
    public ulong ullTotalVirtual;
    public ulong ullAvailVirtual;
    public ulong ullAvailExtendedVirtual;
}

internal static class XPScriptSystemInventoryNative
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] XPScriptMemoryStatusEx lpBuffer);

    [System.Runtime.InteropServices.DllImport("libc", SetLastError = true)]
    public static extern int sysctlbyname(string name, IntPtr oldp, ref nuint oldlenp, IntPtr newp, nuint newlen);
}
""";
}
