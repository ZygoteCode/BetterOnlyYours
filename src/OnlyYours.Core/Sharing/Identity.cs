using System.Security.Cryptography;
using System.Text;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Format;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.Core.Sharing;

/// <summary>
/// The public half of a vault identity — what you give to people who want to send
/// you sealed packages. It carries two key-encapsulation keys (P-384 ECDH and
/// ML-KEM-1024) and two signature keys (ML-DSA-87 and P-384 ECDSA): every package
/// is protected by a classical <em>and</em> a post-quantum primitive at once, so it
/// stays secure as long as either family holds (CNSA 2.0 hybrid profile).
/// </summary>
public sealed class PublicIdentity
{
    public const string TextPrefix = "ONLYYOURS-ID:";
    private static ReadOnlySpan<byte> Magic => "SGID"u8;

    private PublicIdentity(string name, byte[] ecdh, byte[] kem, byte[] dsa, byte[] ecdsa, DateTimeOffset created, byte[] bundle)
    {
        DisplayName = name;
        EcdhPublic = ecdh;
        KemPublic = kem;
        DsaPublic = dsa;
        EcdsaPublic = ecdsa;
        Created = created;
        Bundle = bundle;
        Fingerprint = ComputeFingerprint(ecdh, kem, dsa, ecdsa);
    }

    public string DisplayName { get; }

    public byte[] EcdhPublic { get; }

    public byte[] KemPublic { get; }

    public byte[] DsaPublic { get; }

    public byte[] EcdsaPublic { get; }

    public DateTimeOffset Created { get; }

    public byte[] Bundle { get; }

    /// <summary>First 20 bytes of SHA-512 over the four public keys (the name is not part of it).</summary>
    public byte[] Fingerprint { get; }

    public string FingerprintText => FormatFingerprint(Fingerprint);

    public static PublicIdentity Create(string name, byte[] ecdh, byte[] kem, byte[] dsa, byte[] ecdsa, DateTimeOffset created)
    {
        var output = new BinaryOut();
        output.Raw(Magic);
        output.Byte(1);
        var nameBytes = Encoding.UTF8.GetBytes(name.Length > 80 ? name[..80] : name);
        output.U16((ushort)nameBytes.Length);
        output.Raw(nameBytes);
        foreach (var key in new[] { ecdh, kem, dsa, ecdsa })
        {
            output.U16((ushort)key.Length);
            output.Raw(key);
        }

        output.I64(created.ToUnixTimeMilliseconds());
        return new PublicIdentity(name, ecdh, kem, dsa, ecdsa, created, output.ToArray());
    }

    public static PublicIdentity Parse(ReadOnlySpan<byte> bundle)
    {
        var input = new BinaryIn(bundle);
        if (!input.Raw(4).SequenceEqual(Magic) || input.U8() != 1)
        {
            throw new VaultException(VaultError.NotAVault, "This is not a OnlyYours identity.");
        }

        var name = Encoding.UTF8.GetString(input.Raw(input.U16()));
        var ecdh = input.Raw(input.U16()).ToArray();
        var kem = input.Raw(input.U16()).ToArray();
        var dsa = input.Raw(input.U16()).ToArray();
        var ecdsa = input.Raw(input.U16()).ToArray();
        var created = DateTimeOffset.FromUnixTimeMilliseconds(Math.Clamp(input.I64(), 0, 253_402_300_799_999L));
        if (kem.Length != 1568 || dsa.Length != 2592)
        {
            throw new VaultException(VaultError.Corrupted, "The identity keys have unexpected sizes.");
        }

        return new PublicIdentity(name, ecdh, kem, dsa, ecdsa, created, bundle.ToArray());
    }

    public static bool TryParseText(string text, out PublicIdentity? identity)
    {
        identity = null;
        var trimmed = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        if (trimmed.StartsWith(TextPrefix, StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[TextPrefix.Length..];
        }

        try
        {
            var bytes = Convert.FromBase64String(trimmed.Replace('-', '+').Replace('_', '/').PadRight((trimmed.Length + 3) / 4 * 4, '='));
            identity = Parse(bytes);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (VaultException)
        {
            return false;
        }
    }

    public string ToText()
    {
        var encoded = Convert.ToBase64String(Bundle).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var builder = new StringBuilder(TextPrefix.Length + encoded.Length + (encoded.Length / 64) + 4);
        builder.Append(TextPrefix).Append('\n');
        for (var i = 0; i < encoded.Length; i += 64)
        {
            builder.Append(encoded.AsSpan(i, Math.Min(64, encoded.Length - i))).Append('\n');
        }

        return builder.ToString();
    }

    public static byte[] ComputeFingerprint(byte[] ecdh, byte[] kem, byte[] dsa, byte[] ecdsa)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA512);
        hash.AppendData("OnlyYours/v1/identity"u8);
        hash.AppendData(ecdh);
        hash.AppendData(kem);
        hash.AppendData(dsa);
        hash.AppendData(ecdsa);
        return hash.GetHashAndReset()[..20];
    }

    public static string FormatFingerprint(ReadOnlySpan<byte> fingerprint)
    {
        var hex = Convert.ToHexString(fingerprint);
        var builder = new StringBuilder(hex.Length + (hex.Length / 4));
        for (var i = 0; i < hex.Length; i += 4)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }

            builder.Append(hex.AsSpan(i, Math.Min(4, hex.Length - i)));
        }

        return builder.ToString();
    }
}

/// <summary>The private keys of the vault identity, loaded into CNG for the duration of one operation.</summary>
public sealed class IdentityKeys : IDisposable
{
    private IdentityKeys(ECDiffieHellman ecdh, MLKem kem, MLDsa dsa, ECDsa ecdsa, PublicIdentity publicIdentity)
    {
        Ecdh = ecdh;
        Kem = kem;
        Dsa = dsa;
        Ecdsa = ecdsa;
        Public = publicIdentity;
    }

    public ECDiffieHellman Ecdh { get; }

    public MLKem Kem { get; }

    public MLDsa Dsa { get; }

    public ECDsa Ecdsa { get; }

    public PublicIdentity Public { get; }

    public static bool IsSupported => MLKem.IsSupported && MLDsa.IsSupported;

    /// <summary>Creates a new identity and stores its sealed private keys in the document.</summary>
    public static PublicIdentity CreateIn(VaultSession session, string displayName)
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("ML-KEM and ML-DSA require a recent Windows 11 build.");
        }

        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP384);
        using var kem = MLKem.GenerateKey(MLKemAlgorithm.MLKem1024);
        using var dsa = MLDsa.GenerateKey(MLDsaAlgorithm.MLDsa87);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        var publicIdentity = PublicIdentity.Create(
            displayName,
            ecdh.ExportSubjectPublicKeyInfo(),
            kem.ExportEncapsulationKey(),
            dsa.ExportMLDsaPublicKey(),
            ecdsa.ExportSubjectPublicKeyInfo(),
            DateTimeOffset.Now);

        var fields = session.Fields;
        var record = new IdentityRecord
        {
            Created = publicIdentity.Created,
            DisplayName = displayName,
            EcdhPrivate = SealExport(fields, IdentitySlots.Ecdh, 512, span => ecdh.TryExportECPrivateKey(span, out var n) ? n : -1),
            MlKemSeed = SealExport(fields, IdentitySlots.MlKem, 64, span => { kem.ExportPrivateSeed(span); return 64; }),
            MlDsaSeed = SealExport(fields, IdentitySlots.MlDsa, 32, span => { dsa.ExportMLDsaPrivateSeed(span); return 32; }),
            EcdsaPrivate = SealExport(fields, IdentitySlots.Ecdsa, 512, span => ecdsa.TryExportECPrivateKey(span, out var n) ? n : -1),
            PublicBundle = publicIdentity.Bundle,
        };

        session.Document.Identity = record;
        session.Commit();
        return publicIdentity;
    }

    public static IdentityKeys Load(VaultSession session)
    {
        var record = session.Document.Identity ?? throw new InvalidOperationException("The vault has no identity yet.");
        var fields = session.Fields;
        var publicIdentity = PublicIdentity.Parse(record.PublicBundle);

        var ecdh = ECDiffieHellman.Create();
        var ecdsa = ECDsa.Create();
        MLKem? kem = null;
        MLDsa? dsa = null;
        try
        {
            using (var key = fields.Open(record.EcdhPrivate, IdentitySlots.Owner, IdentitySlots.Ecdh))
            {
                ecdh.ImportECPrivateKey(key.Span, out _);
            }

            using (var key = fields.Open(record.MlKemSeed, IdentitySlots.Owner, IdentitySlots.MlKem))
            {
                kem = MLKem.ImportPrivateSeed(MLKemAlgorithm.MLKem1024, key.Span);
            }

            using (var key = fields.Open(record.MlDsaSeed, IdentitySlots.Owner, IdentitySlots.MlDsa))
            {
                dsa = MLDsa.ImportMLDsaPrivateSeed(MLDsaAlgorithm.MLDsa87, key.Span);
            }

            using (var key = fields.Open(record.EcdsaPrivate, IdentitySlots.Owner, IdentitySlots.Ecdsa))
            {
                ecdsa.ImportECPrivateKey(key.Span, out _);
            }

            return new IdentityKeys(ecdh, kem, dsa, ecdsa, publicIdentity);
        }
        catch
        {
            ecdh.Dispose();
            ecdsa.Dispose();
            kem?.Dispose();
            dsa?.Dispose();
            throw;
        }
    }

    public static PublicIdentity? PublicOf(VaultSession session) =>
        session.Document.Identity is { } record ? PublicIdentity.Parse(record.PublicBundle) : null;

    private static SealedValue SealExport(FieldCipher fields, Guid slot, int capacity, Func<Span<byte>, int> export)
    {
        using var buffer = SecureBuffer.Allocate(capacity);
        var written = export(buffer.Span);
        if (written <= 0)
        {
            throw new CryptographicException("A private key could not be exported.");
        }

        return fields.Seal(buffer.Span[..written], IdentitySlots.Owner, slot);
    }

    public void Dispose()
    {
        Ecdh.Dispose();
        Kem.Dispose();
        Dsa.Dispose();
        Ecdsa.Dispose();
    }
}
