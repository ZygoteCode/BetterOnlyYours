using System.Globalization;
using OnlyYours.Core.Otp;
using OnlyYours.Core.Vault;
using OnlyYours.Core.Vault.Model;

namespace OnlyYours.Core.ImportExport;

public enum CsvFormat
{
    Generic,
    Chrome,
    Firefox,
    Bitwarden,
    OnePassword,
    KeePass,
    LastPass,
    ProtonPass,
    Dashlane,
    OnlyYours,
}

/// <summary>Column mapping deduced from the header row (−1 = absent).</summary>
public sealed class CsvImportPlan
{
    public CsvFormat Format { get; set; } = CsvFormat.Generic;

    public int Title { get; set; } = -1;

    public int Url { get; set; } = -1;

    public int Username { get; set; } = -1;

    public int Email { get; set; } = -1;

    public int Password { get; set; } = -1;

    public int Notes { get; set; } = -1;

    public int Totp { get; set; } = -1;

    public int Tags { get; set; } = -1;

    public int Favorite { get; set; } = -1;

    public int Type { get; set; } = -1;

    public int Kind { get; set; } = -1;

    public int DataRows { get; set; }

    public bool HasHeader { get; set; } = true;

    public bool IsUsable => Title >= 0 || Url >= 0 || Username >= 0 || Password >= 0;
}

public sealed class ImportReport
{
    public int Imported { get; set; }

    public int Duplicates { get; set; }

    public int Skipped { get; set; }

    public int WithTotp { get; set; }

    public int TotpFailed { get; set; }

    public CsvFormat Format { get; set; }
}

/// <summary>Imports CSV exports of the common password managers and browsers.</summary>
public static class CsvImporter
{
    public static CsvImportPlan Detect(CsvDocument document)
    {
        var plan = new CsvImportPlan();
        if (document.Rows.Count == 0)
        {
            return plan;
        }

        var header = document.Rows[0].Select(c => Normalize(document.Text(c))).ToArray();
        int Find(params string[] names)
        {
            foreach (var name in names)
            {
                var index = Array.IndexOf(header, name);
                if (index >= 0)
                {
                    return index;
                }
            }

            return -1;
        }

        bool Has(params string[] names) => names.All(n => header.Contains(n));

        plan.Format = Has("login_uri", "login_username", "login_password") ? CsvFormat.Bitwarden
            : Has("httprealm", "formactionorigin") ? CsvFormat.Firefox
            : Has("otpauth", "archived") ? CsvFormat.OnePassword
            : Has("group", "title", "username", "password") ? CsvFormat.KeePass
            : Has("grouping", "fav", "extra") ? CsvFormat.LastPass
            : Has("vault", "createtime", "modifytime") ? CsvFormat.ProtonPass
            : Has("username2", "otpsecret") || Has("otpsecret", "category") ? CsvFormat.Dashlane
            : Has("kind", "title", "username", "password", "totp") ? CsvFormat.OnlyYours
            : Has("name", "url", "username", "password") ? CsvFormat.Chrome
            : CsvFormat.Generic;

        plan.Title = Find("title", "name", "titolo", "nome", "account", "site", "sito");
        plan.Url = Find("login_uri", "url", "uri", "website", "web site", "sito web", "indirizzo", "address", "origin");
        plan.Username = Find("login_username", "username", "user name", "user", "login", "utente", "nome utente");
        plan.Email = Find("email", "e-mail", "mail");
        plan.Password = Find("login_password", "password", "pass", "pwd", "parola chiave");
        plan.Notes = Find("notes", "note", "extra", "comments", "commento", "description", "descrizione");
        plan.Totp = Find("login_totp", "otpauth", "totp", "otp", "otpsecret", "2fa", "one-time password");
        plan.Tags = Find("tags", "tag", "folder", "group", "grouping", "category", "vault", "cartella");
        plan.Favorite = Find("favorite", "favourite", "fav", "preferito");
        plan.Type = Find("type");
        plan.Kind = Find("kind");
        if (plan.Username < 0)
        {
            plan.Username = plan.Email;
        }

        plan.HasHeader = plan.IsUsable;
        if (!plan.HasHeader && document.Rows[0].Length >= 3)
        {
            // Headerless file: assume title, url, username, password, notes (Chrome order).
            plan.Title = 0;
            plan.Url = 1;
            plan.Username = 2;
            plan.Password = 3;
            plan.Notes = document.Rows[0].Length > 4 ? 4 : -1;
        }

        plan.DataRows = document.Rows.Count - (plan.HasHeader ? 1 : 0);
        return plan;
    }

    public static List<Entry> Build(CsvDocument document, CsvImportPlan plan, VaultSession session, bool skipDuplicates, out ImportReport report)
    {
        report = new ImportReport { Format = plan.Format };
        var result = new List<Entry>();
        var existing = new HashSet<string>(session.Document.Entries.Select(Key), StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.Now;
        var number = 0;

        foreach (var row in document.Rows.Skip(plan.HasHeader ? 1 : 0))
        {
            number++;
            string Cell(int index) => index >= 0 && index < row.Length ? document.Text(row[index]).Trim() : string.Empty;
            bool Present(int index) => index >= 0 && index < row.Length && !document.IsEmpty(row[index]);

            var url = Cell(plan.Url);
            var username = Cell(plan.Username);
            if (username.Length == 0 && plan.Email >= 0)
            {
                username = Cell(plan.Email);
            }

            var title = Cell(plan.Title);
            if (title.Length == 0)
            {
                title = Entry.UrlHost(url) ?? (username.Length > 0 ? username : $"Import {number}");
            }

            var type = Cell(plan.Type).ToLowerInvariant();
            var kind = type switch
            {
                "note" or "securenote" or "secure note" => EntryKind.SecureNote,
                "card" => EntryKind.PaymentCard,
                "identity" => EntryKind.Identity,
                _ => EntryKind.Login,
            };
            if (plan.Kind >= 0 && Enum.TryParse<EntryKind>(Cell(plan.Kind), ignoreCase: true, out var parsedKind))
            {
                kind = parsedKind;
            }

            if (!Present(plan.Password) && !Present(plan.Username) && !Present(plan.Url) && !Present(plan.Notes) && Cell(plan.Title).Length == 0)
            {
                report.Skipped++;
                continue;
            }

            var entry = new Entry { Kind = kind, Title = title, Created = now, Modified = now };
            if (kind is EntryKind.Login)
            {
                entry.Fields.Add(new Field { Kind = FieldKind.Username, Text = NullIfEmpty(username), Modified = now });
                var password = new Field { Kind = FieldKind.Password, Flags = FieldFlags.Sensitive, Modified = now };
                if (Present(plan.Password))
                {
                    using var secret = document.Value(row[plan.Password]);
                    password.Sealed = session.Fields.SealChars(secret.Span, entry.Id, password.Id);
                }

                entry.Fields.Add(password);
                entry.Fields.Add(new Field { Kind = FieldKind.Url, Text = NullIfEmpty(url), Modified = now });

                if (Present(plan.Totp))
                {
                    using var raw = document.Value(row[plan.Totp]);
                    var defaults = new TotpConfig { Issuer = title, Account = username };
                    if (OtpAuthUri.TryParse(raw.Span, defaults, out var setup, out _))
                    {
                        using (setup)
                        {
                            var totp = new Field { Kind = FieldKind.Totp, Flags = FieldFlags.Sensitive, Modified = now };
                            totp.Sealed = session.SealTotp(entry.Id, totp.Id, setup!);
                            entry.Fields.Add(totp);
                            report.WithTotp++;
                        }
                    }
                    else
                    {
                        report.TotpFailed++;
                    }
                }
            }

            var notes = new Field { Kind = FieldKind.Notes, Flags = FieldFlags.Sensitive | FieldFlags.Multiline, Modified = now };
            if (Present(plan.Notes))
            {
                using var secret = document.Value(row[plan.Notes]);
                notes.Sealed = session.Fields.SealChars(secret.Span, entry.Id, notes.Id);
            }

            entry.Fields.Add(notes);

            var tags = Cell(plan.Tags);
            if (tags.Length > 0)
            {
                entry.Tags = VaultSession.NormalizeTags(tags.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries));
            }

            var favorite = Cell(plan.Favorite).ToLowerInvariant();
            entry.Favorite = favorite is "1" or "true" or "yes" or "si" or "sì" or "x";

            if (skipDuplicates && !existing.Add(Key(entry)))
            {
                report.Duplicates++;
                continue;
            }

            result.Add(entry);
            report.Imported++;
        }

        return result;
    }

    private static string Key(Entry entry) =>
        string.Join('\u001f', entry.Title.Trim(), entry.Username ?? string.Empty, entry.Host ?? string.Empty);

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static string Normalize(string header) => header.Trim().Trim('"').ToLower(CultureInfo.InvariantCulture);
}

/// <summary>Plain-text CSV export (only after explicit confirmation and re-authentication).</summary>
public static class CsvExporter
{
    public static void Export(VaultSession session, IEnumerable<Entry> entries, string path)
    {
        using var writer = new CsvWriter();
        foreach (var header in new[] { "kind", "title", "username", "password", "url", "totp", "notes", "tags", "favorite", "created", "modified" })
        {
            writer.Cell(header);
        }

        writer.EndRow();
        foreach (var entry in entries)
        {
            writer.Cell(entry.Kind.ToString());
            writer.Cell(entry.Title);
            writer.Cell(entry.Username ?? string.Empty);
            WriteSecret(session, entry, entry.PrimaryPassword, writer);
            writer.Cell(entry.Url ?? string.Empty);
            if (entry.Totp is { } totp && session.ExportTotpUri(entry, totp) is { } uri)
            {
                using (uri)
                {
                    writer.Cell(uri.Span);
                }
            }
            else
            {
                writer.Cell(string.Empty);
            }

            WriteSecret(session, entry, entry.FirstField(FieldKind.Notes), writer);
            writer.Cell(string.Join(';', entry.Tags));
            writer.Cell(entry.Favorite ? "1" : "0");
            writer.Cell(entry.Created.ToString("O", CultureInfo.InvariantCulture));
            writer.Cell(entry.Modified.ToString("O", CultureInfo.InvariantCulture));
            writer.EndRow();
        }

        writer.WriteTo(path);
    }

    private static void WriteSecret(VaultSession session, Entry entry, Field? field, CsvWriter writer)
    {
        if (field is null || !field.HasValue)
        {
            writer.Cell(string.Empty);
            return;
        }

        using var value = session.Reveal(entry, field);
        writer.Cell(value.Span);
    }
}
