# Hash functions

XPScript includes built-in string hashing and HMAC functions.

All functions convert the input string to UTF-8 bytes and return lowercase hexadecimal text.

## Hash functions

```xpscript
Dim digest As String

digest = MD5("abc")
digest = SHA1("abc")
digest = SHA256("abc")
digest = SHA384("abc")
digest = SHA512("abc")
```

Return lengths:

- `MD5` returns 32 hexadecimal characters.
- `SHA1` returns 40 hexadecimal characters.
- `SHA256` returns 64 hexadecimal characters.
- `SHA384` returns 96 hexadecimal characters.
- `SHA512` returns 128 hexadecimal characters.

`MD5` and `SHA1` are included for compatibility with existing systems. Do not use them for new security-sensitive protocols.

Do not use any of these direct hash functions for password storage. Passwords should use a dedicated password hashing or key-derivation function with salt and an appropriate work factor.

## HMAC functions

HMAC functions are suitable for keyed integrity checks, API request signing and webhook signature verification when the external protocol specifies the same algorithm and canonical input format.

```xpscript
Dim signature As String

signature = HMACSHA256(payload, secret)
signature = HMACSHA384(payload, secret)
signature = HMACSHA512(payload, secret)
```

Both the value and key are encoded as UTF-8 before hashing. The result is lowercase hexadecimal text.

## Example

```xpscript
Dim payload As String
Dim secret As String
Dim signature As String

payload = "customer=42&amount=100"
secret = "shared-secret"

signature = HMACSHA256(payload, secret)
Print signature
```

When verifying signatures received from another system, make sure the remote system uses the same byte encoding, canonicalization rules and output format.
