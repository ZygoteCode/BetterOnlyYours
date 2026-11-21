using OnlyYours.Core.Otp;
using OnlyYours.Core.Security.Crypto;
using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Format;
using OnlyYours.Core.Vault.Model;
using Xunit;

namespace OnlyYours.Core.Tests;

public class VaultLifecycleTests
{
    [Fact]
    public async Task CreateThenReopenPreservesEverything()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir, name: "  My vault  ");
        Guid id;
        using (session)
        {
            Assert.Equal("My vault", session.Document.Name);
            Assert.True(session.HasRecoveryKey);
            var entry = T.AddLogin(session, "GitHub", "ada@example.com", "Tr0ub4dor&3 🔐", "https://github.com", "note\nmultiriga");
            session.SetFavorite(entry.Id, true);
            id = entry.Id;
            await session.SaveNowAsync();
        }

        using var reopened = await T.Open(dir);
        Assert.Equal(UnlockMethod.Password, reopened.UnlockedWith);
        var loaded = reopened.Find(id)!;
        Assert.Equal("GitHub", loaded.Title);
        Assert.True(loaded.Favorite);
        Assert.Equal("ada@example.com", loaded.Username);
        Assert.Equal("https://github.com", loaded.Url);
        Assert.Equal("Tr0ub4dor&3 🔐", T.Reveal(reopened, loaded, loaded.PrimaryPassword!));
        Assert.Equal("note\nmultiriga", T.Reveal(reopened, loaded, loaded.FirstField(FieldKind.Notes)!));
        Assert.Null(loaded.PrimaryPassword!.Text);
        Assert.Contains(reopened.Document.Audit, a => a.Kind == AuditKind.VaultCreated);
    }

    [Fact]
    public async Task WrongPasswordIsRejected()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        session.Dispose();
        var error = await Assert.ThrowsAsync<VaultException>(() => T.Open(dir, "correct horse battery stapler"));
        Assert.Equal(VaultError.InvalidPassword, error.Error);
    }

    [Fact]
    public async Task RecoveryKeyOpensTheVaultAndWrongKeysDoNot()
    {
        using var dir = new TempDir();
        var (session, code) = await T.CreateVault(dir);
        session.Dispose();

        using (var draft = T.Draft(code.ToLowerInvariant().Replace("-", " ")))
        using (var recovered = await VaultSession.OpenWithRecoveryAsync(T.Storage(dir), draft))
        {
            Assert.Equal(UnlockMethod.RecoveryKey, recovered.UnlockedWith);
        }

        using var other = RecoveryKey.Generate(out var otherKey);
        otherKey.Dispose();
        using var wrong = SecretDraft.From(other.Span);
        var error = await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenWithRecoveryAsync(T.Storage(dir), wrong));
        Assert.Equal(VaultError.InvalidRecoveryKey, error.Error);
    }

    [Fact]
    public async Task EveryFlippedBitIsDetected()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            T.AddLogin(session, "A", "a", "secret");
            await session.SaveNowAsync();
        }

        var path = dir.File("vault.onlyyours");
        var original = File.ReadAllBytes(path);
        var header = VaultHeader.Parse(original);
        var probes = new[] { 9, 12, 20, header.ParsedLength - 1, header.ParsedLength + 5, original.Length / 2, original.Length - 1 };
        foreach (var offset in probes.Distinct())
        {
            var tampered = (byte[])original.Clone();
            tampered[offset] ^= 0x20;
            File.WriteAllBytes(path, tampered);
            var error = await Assert.ThrowsAsync<VaultException>(() => T.Open(dir));
            Assert.True(
                error.Error is VaultError.IntegrityFailure or VaultError.InvalidPassword or VaultError.Corrupted or VaultError.NotAVault or VaultError.UnsupportedVersion or VaultError.Truncated or VaultError.UnsafeParameters,
                $"offset {offset}: {error.Error}");
        }

        File.WriteAllBytes(path, original[..(original.Length / 3)]);
        await Assert.ThrowsAsync<VaultException>(() => T.Open(dir));
        File.WriteAllBytes(path, original);
        using var ok = await T.Open(dir);
        Assert.Single(ok.Entries);
    }

    [Fact]
    public async Task FileSizeIsPaddedSoItLeaksLittle()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            var empty = new FileInfo(dir.File("vault.onlyyours")).Length;
            T.AddLogin(session, "x", "y", "z");
            await session.SaveNowAsync();
            var one = new FileInfo(dir.File("vault.onlyyours")).Length;
            Assert.Equal(empty, one);
        }
    }

    [Fact]
    public async Task WriteCounterGrowsWithEverySave()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            var first = session.WriteCounter;
            T.AddLogin(session, "x", "y", "z");
            await session.SaveNowAsync();
            await session.SaveNowAsync();
            Assert.True(session.WriteCounter >= first + 2);
            var probe = session.Storage.Probe();
            Assert.Equal(session.WriteCounter, probe.Header!.WriteCounter);
        }
    }
}

public class VaultEntryTests
{
    [Fact]
    public async Task PasswordChangesKeepHistoryAndRestore()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            var entry = T.AddLogin(session, "Bank", "ada", "first");
            foreach (var next in new[] { "second", "third" })
            {
                using var draft = T.Draft(next);
                session.Apply(T.EditOf(entry, f => f.Kind == FieldKind.Password
                    ? new FieldEdit { Id = f.Id, Kind = f.Kind, Secret = draft, SecretChanged = true }
                    : new FieldEdit { Id = f.Id, Kind = f.Kind, Text = f.Text }));
            }

            Assert.Equal(2, entry.History.Count);
            Assert.Equal("second", Plain(session, entry, entry.History[0]));
            Assert.Equal("first", Plain(session, entry, entry.History[1]));

            Assert.True(session.RestoreHistory(entry.Id, entry.History[1].Id));
            Assert.Equal("first", T.Reveal(session, entry, entry.PrimaryPassword!));
            Assert.DoesNotContain(entry.History, h => Plain(session, entry, h) == "first");
            Assert.Contains(entry.History, h => Plain(session, entry, h) == "third");

            Assert.Equal(entry.History.Count, session.ClearHistory(entry.Id));
            Assert.Empty(entry.History);
        }
    }

    [Fact]
    public async Task HistoryCanBeTurnedOff()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            session.KeepPasswordHistory = false;
            var entry = T.AddLogin(session, "Bank", "ada", "first");
            using var draft = T.Draft("second");
            session.Apply(T.EditOf(entry, f => f.Kind == FieldKind.Password
                ? new FieldEdit { Id = f.Id, Kind = f.Kind, Secret = draft, SecretChanged = true }
                : new FieldEdit { Id = f.Id, Kind = f.Kind, Text = f.Text }));
            Assert.Empty(entry.History);
        }
    }

    [Fact]
    public async Task TrashRestorePurgeAndDuplicate()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            var a = T.AddLogin(session, "A", "a", "pa");
            var b = T.AddLogin(session, "B", "b", "pb");
            Assert.True(session.MoveToTrash(a.Id));
            Assert.DoesNotContain(session.Entries, e => e.Id == a.Id);
            Assert.Contains(session.Document.Trash, e => e.Id == a.Id);
            Assert.True(session.RestoreFromTrash(a.Id));
            Assert.Contains(session.Entries, e => e.Id == a.Id);

            var copy = session.Duplicate(b.Id, "(copy)")!;
            Assert.Equal("B (copy)", copy.Title);
            Assert.NotEqual(b.Id, copy.Id);
            Assert.Equal("pb", T.Reveal(session, copy, copy.PrimaryPassword!));

            session.MoveToTrash(b.Id);
            session.MoveToTrash(copy.Id);
            Assert.Equal(2, session.EmptyTrash());
            Assert.Empty(session.Document.Trash);
            Assert.Single(session.Entries);
        }
    }

    [Fact]
    public async Task ExpiredTrashIsPurged()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            var a = T.AddLogin(session, "A", "a", "pa");
            session.MoveToTrash(a.Id);
            session.Document.Trash[0].Deleted = DateTimeOffset.Now.AddDays(-40);
            Assert.Equal(1, session.PurgeExpiredTrash(TimeSpan.FromDays(30)));
        }
    }

    [Fact]
    public async Task TagsAreNormalizedRenamedAndDeleted()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            Assert.Equal(["Dev", "work"], VaultSession.NormalizeTags([" work ", "Dev", "WORK", "", "dev"]));
            var entry = T.AddLogin(session, "A", "a", "pa");
            var edit = T.EditOf(entry);
            edit.Tags.AddRange(["work", "home"]);
            session.Apply(edit);
            session.RenameTag("work", "office");
            Assert.Contains("office", entry.Tags);
            session.DeleteTag("home");
            Assert.DoesNotContain("home", entry.Tags);
        }
    }

    [Fact]
    public async Task TotpIsSealedAndGeneratesCodes()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        Guid id;
        using (session)
        {
            var entry = T.AddLogin(session, "GitHub", "ada", "pw");
            Assert.True(OtpAuthUri.TryParse("otpauth://totp/GitHub:ada?secret=GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ&digits=8", null, out var setup, out _));
            using (setup)
            {
                session.SetTotp(entry.Id, setup!);
            }

            Assert.True(entry.HasTotp);
            Assert.Null(entry.Totp!.Text);
            var code = session.GenerateTotp(entry, entry.Totp, DateTimeOffset.FromUnixTimeSeconds(59))!;
            Assert.Equal("94287082", code.Value.Value);
            using (var uri = session.ExportTotpUri(entry, entry.Totp))
            {
                Assert.StartsWith("otpauth://totp/", uri!.Span.ToString());
            }

            id = entry.Id;
            await session.SaveNowAsync();
        }

        using var reopened = await T.Open(dir);
        var loaded = reopened.Find(id)!;
        Assert.Equal("94287082", reopened.GenerateTotp(loaded, loaded.Totp!, DateTimeOffset.FromUnixTimeSeconds(59))!.Value.Value);
        Assert.True(reopened.RemoveTotp(id));
        Assert.False(loaded.HasTotp);
    }

    [Fact]
    public async Task UsageIsRecorded()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            var entry = T.AddLogin(session, "A", "a", "pa");
            session.RecordUse(entry.Id);
            session.RecordUse(entry.Id);
            Assert.Equal(2, entry.UseCount);
            Assert.NotNull(entry.LastUsed);
        }
    }

    private static string Plain(VaultSession session, Entry entry, HistoryItem item)
    {
        using var plain = session.RevealHistory(entry, item);
        return plain.Span.ToString();
    }
}

public class VaultKeyManagementTests
{
    [Fact]
    public async Task ChangingThePasswordWithoutRotation()
    {
        using var dir = new TempDir();
        var (session, code) = await T.CreateVault(dir);
        Guid id;
        using (session)
        {
            id = T.AddLogin(session, "A", "a", "secret-a").Id;
            using var newPassword = T.Draft("a brand new master password");
            var newCode = await session.ChangeMasterPasswordAsync(newPassword, T.FastKdf, rotateKey: false);
            Assert.Null(newCode);
            Assert.Contains(session.Document.Audit, a => a.Kind == AuditKind.MasterPasswordChanged);
        }

        await Assert.ThrowsAsync<VaultException>(() => T.Open(dir));
        using (var reopened = await T.Open(dir, "a brand new master password"))
        {
            var entry = reopened.Find(id)!;
            Assert.Equal("secret-a", T.Reveal(reopened, entry, entry.PrimaryPassword!));
        }

        using var draft = T.Draft(code);
        using var recovered = await VaultSession.OpenWithRecoveryAsync(T.Storage(dir), draft);
        Assert.NotNull(recovered.Find(id));
    }

    [Fact]
    public async Task RotatingTheVaultKeyReencryptsEverythingAndReplacesTheRecoveryKey()
    {
        using var dir = new TempDir();
        var (session, oldCode) = await T.CreateVault(dir);
        Guid id;
        byte[] oldBlob;
        string newCode;
        using (session)
        {
            var entry = T.AddLogin(session, "A", "a", "secret-a");
            id = entry.Id;
            oldBlob = entry.PrimaryPassword!.Sealed!.Blob;
            using var newPassword = T.Draft("rotated password");
            using var code = await session.ChangeMasterPasswordAsync(newPassword, T.FastKdf, rotateKey: true);
            Assert.NotNull(code);
            newCode = code!.Span.ToString();
            Assert.NotEqual(oldBlob, entry.PrimaryPassword!.Sealed!.Blob);
            Assert.Equal("secret-a", T.Reveal(session, entry, entry.PrimaryPassword!));
        }

        using (var oldDraft = T.Draft(oldCode))
        {
            await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenWithRecoveryAsync(T.Storage(dir), oldDraft));
        }

        using (var newDraft = T.Draft(newCode))
        using (var recovered = await VaultSession.OpenWithRecoveryAsync(T.Storage(dir), newDraft))
        {
            var entry = recovered.Find(id)!;
            Assert.Equal("secret-a", T.Reveal(recovered, entry, entry.PrimaryPassword!));
        }
    }

    [Fact]
    public async Task RegeneratingTheRecoveryKeyInvalidatesTheOldOne()
    {
        using var dir = new TempDir();
        var (session, oldCode) = await T.CreateVault(dir);
        string newCode;
        using (session)
        {
            using var code = await session.RegenerateRecoveryKeyAsync();
            newCode = code.Span.ToString();
        }

        using (var oldDraft = T.Draft(oldCode))
        {
            await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenWithRecoveryAsync(T.Storage(dir), oldDraft));
        }

        using var newDraft = T.Draft(newCode);
        using var ok = await VaultSession.OpenWithRecoveryAsync(T.Storage(dir), newDraft);
        Assert.Contains(ok.Document.Audit, a => a.Kind == AuditKind.RecoveryKeyRegenerated);
    }

    [Fact]
    public async Task KdfUpgradeIsPersisted()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        var stronger = new KdfParameters(KdfAlgorithm.Argon2id, 16 * 1024, 2, 2);
        using (session)
        {
            using var password = T.Draft(T.Password);
            Assert.True(await session.VerifyPasswordAsync(password));
            await session.UpgradeKdfAsync(password, stronger);
            Assert.Equal(stronger, session.PasswordKdf);
        }

        using var reopened = await T.Open(dir);
        Assert.Equal(stronger, reopened.PasswordKdf);
    }

    [Fact]
    public async Task HelloSlotRoundTripsWithAFakeAuthenticator()
    {
        using var dir = new TempDir();
        var hello = new FakeHello();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            await session.EnableHelloAsync(hello);
            Assert.True(session.HasHello);
        }

        using (var unlocked = await VaultSession.OpenWithHelloAsync(T.Storage(dir), hello))
        {
            Assert.Equal(UnlockMethod.WindowsHello, unlocked.UnlockedWith);
            await unlocked.DisableHelloAsync(hello);
            Assert.False(unlocked.HasHello);
        }

        await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenWithHelloAsync(T.Storage(dir), hello));
    }

    [Fact]
    public async Task AWrongHelloSignatureFails()
    {
        using var dir = new TempDir();
        var hello = new FakeHello();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            await session.EnableHelloAsync(hello);
        }

        var error = await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenWithHelloAsync(T.Storage(dir), new FakeHello(seed: 99)));
        Assert.Equal(VaultError.HelloFailed, error.Error);
    }

    /// <summary>Deterministic stand-in for KeyCredentialManager: signature = HMAC(seed, challenge).</summary>
    private sealed class FakeHello(int seed = 1) : IHelloProvider
    {
        public Task<bool> IsAvailableAsync() => Task.FromResult(true);

        public Task<byte[]?> SignAsync(string credentialName, byte[] challenge, bool createIfMissing, CancellationToken cancellationToken = default)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(BitConverter.GetBytes(seed));
            return Task.FromResult<byte[]?>(hmac.ComputeHash(challenge));
        }

        public Task DeleteAsync(string credentialName) => Task.CompletedTask;
    }
}

public class VaultBackupTests
{
    [Fact]
    public async Task SavesCreateBackupsThatCanBeRestored()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        session.Dispose();

        using (var first = await T.Open(dir))
        {
            T.AddLogin(first, "Kept", "a", "pa");
            await first.SaveNowAsync();
        }

        using var second = await T.Open(dir);
        T.AddLogin(second, "Later", "b", "pb");
        await second.SaveNowAsync(forceBackup: true);
        var backups = second.ListBackups();
        Assert.NotEmpty(backups);
        Assert.All(backups, b => Assert.True(File.Exists(b.Path)));

        // Background and explicit saves may interleave, so find the backup by its content:
        // every backup is a complete vault that opens with the password of its time.
        BackupInfo? kept = null;
        foreach (var backup in backups)
        {
            using var password = T.Draft(T.Password);
            using var probe = await VaultSession.OpenAsync(new VaultStorage(backup.Path, dir.File("probe-backups")), password);
            if (probe.Entries.Any(e => e.Title == "Kept") && probe.Entries.All(e => e.Title != "Later"))
            {
                kept = backup;
                break;
            }
        }

        Assert.NotNull(kept);
        await second.RestoreBackupAsync(kept, null);
        Assert.Contains(second.Entries, e => e.Title == "Kept");
        Assert.DoesNotContain(second.Entries, e => e.Title == "Later");
        Assert.Contains(second.Document.Audit, a => a.Kind == AuditKind.BackupRestored);
    }

    [Fact]
    public void BackupNamesSortByCounter()
    {
        var id = Guid.NewGuid();
        var a = VaultStorage.BackupName(id, 9, DateTimeOffset.Now);
        var b = VaultStorage.BackupName(id, 10, DateTimeOffset.Now);
        Assert.True(string.CompareOrdinal(a, b) < 0);
    }

    [Fact]
    public async Task ProbeDescribesTheFileWithoutAKey()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        session.Dispose();
        var probe = T.Storage(dir).Probe();
        Assert.True(probe.Exists);
        Assert.False(probe.IsLegacyFormat);
        Assert.NotNull(probe.Header);
        Assert.Null(probe.Error);

        File.WriteAllText(dir.File("vault.onlyyours"), "not a vault");
        Assert.Equal(VaultError.NotAVault, T.Storage(dir).Probe().Error);
    }
}

public class FieldCipherTests
{
    [Fact]
    public void ValuesAreBoundToTheirOwnerAndSlot()
    {
        using var cipher = FieldCipher.CreateEphemeral();
        var owner = Guid.NewGuid();
        var slot = Guid.NewGuid();
        var sealedValue = cipher.SealChars("s3cret", owner, slot);
        using (var plain = cipher.OpenChars(sealedValue, owner, slot))
        {
            Assert.Equal("s3cret", plain.Span.ToString());
        }

        Assert.False(cipher.TryOpen(sealedValue, Guid.NewGuid(), slot, out _));
        Assert.False(cipher.TryOpen(sealedValue, owner, Guid.NewGuid(), out _));

        var moved = cipher.Reseal(sealedValue, owner, slot, slot, owner);
        Assert.True(cipher.TryOpen(moved, slot, owner, out var reopened));
        reopened!.Dispose();
    }

    [Fact]
    public void SealingTheSameValueTwiceGivesDifferentBlobs()
    {
        using var cipher = FieldCipher.CreateEphemeral();
        var owner = Guid.NewGuid();
        var a = cipher.SealChars("same", owner, owner);
        var b = cipher.SealChars("same", owner, owner);
        Assert.NotEqual(a.Blob, b.Blob);
    }

    [Fact]
    public void AnotherKeyCannotOpen()
    {
        using var one = FieldCipher.CreateEphemeral();
        using var two = FieldCipher.CreateEphemeral();
        var id = Guid.NewGuid();
        var sealedValue = one.SealChars("x", id, id);
        Assert.False(two.TryOpen(sealedValue, id, id, out _));
        var rewrapped = one.Rewrap(sealedValue, id, id, two);
        Assert.True(two.TryOpen(rewrapped, id, id, out var plain));
        plain!.Dispose();
    }
}

public class DocumentFormatTests
{
    [Fact]
    public void UnknownFieldsSurviveARoundTrip()
    {
        var document = new VaultDocument { Name = "Doc", Created = DateTimeOffset.UnixEpoch, Modified = DateTimeOffset.UnixEpoch };
        var entry = new Entry { Title = "E", Kind = EntryKind.Login, Created = DateTimeOffset.UnixEpoch, Modified = DateTimeOffset.UnixEpoch };
        var field = new Field { Kind = FieldKind.Username, Text = "ada" };

        // Encoded TLV fields from a hypothetical future version (field numbers 900+).
        var future = new TlvProbe().Field(901, "future-entry-data"u8);
        entry.Unknown.Add(future);
        field.Unknown.Add(new TlvProbe().Field(902, "future-field-data"u8));
        document.Unknown.Add(new TlvProbe().Field(903, "future-document-data"u8));
        entry.Fields.Add(field);
        document.Entries.Add(entry);

        using var first = DocumentSerializer.Serialize(document);
        var parsed = DocumentSerializer.Deserialize(first.Span);
        using var second = DocumentSerializer.Serialize(parsed);
        Assert.Equal(first.Span.ToArray(), second.Span.ToArray());
        Assert.Single(parsed.Entries[0].Unknown);
        Assert.Single(parsed.Entries[0].Fields[0].Unknown);
        Assert.Single(parsed.Unknown);
        Assert.Equal("ada", parsed.Entries[0].Fields[0].Text);
    }

    [Fact]
    public void TlvRejectsTruncatedInput()
    {
        var document = new VaultDocument { Name = "Doc" };
        document.Entries.Add(new Entry { Title = "E" });
        using var data = DocumentSerializer.Serialize(document);
        Assert.ThrowsAny<Exception>(() => DocumentSerializer.Deserialize(data.Span[..(data.Length - 3)]));
    }

    /// <summary>Builds one raw TLV field the way the serializer stores unknown data.</summary>
    private sealed class TlvProbe
    {
        public byte[] Field(int number, ReadOnlySpan<byte> payload)
        {
            using var writer = new TlvWriter();
            writer.WriteBytes(number, payload);
            using var buffer = writer.ToSecureBuffer();
            return buffer.Span.ToArray();
        }
    }
}
