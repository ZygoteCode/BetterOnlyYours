using System.Text.Json;

namespace OnlyYours.Core.Settings;

/// <summary>Reads and writes <see cref="AppSettings"/>. Never throws on load: damaged preferences must not block the vault.</summary>
public sealed class SettingsStore
{
    private readonly Lock _gate = new();

    public SettingsStore(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                var fresh = new AppSettings();
                fresh.Normalize();
                return fresh;
            }

            using var stream = File.OpenRead(Path);
            var settings = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            var fallback = new AppSettings();
            fallback.Normalize();
            return fallback;
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            var directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = Path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.AppSettings);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, Path, overwrite: true);
        }
    }

    public bool TrySave(AppSettings settings)
    {
        try
        {
            Save(settings);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
