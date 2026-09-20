namespace XPScript.Compiler;

internal static class ApplicationCryptoRuntimeSource
{
    public const string Code = """
internal static class XPScriptApplicationCryptoRuntime
{
    private const string Prefix = "XPSENC$1$";
    private const string PrefixRoot = "XPSENC$";
    private const byte FormatVersion = 1;
    private const byte KdfNone = 0;
    private const byte KdfPbkdf2Sha256 = 1;
    private const byte CipherAes256Gcm = 1;
    private const int DefaultIterations = 600000;
    private const int KeySize = 32;
    private const int SaltSize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int FixedHeaderSize = 20;
    private const int MaxPlaintextBytes = 16 * 1024 * 1024;
    private const int MaxEnvelopeBytes = MaxPlaintextBytes + 1024;
    private const int MaxContextBytes = 64 * 1024;
    private static readonly byte[] Magic = System.Text.Encoding.ASCII.GetBytes("XPSC");
    private static readonly byte[] AadDomain = System.Text.Encoding.ASCII.GetBytes("XPScript.Application.Crypto\0");
    private static readonly System.Text.UTF8Encoding StrictUtf8 = new(false, true);

    public static string Encrypt(object? value, object? password)
        => Encrypt(value, password, "A256GCM-PBKDF2-SHA256", "");

    public static string Encrypt(object? value, object? password, object? algorithm)
        => Encrypt(value, password, algorithm, "");

    public static string Encrypt(object? value, object? password, object? algorithm, object? context)
    {
        RequireAesGcm();
        var algorithmName = NormalizeAlgorithm(algorithm);
        if (algorithmName is not ("DEFAULT" or "A256GCM-PBKDF2-SHA256"))
            throw new XPScriptRuntimeException(5, "Unsupported Application.Crypto password encryption algorithm: " + algorithmName + ".");

        var passwordText = RequiredPassword(password);
        var plaintext = EncodePlaintext(value);
        var contextText = NormalizeContext(context);
        var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(SaltSize);
        byte[]? key = null;
        try
        {
            key = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                passwordText,
                salt,
                DefaultIterations,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                KeySize);
            return EncryptCore(plaintext, key, KdfPbkdf2Sha256, DefaultIterations, salt, contextText);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext);
            if (key is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        }
    }

    public static string Decrypt(object? value, object? password)
        => Decrypt(value, password, "");

    public static string Decrypt(object? value, object? password, object? context)
    {
        RequireAesGcm();
        var parsed = Parse(value);
        if (parsed.Kdf != KdfPbkdf2Sha256)
            throw new XPScriptRuntimeException(5, "Application.Crypto value requires DecryptWithKey.");

        var passwordText = RequiredPassword(password);
        byte[]? key = null;
        try
        {
            key = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                passwordText,
                parsed.Salt,
                parsed.WorkFactor,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                KeySize);
            return DecryptCore(parsed, key, NormalizeContext(context));
        }
        finally
        {
            if (key is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        }
    }

    public static string GenerateKey()
    {
        var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(KeySize);
        try { return System.Convert.ToBase64String(key); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(key); }
    }

    public static string EncryptWithKey(object? value, object? base64Key)
        => EncryptWithKey(value, base64Key, "");

    public static string EncryptWithKey(object? value, object? base64Key, object? context)
    {
        RequireAesGcm();
        var key = DecodeKey(base64Key);
        var plaintext = EncodePlaintext(value);
        try
        {
            return EncryptCore(plaintext, key, KdfNone, 0, [], NormalizeContext(context));
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        }
    }

    public static string DecryptWithKey(object? value, object? base64Key)
        => DecryptWithKey(value, base64Key, "");

    public static string DecryptWithKey(object? value, object? base64Key, object? context)
    {
        RequireAesGcm();
        var parsed = Parse(value);
        if (parsed.Kdf != KdfNone)
            throw new XPScriptRuntimeException(5, "Application.Crypto value requires password-based Decrypt.");

        var key = DecodeKey(base64Key);
        try { return DecryptCore(parsed, key, NormalizeContext(context)); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(key); }
    }

    public static bool IsEncrypted(object? value)
        => XPScriptRuntime.CStr(value).StartsWith(PrefixRoot, StringComparison.Ordinal);

    public static string Algorithm(object? value)
    {
        var parsed = Parse(value);
        return parsed.Kdf switch
        {
            KdfPbkdf2Sha256 => "A256GCM-PBKDF2-SHA256",
            KdfNone => "A256GCM-KEY",
            _ => throw new XPScriptRuntimeException(5, "Unsupported Application.Crypto key derivation algorithm.")
        };
    }

    public static int Version(object? value) => Parse(value).Version;

    public static bool NeedsUpgrade(object? value)
    {
        var parsed = Parse(value);
        if (parsed.Version != FormatVersion || parsed.Cipher != CipherAes256Gcm) return true;
        return parsed.Kdf == KdfPbkdf2Sha256 && parsed.WorkFactor < DefaultIterations;
    }

    public static string ReEncrypt(object? value, object? oldPassword, object? newPassword)
        => ReEncrypt(value, oldPassword, newPassword, "");

    public static string ReEncrypt(object? value, object? oldPassword, object? newPassword, object? context)
    {
        var plaintext = Decrypt(value, oldPassword, context);
        return Encrypt(plaintext, newPassword, "A256GCM-PBKDF2-SHA256", context);
    }

    private static string EncryptCore(byte[] plaintext, byte[] key, byte kdf, int workFactor, byte[] salt, string context)
    {
        var nonce = System.Security.Cryptography.RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        var header = BuildHeader(kdf, workFactor, salt, nonce, plaintext.Length);
        var aad = BuildAad(header, context);
        try
        {
            using (var aes = new System.Security.Cryptography.AesGcm(key, TagSize))
                aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);

            var envelope = new byte[header.Length + tag.Length + ciphertext.Length];
            System.Buffer.BlockCopy(header, 0, envelope, 0, header.Length);
            System.Buffer.BlockCopy(tag, 0, envelope, header.Length, tag.Length);
            System.Buffer.BlockCopy(ciphertext, 0, envelope, header.Length + tag.Length, ciphertext.Length);
            if (envelope.Length > MaxEnvelopeBytes)
                throw new XPScriptRuntimeException(5, "Application.Crypto encrypted value exceeds the maximum supported size.");
            return Prefix + Base64UrlEncode(envelope);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(aad);
        }
    }

    private static string DecryptCore(ParsedEnvelope parsed, byte[] key, string context)
    {
        var plaintext = new byte[parsed.Ciphertext.Length];
        var aad = BuildAad(parsed.Header, context);
        try
        {
            try
            {
                using var aes = new System.Security.Cryptography.AesGcm(key, parsed.Tag.Length);
                aes.Decrypt(parsed.Nonce, parsed.Ciphertext, parsed.Tag, plaintext, aad);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                throw new XPScriptRuntimeException(5, "Application.Crypto decryption failed: the secret is incorrect, the context does not match, or the encrypted value was modified.");
            }

            try { return StrictUtf8.GetString(plaintext); }
            catch (System.Text.DecoderFallbackException)
            {
                throw new XPScriptRuntimeException(5, "Application.Crypto decrypted value is not valid UTF-8.");
            }
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext);
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(aad);
        }
    }

    private static byte[] BuildHeader(byte kdf, int workFactor, byte[] salt, byte[] nonce, int ciphertextLength)
    {
        var header = new byte[FixedHeaderSize + salt.Length + nonce.Length];
        System.Buffer.BlockCopy(Magic, 0, header, 0, Magic.Length);
        header[4] = FormatVersion;
        header[5] = kdf;
        header[6] = CipherAes256Gcm;
        header[7] = 0;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(8, 4), workFactor);
        header[12] = checked((byte)salt.Length);
        header[13] = checked((byte)nonce.Length);
        header[14] = TagSize;
        header[15] = 0;
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(16, 4), ciphertextLength);
        System.Buffer.BlockCopy(salt, 0, header, FixedHeaderSize, salt.Length);
        System.Buffer.BlockCopy(nonce, 0, header, FixedHeaderSize + salt.Length, nonce.Length);
        return header;
    }

    private static byte[] BuildAad(byte[] header, string context)
    {
        byte[] contextBytes;
        try { contextBytes = StrictUtf8.GetBytes(context); }
        catch (System.Text.EncoderFallbackException)
        {
            throw new XPScriptRuntimeException(5, "Application.Crypto context contains invalid Unicode.");
        }
        if (contextBytes.Length > MaxContextBytes)
            throw new XPScriptRuntimeException(5, "Application.Crypto context exceeds the maximum supported size.");

        var aad = new byte[AadDomain.Length + header.Length + 4 + contextBytes.Length];
        System.Buffer.BlockCopy(AadDomain, 0, aad, 0, AadDomain.Length);
        System.Buffer.BlockCopy(header, 0, aad, AadDomain.Length, header.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(AadDomain.Length + header.Length, 4), contextBytes.Length);
        System.Buffer.BlockCopy(contextBytes, 0, aad, AadDomain.Length + header.Length + 4, contextBytes.Length);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(contextBytes);
        return aad;
    }

    private static ParsedEnvelope Parse(object? value)
    {
        var encoded = XPScriptRuntime.CStr(value);
        if (!encoded.StartsWith(Prefix, StringComparison.Ordinal))
        {
            if (encoded.StartsWith(PrefixRoot, StringComparison.Ordinal))
                throw new XPScriptRuntimeException(5, "Unsupported Application.Crypto encrypted value format version.");
            throw new XPScriptRuntimeException(5, "The value is not an Application.Crypto encrypted value.");
        }
        if (encoded.Length > Prefix.Length + ((MaxEnvelopeBytes + 2) / 3 * 4))
            throw new XPScriptRuntimeException(5, "Application.Crypto encrypted value exceeds the maximum supported size.");

        byte[] envelope;
        try { envelope = Base64UrlDecode(encoded[Prefix.Length..]); }
        catch (FormatException)
        {
            throw new XPScriptRuntimeException(5, "Application.Crypto encrypted value is malformed.");
        }
        if (envelope.Length < FixedHeaderSize + TagSize || envelope.Length > MaxEnvelopeBytes)
            throw new XPScriptRuntimeException(5, "Application.Crypto encrypted value size is invalid.");
        if (!envelope.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new XPScriptRuntimeException(5, "Application.Crypto encrypted value has an invalid header.");

        var version = envelope[4];
        var kdf = envelope[5];
        var cipher = envelope[6];
        var flags = envelope[7];
        var workFactor = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(envelope.AsSpan(8, 4));
        var saltLength = envelope[12];
        var nonceLength = envelope[13];
        var tagLength = envelope[14];
        var reserved = envelope[15];
        var ciphertextLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(envelope.AsSpan(16, 4));

        if (version != FormatVersion)
            throw new XPScriptRuntimeException(5, "Unsupported Application.Crypto encrypted value format version: " + version + ".");
        if (flags != 0 || reserved != 0)
            throw new XPScriptRuntimeException(5, "Application.Crypto encrypted value contains unsupported flags.");
        if (cipher != CipherAes256Gcm)
            throw new XPScriptRuntimeException(5, "Unsupported Application.Crypto encryption algorithm.");
        if (kdf == KdfPbkdf2Sha256)
        {
            if (workFactor < 10000 || workFactor > 100000000)
                throw new XPScriptRuntimeException(5, "Application.Crypto KDF parameters are invalid.");
            if (saltLength < 16 || saltLength > 64)
                throw new XPScriptRuntimeException(5, "Application.Crypto salt length is invalid.");
        }
        else if (kdf == KdfNone)
        {
            if (workFactor != 0 || saltLength != 0)
                throw new XPScriptRuntimeException(5, "Application.Crypto key-based envelope parameters are invalid.");
        }
        else
        {
            throw new XPScriptRuntimeException(5, "Unsupported Application.Crypto key derivation algorithm.");
        }
        if (nonceLength != NonceSize || tagLength != TagSize)
            throw new XPScriptRuntimeException(5, "Application.Crypto nonce or authentication tag length is invalid.");
        if (ciphertextLength < 0 || ciphertextLength > MaxPlaintextBytes)
            throw new XPScriptRuntimeException(5, "Application.Crypto ciphertext length is invalid.");

        var headerLength = FixedHeaderSize + saltLength + nonceLength;
        var expectedLength = (long)headerLength + tagLength + ciphertextLength;
        if (expectedLength != envelope.Length)
            throw new XPScriptRuntimeException(5, "Application.Crypto encrypted value is truncated or malformed.");

        var header = envelope.AsSpan(0, headerLength).ToArray();
        var salt = envelope.AsSpan(FixedHeaderSize, saltLength).ToArray();
        var nonce = envelope.AsSpan(FixedHeaderSize + saltLength, nonceLength).ToArray();
        var tag = envelope.AsSpan(headerLength, tagLength).ToArray();
        var ciphertext = envelope.AsSpan(headerLength + tagLength, ciphertextLength).ToArray();
        return new ParsedEnvelope(version, kdf, cipher, workFactor, salt, nonce, tag, ciphertext, header);
    }

    private static byte[] EncodePlaintext(object? value)
    {
        byte[] bytes;
        try { bytes = StrictUtf8.GetBytes(XPScriptRuntime.CStr(value)); }
        catch (System.Text.EncoderFallbackException)
        {
            throw new XPScriptRuntimeException(5, "Application.Crypto value contains invalid Unicode.");
        }
        if (bytes.Length > MaxPlaintextBytes)
            throw new XPScriptRuntimeException(5, "Application.Crypto value exceeds the maximum supported size.");
        return bytes;
    }

    private static byte[] DecodeKey(object? value)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        byte[] key;
        try { key = System.Convert.FromBase64String(text); }
        catch (FormatException)
        {
            throw new XPScriptRuntimeException(5, "Application.Crypto key must be a Base64-encoded 256-bit key.");
        }
        if (key.Length == KeySize) return key;
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        throw new XPScriptRuntimeException(5, "Application.Crypto key must be exactly 256 bits.");
    }

    private static string RequiredPassword(object? value)
    {
        var password = XPScriptRuntime.CStr(value);
        if (password.Length == 0)
            throw new XPScriptRuntimeException(5, "Application.Crypto password cannot be empty.");
        return password;
    }

    private static string NormalizeAlgorithm(object? value)
    {
        var name = XPScriptRuntime.CStr(value).Trim().ToUpperInvariant();
        return name.Length == 0 ? "DEFAULT" : name;
    }

    private static string NormalizeContext(object? value) => XPScriptRuntime.CStr(value);

    private static void RequireAesGcm()
    {
        if (!System.Security.Cryptography.AesGcm.IsSupported)
            throw new PlatformNotSupportedException("Application.Crypto requires AES-GCM support on the current platform.");
    }

    private static string Base64UrlEncode(byte[] value)
        => System.Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        if (value.Length == 0 || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new FormatException();
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.Length % 4 switch
        {
            0 => base64,
            2 => base64 + "==",
            3 => base64 + "=",
            _ => throw new FormatException()
        };
        return System.Convert.FromBase64String(base64);
    }

    private sealed record ParsedEnvelope(
        byte Version,
        byte Kdf,
        byte Cipher,
        int WorkFactor,
        byte[] Salt,
        byte[] Nonce,
        byte[] Tag,
        byte[] Ciphertext,
        byte[] Header);
}
""";
}
