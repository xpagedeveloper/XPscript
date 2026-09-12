namespace XPScript.Compiler;

internal static class ApplicationSecretsEncryptionRuntimeSource
{
    public const string Code = """
internal static class XPScriptApplicationSecretsEncryptionRuntime
{
    private const string Prefix = "XPSENC1:";
    private const int FormatVersion = 1;
    private const int DefaultIterations = 600000;
    private const int SaltSize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int MaxEnvelopeBytes = 1024 * 1024;

    public static string Get(object? serviceValue, object? accountValue)
    {
        var stored = XPScriptApplicationSecretsBackupRuntime.Get(serviceValue, accountValue);
        if (IsEncrypted(stored))
            throw new XPScriptRuntimeException(5, "Application.Secrets credential is additionally encrypted. Supply the encryption password to Get(service, account, password).");
        return stored;
    }

    public static string Get(object? serviceValue, object? accountValue, object? passwordValue)
    {
        var stored = XPScriptApplicationSecretsBackupRuntime.Get(serviceValue, accountValue);
        if (stored.Length == 0) return "";
        if (!IsEncrypted(stored))
            throw new XPScriptRuntimeException(5, "Application.Secrets credential is not additionally encrypted. Store it with Set(service, account, secret, password) first.");

        var applicationId = RequireApplicationId();
        var service = Required(serviceValue, "service");
        var account = Required(accountValue, "account");
        var password = RequiredPassword(passwordValue);
        return Decrypt(stored, password, applicationId, service, account);
    }

    public static void Set(object? serviceValue, object? accountValue, object? secretValue)
    {
        XPScriptApplicationSecretsBackupRuntime.Set(serviceValue, accountValue, secretValue);
    }

    public static void Set(object? serviceValue, object? accountValue, object? secretValue, object? passwordValue)
    {
        var applicationId = RequireApplicationId();
        var service = Required(serviceValue, "service");
        var account = Required(accountValue, "account");
        var password = RequiredPassword(passwordValue);
        var secret = XPScriptRuntime.CStr(secretValue);
        var encrypted = Encrypt(secret, password, applicationId, service, account);
        XPScriptApplicationSecretsBackupRuntime.Set(service, account, encrypted);
    }

    private static string Encrypt(string secret, string password, string applicationId, string service, string account)
    {
        byte[]? plaintext = null;
        byte[]? key = null;
        try
        {
            plaintext = System.Text.Encoding.UTF8.GetBytes(secret);
            var salt = System.Security.Cryptography.RandomNumberGenerator.GetBytes(SaltSize);
            var nonce = System.Security.Cryptography.RandomNumberGenerator.GetBytes(NonceSize);
            key = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                DefaultIterations,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                KeySize);

            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagSize];
            var aad = BuildAad(applicationId, service, account);
            using (var aes = new System.Security.Cryptography.AesGcm(key, TagSize))
                aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);

            using var stream = new System.IO.MemoryStream();
            using (var writer = new System.IO.BinaryWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true))
            {
                writer.Write(FormatVersion);
                writer.Write(DefaultIterations);
                writer.Write((byte)salt.Length);
                writer.Write(salt);
                writer.Write((byte)nonce.Length);
                writer.Write(nonce);
                writer.Write((byte)tag.Length);
                writer.Write(ciphertext.Length);
                writer.Write(tag);
                writer.Write(ciphertext);
                writer.Flush();
            }

            var envelope = stream.ToArray();
            if (envelope.Length > MaxEnvelopeBytes)
                throw new XPScriptRuntimeException(5, "Application.Secrets encrypted credential exceeds the maximum supported size.");
            return Prefix + System.Convert.ToBase64String(envelope);
        }
        finally
        {
            if (plaintext is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext);
            if (key is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        }
    }

    private static string Decrypt(string stored, string password, string applicationId, string service, string account)
    {
        byte[]? plaintext = null;
        byte[]? key = null;
        try
        {
            byte[] envelope;
            try
            {
                envelope = System.Convert.FromBase64String(stored[Prefix.Length..]);
            }
            catch (FormatException)
            {
                throw new XPScriptRuntimeException(5, "Application.Secrets encrypted credential is malformed.");
            }
            if (envelope.Length <= 0 || envelope.Length > MaxEnvelopeBytes)
                throw new XPScriptRuntimeException(5, "Application.Secrets encrypted credential size is invalid.");

            using var stream = new System.IO.MemoryStream(envelope, writable: false);
            using var reader = new System.IO.BinaryReader(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
            var version = reader.ReadInt32();
            var iterations = reader.ReadInt32();
            if (version != FormatVersion)
                throw new XPScriptRuntimeException(5, "Unsupported Application.Secrets encrypted credential format version: " + version + ".");
            if (iterations < 10000 || iterations > 100000000)
                throw new XPScriptRuntimeException(5, "Application.Secrets encrypted credential KDF parameters are invalid.");

            var saltLength = reader.ReadByte();
            if (saltLength < 16 || saltLength > 64)
                throw new XPScriptRuntimeException(5, "Application.Secrets encrypted credential salt length is invalid.");
            var salt = ReadExact(reader, saltLength);

            var nonceLength = reader.ReadByte();
            if (nonceLength != NonceSize)
                throw new XPScriptRuntimeException(5, "Application.Secrets encrypted credential nonce length is invalid.");
            var nonce = ReadExact(reader, nonceLength);

            var tagLength = reader.ReadByte();
            if (tagLength != TagSize)
                throw new XPScriptRuntimeException(5, "Application.Secrets encrypted credential tag length is invalid.");

            var ciphertextLength = reader.ReadInt32();
            if (ciphertextLength < 0 || ciphertextLength > MaxEnvelopeBytes)
                throw new XPScriptRuntimeException(5, "Application.Secrets encrypted credential payload length is invalid.");
            if (stream.Length - stream.Position != tagLength + ciphertextLength)
                throw new XPScriptRuntimeException(5, "Application.Secrets encrypted credential is truncated or malformed.");

            var tag = ReadExact(reader, tagLength);
            var ciphertext = ReadExact(reader, ciphertextLength);
            key = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                KeySize);
            plaintext = new byte[ciphertext.Length];
            var aad = BuildAad(applicationId, service, account);
            try
            {
                using var aes = new System.Security.Cryptography.AesGcm(key, tagLength);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                throw new XPScriptRuntimeException(5, "Application.Secrets decryption failed: the password is incorrect or the credential was modified or moved.");
            }
            return new System.Text.UTF8Encoding(false, true).GetString(plaintext);
        }
        catch (System.IO.EndOfStreamException)
        {
            throw new XPScriptRuntimeException(5, "Application.Secrets encrypted credential is truncated or malformed.");
        }
        finally
        {
            if (plaintext is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext);
            if (key is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        }
    }

    private static byte[] BuildAad(string applicationId, string service, string account)
    {
        return System.Text.Encoding.UTF8.GetBytes("XPSENC1\n" + applicationId + "\n" + service + "\n" + account);
    }

    private static byte[] ReadExact(System.IO.BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count) throw new System.IO.EndOfStreamException();
        return bytes;
    }

    private static bool IsEncrypted(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);

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

    private static string RequiredPassword(object? value)
    {
        var password = XPScriptRuntime.CStr(value);
        if (password.Length == 0)
            throw new XPScriptRuntimeException(5, "Application.Secrets encryption password cannot be empty.");
        return password;
    }
}
""";
}
