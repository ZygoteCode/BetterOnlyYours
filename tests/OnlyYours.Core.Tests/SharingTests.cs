using OnlyYours.Core.Security.Crypto;
using OnlyYours.Core.Sharing;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Model;
using Xunit;

namespace OnlyYours.Core.Tests;

public class SealedPackageTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static readonly KdfParameters PackageKdf = T.FastKdf;

    [Fact]
    public async Task PasswordPackagesRoundTrip()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            var entry = T.AddLogin(session, "Shared", "ada", "s3cr3t ✓", "https://example.org", "notes");
            using var password = T.Draft("package password");
            var package = SealedPackage.CreateWithPassword(session, [entry], password, PackageKdf, "Ada", "ciao");
            Assert.True(SealedPackage.LooksLikePackage(package));
            var info = SealedPackage.Inspect(package);
            Assert.Equal(PackageMode.Password, info.Mode);

            using var wrong = T.Draft("nope");
            Assert.ThrowsAny<Exception>(() => SealedPackage.OpenWithPassword(package, wrong));

            using var opened = SealedPackage.OpenWithPassword(package, password);
            Assert.Equal("ciao", opened.Message);
            Assert.Single(opened.Entries);

            using var targetDir = new TempDir();
            var (target, _) = await T.CreateVault(targetDir);
            using (target)
            {
                var imported = SealedPackage.ImportInto(opened, target);
                var copy = Assert.Single(imported);
                Assert.Equal("Shared", copy.Title);
                Assert.Equal("ada", copy.Username);
                Assert.Equal("s3cr3t ✓", T.Reveal(target, copy, copy.PrimaryPassword!));
                Assert.Equal("notes", T.Reveal(target, copy, copy.FirstField(FieldKind.Notes)!));
            }
        }
    }

    [Fact]
    public async Task TamperedPasswordPackagesAreRejected()
    {
        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        using (session)
        {
            var entry = T.AddLogin(session, "Shared", "ada", "secret");
            using var password = T.Draft("package password");
            var package = SealedPackage.CreateWithPassword(session, [entry], password, PackageKdf, null, null);
            foreach (var offset in new[] { 10, package.Length / 2, package.Length - 1 })
            {
                var copy = (byte[])package.Clone();
                copy[offset] ^= 1;
                Assert.ThrowsAny<Exception>(() => SealedPackage.OpenWithPassword(copy, password));
            }
        }
    }

    [Fact]
    public async Task RecipientPackagesAreSignedAndOnlyOpenForTheRecipient()
    {
        output.WriteLine($"ML-KEM/ML-DSA supported: {IdentityKeys.IsSupported}");
        if (!IdentityKeys.IsSupported)
        {
            return; // ML-KEM / ML-DSA need an up-to-date Windows 11.
        }

        using var aliceDir = new TempDir();
        using var bobDir = new TempDir();
        using var eveDir = new TempDir();
        var (alice, _) = await T.CreateVault(aliceDir);
        var (bob, _) = await T.CreateVault(bobDir);
        var (eve, _) = await T.CreateVault(eveDir);
        using (alice)
        using (bob)
        using (eve)
        {
            var alicePublic = IdentityKeys.CreateIn(alice, "Alice");
            var bobPublic = IdentityKeys.CreateIn(bob, "Bob");
            IdentityKeys.CreateIn(eve, "Eve");

            // The public identity survives its text form, fingerprint included.
            Assert.True(PublicIdentity.TryParseText(bobPublic.ToText(), out var parsedBob));
            Assert.Equal(bobPublic.Fingerprint, parsedBob!.Fingerprint);
            Assert.Equal(20, bobPublic.Fingerprint.Length);
            Assert.Matches("^[0-9A-F]{4}( [0-9A-F]{4}){9}$", bobPublic.FingerprintText);

            var entry = T.AddLogin(alice, "For Bob", "alice", "pw-for-bob");
            byte[] package;
            using (var aliceKeys = IdentityKeys.Load(alice))
            {
                package = SealedPackage.CreateForRecipient(alice, aliceKeys, parsedBob, [entry], "hi Bob");
            }

            var info = SealedPackage.Inspect(package);
            Assert.Equal(PackageMode.Recipient, info.Mode);
            Assert.Equal(alicePublic.Fingerprint, info.Sender!.Fingerprint);

            using (var eveKeys = IdentityKeys.Load(eve))
            {
                Assert.ThrowsAny<Exception>(() => SealedPackage.OpenForRecipient(package, eveKeys));
            }

            using var bobKeys = IdentityKeys.Load(bob);
            using (var opened = SealedPackage.OpenForRecipient(package, bobKeys))
            {
                Assert.Equal(alicePublic.Fingerprint, opened.Sender!.Fingerprint);
                Assert.Equal("hi Bob", opened.Message);
                var imported = Assert.Single(SealedPackage.ImportInto(opened, bob));
                Assert.Equal("pw-for-bob", T.Reveal(bob, imported, imported.PrimaryPassword!));
            }

            foreach (var offset in new[] { 12, package.Length / 3, package.Length / 2, package.Length - 2 })
            {
                var tampered = (byte[])package.Clone();
                tampered[offset] ^= 0x10;
                Assert.ThrowsAny<Exception>(() => SealedPackage.OpenForRecipient(tampered, bobKeys));
            }
        }
    }

    [Fact]
    public async Task IdentitySurvivesAVaultReopen()
    {
        if (!IdentityKeys.IsSupported)
        {
            return;
        }

        using var dir = new TempDir();
        var (session, _) = await T.CreateVault(dir);
        PublicIdentity created;
        using (session)
        {
            created = IdentityKeys.CreateIn(session, "Ada");
            await session.SaveNowAsync();
        }

        using var reopened = await T.Open(dir);
        var loaded = IdentityKeys.PublicOf(reopened)!;
        Assert.Equal(created.Fingerprint, loaded.Fingerprint);
        using var keys = IdentityKeys.Load(reopened);
        Assert.Equal(created.Fingerprint, keys.Public.Fingerprint);
    }
}
