namespace OnlyYours.Core.Platform;

/// <summary>
/// Where OnlyYours keeps its files. Resolution order:
/// <list type="number">
/// <item><c>ONLYYOURS_HOME</c> environment variable (tests, portable installs);</item>
/// <item>a <c>onlyyours.portable</c> marker next to the executable → <c>.\data</c>;</item>
/// <item><c>%APPDATA%\OnlyYours</c>.</item>
/// </list>
/// </summary>
public sealed class AppPaths
{
    public const string VaultFileName = "vault.onlyyours";
    public const string VaultExtension = ".onlyyours";
    public const string PackageExtension = ".oypkg";
    public const string IdentityExtension = ".oyid";
    public const string PortableMarker = "onlyyours.portable";
    public const string HomeVariable = "ONLYYOURS_HOME";
    public const string VaultVariable = "ONLYYOURS_VAULT";

    public const string LegacyVaultFileName = "credentials.plf";
    public const string LegacyVaultVariable = "BETTERONLYYOURS_VAULT";

    private AppPaths(string dataDirectory, string? vaultOverride, bool portable)
    {
        DataDirectory = dataDirectory;
        VaultOverride = vaultOverride;
        IsPortable = portable;
    }

    public string DataDirectory { get; }

    public string? VaultOverride { get; }

    public bool IsPortable { get; }

    public string DefaultVaultPath => VaultOverride ?? Path.Combine(DataDirectory, VaultFileName);

    public string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public string BackupDirectory => Path.Combine(DataDirectory, "backups");

    public static AppPaths Resolve()
    {
        var vaultOverride = Environment.GetEnvironmentVariable(VaultVariable);
        vaultOverride = string.IsNullOrWhiteSpace(vaultOverride) ? null : Path.GetFullPath(vaultOverride.Trim());

        var home = Environment.GetEnvironmentVariable(HomeVariable);
        if (!string.IsNullOrWhiteSpace(home))
        {
            return new AppPaths(Path.GetFullPath(home.Trim()), vaultOverride, portable: false);
        }

        var exeDirectory = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(exeDirectory, PortableMarker)))
        {
            return new AppPaths(Path.Combine(exeDirectory, "data"), vaultOverride, portable: true);
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
        {
            appData = Environment.CurrentDirectory;
        }

        return new AppPaths(Path.Combine(appData, "OnlyYours"), vaultOverride, portable: false);
    }

    public static AppPaths ForDirectory(string directory) => new(Path.GetFullPath(directory), null, portable: false);

    /// <summary>
    /// Places where BetterOnlyYours (the predecessor) kept <c>credentials.plf</c>, most likely first.
    /// When <c>BETTERONLYYOURS_VAULT</c> is set it is the only candidate (<c>none</c> disables
    /// discovery entirely) so tests and scripted migrations never touch the real profile.
    /// </summary>
    public static IReadOnlyList<string> LegacyVaultCandidates()
    {
        var env = Environment.GetEnvironmentVariable(LegacyVaultVariable);
        if (env is not null)
        {
            env = env.Trim();
            return env.Length == 0 || env.Equals("none", StringComparison.OrdinalIgnoreCase) ? [] : [Path.GetFullPath(env)];
        }

        var candidates = new List<string>();
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
        {
            candidates.Add(Path.Combine(appData, "BetterOnlyYours", "BetterOnlyYours", LegacyVaultFileName));
        }

        candidates.Add(Path.Combine(Environment.CurrentDirectory, LegacyVaultFileName));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, LegacyVaultFileName));
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string? FindLegacyVault()
    {
        foreach (var candidate in LegacyVaultCandidates())
        {
            if (File.Exists(candidate) || File.Exists(candidate + ".bak"))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>BetterOnlyYours kept settings.json next to the vault; the standard profile is the fallback.</summary>
    public static string? LegacySettingsPath(string legacyVaultPath)
    {
        var sibling = Path.Combine(Path.GetDirectoryName(legacyVaultPath) ?? string.Empty, "settings.json");
        if (File.Exists(sibling))
        {
            return sibling;
        }

        if (Environment.GetEnvironmentVariable(LegacyVaultVariable) is not null)
        {
            return null;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var standard = string.IsNullOrEmpty(appData) ? null : Path.Combine(appData, "BetterOnlyYours", "BetterOnlyYours", "settings.json");
        return standard is not null && File.Exists(standard) ? standard : null;
    }
}
