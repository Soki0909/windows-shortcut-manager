using System.ComponentModel;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using ShortcutManager.Windows.Apply;
using ShortcutManager.Windows.ShellLinks;

namespace ShortcutManager.Windows.Inventory;

public sealed record HotkeyRecoveryCopy(string SourcePath, string Hotkey, string BackupPath, string Sha256);

public sealed class ManualHotkeyMigration(string dataDirectory, IReadOnlyList<string> managedDirectories,
    IShellLinkService shellLinks, UrlShortcutReader urls)
{
    private const int MaxSourceBytes = 16 * 1024 * 1024;

    public bool CanGuide(ShortcutInventoryItem item) => ImportKeyChoices.CanImport(item) &&
        item.Hotkey.Length == 10 && item.Hotkey.StartsWith("CTRL+ALT+", StringComparison.OrdinalIgnoreCase) &&
        item.Hotkey[^1] is >= 'A' and <= 'Z' && !IsManaged(item.SourcePath, managedDirectories);

    public HotkeyRecoveryCopy CreateCopy(ShortcutInventoryItem item)
    {
        if (!CanGuide(item)) throw new InvalidOperationException("この移行元のキー解除は案内できません。");
        using var input = OpenOriginal(item.SourcePath);
        var url = item.FileType.Equals(".url", StringComparison.OrdinalIgnoreCase);
        var (key, target, arguments, directory) = ReadLaunchDetails(item.SourcePath, url);
        if (!key.Equals(item.Hotkey, StringComparison.OrdinalIgnoreCase) || target != item.Target ||
            arguments != item.Arguments || directory != item.WorkingDirectory)
            throw new InvalidDataException("スキャン後にキーまたは起動先が変わりました。スキャンし直してください。");
        if (input.Length > MaxSourceBytes) throw new InvalidDataException("移行元が大きすぎるため、復旧用コピーを作成できません。");

        var backupRoot = Path.GetFullPath(Path.Combine(dataDirectory, "migration-source-backups"));
        var drive = new DriveInfo(Path.GetPathRoot(backupRoot)!);
        if (drive.DriveType != DriveType.Fixed || backupRoot.StartsWith(@"\\", StringComparison.Ordinal))
            throw new IOException("復旧用コピーの保存先にはローカルの固定ドライブが必要です。");
        EnsureNoReparsePoints(backupRoot, mayBeMissing: true);
        Directory.CreateDirectory(backupRoot);
        EnsureNoReparsePoints(backupRoot);
        var backupPath = Path.Combine(backupRoot, $"{DateTime.Now:yyyyMMdd-HHmmssfff}_{Guid.NewGuid():N}.zip");
        var sourceHash = Convert.ToHexString(SHA256.HashData(input));
        input.Position = 0;
        var temporary = backupPath + ".partial";
        var created = false;
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                created = true;
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                {
                    using (var copy = archive.CreateEntry("original/" + Path.GetFileName(item.SourcePath)).Open())
                        input.CopyTo(copy);
                    using var manifest = archive.CreateEntry("migration-source.json").Open();
                    JsonSerializer.Serialize(manifest, new { OriginalPath = item.SourcePath, OriginalHotkey = key,
                        Sha256 = sourceHash, CreatedAt = DateTimeOffset.Now });
                }
                output.Flush(flushToDisk: true);
                output.Position = 0;
                using var verified = new ZipArchive(output, ZipArchiveMode.Read, leaveOpen: true);
                using var copiedSource = verified.GetEntry("original/" + Path.GetFileName(item.SourcePath))!.Open();
                if (Convert.ToHexString(SHA256.HashData(copiedSource)) != sourceHash)
                    throw new InvalidDataException("復旧用コピーの内容が原本と一致しません。");
            }
            File.Move(temporary, backupPath);
            created = false;
        }
        finally
        {
            if (created) File.Delete(temporary);
        }
        return new(item.SourcePath, key, backupPath, sourceHash);
    }

    public void CheckBeforeProperties(HotkeyRecoveryCopy copy)
    {
        using var archive = ZipFile.OpenRead(copy.BackupPath);
        var entry = archive.GetEntry("original/" + Path.GetFileName(copy.SourcePath))
            ?? throw new InvalidDataException("復旧用コピーに元ファイルがありません。");
        using var saved = entry.Open();
        if (entry.Length > MaxSourceBytes || Convert.ToHexString(SHA256.HashData(saved)) != copy.Sha256)
            throw new InvalidDataException("保存済みの復旧用コピーが変わりました。案内を開き直してください。");
        using var input = OpenOriginal(copy.SourcePath);
        if (input.Length > MaxSourceBytes || Convert.ToHexString(SHA256.HashData(input)) != copy.Sha256)
            throw new InvalidDataException("コピー作成後に移行元が変わりました。再スキャンして対象を確認してください。");
    }

    public void VerifyRelease(ShortcutInventoryItem original, InventoryScanReport report)
    {
        if (!report.IsComplete)
            throw new InvalidDataException("一部を読み取れなかったため、キー解除と競合解消を確認できません。" + report.Summary);
        var current = report.Items.SingleOrDefault(item => item.SourcePath.Equals(original.SourcePath, StringComparison.OrdinalIgnoreCase));
        if (current is null || !ImportKeyChoices.CanImport(current) || current.Target != original.Target ||
            current.Arguments != original.Arguments || current.WorkingDirectory != original.WorkingDirectory)
            throw new InvalidDataException("元ファイルが見つからないか、起動先や引数が変わりました。復旧用コピーを確認してください。");
        using var input = OpenOriginal(original.SourcePath);
        var (key, target, arguments, directory) = ReadLaunchDetails(original.SourcePath, original.FileType.Equals(".url", StringComparison.OrdinalIgnoreCase));
        if (target != original.Target || arguments != original.Arguments || directory != original.WorkingDirectory)
            throw new InvalidDataException("再スキャン後に起動先や引数が変わりました。");
        if (current.Hotkey.Length != 0 || key.Length != 0)
            throw new InvalidDataException("元ファイルにキーが残っているか値を確認できません。プロパティを確認してください。");
        var conflicts = report.Items.Where(item => !item.IsManaged && item.Hotkey.Equals(original.Hotkey, StringComparison.OrdinalIgnoreCase));
        if (conflicts.Any())
            throw new InvalidDataException("同じキーを使う別のファイルが残っています。\n" + string.Join("\n", conflicts.Select(item => item.SourcePath)));
    }

    private (string Key, string Target, string Arguments, string Directory) ReadLaunchDetails(string path, bool url)
    {
        if (url)
        {
            var info = urls.Read(path);
            return (info.Hotkey.Length == 0 && !info.HasNoHotkey ? "確認できない値" : info.Hotkey,
                info.Url, string.Empty, string.Empty);
        }
        var link = shellLinks.Read(path);
        return (link.Hotkey, link.TargetPath, link.Arguments, link.WorkingDirectory);
    }

    private FileStream OpenOriginal(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new IOException("移行元の絶対パスを確認できません。");
        EnsureNoReparsePoints(path);
        var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var sourcePath = FinalPath(stream.SafeFileHandle);
            if (!GetFileInformationByHandle(stream.SafeFileHandle, out var information)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (information.NumberOfLinks != 1 || IsManaged(sourcePath, managedDirectories.Select(ResolveDirectory)) || HasOwnershipMarker(sourcePath))
                throw new IOException("管理フォルダー内のファイルや別名を持つファイルは案内できません。");
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static bool IsManaged(string path, IEnumerable<string> directories) => directories.Any(directory =>
        Path.GetFullPath(path).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase));

    private static bool HasOwnershipMarker(string path)
    {
        for (var directory = Path.GetDirectoryName(path); directory is not null; directory = Path.GetDirectoryName(directory))
        {
            try
            {
                _ = File.GetAttributes(Path.Combine(directory, ManagedShortcutApplier.OwnershipMarker));
                return true;
            }
            catch (FileNotFoundException) { }
        }
        return false;
    }

    private static void EnsureNoReparsePoints(string path, bool mayBeMissing = false)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if (File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                    throw new IOException("リンクやジャンクション経由のファイルは案内できません。");
            }
            catch (FileNotFoundException) when (mayBeMissing) { }
            catch (DirectoryNotFoundException) when (mayBeMissing) { }
        }
    }

    private static string ResolveDirectory(string path)
    {
        using var handle = CreateFile(path, 0x80, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (!handle.IsInvalid) return FinalPath(handle);
        var error = Marshal.GetLastWin32Error();
        if (error is 2 or 3) return path;
        throw new Win32Exception(error);
    }

    private static string FinalPath(SafeFileHandle handle)
    {
        var path = new StringBuilder(32768);
        var length = GetFinalPathNameByHandle(handle, path, (uint)path.Capacity, 0);
        if (length == 0 || length >= path.Capacity) throw new Win32Exception(Marshal.GetLastWin32Error());
        var result = path.ToString();
        if (result.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + result[8..];
        return result.StartsWith(@"\\?\", StringComparison.Ordinal) ? result[4..] : result;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string filename, uint access, uint share, IntPtr security, uint mode, uint flags, IntPtr template);

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint capacity, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
        public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }
}
