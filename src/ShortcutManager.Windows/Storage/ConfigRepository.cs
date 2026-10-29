using ShortcutManager.Core.Models;
using ShortcutManager.Core.Serialization;

namespace ShortcutManager.Windows.Storage;

public sealed class ConfigRepository(string path)
{
    public string Path { get; } = path;

    public ShortcutConfig Load()
    {
        if (!File.Exists(Path)) return new ShortcutConfig();
        if (new FileInfo(Path).Length > Core.Validation.ConfigStructure.MaxJsonCharacters) throw new InvalidDataException("$: config.json は1 MiB以下である必要があります。");
        return ConfigJson.Deserialize(File.ReadAllText(Path));
    }

    public void SaveAtomic(ShortcutConfig config)
    {
        var directory = System.IO.Path.GetDirectoryName(Path) ?? throw new InvalidOperationException("Config path has no directory.");
        Directory.CreateDirectory(directory);
        var temporary = System.IO.Path.Combine(directory, $"config.json.tmp-{Guid.NewGuid():N}");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(ConfigJson.Serialize(config)); writer.Flush(); stream.Flush(true);
            }
            if (File.Exists(Path)) File.Replace(temporary, Path, null);
            else File.Move(temporary, Path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

