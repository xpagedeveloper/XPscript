# Application Registry and Secrets

`Application.Registry` provides one XPscript API for persistent application settings across Windows, Linux and macOS. `Application.Secrets` provides a separate API for credentials and other secrets that must be stored in the operating system credential store rather than in configuration files.

## Registry stores

Two stores are available:

- `Application.Registry.User` for settings owned by the current user.
- `Application.Registry.System` for machine-wide settings. Writing system settings normally requires elevated operating-system permissions.

The platform mapping is:

| XPscript store | Windows | Linux | macOS |
|---|---|---|---|
| `Application.Registry.User` | `HKEY_CURRENT_USER` | `~/.config/<app>/` | `~/Library/Preferences/<app>/` |
| `Application.Registry.System` | `HKEY_LOCAL_MACHINE` | `/etc/<app>/` | `/Library/Preferences/<app>/` |

On Linux and macOS, `<app>` is derived from `Application.ExecutableFileName` without the file extension. Registry key path segments and value names are encoded below that application directory so arbitrary names cannot escape the application storage root.

### Get

```xpscript
value = Application.Registry.User.Get(path, name)
```

`path` is relative to the selected registry root. On Windows `/` and `\` are both accepted as separators and map to registry key separators. If the key or value does not exist, `Get` returns an empty Variant value.

Windows example:

```xpscript
Dim value As Variant
value = Application.Registry.User.Get( _
    "Microsoft/Windows/CurrentVersion/Explorer/StartupApproved/Run", _
    "OneDrive")
```

The path is used exactly relative to `HKCU` or `HKLM`; XPscript does not automatically prepend `Software`.

### Set with inferred type

```xpscript
Call Application.Registry.User.Set(path, name, value)
```

Without an explicit type XPscript infers the registry type:

| XPscript value | Registry type |
|---|---|
| String and other values | `String` |
| Boolean, Byte, Integer | `DWord` |
| Long | `QWord` |
| Byte array | `Binary` |
| String array | `MultiString` |

Example:

```xpscript
Call Application.Registry.User.Set( _
    "Software/MyCompany/MyApp", _
    "WindowWidth", _
    1200)
```

### Set with explicit type

An optional fourth parameter selects the storage type explicitly:

```xpscript
Call Application.Registry.User.Set(path, name, value, type)
```

Supported types are:

- `String`
- `ExpandString`
- `Binary`
- `DWord`
- `MultiString`
- `QWord`

For example:

```xpscript
Call Application.Registry.User.Set( _
    "Software/MyCompany/MyApp", _
    "InstallPath", _
    "%LOCALAPPDATA%/MyApp", _
    "ExpandString")
```

```xpscript
Dim data(0 To 2) As Byte
data(0) = 1
data(1) = 2
data(2) = 255
Call Application.Registry.User.Set("Software/MyCompany/MyApp", "BinaryData", data, "Binary")
```

```xpscript
Dim servers(0 To 1) As String
servers(0) = "server-a"
servers(1) = "server-b"
Call Application.Registry.User.Set("Software/MyCompany/MyApp", "Servers", servers, "MultiString")
```

On Windows the requested type is stored as the corresponding native Registry value kind. On Linux and macOS XPscript stores equivalent type metadata with the value, so values retain their XPscript type when read back.

## Secrets

The API is intentionally separate from `Application.Registry`. Secrets must not be placed in normal registry/config files.

### Set

```xpscript
Call Application.Secrets.Set(service, account, secret)
```

### Get

```xpscript
secret = Application.Secrets.Get(service, account)
```

Example:

```xpscript
Call Application.Secrets.Set("MyApplication", "ApiUser", "very-secret-value")

Dim secret As String
secret = Application.Secrets.Get("MyApplication", "ApiUser")
```

The platform mapping is:

| Platform | Backend |
|---|---|
| Windows | Windows Credential Manager, Generic Credentials |
| macOS | macOS Keychain, generic password item |
| Linux | freedesktop Secret Service through `secret-tool` / libsecret |

A missing credential returns an empty string. Other credential-store failures raise an XPscript runtime error.

### Linux requirements

Linux secret storage requires `secret-tool` from libsecret and an available Secret Service provider, for example GNOME Keyring or another compatible provider. The secret value is passed to `secret-tool` through standard input and is never included in the process command line.

A graphical Linux desktop commonly provides a Secret Service session automatically. A headless server, including a Domino server, may not have a D-Bus user session or Secret Service provider. In that case `Application.Secrets.Get` and `.Set` return a clear runtime error rather than falling back to an unencrypted file.

## Security and permissions

`Application.Registry.System.Set` can require administrator/root permissions. XPscript does not elevate the process automatically.

`Application.Secrets` uses the access-control behavior of the operating system credential store. macOS Keychain can prompt the user depending on Keychain access policy. Windows Credential Manager and Linux Secret Service likewise use the current user's credential-store context.

The runnable registry sample and compile-time secrets surface are in [samples/application-registry-secrets.xps](../samples/application-registry-secrets.xps).
