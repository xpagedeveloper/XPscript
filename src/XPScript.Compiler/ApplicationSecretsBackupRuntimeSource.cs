namespace XPScript.Compiler;

internal static class ApplicationSecretsBackupRuntimeSource
{
    public const string Code = """
internal static class XPScriptApplicationSecretsBackupRuntime
{
    private const int FormatVersion = 1;
    private const byte KdfPbkdf2Sha256 = 1;
    private const byte CipherAes256Gcm = 1;
    private const int DefaultIterations = 600000;
    private const int SaltSize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int MaxBackupBytes = 16 * 1024 * 1024;
    private const int MaxEntries = 10000;
    private static readonly byte[] Magic = System.Text.Encoding.ASCII.GetBytes("XPSSECRETS");
    private static readonly object IndexSync = new();

    public static string Get(object? serviceValue, object? accountValue)
    {
        var service = Required(serviceValue, "service");
        var account = Required(accountValue, "account");
        var secret = XPScriptApplicationSecretsRuntime.Get(service, account);
        Track(service, account);
        return secret;
    }

    public static void Set(object? serviceValue, object? accountValue, object? secretValue)
    {
        var service = Required(serviceValue, "service");
        var account = Required(accountValue, "account");
        XPScriptApplicationSecretsRuntime.Set(service, account, secretValue);
        Track(service, account);
    }

    public static void Backup(object? fileValue, object? passwordValue)
    {
        var path = RequiredPath(fileValue);
        var password = RequiredPassword(passwordValue);
        byte[]? plaintext = null;
        byte[]? key = null;

        try
        {
            var entries = ReadEntries();
            using (var payloadStream = new System.IO.MemoryStream())
            {
                using var writer = new System.IO.BinaryWriter(payloadStream, new System.Text.UTF8Encoding(false), leaveOpen: true);
                writer.Write(FormatVersion);
                writer.Write(entries.Count);
                foreach (var entry in entries)
                {
                    writer.Write(entry.Service);
                    writer.Write(entry.Account);
                    writer.Write(XPScriptApplicationSecretsRuntime.Get(entry.Service, entry.Account));
                }
                writer.Flush();
                plaintext = payloadStream.ToArray();
            }

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
            var aad = BuildAuthenticatedHeader(FormatVersion, KdfPbkdf2Sha256, CipherAes256Gcm, DefaultIterations, salt, nonce, TagSize);
            using (var aes = new System.Security.Cryptography.AesGcm(key, TagSize))
                aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);

            using var fileStream = new System.IO.MemoryStream();
            using (var writer = new System.IO.BinaryWriter(fileStream, new System.Text.UTF8Encoding(false), leaveOpen: true))
            {
                writer.Write(Magic);
                writer.Write(FormatVersion);
                writer.Write(KdfPbkdf2Sha256);
                writer.Write(CipherAes256Gcm);
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

            var bytes = fileStream.ToArray();
            if (bytes.Length > MaxBackupBytes)
                throw new XPScriptRuntimeException(5, "Application.Secrets backup exceeds the maximum supported size.");
            System.IO.File.WriteAllBytes(path, bytes);
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Application.Secrets backup failed: " + ex.Message);
        }
        finally
        {
            if (plaintext is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext);
            if (key is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        }
    }

    public static void Restore(object? fileValue, object? passwordValue)
    {
        var path = RequiredPath(fileValue);
        var password = RequiredPassword(passwordValue);
        byte[]? plaintext = null;
        byte[]? key = null;

        try
        {
            var fileInfo = new System.IO.FileInfo(path);
            if (!fileInfo.Exists)
                throw new XPScriptRuntimeException(5, "Application.Secrets backup file was not found: " + path);
            if (fileInfo.Length <= 0 || fileInfo.Length > MaxBackupBytes)
                throw new XPScriptRuntimeException(5, "Application.Secrets backup file size is invalid.");

            var bytes = System.IO.File.ReadAllBytes(path);
            using var stream = new System.IO.MemoryStream(bytes, writable: false);
            using var reader = new System.IO.BinaryReader(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);

            var magic = ReadExact(reader, Magic.Length);
            if (!magic.SequenceEqual(Magic))
                throw new XPScriptRuntimeException(5, "The file is not an XPscript secrets backup.");

            var version = reader.ReadInt32();
            var kdf = reader.ReadByte();
            var cipher = reader.ReadByte();
            var iterations = reader.ReadInt32();
            if (version != FormatVersion)
                throw new XPScriptRuntimeException(5, "Unsupported Application.Secrets backup format version: " + version + ".");
            if (kdf != KdfPbkdf2Sha256)
                throw new XPScriptRuntimeException(5, "Unsupported Application.Secrets backup key derivation algorithm.");
            if (cipher != CipherAes256Gcm)
                throw new XPScriptRuntimeException(5, "Unsupported Application.Secrets backup encryption algorithm.");
            if (iterations < 10000 || iterations > 100000000)
                throw new XPScriptRuntimeException(5, "Application.Secrets backup KDF parameters are invalid.");

            var saltLength = reader.ReadByte();
            if (saltLength < 16 || saltLength > 64)
                throw new XPScriptRuntimeException(5, "Application.Secrets backup salt length is invalid.");
            var salt = ReadExact(reader, saltLength);

            var nonceLength = reader.ReadByte();
            if (nonceLength != NonceSize)
                throw new XPScriptRuntimeException(5, "Application.Secrets backup nonce length is invalid.");
            var nonce = ReadExact(reader, nonceLength);

            var tagLength = reader.ReadByte();
            if (tagLength != TagSize)
                throw new XPScriptRuntimeException(5, "Application.Secrets backup authentication tag length is invalid.");

            var ciphertextLength = reader.ReadInt32();
            if (ciphertextLength < 0 || ciphertextLength > MaxBackupBytes)
                throw new XPScriptRuntimeException(5, "Application.Secrets backup payload length is invalid.");
            if (stream.Length - stream.Position != tagLength + ciphertextLength)
                throw new XPScriptRuntimeException(5, "Application.Secrets backup file is truncated or malformed.");

            var tag = ReadExact(reader, tagLength);
            var ciphertext = ReadExact(reader, ciphertextLength);
            key = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                KeySize);
            plaintext = new byte[ciphertext.Length];
            var aad = BuildAuthenticatedHeader(version, kdf, cipher, iterations, salt, nonce, tagLength);

            try
            {
                using var aes = new System.Security.Cryptography.AesGcm(key, tagLength);
                aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                throw new XPScriptRuntimeException(5, "Application.Secrets restore failed: the password is incorrect or the backup file was modified.");
            }

            var entries = ReadPayload(plaintext);
            foreach (var entry in entries)
            {
                XPScriptApplicationSecretsRuntime.Set(entry.Service, entry.Account, entry.Secret);
                Track(entry.Service, entry.Account);
            }
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (System.IO.EndOfStreamException)
        {
            throw new XPScriptRuntimeException(5, "Application.Secrets backup file is truncated or malformed.");
        }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Application.Secrets restore failed: " + ex.Message);
        }
        finally
        {
            if (plaintext is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(plaintext);
            if (key is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        }
    }

    private static System.Collections.Generic.List<BackupEntry> ReadPayload(byte[] plaintext)
    {
        using var stream = new System.IO.MemoryStream(plaintext, writable: false);
        using var reader = new System.IO.BinaryReader(stream, new System.Text.UTF8Encoding(false, true), leaveOpen: true);
        var payloadVersion = reader.ReadInt32();
        if (payloadVersion != FormatVersion)
            throw new XPScriptRuntimeException(5, "Unsupported Application.Secrets backup payload version: " + payloadVersion + ".");
        var count = reader.ReadInt32();
        if (count < 0 || count > MaxEntries)
            throw new XPScriptRuntimeException(5, "Application.Secrets backup entry count is invalid.");

        var result = new System.Collections.Generic.List<BackupEntry>(count);
        for (var i = 0; i < count; i++)
        {
            var service = reader.ReadString();
            var account = reader.ReadString();
            var secret = reader.ReadString();
            if (string.IsNullOrWhiteSpace(service) || string.IsNullOrWhiteSpace(account))
                throw new XPScriptRuntimeException(5, "Application.Secrets backup contains an invalid credential identifier.");
            result.Add(new BackupEntry(service, account, secret));
        }
        if (stream.Position != stream.Length)
            throw new XPScriptRuntimeException(5, "Application.Secrets backup payload contains unexpected trailing data.");
        return result;
    }

    private static byte[] BuildAuthenticatedHeader(int version, byte kdf, byte cipher, int iterations, byte[] salt, byte[] nonce, int tagLength)
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
        writer.Write(Magic);
        writer.Write(version);
        writer.Write(kdf);
        writer.Write(cipher);
        writer.Write(iterations);
        writer.Write((byte)salt.Length);
        writer.Write(salt);
        writer.Write((byte)nonce.Length);
        writer.Write(nonce);
        writer.Write((byte)tagLength);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] ReadExact(System.IO.BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count) throw new System.IO.EndOfStreamException();
        return bytes;
    }

    private static void Track(string service, string account)
    {
        var token = Encode(service) + "." + Encode(account);
        lock (IndexSync)
        {
            var tokens = ReadIndexTokens();
            if (tokens.Any(x => string.Equals(x, token, StringComparison.Ordinal))) return;
            tokens.Add(token);
            tokens.Sort(StringComparer.Ordinal);
            XPScriptApplicationRegistryRuntime.User.Set(IndexPath(), "Entries", tokens.ToArray(), "MultiString");
        }
    }

    private static System.Collections.Generic.List<SecretRef> ReadEntries()
    {
        lock (IndexSync)
        {
            var result = new System.Collections.Generic.List<SecretRef>();
            foreach (var token in ReadIndexTokens())
            {
                var separator = token.IndexOf('.');
                if (separator <= 0 || separator >= token.Length - 1) continue;
                try
                {
                    result.Add(new SecretRef(Decode(token[..separator]), Decode(token[(separator + 1)..])));
                }
                catch (FormatException) { }
            }
            return result;
        }
    }

    private static System.Collections.Generic.List<string> ReadIndexTokens()
    {
        var value = XPScriptApplicationRegistryRuntime.User.Get(IndexPath(), "Entries");
        var result = new System.Collections.Generic.List<string>();
        if (value is not LSArray array || !array.IsAllocated) return result;
        if (array.Rank != 1) return result;
        for (var i = array.LBound(); i <= array.UBound(); i++)
        {
            var token = XPScriptRuntime.CStr(array.Get(i));
            if (token.Length > 0) result.Add(token);
        }
        return result;
    }

    private static string IndexPath()
    {
        var app = System.IO.Path.GetFileNameWithoutExtension(XPScriptApplicationRuntime.ExecutableFileName);
        if (string.IsNullOrWhiteSpace(app)) app = "xpscript";
        var encoded = System.Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(app));
        return "Software/XPscript/Applications/" + encoded + "/Secrets";
    }

    private static string Encode(string value) => System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
    private static string Decode(string value) => System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(value));

    private static string Required(object? value, string name)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length == 0) throw new XPScriptRuntimeException(5, "Application.Secrets " + name + " cannot be empty.");
        return text;
    }

    private static string RequiredPath(object? value)
    {
        var path = XPScriptRuntime.CStr(value).Trim();
        if (path.Length == 0) throw new XPScriptRuntimeException(5, "Application.Secrets backup file path cannot be empty.");
        return System.IO.Path.GetFullPath(path);
    }

    private static string RequiredPassword(object? value)
    {
        var password = XPScriptRuntime.CStr(value);
        if (password.Length == 0) throw new XPScriptRuntimeException(5, "Application.Secrets backup password cannot be empty.");
        return password;
    }

    private sealed record SecretRef(string Service, string Account);
    private sealed record BackupEntry(string Service, string Account, string Secret);
}
""";
}
