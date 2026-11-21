using System.Text;
using OnlyYours.Core.Security.Crypto;

namespace OnlyYours.Core.Vault.Format;

public enum KeySlotKind : byte
{
    Password = 1,
    Recovery = 2,
    WindowsHello = 3,
}

/// <summary>
/// One way of unwrapping the vault master key (VMK). Every slot wraps the same
/// random 256-bit VMK with AES-256-GCM under its own key-encryption key, and binds
/// its own parameters (KDF costs, salt, credential name…) as associated data, so a
/// tampered slot fails authentication instead of being silently weakened.
/// </summary>
public abstract class KeySlot
{
    public const int SaltBytes = 32;
    public const int NonceBytes = 12;
    public const int WrappedBytes = 48;

    public abstract KeySlotKind Kind { get; }

    public byte[] Salt { get; init; } = [];

    public byte[] Nonce { get; init; } = [];

    public byte[] WrappedKey { get; init; } = [];

    internal abstract void WriteParameters(BinaryOut output);

    internal virtual void WriteBody(BinaryOut output)
    {
        WriteParameters(output);
        output.Short(Nonce);
        output.Short(WrappedKey);
    }

    internal byte[] AssociatedData(Guid vaultId)
    {
        var output = new BinaryOut();
        output.Raw("OnlyYours/v1/slot"u8);
        output.Guid(vaultId);
        output.Byte((byte)Kind);
        WriteParameters(output);
        return output.ToArray();
    }
}

public sealed class PasswordSlot : KeySlot
{
    public override KeySlotKind Kind => KeySlotKind.Password;

    public required KdfParameters Kdf { get; init; }

    internal override void WriteParameters(BinaryOut output)
    {
        output.Byte((byte)Kdf.Algorithm);
        output.U32(Kdf.MemoryKiB);
        output.U32(Kdf.Iterations);
        output.U32(Kdf.Parallelism);
        output.Short(Salt);
    }
}

public sealed class RecoverySlot : KeySlot
{
    public override KeySlotKind Kind => KeySlotKind.Recovery;

    internal override void WriteParameters(BinaryOut output) => output.Short(Salt);
}

public sealed class HelloSlot : KeySlot
{
    public override KeySlotKind Kind => KeySlotKind.WindowsHello;

    public required string CredentialName { get; init; }

    public required byte[] Challenge { get; init; }

    internal override void WriteParameters(BinaryOut output)
    {
        output.Short(Encoding.UTF8.GetBytes(CredentialName));
        output.Short(Challenge);
        output.Short(Salt);
    }
}

/// <summary>A slot written by a newer version; kept verbatim so it survives a save.</summary>
public sealed class UnknownSlot : KeySlot
{
    public required byte RawKind { get; init; }

    public required byte[] Body { get; init; }

    public override KeySlotKind Kind => (KeySlotKind)RawKind;

    internal override void WriteParameters(BinaryOut output) => output.Raw(Body);

    internal override void WriteBody(BinaryOut output) => output.Raw(Body);
}

/// <summary>
/// Plaintext — but fully authenticated — header of a <c>.onlyyours</c> vault file.
/// <code>
/// magic[8]  89 'O' 'Y' 'V' 'T' 0D 0A 1A   (PNG-style: detects text-mode mangling)
/// u16 format version · u16 flags · u32 header length
/// vault id[16] · u64 write counter
/// u8 slot count · { u8 kind · u16 body length · body }*
/// payload salt[32] · ChaCha20 nonce[12] · AES-GCM nonce[12]
/// </code>
/// The header bytes are the associated data of both AEAD layers and are covered by
/// the HMAC-SHA-512 commitment, so no field can be altered without detection.
/// </summary>
public sealed class VaultHeader
{
    public const ushort CurrentFormatVersion = 1;
    public const int MagicLength = 8;
    public const int FixedPrefixLength = 41;
    public const int MaxHeaderLength = 64 * 1024;

    public static ReadOnlySpan<byte> Magic => [0x89, 0x4F, 0x59, 0x56, 0x54, 0x0D, 0x0A, 0x1A];

    public ushort FormatVersion { get; init; } = CurrentFormatVersion;

    public ushort Flags { get; init; }

    public required Guid VaultId { get; init; }

    /// <summary>Incremented on every save; the app remembers the highest value seen to detect rollbacks.</summary>
    public ulong WriteCounter { get; set; }

    public List<KeySlot> Slots { get; init; } = [];

    public byte[] PayloadSalt { get; set; } = new byte[32];

    public byte[] ChaChaNonce { get; set; } = new byte[CascadeAead.NonceBytes];

    public byte[] AesNonce { get; set; } = new byte[CascadeAead.NonceBytes];

    /// <summary>Length of the header as found in the parsed file.</summary>
    public int ParsedLength { get; private init; }

    public PasswordSlot? PasswordSlot => Slots.OfType<PasswordSlot>().FirstOrDefault();

    public RecoverySlot? RecoverySlot => Slots.OfType<RecoverySlot>().FirstOrDefault();

    public HelloSlot? HelloSlot => Slots.OfType<HelloSlot>().FirstOrDefault();

    public static bool HasMagic(ReadOnlySpan<byte> data) => data.Length >= MagicLength && data[..MagicLength].SequenceEqual(Magic);

    public VaultHeader CloneForSave() => new()
    {
        FormatVersion = CurrentFormatVersion,
        Flags = Flags,
        VaultId = VaultId,
        WriteCounter = WriteCounter,
        Slots = [.. Slots],
    };

    public byte[] Serialize()
    {
        var output = new BinaryOut();
        output.Raw(Magic);
        output.U16(CurrentFormatVersion);
        output.U16(Flags);
        output.U32(0); // patched below
        output.Guid(VaultId);
        output.U64(WriteCounter);
        if (Slots.Count > byte.MaxValue)
        {
            throw new InvalidOperationException("Too many key slots.");
        }

        output.Byte((byte)Slots.Count);
        foreach (var slot in Slots)
        {
            var body = new BinaryOut();
            slot.WriteBody(body);
            if (body.Length > ushort.MaxValue)
            {
                throw new InvalidOperationException("Key slot too large.");
            }

            output.Byte((byte)slot.Kind);
            output.U16((ushort)body.Length);
            output.Raw(body.Written);
        }

        output.Raw(PayloadSalt);
        output.Raw(ChaChaNonce);
        output.Raw(AesNonce);
        output.PatchU32(12, (uint)output.Length);
        return output.ToArray();
    }

    public static VaultHeader Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < MagicLength || !HasMagic(data))
        {
            throw new VaultException(VaultError.NotAVault, "The file does not carry the OnlyYours vault signature.");
        }

        var input = new BinaryIn(data);
        input.Raw(MagicLength);
        var version = input.U16();
        if (version != CurrentFormatVersion)
        {
            throw new VaultException(VaultError.UnsupportedVersion, $"Vault format version {version} is not supported.");
        }

        var flags = input.U16();
        var headerLength = input.U32();
        if (headerLength < FixedPrefixLength || headerLength > MaxHeaderLength || headerLength > data.Length)
        {
            throw new VaultException(VaultError.Truncated, "The vault header length is invalid.");
        }

        var vaultId = input.Guid();
        var counter = input.U64();
        var slotCount = input.U8();
        var slots = new List<KeySlot>(slotCount);
        for (var i = 0; i < slotCount; i++)
        {
            var kind = input.U8();
            var bodyLength = input.U16();
            var body = input.Raw(bodyLength);
            slots.Add(ParseSlot(kind, body));
        }

        var header = new VaultHeader
        {
            FormatVersion = version,
            Flags = flags,
            VaultId = vaultId,
            WriteCounter = counter,
            Slots = slots,
            PayloadSalt = input.Raw(32).ToArray(),
            ChaChaNonce = input.Raw(CascadeAead.NonceBytes).ToArray(),
            AesNonce = input.Raw(CascadeAead.NonceBytes).ToArray(),
            ParsedLength = (int)headerLength,
        };

        if (input.Position != headerLength)
        {
            throw new VaultException(VaultError.Corrupted, "The vault header length does not match its content.");
        }

        return header;
    }

    private static KeySlot ParseSlot(byte kind, ReadOnlySpan<byte> body)
    {
        var input = new BinaryIn(body);
        switch ((KeySlotKind)kind)
        {
            case KeySlotKind.Password:
            {
                var algorithm = (KdfAlgorithm)input.U8();
                var memory = input.U32();
                var iterations = input.U32();
                var parallelism = input.U32();
                var salt = input.Short().ToArray();
                var nonce = input.Short().ToArray();
                var wrapped = input.Short().ToArray();
                return new PasswordSlot
                {
                    Kdf = new KdfParameters(algorithm, memory, iterations, parallelism),
                    Salt = salt,
                    Nonce = nonce,
                    WrappedKey = wrapped,
                };
            }

            case KeySlotKind.Recovery:
                return new RecoverySlot
                {
                    Salt = input.Short().ToArray(),
                    Nonce = input.Short().ToArray(),
                    WrappedKey = input.Short().ToArray(),
                };

            case KeySlotKind.WindowsHello:
            {
                var name = Encoding.UTF8.GetString(input.Short());
                var challenge = input.Short().ToArray();
                return new HelloSlot
                {
                    CredentialName = name,
                    Challenge = challenge,
                    Salt = input.Short().ToArray(),
                    Nonce = input.Short().ToArray(),
                    WrappedKey = input.Short().ToArray(),
                };
            }

            default:
                return new UnknownSlot { RawKind = kind, Body = body.ToArray() };
        }
    }
}
