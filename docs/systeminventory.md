# SystemInventory

`SystemInventory` provides read-only host inventory for XPscript applications. It is intended for enterprise inventory, diagnostics and compliance checks. The implementation uses native .NET 10 APIs and OS metadata sources and does not add any third-party NuGet package.

`SystemInventory` is not available for `browser-wasm` because browser sandboxes do not expose host hardware, registry, package databases or machine-wide inventory APIs.

## Example

```vb
Dim inventory As New SystemInventory
Dim systemInfo As SystemInventorySystemInfo
Dim cpu As SystemInventoryCpuInfo
Dim memory As SystemInventoryMemoryInfo

Set systemInfo = inventory.GetSystemInfo()
Set cpu = inventory.GetCpuInfo()
Set memory = inventory.GetMemoryInfo()

Print systemInfo.Manufacturer & " " & systemInfo.Model
Print cpu.Name
Print CStr(memory.TotalPhysicalMemory)
```

## API

### `GetSnapshot()`

Returns a `SystemInventorySnapshot` containing:

- `System`
- `Cpu`
- `Memory`
- `PhysicalDisks`
- `Volumes`
- `GraphicsAdapters`
- `NetworkAdapters`
- `InstalledApplications`
- `InstalledPackages`

### `GetSystemInfo()`

Returns `SystemInventorySystemInfo`:

- `MachineName`
- `OperatingSystem`
- `OsVersion`
- `OsArchitecture`
- `ProcessArchitecture`
- `RuntimeVersion`
- `Manufacturer`
- `Model`
- `SerialNumber`
- `BiosVendor`
- `BiosVersion`
- `Is64BitOperatingSystem`
- `Is64BitProcess`
- `LogicalProcessorCount`

The cross-platform base values come from `Environment` and `RuntimeInformation`. Windows hardware identity is read from the BIOS registry key. Linux hardware identity is read from `/sys/class/dmi/id`. macOS model information is read with `sysctlbyname`.

### `GetCpuInfo()`

Returns `SystemInventoryCpuInfo`:

- `Name`
- `Manufacturer`
- `Architecture`
- `PhysicalCores`
- `LogicalProcessors`
- `MaxClockMHz`

Windows uses `HARDWARE\\DESCRIPTION\\System\\CentralProcessor`. Linux uses `/proc/cpuinfo`. macOS uses `sysctlbyname`.

`PhysicalCores` may be `0` when the host platform does not expose a reliable value through the provider currently implemented.

### `GetMemoryInfo()`

Returns `SystemInventoryMemoryInfo`:

- `TotalPhysicalMemory`
- `AvailablePhysicalMemory`
- `TotalSwap`
- `AvailableSwap`

All sizes are bytes.

Windows uses `GlobalMemoryStatusEx`. Linux uses `/proc/meminfo`. macOS uses `hw.memsize`. When the native provider cannot return total memory, the .NET GC memory limit is used as a conservative fallback.

### `GetPhysicalDisks()`

Returns an array of `SystemInventoryPhysicalDiskInfo`:

- `Name`
- `Model`
- `Manufacturer`
- `SerialNumber`
- `Size`
- `IsRemovable`
- `Source`

The initial native implementation exposes physical block devices on Linux through `/sys/block`. Windows and macOS return an empty physical-disk array until a provider can expose the same semantics without shell commands or an additional dependency. Use `GetVolumes()` for portable storage capacity information.

### `GetVolumes()`

Returns `SystemInventoryVolumeInfo` using `System.IO.DriveInfo`:

- `Name`
- `MountPoint`
- `DriveType`
- `FileSystem`
- `TotalSize`
- `FreeSpace`

This works on Windows, Linux and macOS.

### `GetGraphicsAdapters()`

Returns `SystemInventoryGraphicsAdapterInfo`:

- `Name`
- `Vendor`
- `DriverVersion`
- `DeviceId`
- `Source`

Windows currently uses display adapter registry metadata. Linux uses `/sys/class/drm`. macOS currently returns an empty array because complete GPU inventory would require an IOKit provider or invoking `system_profiler`; XPscript does not invoke that command.

### `GetNetworkAdapters()`

Returns `SystemInventoryNetworkAdapterInfo`:

- `Name`
- `Description`
- `Id`
- `Type`
- `Status`
- `Speed`
- `MacAddress`
- `Addresses`

This uses `System.Net.NetworkInformation.NetworkInterface` and works across supported desktop/server platforms.

## Installed software

### `GetInstalledApplications()`

Returns user-facing applications as `InstalledSoftwareInfo` entries.

### `GetInstalledPackages()`

Returns package-manager or machine package entries as `InstalledSoftwareInfo` entries.

### `FindInstalledSoftware(name)`

Returns installed application/package entries where `Name` or `PackageId` contains the supplied text, case-insensitively.

### `IsSoftwareInstalled(name)`

Returns `True` when an installed item has an exact case-insensitive match on `Name` or `PackageId`.

### `InstalledSoftwareInfo`

Properties:

- `Name`
- `Version`
- `Publisher`
- `Architecture`
- `InstallDate`
- `InstallLocation`
- `PackageId`
- `PackageManager`
- `Scope`
- `Source`

Not every package source exposes every property. Missing metadata is returned as an empty string rather than inferred.

## Software providers

### Windows

Applications and packages are read from the uninstall registry keys in both 32-bit and 64-bit registry views and for both machine and current-user scope. `Win32_Product` is deliberately not used because querying it can be slow and can trigger Windows Installer consistency checks/repairs.

`PackageManager` is `WindowsRegistry`.

### Linux

The initial provider supports:

- desktop applications from `/usr/share/applications`, `/usr/local/share/applications` and `~/.local/share/applications`
- Debian/Ubuntu packages from `/var/lib/dpkg/status`
- Snap installations by inspecting `/snap`
- Flatpak application roots under machine and user storage

`rpm`/DNF package databases are not parsed in the initial implementation because there is no stable common .NET BCL API for RPM metadata and XPscript deliberately does not shell out to `rpm` or `dnf`.

### macOS

Applications are discovered from `.app` bundles in `/Applications`, `/System/Applications` and the current user's `Applications` folder. XML `Info.plist` files are read for bundle name, version and identifier when available. Binary plist metadata is left empty rather than invoking `plutil`.

Homebrew packages are discovered by inspecting `/opt/homebrew/Cellar` and `/usr/local/Cellar`.

## Design and security notes

`SystemInventory` is read-only. It does not install, uninstall or modify software and it does not change hardware or OS configuration.

The object deliberately avoids shelling out to tools such as `wmic`, `system_profiler`, `lscpu`, `lsblk`, `dpkg-query`, `rpm`, `snap` or `flatpak`. This keeps behavior deterministic and avoids command-line parsing and command-injection surfaces.

Some inventory fields may require elevated OS permissions or may be hidden by virtual machines, containers, endpoint-security products or privacy controls. Such values are returned empty rather than causing the whole inventory request to fail.
