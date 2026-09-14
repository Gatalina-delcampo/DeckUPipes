using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeckUPipes.Core;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public AppSettings Settings { get; private set; } = new();

    public string FilePath { get; }

    public string LegacyFilePath { get; }

    /// <summary>True when no settings file existed at load time (fresh install).</summary>
    public bool IsFirstRun { get; private set; }

    public event EventHandler? SettingsChanged;

    public SettingsStore(string? filePath = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        FilePath = filePath ?? Path.Combine(appData, "DeckUPipes", "settings.json");
        LegacyFilePath = filePath is null
            ? Path.Combine(appData, "EarClarinet", "settings.json")
            : string.Empty;
    }

    public void Load()
    {
        var sourcePath = File.Exists(FilePath)
            ? FilePath
            : !string.IsNullOrEmpty(LegacyFilePath) ? LegacyFilePath : FilePath;
        IsFirstRun = !File.Exists(sourcePath);
        try
        {
            if (File.Exists(sourcePath))
            {
                var json = Migrate(File.ReadAllText(sourcePath));
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    Settings = loaded;
                    if (!string.Equals(sourcePath, FilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        Save();
                    }
                }
            }
        }
        catch (Exception)
        {
            Settings = new AppSettings();
        }

        Settings.Clamp();
    }

    // Retired anchors mapped onto the two that remain: right-hand edges keep the
    // right anchor, everything else lands on the left.
    private static readonly Dictionary<string, string> LegacySides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Right"] = nameof(OverlaySide.TopRight),
        ["BottomRight"] = nameof(OverlaySide.TopRight),
        ["Left"] = nameof(OverlaySide.TopLeft),
        ["TopCenter"] = nameof(OverlaySide.TopLeft),
        ["Bottom"] = nameof(OverlaySide.TopLeft),
        ["BottomLeft"] = nameof(OverlaySide.TopLeft),
        ["BottomCenter"] = nameof(OverlaySide.TopLeft),
    };

    /// <summary>
    /// Rewrites retired Side values before deserialization: an enum value that no
    /// longer exists would otherwise fail the whole settings file. The pattern is
    /// anchored to the Side property so a hotkey containing the same word is safe.
    /// </summary>
    private static string Migrate(string json)
    {
        foreach (var (legacy, replacement) in LegacySides)
        {
            json = System.Text.RegularExpressions.Regex.Replace(
                json,
                $"(\"Side\"\\s*:\\s*\"){legacy}(\")",
                $"$1{replacement}$2",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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

