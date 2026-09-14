using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeckUPipes.Core;

public sealed class SkinManifest
{
    public string Format { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Id { get; set; } = string.Empty;
    public Dictionary<string, SkinSlotDefinition> Slots { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<SkinProgramDefinition> Programs { get; set; } = new();
}

public sealed class SkinSlotDefinition
{
    public string Asset { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Repeat { get; set; }
    public int? RepeatCount { get; set; }
    public bool Once { get; set; }
}

public sealed class SkinProgramDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = "Program";
}

public sealed record SkinValidationResult(bool IsValid, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public static SkinValidationResult Valid(IReadOnlyList<string>? warnings = null) =>
        new(true, Array.Empty<string>(), warnings ?? Array.Empty<string>());
}

public sealed class SkinPackage
{
    public SkinPackage(string directoryPath, SkinManifest manifest, SkinValidationResult validation)
    {
        DirectoryPath = directoryPath;
        Manifest = manifest;
        Validation = validation;
    }

    public string DirectoryPath { get; }
    public SkinManifest Manifest { get; }
    public SkinValidationResult Validation { get; }
    public string ManifestPath => Path.Combine(DirectoryPath, "skin.json");
    public string GetAssetPath(string slotId) => Path.Combine(DirectoryPath, Manifest.Slots[slotId].Asset.Replace('/', Path.DirectorySeparatorChar));
}

public static class SkinManifestJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };
}

