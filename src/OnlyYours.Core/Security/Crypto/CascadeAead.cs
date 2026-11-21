using System.Security.Cryptography;

namespace OnlyYours.Core.Security.Crypto;

/// <summary>
/// Defence in depth for the vault payload: two independent AEADs with
/// independent keys, from two unrelated design families.
/// <code>
/// inner = ChaCha20-Poly1305(k1, n1, plaintext, aad)   // ARX stream cipher
/// outer = AES-256-GCM   (k2, n2, inner,     aad)       // SPN block cipher
/// </code>
/// Breaking the vault requires breaking <em>both</em> constructions. Neither AEAD
/// is key-committing on its own, which is why the vault adds an HMAC-SHA-512
/// commitment over the whole file (see <c>VaultCodec</c>).
/// </summary>
public static class CascadeAead
{
    public const int KeyBytes = 32;
    public const int NonceBytes = 12;
    public const int TagBytes = 16;
    public const int Overhead = 2 * TagBytes;

    public static void Encrypt(
        ReadOnlySpan<byte> chachaKey,
        ReadOnlySpan<byte> aesKey,
        ReadOnlySpan<byte> chachaNonce,
        ReadOnlySpan<byte> aesNonce,
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> associatedData,
        Span<byte> destination)
    {
        if (destination.Length != plaintext.Length + Overhead)
        {
            throw new ArgumentException("Destination must be plaintext + 32 bytes.", nameof(destination));
        }

        // The inner ciphertext is already encrypted, so it may live on the managed heap.
        var inner = new byte[plaintext.Length + TagBytes];
        using (var chacha = new ChaCha20Poly1305(chachaKey))
        {
            chacha.Encrypt(chachaNonce, plaintext, inner.AsSpan(0, plaintext.Length), inner.AsSpan(plaintext.Length), associatedData);
        }

        using var aes = new AesGcm(aesKey, TagBytes);
        aes.Encrypt(aesNonce, inner, destination[..inner.Length], destination[inner.Length..], associatedData);
    }

    public static bool TryDecrypt(
        ReadOnlySpan<byte> chachaKey,
        ReadOnlySpan<byte> aesKey,
        ReadOnlySpan<byte> chachaNonce,
        ReadOnlySpan<byte> aesNonce,
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> associatedData,
        Span<byte> plaintext)
    {
        if (ciphertext.Length < Overhead || plaintext.Length != ciphertext.Length - Overhead)
        {
            return false;
        }

        var inner = new byte[ciphertext.Length - TagBytes];
        try
        {
            using (var aes = new AesGcm(aesKey, TagBytes))
            {
                aes.Decrypt(aesNonce, ciphertext[..inner.Length], ciphertext[inner.Length..], inner, associatedData);
            }

            using var chacha = new ChaCha20Poly1305(chachaKey);
            chacha.Decrypt(chachaNonce, inner.AsSpan(0, plaintext.Length), inner.AsSpan(plaintext.Length), plaintext, associatedData);
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            plaintext.Clear();
            return false;
        }
        catch (CryptographicException)
        {
            plaintext.Clear();
            return false;
        }
    }
}
