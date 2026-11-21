using System.Text.Json;
using System.Text.Json.Serialization;

namespace OnlyYours.Core.Settings;

public enum GeneratorMode
{
    Characters,
    Passphrase,
    Pin,
}

public sealed class GeneratorSettings
{
    public const int MinLength = 6;
    public const int MaxLength = 128;
    public const int MinWords = 3;
    public const int MaxWords = 12;
    public const int MinPin = 4;
    public const int MaxPin = 16;

    public GeneratorMode Mode { get; set; } = GeneratorMode.Characters;

    public int Length { get; set; } = 24;

    public bool Lowercase { get; set; } = true;

    public bool Uppercase { get; set; } = true;

    public bool Digits { get; set; } = true;

    public bool Symbols { get; set; } = true;

    public bool AvoidAmbiguous { get; set; }

    public string ExcludeCharacters { get; set; } = string.Empty;

    public int Words { get; set; } = 5;

    public string Separator { get; set; } = "-";

    public bool CapitalizeWords { get; set; } = true;

    public bool AppendNumber { get; set; } = true;

    /// <summary>Passphrase word list: "en" (default) or "it".</summary>
    public string WordList { get; set; } = "en";

    public int PinLength { get; set; } = 6;

    public GeneratorSettings Clone() => (GeneratorSettings)MemberwiseClone();

    public void Normalize()
    {
        Length = Math.Clamp(Length, MinLength, MaxLength);
        Words = Math.Clamp(Words, MinWords, MaxWords);
        PinLength = Math.Clamp(PinLength, MinPin, MaxPin);
        Separator ??= "-";
        ExcludeCharacters ??= string.Empty;
        WordList = WordList is "en" or "it" ? WordList : "en";
    }
}

public sealed class WindowPlacement
{
    public double Left { get; set; }

    public double Top { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public bool Maximized { get; set; }
}

/// <summary>
/// Non-sensitive preferences, stored as plain JSON next to the vault. Never contains
/// secrets, entry names or anything derived from the vault content.
/// </summary>
public sealed class AppSettings
{
    public static readonly int[] AutoLockChoices = [0, 1, 2, 5, 10, 15, 30, 60];
    public static readonly int[] ClipboardChoices = [0, 10, 20, 30, 45, 60, 120];

    /// <summary>"dark", "light" or "system".</summary>
    public string Theme { get; set; } = "dark";

    /// <summary>"aurum", "copper", "platinum" or "jade".</summary>
    public string Accent { get; set; } = "aurum";

    /// <summary>"cinematic", "reduced" or "off".</summary>
    public string Motion { get; set; } = "cinematic";

    /// <summary>"cinematic", "short" or "off".</summary>
    public string Intro { get; set; } = "cinematic";

    public int AutoLockMinutes { get; set; } = 10;

    public bool LockOnWindowsLock { get; set; } = true;

    public bool LockOnSleep { get; set; } = true;

    public bool LockOnMinimize { get; set; }

    public int ClipboardClearSeconds { get; set; } = 30;

    public bool ClipboardExcludeHistory { get; set; } = true;

    public bool ScreenCaptureProtection { get; set; } = true;

    public bool ProcessHardening { get; set; } = true;

    public bool ConfirmDelete { get; set; } = true;

    public bool KeepPasswordHistory { get; set; } = true;

    public bool ColorizePasswords { get; set; } = true;

    public bool MinimizeToTray { get; set; }

    public bool CloseToTray { get; set; }

    public bool StartWithWindows { get; set; }

    public bool StartMinimized { get; set; }

    public bool GlobalHotkeys { get; set; } = true;

    public int AutoTypeDelayMs { get; set; } = 20;

    public int TrashRetentionDays { get; set; } = 30;

    public int BackupRetention { get; set; } = 20;

    /// <summary>"standard", "high" or "extreme": the cost of new or re-keyed vaults.</summary>
    public string KdfStrength { get; set; } = "high";

    /// <summary>"title", "recent", "modified", "created" or "strength".</summary>
    public string EntrySort { get; set; } = "title";

    public double ListWidth { get; set; } = 340;

    public bool RailCollapsed { get; set; }

    public GeneratorSettings Generator { get; set; } = new();

    public WindowPlacement? Window { get; set; }

    public string? LastVaultPath { get; set; }

    public List<string> RecentVaults { get; set; } = [];

    /// <summary>Highest write counter seen per vault id — the anti-rollback memory.</summary>
    public Dictionary<string, ulong> HighestWriteCounters { get; set; } = [];

    /// <summary>Display names per vault id, so the lock screen can greet the right vault.</summary>
    public Dictionary<string, string> VaultNames { get; set; } = [];

    public bool LegacyMigrationDismissed { get; set; }

    public DateTimeOffset? LastUnlock { get; set; }

    public int FailedUnlocks { get; set; }

    public bool FirstRunCompleted { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    public void Normalize()
    {
        Theme = Theme is "light" or "system" ? Theme : "dark";
        Accent = Accent is "copper" or "platinum" or "jade" ? Accent : "aurum";
        Motion = Motion is "reduced" or "off" ? Motion : "cinematic";
        Intro = Intro is "short" or "off" ? Intro : "cinematic";
        AutoLockMinutes = AutoLockChoices.Contains(AutoLockMinutes) ? AutoLockMinutes : 10;
        ClipboardClearSeconds = ClipboardChoices.Contains(ClipboardClearSeconds) ? ClipboardClearSeconds : 30;
        AutoTypeDelayMs = Math.Clamp(AutoTypeDelayMs, 0, 250);
        TrashRetentionDays = Math.Clamp(TrashRetentionDays, 1, 365);
        BackupRetention = Math.Clamp(BackupRetention, 1, 200);
        KdfStrength = KdfStrength is "standard" or "extreme" ? KdfStrength : "high";
        EntrySort = EntrySort is "recent" or "modified" or "created" or "strength" ? EntrySort : "title";
        ListWidth = Math.Clamp(ListWidth, 260, 560);
        Generator ??= new GeneratorSettings();
        Generator.Normalize();
        RecentVaults ??= [];
        HighestWriteCounters ??= [];
        VaultNames ??= [];
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
