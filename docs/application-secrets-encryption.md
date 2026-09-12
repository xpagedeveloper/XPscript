# Application.Secrets value encryption

`Application.Secrets` can optionally add an application-level encryption layer before a value is written to the operating-system credential store.

`Application.Id` is still mandatory and continues to provide the credential namespace.

## Normal credential-store protection

The existing API is unchanged:

```xpscript
Application.Id = "com.example.accounting"
Call Application.Secrets.Set("Database", "Production", "secret-value")
Dim secret As String
secret = Application.Secrets.Get("Database", "Production")
```

In this mode the operating system protects the value using Windows Credential Manager, macOS Keychain or Linux Secret Service.

## Additional value encryption

Supply a fourth argument to `Set` and a third argument to `Get` to encrypt the value before it reaches the native credential store:

```xpscript
Application.Id = "com.example.accounting"

Call Application.Secrets.Set( _
    "Database", _
    "Production", _
    "secret-value", _
    "a-long-unique-encryption-password")

Dim secret As String
secret = Application.Secrets.Get( _
    "Database", _
    "Production", _
    "a-long-unique-encryption-password")
```

The additional encryption layer uses only cryptography built into .NET:

- PBKDF2-HMAC-SHA256
- 600,000 PBKDF2 iterations
- random 256-bit salt for every stored value
- AES-256-GCM
- unique 96-bit nonce for every stored value
- 128-bit authentication tag

The encrypted envelope is versioned so future XPscript versions can introduce new algorithms without making existing encrypted credentials unreadable.

## Binding to the credential identity

AES-GCM authenticated data binds the encrypted value to:

- `Application.Id`
- `service`
- `account`
- the encrypted-value format version

Copying the encrypted native credential value to another application ID, service or account therefore causes authentication to fail during decryption.

## Fail-closed behavior

An additionally encrypted credential cannot be read with the two-argument `Get` call. XPscript raises a runtime error instructing the application to supply the encryption password.

Likewise, the password-aware `Get(service, account, password)` rejects an existing unencrypted credential rather than silently returning it. To migrate an old credential, write it again with the four-argument `Set` overload.

A missing credential still returns an empty string.

Wrong passwords, modified ciphertext and credentials moved to another application/service/account produce the same decryption failure category rather than revealing which condition occurred.

## Backup and restore

`Application.Secrets.Backup` does not decrypt additionally encrypted credentials. The encrypted credential envelope is stored inside the already encrypted `.xpssecrets` backup payload.

This means the credential encryption password and the backup password remain independent protection layers:

```text
secret value
    -> credential AES-256-GCM encryption
    -> operating-system credential store
    -> .xpssecrets backup AES-256-GCM encryption
```

After restore, the original credential encryption password is still required to read an additionally encrypted credential.

## Security notes

Do not derive the encryption password from `Application.Id`. The application ID is an identifier, not a secret.

Do not hard-code the encryption password in source code for production applications. Obtain it from an appropriate protected source or from the user when needed.

The operating-system credential store remains the primary storage security boundary. This optional layer is useful when an application also wants stored credential values to remain opaque if the native entry is inspected or exported by another process with access to the user's credential store.

Managed .NET `String` instances cannot be reliably zeroed. XPscript clears mutable derived-key and plaintext byte buffers with `CryptographicOperations.ZeroMemory` where possible.

Native credential stores can impose lower maximum value sizes than XPscript's encrypted-envelope parser. If the platform rejects an oversized encrypted value, the native credential-store error is returned.
