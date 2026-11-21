using System.Buffers.Binary;
using System.Security.Cryptography;
using OnlyYours.Core.Security.Crypto;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault;

namespace OnlyYours.Core.Legacy;

public enum LegacyKdf
{
    Pbkdf2Sha256,
    Argon2id,
}

/// <summary>Header of a BetterOnlyYours <c>credentials.plf</c> file (formats v1, v2 and v3).</summary>
public sealed record LegacyHeader(int Version, LegacyKdf Kdf, uint MemoryKiB, uint Iterations, uint Parallelism, byte[] Salt, byte[] Nonce, int Length)
{
    public string Describe() => Kdf == LegacyKdf.Argon2id
        ? $"BetterOnlyYours v{Version} · Argon2id · {MemoryKiB / 1024} MiB · t={Iterations} · p={Parallelism}"
        : $"BetterOnlyYours v{Version} · PBKDF2-HMAC-SHA256 · {Iterations:N0} iterations";
}

/// <summary>Result of decrypting a legacy vault: its JSON payload, held in locked memory.</summary>
public sealed class LegacyPayload : IDisposable
{
    internal LegacyPayload(SecureBuffer json, LegacyHeader header, bool fromBackup, string path)
    {
        Json = json;
        Header = header;
        RecoveredFromBackup = fromBackup;
        Path = path;
    }

    public SecureBuffer Json { get; }

    public LegacyHeader Header { get; }

    public bool RecoveredFromBackup { get; }

    public string Path { get; }

    public void Dispose() => Json.Dispose();
}

/// <summary>
/// Reader for the predecessor's vault format, byte-for-byte compatible with the
/// Dart implementation:
/// <code>
/// v1: FF FE 0D 0A | 01 | salt[16] | nonce[12] | AES-256-GCM            (PBKDF2-SHA256 × 3 000)
/// v2: FF FE 0D 0A | 02 | kdf=1 | iterations u32be | salt | nonce | …   (PBKDF2-SHA256 × n)
/// v3: FF FE 0D 0A | 03 | kdf | m u32be | t u32be | p u8 | salt | nonce | …
/// </code>
/// The whole header is the GCM associated data. The legacy file is only ever read.
/// </summary>
public static class LegacyVault
{
    public static ReadOnlySpan<byte> Magic => [0xFF, 0xFE, 0x0D, 0x0A];

    private const int SaltLength = 16;
    private const int NonceLength = 12;
    private const int V1Length = 4 + 1 + SaltLength + NonceLength;
    private const int V2Length = 4 + 1 + 1 + 4 + SaltLength + NonceLength;
    private const int V3Length = 4 + 1 + 1 + 9 + SaltLength + NonceLength;

    public static bool LooksLegacy(ReadOnlySpan<byte> data) => data.Length >= 5 && data[..4].SequenceEqual(Magic);

    public static LegacyHeader ParseHeader(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < 5)
        {
            throw new VaultException(VaultError.Truncated, "The file is shorter than a BetterOnlyYours header.");
        }

        if (!LooksLegacy(raw))
        {
            throw new VaultException(VaultError.NotAVault, "This is not a BetterOnlyYours vault.");
        }

        switch (raw[4])
        {
            case 1:
                Require(raw, V1Length);
                return new LegacyHeader(1, LegacyKdf.Pbkdf2Sha256, 0, 3000, 0, raw.Slice(5, SaltLength).ToArray(), raw.Slice(5 + SaltLength, NonceLength).ToArray(), V1Length);

            case 2:
            {
                Require(raw, V2Length);
                if (raw[5] != 1)
                {
                    throw new VaultException(VaultError.UnsupportedVersion, $"Unknown key derivation id {raw[5]}.");
                }

                var iterations = BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(6, 4));
                return new LegacyHeader(2, LegacyKdf.Pbkdf2Sha256, 0, iterations, 0, raw.Slice(10, SaltLength).ToArray(), raw.Slice(10 + SaltLength, NonceLength).ToArray(), V2Length);
            }

            case 3:
            {
                Require(raw, V3Length);
                var kdfId = raw[5];
                var memory = BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(6, 4));
                var iterations = BinaryPrimitives.ReadUInt32BigEndian(raw.Slice(10, 4));
                uint parallelism = raw[14];
                var kdf = kdfId switch
                {
                    1 => LegacyKdf.Pbkdf2Sha256,
                    2 => LegacyKdf.Argon2id,
                    _ => throw new VaultException(VaultError.UnsupportedVersion, $"Unknown key derivation id {kdfId}."),
                };
                return new LegacyHeader(3, kdf, memory, iterations, parallelism, raw.Slice(15, SaltLength).ToArray(), raw.Slice(15 + SaltLength, NonceLength).ToArray(), V3Length);
            }

            default:
                throw new VaultException(VaultError.UnsupportedVersion, $"BetterOnlyYours format version {raw[4]} is not supported.");
        }
    }

    public static LegacyHeader? TryReadHeader(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[V3Length + 1];
            var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            return ParseHeader(buffer.AsSpan(0, read));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Decrypts a legacy vault (falling back to its <c>.bak</c> copy when the main file is damaged).</summary>
    public static async Task<LegacyPayload> OpenAsync(string path, SecretDraft password, CancellationToken cancellationToken = default)
    {
        using var passwordUtf8 = password.ToUtf8();
        var backup = path + ".bak";
        if (!File.Exists(path))
        {
            if (!File.Exists(backup))
            {
                throw new VaultException(VaultError.NotFound, "No BetterOnlyYours vault was found at that location.");
            }

            return await Task.Run(() => Decrypt(VaultStorage.ReadFile(backup), passwordUtf8, true, backup, cancellationToken), cancellationToken).ConfigureAwait(false);
        }

        try
        {
            return await Task.Run(() => Decrypt(VaultStorage.ReadFile(path), passwordUtf8, false, path, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (VaultException error) when (error.Error is VaultError.NotAVault or VaultError.Truncated or VaultError.Corrupted && File.Exists(backup))
        {
            return await Task.Run(() => Decrypt(VaultStorage.ReadFile(backup), passwordUtf8, true, backup, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
    }

    internal static LegacyPayload Decrypt(byte[] raw, SecureBuffer passwordUtf8, bool fromBackup, string path, CancellationToken cancellationToken)
    {
        var header = ParseHeader(raw);
        if (raw.Length <= header.Length + 16)
        {
            throw new VaultException(VaultError.Truncated, "The legacy vault payload is missing.");
        }

        using var key = SecureBuffer.Allocate(32);
        DeriveKey(header, passwordUtf8.Span, key.Span, cancellationToken);

        var ciphertext = raw.AsSpan(header.Length, raw.Length - header.Length - 16);
        var tag = raw.AsSpan(raw.Length - 16, 16);
        var json = SecureBuffer.Allocate(ciphertext.Length);
        try
        {
            using var aes = new AesGcm(key.Span, 16);
            aes.Decrypt(header.Nonce, ciphertext, tag, json.Span, raw.AsSpan(0, header.Length));
        }
        catch (CryptographicException error)
        {
            json.Dispose();
            throw new VaultException(VaultError.InvalidPassword, "The BetterOnlyYours master password is not correct.", error);
        }

        return new LegacyPayload(json, header, fromBackup, path);
    }

    internal static void DeriveKey(LegacyHeader header, ReadOnlySpan<byte> password, Span<byte> key, CancellationToken cancellationToken)
    {
        switch (header.Kdf)
        {
            case LegacyKdf.Pbkdf2Sha256:
                if (header.Iterations is < 1000 or > 20_000_000)
                {
                    throw new VaultException(VaultError.UnsafeParameters, $"Implausible PBKDF2 iteration count {header.Iterations}.");
                }

                Rfc2898DeriveBytes.Pbkdf2(password, header.Salt, key, (int)header.Iterations, HashAlgorithmName.SHA256);
                break;

            case LegacyKdf.Argon2id:
                if (header.MemoryKiB is < 8 or > 1_048_576 || header.Iterations is < 1 or > 64 || header.Parallelism is < 1 or > 16)
                {
                    throw new VaultException(VaultError.UnsafeParameters, "Implausible Argon2id parameters in the legacy header.");
                }

                Argon2.DeriveId(password, header.Salt, header.MemoryKiB, header.Iterations, header.Parallelism, key, cancellationToken: cancellationToken);
                break;
        }
    }

    private static void Require(ReadOnlySpan<byte> raw, int headerLength)
    {
        if (raw.Length < headerLength)
        {
            throw new VaultException(VaultError.Truncated, "The legacy vault header is incomplete.");
        }
    }
}
