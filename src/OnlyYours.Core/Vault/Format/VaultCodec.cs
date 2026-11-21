using System.Buffers.Binary;
using System.Security.Cryptography;
using OnlyYours.Core.Security.Crypto;
using OnlyYours.Core.Security.Memory;

namespace OnlyYours.Core.Vault.Format;

/// <summary>
/// Encryption and decryption of a whole vault file.
/// <code>
/// file    = header ‖ ciphertext ‖ HMAC-SHA-512(k_commit, header ‖ ciphertext)
/// k_*     = HKDF-SHA-512(VMK, salt = payload salt, info = label ‖ vault id)
/// plain   = u32 length ‖ document ‖ zero padding (PADMÉ)
/// cipher  = AES-256-GCM(k_aes, ChaCha20-Poly1305(k_chacha, plain))      aad = header
/// </code>
/// Every save draws a fresh payload salt and fresh nonces, so payload keys are
/// never reused. The HMAC makes the construction key-committing and is checked,
/// in constant time, before anything is decrypted.
/// </summary>
public static class VaultCodec
{
    public const int VmkBytes = 32;
    public const int MacBytes = 64;

    internal static ReadOnlySpan<byte> LabelChaCha => "OnlyYours/v1/payload/chacha20-poly1305"u8;
    internal static ReadOnlySpan<byte> LabelAes => "OnlyYours/v1/payload/aes-256-gcm"u8;
    internal static ReadOnlySpan<byte> LabelCommit => "OnlyYours/v1/payload/commitment"u8;
    internal static ReadOnlySpan<byte> LabelFieldKey => "OnlyYours/v1/field-key"u8;
    internal static ReadOnlySpan<byte> LabelIndexKey => "OnlyYours/v1/index-key"u8;
    internal static ReadOnlySpan<byte> LabelRecovery => "OnlyYours/v1/slot/recovery"u8;
    internal static ReadOnlySpan<byte> LabelHello => "OnlyYours/v1/slot/hello"u8;

    /// <summary>Encrypts <paramref name="document"/>; fills the header's payload salt and nonces.</summary>
    public static byte[] Seal(VaultHeader header, ReadOnlySpan<byte> vmk, ReadOnlySpan<byte> document)
    {
        RandomNumberGenerator.Fill(header.PayloadSalt);
        RandomNumberGenerator.Fill(header.ChaChaNonce);
        RandomNumberGenerator.Fill(header.AesNonce);
        var headerBytes = header.Serialize();

        var padded = checked((int)Padme.PaddedLength(4L + document.Length));
        using var framed = SecureBuffer.Allocate(padded);
        framed.Span.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(framed.Span, (uint)document.Length);
        document.CopyTo(framed.Span[4..]);

        var file = new byte[headerBytes.Length + padded + CascadeAead.Overhead + MacBytes];
        headerBytes.CopyTo(file, 0);

        Span<byte> keys = stackalloc byte[32 + 32 + 64];
        try
        {
            DerivePayloadKeys(vmk, header.VaultId, header.PayloadSalt, keys);
            CascadeAead.Encrypt(
                keys[..32],
                keys.Slice(32, 32),
                header.ChaChaNonce,
                header.AesNonce,
                framed.Span,
                headerBytes,
                file.AsSpan(headerBytes.Length, padded + CascadeAead.Overhead));
            HMACSHA512.HashData(keys[64..], file.AsSpan(0, file.Length - MacBytes), file.AsSpan(file.Length - MacBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keys);
        }

        return file;
    }

    /// <summary>Verifies and decrypts a vault file. Returns the document bytes in locked memory.</summary>
    public static SecureBuffer Open(ReadOnlySpan<byte> file, VaultHeader header, ReadOnlySpan<byte> vmk)
    {
        var headerLength = header.ParsedLength;
        var macStart = file.Length - MacBytes;
        if (headerLength <= 0 || macStart < headerLength + CascadeAead.Overhead + 4)
        {
            throw new VaultException(VaultError.Truncated, "The vault file is shorter than its header announces.");
        }

        Span<byte> keys = stackalloc byte[32 + 32 + 64];
        Span<byte> mac = stackalloc byte[MacBytes];
        try
        {
            DerivePayloadKeys(vmk, header.VaultId, header.PayloadSalt, keys);
            HMACSHA512.HashData(keys[64..], file[..macStart], mac);
            if (!CryptographicOperations.FixedTimeEquals(mac, file[macStart..]))
            {
                throw new VaultException(VaultError.IntegrityFailure, "The vault file failed its integrity check: it was modified or damaged.");
            }

            var ciphertext = file[headerLength..macStart];
            using var framed = SecureBuffer.Allocate(ciphertext.Length - CascadeAead.Overhead);
            if (!CascadeAead.TryDecrypt(keys[..32], keys.Slice(32, 32), header.ChaChaNonce, header.AesNonce, ciphertext, file[..headerLength], framed.Span))
            {
                throw new VaultException(VaultError.IntegrityFailure, "Authenticated decryption of the vault failed.");
            }

            var length = BinaryPrimitives.ReadUInt32LittleEndian(framed.Span);
            if (length > (uint)(framed.Length - 4))
            {
                throw new VaultException(VaultError.Corrupted, "The decrypted vault has an impossible length.");
            }

            return SecureBuffer.CopyOf(framed.Span.Slice(4, (int)length));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keys);
            CryptographicOperations.ZeroMemory(mac);
        }
    }

    internal static void DerivePayloadKeys(ReadOnlySpan<byte> vmk, Guid vaultId, ReadOnlySpan<byte> salt, Span<byte> keys)
    {
        Derive(vmk, salt, LabelChaCha, vaultId, keys[..32]);
        Derive(vmk, salt, LabelAes, vaultId, keys.Slice(32, 32));
        Derive(vmk, salt, LabelCommit, vaultId, keys.Slice(64, 64));
    }

    internal static void DeriveFieldKey(ReadOnlySpan<byte> vmk, Guid vaultId, Span<byte> key)
    {
        Span<byte> salt = stackalloc byte[16];
        vaultId.TryWriteBytes(salt);
        Derive(vmk, salt, LabelFieldKey, vaultId, key);
    }

    internal static void DeriveIndexKey(ReadOnlySpan<byte> vmk, Guid vaultId, Span<byte> key)
    {
        Span<byte> salt = stackalloc byte[16];
        vaultId.TryWriteBytes(salt);
        Derive(vmk, salt, LabelIndexKey, vaultId, key);
    }

    internal static void Derive(ReadOnlySpan<byte> ikm, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> label, Guid vaultId, Span<byte> output, ReadOnlySpan<byte> extra = default)
    {
        Span<byte> info = stackalloc byte[label.Length + 16 + extra.Length];
        label.CopyTo(info);
        vaultId.TryWriteBytes(info.Slice(label.Length, 16));
        extra.CopyTo(info[(label.Length + 16)..]);
        HKDF.DeriveKey(HashAlgorithmName.SHA512, ikm, output, salt, info);
    }

    // ------------------------------------------------------------- key slots

    public static PasswordSlot CreatePasswordSlot(Guid vaultId, ReadOnlySpan<byte> passwordUtf8, ReadOnlySpan<byte> vmk, KdfParameters kdf, CancellationToken cancellationToken = default)
    {
        if (!kdf.IsWithinBounds)
        {
            throw new VaultException(VaultError.UnsafeParameters, "Key-derivation parameters are outside the supported range.");
        }

        var slot = new PasswordSlot
        {
            Kdf = kdf,
            Salt = RandomNumberGenerator.GetBytes(KeySlot.SaltBytes),
            Nonce = RandomNumberGenerator.GetBytes(KeySlot.NonceBytes),
            WrappedKey = new byte[KeySlot.WrappedBytes],
        };

        Span<byte> kek = stackalloc byte[32];
        try
        {
            DerivePasswordKek(vaultId, slot, passwordUtf8, kek, cancellationToken);
            Wrap(kek, slot.Nonce, vmk, slot.AssociatedData(vaultId), slot.WrappedKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }

        return slot;
    }

    public static void DerivePasswordKek(Guid vaultId, PasswordSlot slot, ReadOnlySpan<byte> passwordUtf8, Span<byte> kek, CancellationToken cancellationToken = default)
    {
        if (!slot.Kdf.IsWithinBounds)
        {
            throw new VaultException(VaultError.UnsafeParameters, "The vault asks for key-derivation parameters outside the safe range.");
        }

        Span<byte> associated = stackalloc byte[16];
        vaultId.TryWriteBytes(associated);
        Argon2.DeriveId(passwordUtf8, slot.Salt, slot.Kdf.MemoryKiB, slot.Kdf.Iterations, slot.Kdf.Parallelism, kek, associatedData: associated, cancellationToken: cancellationToken);
    }

    public static RecoverySlot CreateRecoverySlot(Guid vaultId, ReadOnlySpan<byte> recoveryKey, ReadOnlySpan<byte> vmk)
    {
        var slot = new RecoverySlot
        {
            Salt = RandomNumberGenerator.GetBytes(KeySlot.SaltBytes),
            Nonce = RandomNumberGenerator.GetBytes(KeySlot.NonceBytes),
            WrappedKey = new byte[KeySlot.WrappedBytes],
        };

        Span<byte> kek = stackalloc byte[32];
        try
        {
            Derive(recoveryKey, slot.Salt, LabelRecovery, vaultId, kek);
            Wrap(kek, slot.Nonce, vmk, slot.AssociatedData(vaultId), slot.WrappedKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }

        return slot;
    }

    public static void DeriveRecoveryKek(Guid vaultId, RecoverySlot slot, ReadOnlySpan<byte> recoveryKey, Span<byte> kek) =>
        Derive(recoveryKey, slot.Salt, LabelRecovery, vaultId, kek);

    public static HelloSlot CreateHelloSlot(Guid vaultId, string credentialName, byte[] challenge, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> vmk)
    {
        var slot = new HelloSlot
        {
            CredentialName = credentialName,
            Challenge = challenge,
            Salt = RandomNumberGenerator.GetBytes(KeySlot.SaltBytes),
            Nonce = RandomNumberGenerator.GetBytes(KeySlot.NonceBytes),
            WrappedKey = new byte[KeySlot.WrappedBytes],
        };

        Span<byte> kek = stackalloc byte[32];
        try
        {
            DeriveHelloKek(vaultId, slot, signature, kek);
            Wrap(kek, slot.Nonce, vmk, slot.AssociatedData(vaultId), slot.WrappedKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }

        return slot;
    }

    public static void DeriveHelloKek(Guid vaultId, HelloSlot slot, ReadOnlySpan<byte> signature, Span<byte> kek) =>
        Derive(signature, slot.Salt, LabelHello, vaultId, kek, slot.Challenge);

    public static bool TryUnwrap(KeySlot slot, Guid vaultId, ReadOnlySpan<byte> kek, Span<byte> vmk)
    {
        if (slot.WrappedKey.Length != KeySlot.WrappedBytes || slot.Nonce.Length != KeySlot.NonceBytes || vmk.Length != VmkBytes)
        {
            return false;
        }

        try
        {
            using var aes = new AesGcm(kek, 16);
            aes.Decrypt(slot.Nonce, slot.WrappedKey.AsSpan(0, VmkBytes), slot.WrappedKey.AsSpan(VmkBytes, 16), vmk, slot.AssociatedData(vaultId));
            return true;
        }
        catch (CryptographicException)
        {
            vmk.Clear();
            return false;
        }
    }

    private static void Wrap(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> vmk, ReadOnlySpan<byte> associatedData, Span<byte> wrapped)
    {
        using var aes = new AesGcm(kek, 16);
        aes.Encrypt(nonce, vmk, wrapped[..VmkBytes], wrapped.Slice(VmkBytes, 16), associatedData);
    }
}
