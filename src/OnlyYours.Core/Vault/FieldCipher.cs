using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault.Format;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.Core.Vault;

/// <summary>
/// Field-level envelope encryption. Every sensitive value (password, TOTP secret,
/// card number, notes, history…) is sealed on its own with AES-256-GCM under a
/// field key derived from the VMK, and stays sealed in memory while the vault is
/// unlocked: it is opened only to reveal, copy or type it, then wiped.
/// <code>
/// blob = 0x01 ‖ nonce[12] ‖ GCM(u32 length ‖ utf8 ‖ zero padding to 32 bytes) ‖ tag[16]
/// aad  = "OnlyYours/v1/field" ‖ owner id ‖ slot id
/// </code>
/// The associated data binds each blob to its entry and field, so ciphertexts
/// cannot be swapped between entries or fields without detection.
/// </summary>
public sealed class FieldCipher : IDisposable
{
    public const byte Version = 1;
    private const int NonceBytes = 12;
    private const int TagBytes = 16;
    private const int Block = 32;

    private readonly ProtectedBuffer _key;

    private FieldCipher(ProtectedBuffer key)
    {
        _key = key;
    }

    public static FieldCipher FromVmk(ReadOnlySpan<byte> vmk, Guid vaultId)
    {
        Span<byte> key = stackalloc byte[32];
        try
        {
            VaultCodec.DeriveFieldKey(vmk, vaultId, key);
            return new FieldCipher(ProtectedBuffer.Create(key));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    /// <summary>Only for tests and package import: a cipher over an explicit random key.</summary>
    internal static FieldCipher CreateEphemeral() => new(ProtectedBuffer.CreateRandom(32));

    public SealedValue Seal(ReadOnlySpan<byte> plaintext, Guid owner, Guid slot)
    {
        var padded = (4 + plaintext.Length + Block - 1) / Block * Block;
        using var framed = SecureBuffer.Allocate(padded);
        framed.Span.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(framed.Span, (uint)plaintext.Length);
        plaintext.CopyTo(framed.Span[4..]);

        var blob = new byte[1 + NonceBytes + padded + TagBytes];
        blob[0] = Version;
        RandomNumberGenerator.Fill(blob.AsSpan(1, NonceBytes));

        Span<byte> aad = stackalloc byte[48];
        BuildAad(owner, slot, aad);
        using (var lease = _key.Unprotect())
        using (var aes = new AesGcm(lease.Span, TagBytes))
        {
            aes.Encrypt(blob.AsSpan(1, NonceBytes), framed.Span, blob.AsSpan(1 + NonceBytes, padded), blob.AsSpan(1 + NonceBytes + padded, TagBytes), aad);
        }

        return new SealedValue(blob);
    }

    public SealedValue SealChars(ReadOnlySpan<char> chars, Guid owner, Guid slot)
    {
        var count = Encoding.UTF8.GetByteCount(chars);
        using var utf8 = SecureBuffer.Allocate(count);
        Encoding.UTF8.GetBytes(chars, utf8.Span);
        return Seal(utf8.Span, owner, slot);
    }

    public SealedValue SealDraft(SecretDraft draft, Guid owner, Guid slot)
    {
        using var lease = draft.Read();
        return SealChars(lease.Chars, owner, slot);
    }

    /// <summary>Decrypts a sealed value into locked memory. Throws <see cref="VaultException"/> if it does not authenticate.</summary>
    public SecureBuffer Open(SealedValue value, Guid owner, Guid slot)
    {
        if (!TryOpen(value, owner, slot, out var plaintext))
        {
            throw new VaultException(VaultError.IntegrityFailure, "A sealed field failed authentication.");
        }

        return plaintext!;
    }

    public bool TryOpen(SealedValue value, Guid owner, Guid slot, out SecureBuffer? plaintext)
    {
        plaintext = null;
        var blob = value.Blob;
        if (blob.Length < 1 + NonceBytes + Block + TagBytes || blob[0] != Version)
        {
            return false;
        }

        var padded = blob.Length - 1 - NonceBytes - TagBytes;
        if (padded % Block != 0)
        {
            return false;
        }

        Span<byte> aad = stackalloc byte[48];
        BuildAad(owner, slot, aad);
        using var framed = SecureBuffer.Allocate(padded);
        try
        {
            using var lease = _key.Unprotect();
            using var aes = new AesGcm(lease.Span, TagBytes);
            aes.Decrypt(blob.AsSpan(1, NonceBytes), blob.AsSpan(1 + NonceBytes, padded), blob.AsSpan(1 + NonceBytes + padded, TagBytes), framed.Span, aad);
        }
        catch (CryptographicException)
        {
            return false;
        }

        var length = BinaryPrimitives.ReadUInt32LittleEndian(framed.Span);
        if (length > (uint)(padded - 4))
        {
            return false;
        }

        plaintext = SecureBuffer.CopyOf(framed.Span.Slice(4, (int)length));
        return true;
    }

    public SecureCharBuffer OpenChars(SealedValue value, Guid owner, Guid slot)
    {
        using var utf8 = Open(value, owner, slot);
        return SecureCharBuffer.FromUtf8(utf8.Span);
    }

    /// <summary>Moves a sealed value to another owner/slot (decrypt + re-encrypt, never exposed).</summary>
    public SealedValue Reseal(SealedValue value, Guid fromOwner, Guid fromSlot, Guid toOwner, Guid toSlot)
    {
        using var plaintext = Open(value, fromOwner, fromSlot);
        return Seal(plaintext.Span, toOwner, toSlot);
    }

    /// <summary>Re-encrypts a value for another cipher (used when the VMK is rotated).</summary>
    public SealedValue Rewrap(SealedValue value, Guid owner, Guid slot, FieldCipher target)
    {
        using var plaintext = Open(value, owner, slot);
        return target.Seal(plaintext.Span, owner, slot);
    }

    private static void BuildAad(Guid owner, Guid slot, Span<byte> aad)
    {
        "OnlyYours/v1/field"u8.CopyTo(aad);
        owner.TryWriteBytes(aad.Slice(16, 16));
        slot.TryWriteBytes(aad.Slice(32, 16));
    }

    public void Dispose() => _key.Dispose();
}
