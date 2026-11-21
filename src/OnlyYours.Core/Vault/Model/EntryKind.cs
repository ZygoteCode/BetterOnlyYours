namespace OnlyYours.Core.Vault.Model;

public enum EntryKind
{
    Login = 0,
    SecureNote = 1,
    PaymentCard = 2,
    Identity = 3,
    WiFi = 4,
    SshKey = 5,
    ApiKey = 6,
    BankAccount = 7,
}

[Flags]
public enum FieldFlags
{
    None = 0,

    /// <summary>The value is sealed and hidden by default.</summary>
    Sensitive = 1,

    /// <summary>Multi-line text.</summary>
    Multiline = 2,
}

/// <summary>Semantic type of a field; drives icon, input hints, validation and default sensitivity.</summary>
public enum FieldKind
{
    Username = 0,
    Email = 1,
    Password = 2,
    Url = 3,
    Totp = 4,
    Notes = 5,

    CardHolder = 10,
    CardNumber = 11,
    CardExpiry = 12,
    CardCvv = 13,
    CardPin = 14,

    FullName = 20,
    Phone = 21,
    Address = 22,
    BirthDate = 23,
    Company = 24,
    DocumentNumber = 25,

    Ssid = 30,
    WifiSecurity = 31,

    PrivateKey = 40,
    PublicKey = 41,
    Passphrase = 42,
    Host = 43,

    ApiKeyId = 50,
    ApiSecret = 51,

    BankName = 60,
    AccountHolder = 61,
    Iban = 62,
    Swift = 63,
    AccountNumber = 64,

    CustomText = 100,
    CustomSecret = 101,
    CustomUrl = 102,
    CustomNumber = 103,
    CustomDate = 104,
}

public static class FieldKindInfo
{
    public static bool IsSensitive(this FieldKind kind) => kind switch
    {
        FieldKind.Password or FieldKind.Totp or FieldKind.Notes or FieldKind.CardNumber or FieldKind.CardCvv
            or FieldKind.CardPin or FieldKind.PrivateKey or FieldKind.Passphrase or FieldKind.ApiSecret
            or FieldKind.Iban or FieldKind.AccountNumber or FieldKind.DocumentNumber or FieldKind.CustomSecret => true,
        _ => false,
    };

    public static bool IsMultiline(this FieldKind kind) => kind switch
    {
        FieldKind.Notes or FieldKind.Address or FieldKind.PrivateKey or FieldKind.PublicKey => true,
        _ => false,
    };

    /// <summary>Secrets that are copied and typed rather than read (rendered in a monospace face).</summary>
    public static bool IsMonospace(this FieldKind kind) => kind switch
    {
        FieldKind.Password or FieldKind.CardNumber or FieldKind.CardCvv or FieldKind.CardPin or FieldKind.PrivateKey
            or FieldKind.PublicKey or FieldKind.Passphrase or FieldKind.ApiKeyId or FieldKind.ApiSecret or FieldKind.Iban
            or FieldKind.AccountNumber or FieldKind.CustomSecret or FieldKind.Swift => true,
        _ => false,
    };

    public static bool IsCustom(this FieldKind kind) => kind >= FieldKind.CustomText;

    public static bool IsUrl(this FieldKind kind) => kind is FieldKind.Url or FieldKind.CustomUrl;

    /// <summary>Fields whose value changes are recorded in the password history.</summary>
    public static bool KeepsHistory(this FieldKind kind) => kind is FieldKind.Password or FieldKind.Passphrase or FieldKind.ApiSecret or FieldKind.CardPin;

    /// <summary>Template fields created for a new entry of each kind.</summary>
    public static IReadOnlyList<FieldKind> Template(EntryKind kind) => kind switch
    {
        EntryKind.Login => [FieldKind.Username, FieldKind.Password, FieldKind.Url, FieldKind.Notes],
        EntryKind.SecureNote => [FieldKind.Notes],
        EntryKind.PaymentCard => [FieldKind.CardHolder, FieldKind.CardNumber, FieldKind.CardExpiry, FieldKind.CardCvv, FieldKind.CardPin, FieldKind.Notes],
        EntryKind.Identity => [FieldKind.FullName, FieldKind.Email, FieldKind.Phone, FieldKind.Address, FieldKind.BirthDate, FieldKind.DocumentNumber, FieldKind.Notes],
        EntryKind.WiFi => [FieldKind.Ssid, FieldKind.Password, FieldKind.WifiSecurity, FieldKind.Notes],
        EntryKind.SshKey => [FieldKind.Host, FieldKind.Username, FieldKind.PrivateKey, FieldKind.PublicKey, FieldKind.Passphrase, FieldKind.Notes],
        EntryKind.ApiKey => [FieldKind.ApiKeyId, FieldKind.ApiSecret, FieldKind.Url, FieldKind.Notes],
        EntryKind.BankAccount => [FieldKind.BankName, FieldKind.AccountHolder, FieldKind.Iban, FieldKind.Swift, FieldKind.AccountNumber, FieldKind.Notes],
        _ => [FieldKind.Notes],
    };

    /// <summary>The field shown as subtitle in lists, per kind.</summary>
    public static FieldKind? SubtitleField(EntryKind kind) => kind switch
    {
        EntryKind.Login => FieldKind.Username,
        EntryKind.PaymentCard => FieldKind.CardHolder,
        EntryKind.Identity => FieldKind.FullName,
        EntryKind.WiFi => FieldKind.Ssid,
        EntryKind.SshKey => FieldKind.Host,
        EntryKind.ApiKey => FieldKind.ApiKeyId,
        EntryKind.BankAccount => FieldKind.BankName,
        _ => null,
    };
}
