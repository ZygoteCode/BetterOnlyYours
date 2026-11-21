using System.Globalization;
using System.Security.Cryptography;
using OnlyYours.Core.Vault.Format;

namespace OnlyYours.Core.Vault;

/// <summary>What can be learned about a vault file without any key.</summary>
public sealed record VaultProbe(
    string Path,
    bool Exists,
    long Size,
    DateTimeOffset? Modified,
    VaultHeader? Header,
    bool IsLegacyFormat,
    VaultError? Error,
    int BackupCount);

public sealed record BackupInfo(string Path, DateTimeOffset Created, ulong WriteCounter, long Size);

/// <summary>
/// File I/O for a vault: crash-safe writes and rotating encrypted backups.
/// <para>
/// A save writes a temporary file with write-through, flushes it to the device,
/// then swaps it in with <c>ReplaceFile</c> — at no point can a crash leave a
/// half-written vault. The previous version is moved into <c>backups\</c> at most
/// once per <see cref="BackupInterval"/> (and on the first save of a session), and
/// only the newest <see cref="BackupRetention"/> copies are kept. Backups are full
/// vault files: just as encrypted as the vault itself.
/// </para>
/// </summary>
public sealed class VaultStorage
{
    private DateTimeOffset _lastBackup = DateTimeOffset.MinValue;

    public VaultStorage(string vaultPath, string? backupDirectory = null)
    {
        VaultPath = System.IO.Path.GetFullPath(vaultPath);
        BackupDirectory = backupDirectory ?? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(VaultPath) ?? ".", "backups");
    }

    public string VaultPath { get; }

    public string BackupDirectory { get; }

    public int BackupRetention { get; set; } = 20;

    public TimeSpan BackupInterval { get; set; } = TimeSpan.FromMinutes(15);

    public bool Exists => File.Exists(VaultPath);

    public VaultProbe Probe()
    {
        var backups = SafeBackupCount();
        if (!File.Exists(VaultPath))
        {
            return new VaultProbe(VaultPath, false, 0, null, null, false, VaultError.NotFound, backups);
        }

        try
        {
            var info = new FileInfo(VaultPath);
            using var stream = new FileStream(VaultPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096);
            Span<byte> prefix = stackalloc byte[16];
            var read = stream.ReadAtLeast(prefix, 16, throwOnEndOfStream: false);
            if (read >= 5 && prefix[0] == 0xFF && prefix[1] == 0xFE && prefix[2] == 0x0D && prefix[3] == 0x0A)
            {
                return new VaultProbe(VaultPath, true, info.Length, info.LastWriteTime, null, true, null, backups);
            }

            if (read < 16 || !VaultHeader.HasMagic(prefix))
            {
                return new VaultProbe(VaultPath, true, info.Length, info.LastWriteTime, null, false, VaultError.NotAVault, backups);
            }

            var headerLength = BitConverter.ToUInt32(prefix[12..16]);
            if (headerLength < VaultHeader.FixedPrefixLength || headerLength > VaultHeader.MaxHeaderLength || headerLength > info.Length)
            {
                return new VaultProbe(VaultPath, true, info.Length, info.LastWriteTime, null, false, VaultError.Truncated, backups);
            }

            var buffer = new byte[headerLength];
            prefix[..read].CopyTo(buffer);
            stream.ReadExactly(buffer, read, (int)headerLength - read);
            var header = VaultHeader.Parse(buffer);
            return new VaultProbe(VaultPath, true, info.Length, info.LastWriteTime, header, false, null, backups);
        }
        catch (VaultException error)
        {
            return new VaultProbe(VaultPath, true, 0, null, null, false, error.Error, backups);
        }
        catch (UnauthorizedAccessException)
        {
            return new VaultProbe(VaultPath, true, 0, null, null, false, VaultError.AccessDenied, backups);
        }
        catch (IOException)
        {
            return new VaultProbe(VaultPath, true, 0, null, null, false, VaultError.IoFailure, backups);
        }
    }

    public byte[] ReadAll() => ReadFile(VaultPath);

    public static byte[] ReadFile(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (FileNotFoundException error)
        {
            throw new VaultException(VaultError.NotFound, "The vault file does not exist.", error);
        }
        catch (DirectoryNotFoundException error)
        {
            throw new VaultException(VaultError.NotFound, "The vault folder does not exist.", error);
        }
        catch (UnauthorizedAccessException error)
        {
            throw new VaultException(VaultError.AccessDenied, "Access to the vault file was denied.", error);
        }
        catch (IOException error)
        {
            throw new VaultException(VaultError.IoFailure, "The vault file could not be read.", error);
        }
    }

    /// <summary>Crash-safe replace of the vault file.</summary>
    public void WriteAtomic(ReadOnlySpan<byte> data, Guid vaultId, ulong writeCounter, bool forceBackup = false)
    {
        var directory = System.IO.Path.GetDirectoryName(VaultPath) ?? ".";
        var temp = System.IO.Path.Combine(directory, $".{System.IO.Path.GetFileName(VaultPath)}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(4))}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(VaultPath))
            {
                string? backup = null;
                var now = DateTimeOffset.Now;
                if (forceBackup || now - _lastBackup >= BackupInterval)
                {
                    Directory.CreateDirectory(BackupDirectory);
                    backup = System.IO.Path.Combine(BackupDirectory, BackupName(vaultId, writeCounter - 1, now));
                }

                Retry(() => File.Replace(temp, VaultPath, backup, ignoreMetadataErrors: true));
                if (backup is not null)
                {
                    _lastBackup = now;
                    PruneBackups(vaultId);
                }
            }
            else
            {
                Retry(() => File.Move(temp, VaultPath));
            }
        }
        catch (UnauthorizedAccessException error)
        {
            TryDelete(temp);
            throw new VaultException(VaultError.AccessDenied, "Permission denied while writing the vault.", error);
        }
        catch (IOException error)
        {
            TryDelete(temp);
            throw new VaultException(VaultError.IoFailure, $"The vault could not be written: {error.Message}", error);
        }
    }

    public static string BackupName(Guid vaultId, ulong counter, DateTimeOffset at) =>
        $"{vaultId:N}-{counter:D10}-{at.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}{Platform.AppPaths.VaultExtension}";

    public IReadOnlyList<BackupInfo> ListBackups(Guid vaultId)
    {
        if (!Directory.Exists(BackupDirectory))
        {
            return [];
        }

        var result = new List<BackupInfo>();
        foreach (var file in Directory.EnumerateFiles(BackupDirectory, $"{vaultId:N}-*{Platform.AppPaths.VaultExtension}"))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(file);
            var parts = name.Split('-');
            if (parts.Length < 4 || !ulong.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var counter))
            {
                continue;
            }

            if (!DateTime.TryParseExact($"{parts[2]}-{parts[3]}", "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var stamp))
            {
                stamp = File.GetLastWriteTime(file);
            }

            result.Add(new BackupInfo(file, new DateTimeOffset(stamp), counter, new FileInfo(file).Length));
        }

        return result.OrderByDescending(b => b.WriteCounter).ThenByDescending(b => b.Created).ToList();
    }

    public void PruneBackups(Guid vaultId)
    {
        foreach (var stale in ListBackups(vaultId).Skip(Math.Max(1, BackupRetention)))
        {
            TryDelete(stale.Path);
        }
    }

    public int DeleteAllBackups(Guid vaultId)
    {
        var count = 0;
        foreach (var backup in ListBackups(vaultId))
        {
            if (TryDelete(backup.Path))
            {
                count++;
            }
        }

        return count;
    }

    private int SafeBackupCount()
    {
        try
        {
            return Directory.Exists(BackupDirectory) ? Directory.EnumerateFiles(BackupDirectory, "*" + Platform.AppPaths.VaultExtension).Count() : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static void Retry(Action action)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (IOException) when (attempt < 4)
            {
                // Antivirus scanners and indexers briefly hold freshly written files.
                Thread.Sleep(15 * (attempt + 1) * (attempt + 1));
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(15 * (attempt + 1) * (attempt + 1));
            }
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch
        {
            // Nothing actionable.
        }

        return false;
    }
}
