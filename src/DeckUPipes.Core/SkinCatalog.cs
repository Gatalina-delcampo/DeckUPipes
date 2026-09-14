using System.Buffers.Binary;
using System.Text.Json;

namespace DeckUPipes.Core;

public sealed class SkinCatalog
{
    private static readonly IReadOnlyDictionary<string, (int Width, int Height, string? Repeat, int? RepeatCount, bool Once)> RequiredSlots =
        new Dictionary<string, (int, int, string?, int?, bool)>(StringComparer.OrdinalIgnoreCase)
        {
            ["globalBackground"] = (128, 140, "tile", null, false),
            ["avatar"] = (124, 124, null, null, false),
            ["globalVolume"] = (48, 48, null, 10, false),
            ["volumeBackground"] = (520, 60, null, null, false),
            ["globalMute"] = (108, 108, null, null, false),
            ["programBackground"] = (128, 90, "tile", null, false),
            ["programIcon"] = (84, 84, null, null, false),
            ["programVolume"] = (48, 48, null, 10, false),
            ["programMute"] = (68, 68, null, null, false),
            ["connector"] = (128, 90, "tile", null, false),
            ["termination"] = (756, 42, null, null, true),
        };

    public SkinCatalog(string? rootDirectory = null)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        RootDirectory = rootDirectory ?? Path.Combine(appData, "DeckUPipes", "skins");
        LegacyRootDirectory = Path.Combine(appData, "EarClarinet", "skins");
    }

    public string RootDirectory { get; }
    public string LegacyRootDirectory { get; }

    public IReadOnlyList<SkinPackage> Discover()
    {
        var packages = new Dictionary<string, SkinPackage>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[] { RootDirectory, LegacyRootDirectory })
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(root))
            {
                var package = TryLoad(directory);
                if (package is not null && !packages.ContainsKey(package.Manifest.Id))
                {
                    packages[package.Manifest.Id] = package;
                }
            }
        }

        return packages.Values
            .OrderBy(package => package.Manifest.NameOrId(), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public SkinPackage? TryLoad(string directoryPath)
    {
        var manifestPath = Path.Combine(directoryPath, "skin.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<SkinManifest>(File.ReadAllText(manifestPath), SkinManifestJson.Options);
            if (manifest is null)
            {
                return null;
            }

            var validation = Validate(directoryPath, manifest);
            return new SkinPackage(directoryPath, manifest, validation);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static SkinValidationResult Validate(string directoryPath, SkinManifest manifest)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (!IsSupportedFormat(manifest.Format))
        {
            errors.Add("Unsupported skin format.");
        }

        if (manifest.Version != 1)
        {
            errors.Add("Unsupported skin version.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            errors.Add("Skin id is required.");
        }
        else if (!IsSafeFolderName(manifest.Id))
        {
            errors.Add("Skin id must be a valid folder name.");
        }

        foreach (var required in RequiredSlots)
        {
            if (!manifest.Slots.TryGetValue(required.Key, out var slot))
            {
                errors.Add($"Missing required slot '{required.Key}'.");
                continue;
            }

            if (slot.Width != required.Value.Width || slot.Height != required.Value.Height)
            {
                errors.Add($"Slot '{required.Key}' must be {required.Value.Width}x{required.Value.Height} pixels.");
            }

            if (!string.Equals(slot.Repeat, required.Value.Repeat, StringComparison.OrdinalIgnoreCase) || slot.RepeatCount != required.Value.RepeatCount || slot.Once != required.Value.Once)
            {
                errors.Add($"Slot '{required.Key}' has invalid repeat settings.");
            }

            if (!TryResolveAsset(directoryPath, slot.Asset, out var assetPath))
            {
                errors.Add($"Slot '{required.Key}' has an unsafe asset path.");
            }
            else if (!File.Exists(assetPath))
            {
                errors.Add($"Asset for slot '{required.Key}' was not found.");
            }
            else if (!TryReadPngDimensions(assetPath, out var actualWidth, out var actualHeight))
            {
                errors.Add($"Asset for slot '{required.Key}' is not a readable PNG.");
            }
            else if (actualWidth != required.Value.Width || actualHeight != required.Value.Height)
            {
                errors.Add($"Asset for slot '{required.Key}' must be {required.Value.Width}x{required.Value.Height} pixels, but is {actualWidth}x{actualHeight}.");
            }
        }

        var programIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var program in manifest.Programs)
        {
            if (string.IsNullOrWhiteSpace(program.Id) || !programIds.Add(program.Id))
            {
                errors.Add("Program ids must be unique and non-empty.");
            }
        }

        foreach (var slot in manifest.Slots.Keys.Except(RequiredSlots.Keys, StringComparer.OrdinalIgnoreCase))
        {
            warnings.Add($"Unknown slot '{slot}' will be ignored.");
        }

        return new SkinValidationResult(errors.Count == 0, errors, warnings);
    }

    // "deckupipes-skin" is the current name. Every skin written before the rename
    // declares "earclarinet-skin", so both stay readable and the editor writes the
    // new one.
    private static readonly string[] SupportedFormats = { "deckupipes-skin", "earclarinet-skin" };

    private static bool IsSupportedFormat(string? format) =>
        format is not null && SupportedFormats.Contains(format, StringComparer.OrdinalIgnoreCase);

    // The id doubles as the installed folder name, so it must never be able to
    // point outside the skins root or collide with a device name.
    private static readonly string[] ReservedNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static bool IsSafeFolderName(string id)
    {
        if (id.Length == 0 || !string.Equals(id, id.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        if (id is "." or "..")
        {
            return false;
        }

        if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        if (id.EndsWith('.') || id.EndsWith(' '))
        {
            return false;
        }

        return !ReservedNames.Contains(id, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryResolveAsset(string directoryPath, string? asset, out string resolved)
    {
        resolved = string.Empty;
        if (string.IsNullOrWhiteSpace(asset) || Path.IsPathRooted(asset))
        {
            return false;
        }

        var root = Path.GetFullPath(directoryPath);
        resolved = Path.GetFullPath(Path.Combine(root, asset.Replace('/', Path.DirectorySeparatorChar)));
        return resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadPngDimensions(string path, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            Span<byte> header = stackalloc byte[24];
            using var stream = File.OpenRead(path);
            if (stream.Read(header) != header.Length ||
                !header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
                !header.Slice(12, 4).SequenceEqual("IHDR"u8))
            {
                return false;
            }

            width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(header.Slice(16, 4)));
            height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(header.Slice(20, 4)));
            return width > 0 && height > 0;
        }
        catch (System.Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or OverflowException)
        {
            return false;
        }
    }
}

file static class SkinManifestExtensions
{
    public static string NameOrId(this SkinManifest manifest) =>
        string.IsNullOrWhiteSpace(manifest.Id) ? manifest.Format : manifest.Id;
}

