using System.Text;
using OnlyYours.Core.Security.Crypto;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.Core.Tests;

/// <summary>A throw-away directory under %TEMP% — tests never touch the real profile.</summary>
public sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "onlyyours-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // A lingering handle on CI must not fail the test that already passed.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal static class T
{
    /// <summary>The lightest parameters the format accepts: keeps the suite fast.</summary>
    public static readonly KdfParameters FastKdf = new(KdfAlgorithm.Argon2id, KdfParameters.MinMemoryKiB, 1, 1);

    public const string Password = "correct horse battery staple";

    public static SecretDraft Draft(string text) => SecretDraft.From(text);

    public static byte[] Hex(string hex) => Convert.FromHexString(hex);

    public static string ToHex(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(bytes);

    public static string FixturePath(params string[] parts) =>
        System.IO.Path.Combine([AppContext.BaseDirectory, "Fixtures", .. parts]);

    public static async Task<(VaultSession Session, string RecoveryCode)> CreateVault(TempDir dir, string password = Password, string name = "Test")
    {
        var storage = new VaultStorage(dir.File("vault.onlyyours"), dir.File("backups"));
        using var draft = Draft(password);
        var (session, code) = await VaultSession.CreateAsync(storage, name, draft, FastKdf);
        using (code)
        {
            return (session, code.Span.ToString());
        }
    }

    public static VaultStorage Storage(TempDir dir) => new(dir.File("vault.onlyyours"), dir.File("backups"));

    public static async Task<VaultSession> Open(TempDir dir, string password = Password)
    {
        using var draft = Draft(password);
        return await VaultSession.OpenAsync(Storage(dir), draft);
    }

    public static Entry AddLogin(VaultSession session, string title, string username, string password, string? url = null, string? notes = null)
    {
        using var secret = Draft(password);
        using var notesDraft = Draft(notes ?? string.Empty);
        var edit = new EntryEdit
        {
            Kind = EntryKind.Login,
            Title = title,
            Fields =
            [
                new FieldEdit { Kind = FieldKind.Username, Text = username },
                new FieldEdit { Kind = FieldKind.Password, Secret = secret, SecretChanged = true },
                new FieldEdit { Kind = FieldKind.Url, Text = url },
                new FieldEdit { Kind = FieldKind.Notes, Secret = notesDraft, SecretChanged = true },
            ],
        };
        return session.Apply(edit);
    }

    public static EntryEdit EditOf(Entry entry, Func<Field, FieldEdit>? map = null) => new()
    {
        EntryId = entry.Id,
        Kind = entry.Kind,
        Title = entry.Title,
        Favorite = entry.Favorite,
        Tags = [.. entry.Tags],
        Expires = entry.Expires,
        AutoType = entry.AutoType,
        Fields = [.. entry.Fields.Select(f => map?.Invoke(f) ?? new FieldEdit { Id = f.Id, Kind = f.Kind, Label = f.Label, Flags = f.Flags, Text = f.Text })],
    };

    public static string Reveal(VaultSession session, Entry entry, Field field)
    {
        using var plain = session.Reveal(entry, field);
        return plain.Span.ToString();
    }

    public static string Utf8(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);
}
