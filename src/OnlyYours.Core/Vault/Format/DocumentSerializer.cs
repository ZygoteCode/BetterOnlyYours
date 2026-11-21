using OnlyYours.Core.Security.Memory;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.Core.Vault.Format;

/// <summary>
/// TLV encoding of <see cref="VaultDocument"/>. Field numbers are part of the file
/// format and must never be reused; unknown fields are carried through untouched
/// so a newer vault opened by an older build loses nothing.
/// </summary>
public static class DocumentSerializer
{
    private const int DocSchema = 1;
    private const int DocName = 2;
    private const int DocCreated = 3;
    private const int DocModified = 4;
    private const int DocEntry = 5;
    private const int DocTrash = 6;
    private const int DocIdentity = 7;
    private const int DocContact = 8;
    private const int DocAudit = 9;
    private const int DocTag = 10;

    private const int EntryId = 1;
    private const int EntryKindTag = 2;
    private const int EntryTitle = 3;
    private const int EntryFavorite = 4;
    private const int EntryTagName = 5;
    private const int EntryCreated = 6;
    private const int EntryModified = 7;
    private const int EntryLastUsed = 8;
    private const int EntryUseCount = 9;
    private const int EntryExpires = 10;
    private const int EntryDeleted = 11;
    private const int EntryField = 12;
    private const int EntryHistory = 13;
    private const int EntryAutoType = 14;

    private const int FieldId = 1;
    private const int FieldKindTag = 2;
    private const int FieldLabel = 3;
    private const int FieldText = 4;
    private const int FieldSealed = 5;
    private const int FieldFlagsTag = 6;
    private const int FieldModified = 7;

    private const int HistoryId = 1;
    private const int HistoryField = 2;
    private const int HistoryValue = 3;
    private const int HistoryReplaced = 4;

    private const int IdentityCreated = 1;
    private const int IdentityName = 2;
    private const int IdentityEcdh = 3;
    private const int IdentityKem = 4;
    private const int IdentityDsa = 5;
    private const int IdentityEcdsa = 6;
    private const int IdentityBundle = 7;

    private const int ContactId = 1;
    private const int ContactName = 2;
    private const int ContactBundle = 3;
    private const int ContactAdded = 4;
    private const int ContactVerified = 5;
    private const int ContactNote = 6;

    private const int AuditAt = 1;
    private const int AuditKindTag = 2;
    private const int AuditDetail = 3;

    private const int TagName = 1;
    private const int TagColor = 2;

    public static SecureBuffer Serialize(VaultDocument document)
    {
        using var writer = new TlvWriter(16 * 1024);
        writer.WriteVarint(DocSchema, (ulong)document.Schema);
        writer.WriteString(DocName, document.Name);
        writer.WriteTimestamp(DocCreated, document.Created);
        writer.WriteTimestamp(DocModified, document.Modified);

        foreach (var entry in document.Entries)
        {
            var at = writer.BeginMessage(DocEntry);
            WriteEntry(writer, entry);
            writer.EndMessage(at);
        }

        foreach (var entry in document.Trash)
        {
            var at = writer.BeginMessage(DocTrash);
            WriteEntry(writer, entry);
            writer.EndMessage(at);
        }

        if (document.Identity is { } identity)
        {
            var at = writer.BeginMessage(DocIdentity);
            writer.WriteTimestamp(IdentityCreated, identity.Created);
            writer.WriteString(IdentityName, identity.DisplayName);
            writer.WriteBytes(IdentityEcdh, identity.EcdhPrivate.Blob);
            writer.WriteBytes(IdentityKem, identity.MlKemSeed.Blob);
            writer.WriteBytes(IdentityDsa, identity.MlDsaSeed.Blob);
            writer.WriteBytes(IdentityEcdsa, identity.EcdsaPrivate.Blob);
            writer.WriteBytes(IdentityBundle, identity.PublicBundle);
            writer.EndMessage(at);
        }

        foreach (var contact in document.Contacts)
        {
            var at = writer.BeginMessage(DocContact);
            writer.WriteGuid(ContactId, contact.Id);
            writer.WriteString(ContactName, contact.Name);
            writer.WriteBytes(ContactBundle, contact.PublicBundle);
            writer.WriteTimestamp(ContactAdded, contact.Added);
            writer.WriteBool(ContactVerified, contact.Verified);
            writer.WriteString(ContactNote, contact.Note);
            writer.EndMessage(at);
        }

        foreach (var audit in document.Audit)
        {
            var at = writer.BeginMessage(DocAudit);
            writer.WriteTimestamp(AuditAt, audit.At);
            writer.WriteVarint(AuditKindTag, (ulong)audit.Kind);
            writer.WriteString(AuditDetail, audit.Detail);
            writer.EndMessage(at);
        }

        foreach (var tag in document.TagInfos)
        {
            var at = writer.BeginMessage(DocTag);
            writer.WriteString(TagName, tag.Name);
            writer.WriteVarint(TagColor, (ulong)tag.Color);
            writer.EndMessage(at);
        }

        foreach (var raw in document.Unknown)
        {
            writer.WriteRaw(raw);
        }

        return writer.ToSecureBuffer();
    }

    private static void WriteEntry(TlvWriter writer, Entry entry)
    {
        writer.WriteGuid(EntryId, entry.Id);
        writer.WriteVarint(EntryKindTag, (ulong)entry.Kind);
        writer.WriteString(EntryTitle, entry.Title);
        writer.WriteBool(EntryFavorite, entry.Favorite);
        foreach (var tag in entry.Tags)
        {
            writer.WriteString(EntryTagName, tag);
        }

        writer.WriteTimestamp(EntryCreated, entry.Created);
        writer.WriteTimestamp(EntryModified, entry.Modified);
        writer.WriteTimestamp(EntryLastUsed, entry.LastUsed);
        if (entry.UseCount > 0)
        {
            writer.WriteVarint(EntryUseCount, (ulong)entry.UseCount);
        }

        writer.WriteTimestamp(EntryExpires, entry.Expires);
        writer.WriteTimestamp(EntryDeleted, entry.Deleted);
        foreach (var field in entry.Fields)
        {
            var at = writer.BeginMessage(EntryField);
            writer.WriteGuid(FieldId, field.Id);
            writer.WriteVarint(FieldKindTag, (ulong)field.Kind);
            writer.WriteString(FieldLabel, field.Label);
            writer.WriteString(FieldText, field.Text);
            if (field.Sealed is { } sealedValue)
            {
                writer.WriteBytes(FieldSealed, sealedValue.Blob);
            }

            if (field.Flags != FieldFlags.None)
            {
                writer.WriteVarint(FieldFlagsTag, (ulong)field.Flags);
            }

            writer.WriteTimestamp(FieldModified, field.Modified);
            foreach (var raw in field.Unknown)
            {
                writer.WriteRaw(raw);
            }

            writer.EndMessage(at);
        }

        foreach (var item in entry.History)
        {
            var at = writer.BeginMessage(EntryHistory);
            writer.WriteGuid(HistoryId, item.Id);
            writer.WriteGuid(HistoryField, item.FieldId);
            writer.WriteBytes(HistoryValue, item.Value.Blob);
            writer.WriteTimestamp(HistoryReplaced, item.ReplacedAt);
            writer.EndMessage(at);
        }

        writer.WriteString(EntryAutoType, entry.AutoType);
        foreach (var raw in entry.Unknown)
        {
            writer.WriteRaw(raw);
        }
    }

    public static VaultDocument Deserialize(ReadOnlySpan<byte> data)
    {
        var document = new VaultDocument();
        var reader = new TlvReader(data);
        while (reader.TryReadField(out var field, out var type))
        {
            switch (field)
            {
                case DocSchema when type == WireType.Varint:
                    document.Schema = reader.ReadInt32();
                    break;
                case DocName when type == WireType.Bytes:
                    document.Name = reader.ReadString();
                    break;
                case DocCreated when type == WireType.Fixed64:
                    document.Created = reader.ReadTimestamp();
                    break;
                case DocModified when type == WireType.Fixed64:
                    document.Modified = reader.ReadTimestamp();
                    break;
                case DocEntry when type == WireType.Bytes:
                    document.Entries.Add(ReadEntry(reader.ReadBytes()));
                    break;
                case DocTrash when type == WireType.Bytes:
                    document.Trash.Add(ReadEntry(reader.ReadBytes()));
                    break;
                case DocIdentity when type == WireType.Bytes:
                    document.Identity = ReadIdentity(reader.ReadBytes());
                    break;
                case DocContact when type == WireType.Bytes:
                    if (ReadContact(reader.ReadBytes()) is { } contact)
                    {
                        document.Contacts.Add(contact);
                    }

                    break;
                case DocAudit when type == WireType.Bytes:
                    document.Audit.Add(ReadAudit(reader.ReadBytes()));
                    break;
                case DocTag when type == WireType.Bytes:
                    if (ReadTag(reader.ReadBytes()) is { } tag)
                    {
                        document.TagInfos.Add(tag);
                    }

                    break;
                default:
                    document.Unknown.Add(reader.Capture(type));
                    break;
            }
        }

        return document;
    }

    private static Entry ReadEntry(ReadOnlySpan<byte> data)
    {
        var entry = new Entry();
        var reader = new TlvReader(data);
        while (reader.TryReadField(out var field, out var type))
        {
            switch (field)
            {
                case EntryId when type == WireType.Bytes:
                    entry.Id = reader.ReadGuid();
                    break;
                case EntryKindTag when type == WireType.Varint:
                    entry.Kind = (EntryKind)reader.ReadInt32();
                    break;
                case EntryTitle when type == WireType.Bytes:
                    entry.Title = reader.ReadString();
                    break;
                case EntryFavorite when type == WireType.Varint:
                    entry.Favorite = reader.ReadBool();
                    break;
                case EntryTagName when type == WireType.Bytes:
                    entry.Tags.Add(reader.ReadString());
                    break;
                case EntryCreated when type == WireType.Fixed64:
                    entry.Created = reader.ReadTimestamp();
                    break;
                case EntryModified when type == WireType.Fixed64:
                    entry.Modified = reader.ReadTimestamp();
                    break;
                case EntryLastUsed when type == WireType.Fixed64:
                    entry.LastUsed = reader.ReadTimestamp();
                    break;
                case EntryUseCount when type == WireType.Varint:
                    entry.UseCount = reader.ReadInt32();
                    break;
                case EntryExpires when type == WireType.Fixed64:
                    entry.Expires = reader.ReadTimestamp();
                    break;
                case EntryDeleted when type == WireType.Fixed64:
                    entry.Deleted = reader.ReadTimestamp();
                    break;
                case EntryField when type == WireType.Bytes:
                    entry.Fields.Add(ReadField(reader.ReadBytes()));
                    break;
                case EntryHistory when type == WireType.Bytes:
                    if (ReadHistory(reader.ReadBytes()) is { } item)
                    {
                        entry.History.Add(item);
                    }

                    break;
                case EntryAutoType when type == WireType.Bytes:
                    entry.AutoType = reader.ReadString();
                    break;
                default:
                    entry.Unknown.Add(reader.Capture(type));
                    break;
            }
        }

        return entry;
    }

    private static Field ReadField(ReadOnlySpan<byte> data)
    {
        var result = new Field();
        var reader = new TlvReader(data);
        while (reader.TryReadField(out var field, out var type))
        {
            switch (field)
            {
                case FieldId when type == WireType.Bytes:
                    result.Id = reader.ReadGuid();
                    break;
                case FieldKindTag when type == WireType.Varint:
                    result.Kind = (FieldKind)reader.ReadInt32();
                    break;
                case FieldLabel when type == WireType.Bytes:
                    result.Label = reader.ReadString();
                    break;
                case FieldText when type == WireType.Bytes:
                    result.Text = reader.ReadString();
                    break;
                case FieldSealed when type == WireType.Bytes:
                    result.Sealed = new SealedValue(reader.ReadBytes().ToArray());
                    break;
                case FieldFlagsTag when type == WireType.Varint:
                    result.Flags = (FieldFlags)reader.ReadInt32();
                    break;
                case FieldModified when type == WireType.Fixed64:
                    result.Modified = reader.ReadTimestamp();
                    break;
                default:
                    result.Unknown.Add(reader.Capture(type));
                    break;
            }
        }

        return result;
    }

    private static HistoryItem? ReadHistory(ReadOnlySpan<byte> data)
    {
        Guid id = Guid.Empty, fieldId = Guid.Empty;
        byte[]? value = null;
        DateTimeOffset replaced = default;
        var reader = new TlvReader(data);
        while (reader.TryReadField(out var field, out var type))
        {
            switch (field)
            {
                case HistoryId when type == WireType.Bytes:
                    id = reader.ReadGuid();
                    break;
                case HistoryField when type == WireType.Bytes:
                    fieldId = reader.ReadGuid();
                    break;
                case HistoryValue when type == WireType.Bytes:
                    value = reader.ReadBytes().ToArray();
                    break;
                case HistoryReplaced when type == WireType.Fixed64:
                    replaced = reader.ReadTimestamp();
                    break;
                default:
                    reader.Skip(type);
                    break;
            }
        }

        return value is null ? null : new HistoryItem { Id = id, FieldId = fieldId, Value = new SealedValue(value), ReplacedAt = replaced };
    }

    private static IdentityRecord? ReadIdentity(ReadOnlySpan<byte> data)
    {
        DateTimeOffset created = default;
        string name = string.Empty;
        byte[]? ecdh = null, kem = null, dsa = null, ecdsa = null, bundle = null;
        var reader = new TlvReader(data);
        while (reader.TryReadField(out var field, out var type))
        {
            switch (field)
            {
                case IdentityCreated when type == WireType.Fixed64:
                    created = reader.ReadTimestamp();
                    break;
                case IdentityName when type == WireType.Bytes:
                    name = reader.ReadString();
                    break;
                case IdentityEcdh when type == WireType.Bytes:
                    ecdh = reader.ReadBytes().ToArray();
                    break;
                case IdentityKem when type == WireType.Bytes:
                    kem = reader.ReadBytes().ToArray();
                    break;
                case IdentityDsa when type == WireType.Bytes:
                    dsa = reader.ReadBytes().ToArray();
                    break;
                case IdentityEcdsa when type == WireType.Bytes:
                    ecdsa = reader.ReadBytes().ToArray();
                    break;
                case IdentityBundle when type == WireType.Bytes:
                    bundle = reader.ReadBytes().ToArray();
                    break;
                default:
                    reader.Skip(type);
                    break;
            }
        }

        if (ecdh is null || kem is null || dsa is null || ecdsa is null || bundle is null)
        {
            return null;
        }

        return new IdentityRecord
        {
            Created = created,
            DisplayName = name,
            EcdhPrivate = new SealedValue(ecdh),
            MlKemSeed = new SealedValue(kem),
            MlDsaSeed = new SealedValue(dsa),
            EcdsaPrivate = new SealedValue(ecdsa),
            PublicBundle = bundle,
        };
    }

    private static Contact? ReadContact(ReadOnlySpan<byte> data)
    {
        var id = Guid.NewGuid();
        string name = string.Empty;
        string? note = null;
        byte[]? bundle = null;
        DateTimeOffset added = default;
        var verified = false;
        var reader = new TlvReader(data);
        while (reader.TryReadField(out var field, out var type))
        {
            switch (field)
            {
                case ContactId when type == WireType.Bytes:
                    id = reader.ReadGuid();
                    break;
                case ContactName when type == WireType.Bytes:
                    name = reader.ReadString();
                    break;
                case ContactBundle when type == WireType.Bytes:
                    bundle = reader.ReadBytes().ToArray();
                    break;
                case ContactAdded when type == WireType.Fixed64:
                    added = reader.ReadTimestamp();
                    break;
                case ContactVerified when type == WireType.Varint:
                    verified = reader.ReadBool();
                    break;
                case ContactNote when type == WireType.Bytes:
                    note = reader.ReadString();
                    break;
                default:
                    reader.Skip(type);
                    break;
            }
        }

        return bundle is null ? null : new Contact { Id = id, Name = name, PublicBundle = bundle, Added = added, Verified = verified, Note = note };
    }

    private static AuditEvent ReadAudit(ReadOnlySpan<byte> data)
    {
        DateTimeOffset at = default;
        var kind = AuditKind.Unlocked;
        string? detail = null;
        var reader = new TlvReader(data);
        while (reader.TryReadField(out var field, out var type))
        {
            switch (field)
            {
                case AuditAt when type == WireType.Fixed64:
                    at = reader.ReadTimestamp();
                    break;
                case AuditKindTag when type == WireType.Varint:
                    kind = (AuditKind)reader.ReadInt32();
                    break;
                case AuditDetail when type == WireType.Bytes:
                    detail = reader.ReadString();
                    break;
                default:
                    reader.Skip(type);
                    break;
            }
        }

        return new AuditEvent(at, kind, detail);
    }

    private static TagInfo? ReadTag(ReadOnlySpan<byte> data)
    {
        string? name = null;
        var color = 0;
        var reader = new TlvReader(data);
        while (reader.TryReadField(out var field, out var type))
        {
            switch (field)
            {
                case TagName when type == WireType.Bytes:
                    name = reader.ReadString();
                    break;
                case TagColor when type == WireType.Varint:
                    color = reader.ReadInt32();
                    break;
                default:
                    reader.Skip(type);
                    break;
            }
        }

        return name is null ? null : new TagInfo { Name = name, Color = color };
    }
}
