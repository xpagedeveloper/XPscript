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

### Application identity is required

`Application.Secrets` cannot use the operating-system credential store until `Application.Id` has been set. There is no executable-name fallback.

Use a stable identifier that belongs to the application, preferably a reverse-domain style value:

```xpscript
Application.Id = "com.example.accounting"
```

`Application.Id` must be set before the first call to `Application.Secrets.Get`, `.Set`, `.Backup` or `.Restore`. Once the secrets API has been used, changing `Application.Id` during the same process is rejected.

The application ID is part of XPscript's internal credential namespace. Two applications can therefore use the same service and account names without sharing credentials:

```text
com.example.accounting + Database + Production
com.example.hr         + Database + Production
```

These resolve to different native credential-store entries. The public `service` and `account` values are not expected to contain the application ID themselves.

XPscript does not expose an API for enumerating arbitrary credentials from Windows Credential Manager, macOS Keychain or Linux Secret Service. `Application.Secrets` operates only on credentials addressed through the current `Application.Id` namespace.

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
Application.Id = "com.example.myapplication"
Call Application.Secrets.Set("Database", "ApiUser", "very-secret-value")

Dim secret As String
secret = Application.Secrets.Get("Database", "ApiUser")
```

The platform mapping is:

| Platform | Backend |
|---|---|
| Windows | Windows Credential Manager, Generic Credentials |
| macOS | macOS Keychain, generic password item |
| Linux | freedesktop Secret Service through `secret-tool` / libsecret |

A missing credential returns an empty string. Other credential-store failures raise an XPscript runtime error.

XPscript keeps a metadata-only index for each `Application.Id` of credentials accessed through `Application.Secrets.Get` or written through `Application.Secrets.Set`. The index contains only the public service and account identifiers. Secret values remain in the operating-system credential store.

### Encrypted backup

Use `Backup` to create a password-protected `.xpssecrets` file containing the XPscript-managed credentials belonging to the current `Application.Id`:

```xpscript
Application.Id = "com.example.myapplication"
Call Application.Secrets.Backup("application.xpssecrets", "a-long-unique-backup-password")
```

The backup format is versioned and algorithm-agile. Version 1 uses only cryptography built into .NET:

- PBKDF2-HMAC-SHA256 for password-based key derivation.
- A random 256-bit salt.
- 600,000 PBKDF2 iterations.
- AES-256-GCM authenticated encryption.
- A unique 96-bit nonce for each backup.
- A 128-bit authentication tag.
- Authenticated format and KDF metadata so tampering is detected.

The application ID, service name, account name and secret value are all inside the encrypted payload. The backup file does not expose those identifiers in plaintext.

The file stores the format version, algorithm identifiers and KDF parameters needed to restore old backups if XPscript adopts newer cryptographic defaults in the future.

Backup files are limited to 16 MiB and 10,000 credential entries.

### Restore

Restore imports all credentials from a valid backup into the current operating-system credential store:

```xpscript
Application.Id = "com.example.myapplication"
Call Application.Secrets.Restore("application.xpssecrets", "a-long-unique-backup-password")
```

The encrypted backup contains its source `Application.Id`. Restore rejects the file if that ID differs from the currently configured `Application.Id`. This prevents a backup from one XPscript application from being accidentally imported into another application's credential namespace.

Existing credentials with the same service/account identifiers inside the same application namespace are updated using the normal `Application.Secrets.Set` behavior of the platform backend. Restored credentials are added to that application's managed-secret index.

Restore rejects unsupported format versions, unsupported algorithms, malformed or truncated files, unreasonable KDF parameters, invalid entry counts, modified ciphertext and incorrect passwords. Authentication failure is reported without distinguishing between a wrong password and a modified backup.

A backup is portable across Windows, Linux and macOS because the `.xpssecrets` container is independent of the native credential-store format. Restore writes each credential through the destination platform's normal credential-store backend.

Treat `.xpssecrets` files as sensitive encrypted backups. Use a long unique password and keep a separate protected copy of that password. If the password is lost, XPscript cannot recover the encrypted secrets.

### Linux requirements

Linux secret storage requires `secret-tool` from libsecret and an available Secret Service provider, for example GNOME Keyring or another compatible provider. The secret value is passed to `secret-tool` through standard input and is never included in the process command line.

A graphical Linux desktop commonly provides a Secret Service session automatically. A headless server, including a Domino server, may not have a D-Bus user session or Secret Service provider. In that case `Application.Secrets.Get`, `.Set`, `.Backup` and `.Restore` return a clear runtime error when the operation needs the unavailable credential store rather than falling back to an unencrypted file.

## Security and permissions

`Application.Registry.System.Set` can require administrator/root permissions. XPscript does not elevate the process automatically.

`Application.Secrets` uses the access-control behavior of the operating system credential store in addition to XPscript's `Application.Id` namespace. macOS Keychain can prompt the user depending on Keychain access policy. Windows Credential Manager and Linux Secret Service likewise use the current user's credential-store context.

`Application.Id` provides XPscript application isolation, but it is not intended to replace operating-system user/process security. Another program running with sufficient privileges as the same operating-system user may still be able to access that user's native credential store according to the platform's security model.

Secrets are never deliberately written to the XPscript metadata index. Backup encryption keys and decrypted payload byte buffers are cleared with `CryptographicOperations.ZeroMemory` after use where .NET exposes mutable buffers. Managed `String` values cannot be reliably zeroed by an application and should therefore not be retained longer than necessary.

The runnable registry sample and compile-time secrets surface are in [samples/application-registry-secrets.xps](../samples/application-registry-secrets.xps).
