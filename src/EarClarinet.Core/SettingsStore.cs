using System.Text.Json;
using System.Text.Json.Serialization;

namespace EarClarinet.Core;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public AppSettings Settings { get; private set; } = new();

    public string FilePath { get; }

    public event EventHandler? SettingsChanged;

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EarClarinet",
                "settings.json");
    }

    public void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                json = Migrate(json);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    Settings = loaded;
                }
            }
        }
        catch (Exception)
        {
            Settings = new AppSettings();
        }

        Settings.Clamp();
    }

    /// <summary>
    /// Upgrades legacy setting values so old files keep loading instead of
    /// falling back to defaults (an unknown enum value fails the whole parse).
    /// </summary>
    private static string Migrate(string json)
    {
        if (json.Contains("\"Side\"", StringComparison.OrdinalIgnoreCase) &&
            json.Contains("\"Bottom\"", StringComparison.OrdinalIgnoreCase))
        {
            json = json.Replace("\"Bottom\"", "\"BottomCenter\"", StringComparison.OrdinalIgnoreCase);
        }

        return json;
    }

    public void Save()
    {
        Settings.Clamp();
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(FilePath, JsonSerializer.Serialize(Settings, JsonOptions));
    }

    public void Update(Action<AppSettings> change)
    {
        change(Settings);
        Save();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
