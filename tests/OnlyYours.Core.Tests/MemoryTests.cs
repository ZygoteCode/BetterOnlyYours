using OnlyYours.Core.Security.Memory;
using Xunit;

namespace OnlyYours.Core.Tests;

public class ProtectedBufferTests
{
    [Fact]
    public void SealsAtRestAndOpensOnlyThroughALease()
    {
        var data = "top secret key material!!"u8.ToArray();
        using var buffer = ProtectedBuffer.Create(data);
        Assert.Equal(data.Length, buffer.Length);
        Assert.True(buffer.IsSealed);

        using (var lease = buffer.Unprotect())
        {
            Assert.False(buffer.IsSealed);
            Assert.Equal(data, lease.Span.ToArray());
        }

        Assert.True(buffer.IsSealed);
        Assert.True(buffer.FixedTimeEquals(data));
        Assert.False(buffer.FixedTimeEquals("top secret key material!?"u8));
    }

    [Fact]
    public void NestedLeasesKeepTheBufferOpenUntilTheLastOneEnds()
    {
        using var buffer = ProtectedBuffer.CreateRandom(32);
        var outer = buffer.Unprotect();
        var inner = buffer.Unprotect();
        inner.Dispose();
        Assert.False(buffer.IsSealed);
        outer.Dispose();
        Assert.True(buffer.IsSealed);
    }

    [Fact]
    public void CloneAndCompare()
    {
        using var a = ProtectedBuffer.Create(32, span => span.Fill(7));
        using var b = a.Clone();
        Assert.True(a.FixedTimeEquals(b));
        using var c = ProtectedBuffer.CreateZeroed(32);
        Assert.False(a.FixedTimeEquals(c));
    }

    [Fact]
    public void DisposedBufferRefusesAccess()
    {
        var buffer = ProtectedBuffer.CreateRandom(16);
        buffer.Dispose();
        Assert.True(buffer.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() =>
        {
            using var lease = buffer.Unprotect();
        });
    }

    [Fact]
    public void StatusTracksLiveRegions()
    {
        using (var buffer = SecureBuffer.Allocate(100))
        {
            var during = SecureMemory.Status;
            Assert.True(during.Regions >= 1);
            Assert.True(during.LockedBytes + during.UnlockedBytes >= buffer.Capacity);
        }
    }
}

public class SecureCharBufferTests
{
    [Fact]
    public void EditsLikeAStringBuilderWithoutStrings()
    {
        using var buffer = new SecureCharBuffer(2);
        buffer.Append("hello");
        buffer.Append(' ');
        buffer.Append("world");
        buffer.Insert(0, ">> ");
        buffer.Remove(3, 6);
        Assert.True(buffer.ContentEquals(">> world"));
        buffer.Truncate(2);
        Assert.True(buffer.ContentEquals(">>"));
        buffer.Clear();
        Assert.True(buffer.IsEmpty);
    }

    [Fact]
    public void Utf8RoundTripsAnyUnicode()
    {
        const string text = "Pàss€wörd!🔐 κλειδί";
        using var chars = SecureCharBuffer.CopyOf(text);
        using var utf8 = chars.ToUtf8();
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(text), utf8.Span.ToArray());
        using var back = SecureCharBuffer.FromUtf8(utf8.Span);
        Assert.Equal(text, back.Span.ToString());
    }

    [Fact]
    public void GrowsBeyondItsInitialCapacity()
    {
        using var buffer = new SecureCharBuffer(1);
        for (var i = 0; i < 5000; i++)
        {
            buffer.Append((char)('a' + (i % 26)));
        }

        Assert.Equal(5000, buffer.Length);
        Assert.Equal('a', buffer.Span[0]);
        Assert.Equal((char)('a' + (4999 % 26)), buffer.Span[4999]);
    }
}

public class SecretDraftTests
{
    [Fact]
    public void TracksEditsAndRaisesChanged()
    {
        using var draft = new SecretDraft();
        var changes = 0;
        draft.Changed += (_, _) => changes++;
        draft.Append("abc");
        draft.Insert(1, "XY");
        draft.Remove(0, 1);
        Assert.Equal(3, changes);
        Assert.True(draft.Version >= 3);
        using (var lease = draft.Read())
        {
            Assert.Equal("XYbc", lease.Chars.ToString());
        }

        draft.Set("replaced");
        using var other = SecretDraft.From("replaced");
        Assert.True(draft.ContentEquals(other));
        draft.Clear();
        Assert.True(draft.IsEmpty);
    }

    [Fact]
    public void ConvertsToUtf8AndBack()
    {
        using var draft = SecretDraft.From("ünïcødé ✓");
        using var utf8 = draft.ToUtf8();
        using var copy = new SecretDraft();
        copy.SetUtf8(utf8.Span);
        Assert.True(copy.ContentEquals(draft));
        using var chars = draft.ToCharBuffer();
        Assert.Equal("ünïcødé ✓", chars.Span.ToString());
    }

    [Fact]
    public void CopyFromDuplicatesContent()
    {
        using var source = SecretDraft.From("source");
        using var target = SecretDraft.From("old");
        target.CopyFrom(source);
        Assert.True(target.ContentEquals(source));
    }
}
