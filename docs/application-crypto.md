# Application.Crypto

`Application.Crypto` provides authenticated string encryption for normal Windows, Linux and macOS applications and server-side web code. It is intentionally unavailable in browser-WASM client code because the generated .NET browser runtime does not provide `AesGcm`.

The API uses a versioned, self-describing XPscript envelope. Applications should store the complete returned string without changing it. Decryption reads the algorithm and parameters from the envelope, so future XPscript releases can adopt new defaults while retaining readers for existing values.

## Password-based encryption

Use `Encrypt` and `Decrypt` when the caller has a password or passphrase:

```xpscript
Dim encrypted As String
Dim plaintext As String

encrypted = Application.Crypto.Encrypt("secret value", "a-long-unique-password")
plaintext = Application.Crypto.Decrypt(encrypted, "a-long-unique-password")
```

The default and currently supported password profile is `A256GCM-PBKDF2-SHA256`. Applications normally omit the profile so XPscript can select the current secure default. The explicit overload exists for compatibility:

```xpscript
encrypted = Application.Crypto.Encrypt( _
    "secret value", _
    "a-long-unique-password", _
    "A256GCM-PBKDF2-SHA256")
```

Version 1 uses PBKDF2-HMAC-SHA256 with 600,000 iterations, a random 256-bit salt, AES-256-GCM, a random 96-bit nonce and a 128-bit authentication tag. The work factor is stored inside the authenticated envelope.

## Key-based encryption

Use a generated 256-bit key when the application already has a protected place to store cryptographic key material:

```xpscript
Dim key As String
Dim encrypted As String

key = Application.Crypto.GenerateKey()
encrypted = Application.Crypto.EncryptWithKey("secret value", key)
Print Application.Crypto.DecryptWithKey(encrypted, key)
```

Keys use standard Base64 text and decode to exactly 32 bytes. `EncryptWithKey` does not accept passwords and `Encrypt` does not treat a password as a finished AES key. Keep generated keys in `Application.Secrets`, a platform key store, KMS, HSM or another protected secret source. Do not store a key next to the value it encrypts.

## Bind ciphertext to a context

The optional context is authenticated but not stored in the envelope. Decryption requires the identical value:

```xpscript
encrypted = Application.Crypto.Encrypt( _
    token, _
    password, _
    "A256GCM-PBKDF2-SHA256", _
    "tenant:42:oauth-token")

token = Application.Crypto.Decrypt( _
    encrypted, _
    password, _
    "tenant:42:oauth-token")
```

Use a stable context to prevent a valid encrypted value from being copied into another logical field or tenant. Changing the context causes authenticated decryption to fail.

## Inspect and migrate

```xpscript
If Application.Crypto.IsEncrypted(value) Then
    Print Application.Crypto.Algorithm(value)
    Print CStr(Application.Crypto.Version(value))

    If Application.Crypto.NeedsUpgrade(value) Then
        value = Application.Crypto.ReEncrypt(value, oldPassword, newPassword)
    End If
End If
```

`IsEncrypted` recognizes the XPscript envelope prefix. `Algorithm` and `Version` validate and inspect the complete envelope. `NeedsUpgrade` reports whether the value uses an older supported profile or weaker parameters. `ReEncrypt` decrypts a password-based value and writes a new envelope with the current default profile.

## Envelope and compatibility policy

The textual format starts with `XPSENC$1$` followed by an unpadded Base64URL payload. Version 1 records and authenticates:

- format version
- KDF identifier and work factor
- cipher identifier
- salt
- nonce
- authentication-tag length
- ciphertext length
- ciphertext

XPscript writes new values with its current default profile. Published envelope identifiers never change meaning. Future releases must retain readers for previously supported envelope versions and use new identifiers for new formats or algorithms.

Malformed values, unsupported versions, unreasonable parameters and truncated payloads fail before decryption. An incorrect password or key, changed context and modified ciphertext use the same authentication-failure category.

Plaintext is UTF-8 and limited to 16 MiB. Context is limited to 64 KiB of UTF-8. Mutable plaintext and key byte buffers are cleared where .NET permits it. Managed `String` values cannot be reliably cleared.

## Password storage

`Application.Crypto.Encrypt` is reversible and must not be used to store login passwords. Login-password storage needs a dedicated one-way password hashing and verification API.

