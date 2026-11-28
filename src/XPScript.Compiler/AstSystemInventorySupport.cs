using XPScript.Compiler.Binding;
using Member = XPScript.Compiler.AstNativeObjectSurface.Member;

namespace XPScript.Compiler;

/// <summary>Typed AST metadata for the existing SystemInventory runtime.</summary>
internal static class AstSystemInventorySupport
{
    internal static readonly string[] Names =
    [
        "SystemInventory", "SystemInventorySnapshot", "SystemInventorySystemInfo", "SystemInventoryCpuInfo",
        "SystemInventoryMemoryInfo", "SystemInventoryPhysicalDiskInfo", "SystemInventoryVolumeInfo",
        "SystemInventoryGraphicsAdapterInfo", "SystemInventoryNetworkAdapterInfo", "InstalledSoftwareInfo"
    ];

    internal static readonly Member[] Members =
    [
        new("SystemInventory", "GetSnapshot", "SystemInventorySnapshot", [], [], false, false, false),
        new("SystemInventory", "GetSystemInfo", "SystemInventorySystemInfo", [], [], false, false, false),
        new("SystemInventory", "GetCpuInfo", "SystemInventoryCpuInfo", [], [], false, false, false),
        new("SystemInventory", "GetMemoryInfo", "SystemInventoryMemoryInfo", [], [], false, false, false),
        new("SystemInventory", "GetPhysicalDisks", "object", [], [], false, false, false),
        new("SystemInventory", "GetVolumes", "object", [], [], false, false, false),
        new("SystemInventory", "GetGraphicsAdapters", "object", [], [], false, false, false),
        new("SystemInventory", "GetNetworkAdapters", "object", [], [], false, false, false),
        new("SystemInventory", "GetInstalledApplications", "object", [], [], false, false, false),
        new("SystemInventory", "GetInstalledPackages", "object", [], [], false, false, false),
        new("SystemInventory", "FindInstalledSoftware", "object", ["object"], ["name"], false, false, false),
        new("SystemInventory", "IsSoftwareInstalled", "bool", ["object"], ["name"], false, false, false),
        ..Properties("SystemInventorySnapshot", "System", "SystemInventorySystemInfo", "Cpu", "SystemInventoryCpuInfo", "Memory", "SystemInventoryMemoryInfo", "PhysicalDisks", "object", "Volumes", "object", "GraphicsAdapters", "object", "NetworkAdapters", "object", "InstalledApplications", "object", "InstalledPackages", "object"),
        ..Properties("SystemInventorySystemInfo", "MachineName", "string", "OperatingSystem", "string", "OsVersion", "string", "OsArchitecture", "string", "ProcessArchitecture", "string", "RuntimeVersion", "string", "Manufacturer", "string", "Model", "string", "SerialNumber", "string", "BiosVendor", "string", "BiosVersion", "string", "Is64BitOperatingSystem", "bool", "Is64BitProcess", "bool", "LogicalProcessorCount", "int"),
        ..Properties("SystemInventoryCpuInfo", "Name", "string", "Manufacturer", "string", "Architecture", "string", "PhysicalCores", "int", "LogicalProcessors", "int", "MaxClockMHz", "long"),
        ..Properties("SystemInventoryMemoryInfo", "TotalPhysicalMemory", "long", "AvailablePhysicalMemory", "long", "TotalSwap", "long", "AvailableSwap", "long"),
        ..Properties("SystemInventoryPhysicalDiskInfo", "Name", "string", "Model", "string", "Manufacturer", "string", "SerialNumber", "string", "Size", "long", "IsRemovable", "bool", "Source", "string"),
        ..Properties("SystemInventoryVolumeInfo", "Name", "string", "MountPoint", "string", "DriveType", "string", "FileSystem", "string", "TotalSize", "long", "FreeSpace", "long"),
        ..Properties("SystemInventoryGraphicsAdapterInfo", "Name", "string", "Vendor", "string", "DriverVersion", "string", "DeviceId", "string", "Source", "string"),
        ..Properties("SystemInventoryNetworkAdapterInfo", "Name", "string", "Description", "string", "Id", "string", "Type", "string", "Status", "string", "Speed", "long", "MacAddress", "string", "Addresses", "object"),
        ..Properties("InstalledSoftwareInfo", "Name", "string", "Version", "string", "Publisher", "string", "Architecture", "string", "InstallDate", "string", "InstallLocation", "string", "PackageId", "string", "PackageManager", "string", "Scope", "string", "Source", "string")
    ];

    private static IEnumerable<Member> Properties(string owner, params string[] values)
    {
        for (var i = 0; i + 1 < values.Length; i += 2)
            yield return new(owner, values[i], values[i + 1], [], [], false, true, false);
    }

    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" or "byte[]" => "Variant", "int" => "Integer", "long" => "Long",
        "string" => "String", "bool" => "Boolean", _ => type.TrimEnd('?')
    };

    internal static bool Uses(string source) => Names.Any(name => AstObjectSupportRegistry.UsesObject(source, name));
    internal static string? RuntimeType(string name) => Names.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } canonical
        ? "XPScript" + canonical : null;
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
}
