using System.Text.Json;
using DeckUPipes.Core;

namespace DeckUPipes.Tests;

public class SkinCatalogTests
{
    [Fact]
    public void Validate_Accepts_Complete_Skin_Package()
    {
        using var package = TestPackage.Create();
        var catalog = new SkinCatalog(package.Root);
        var result = SkinCatalog.Validate(package.SkinDirectory, package.Manifest);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(11, package.Manifest.Slots.Count);
    }

    [Fact]
    public void Validate_Rejects_Missing_Slot()
    {
        using var package = TestPackage.Create();
        package.Manifest.Slots.Remove("termination");

        var result = SkinCatalog.Validate(package.SkinDirectory, package.Manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("termination", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_Rejects_Path_Traversal()
    {
        using var package = TestPackage.Create();
        package.Manifest.Slots["avatar"].Asset = "../avatar.png";

        var result = SkinCatalog.Validate(package.SkinDirectory, package.Manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("unsafe", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("deckupipes-skin")]
    [InlineData("earclarinet-skin")]
    [InlineData("DECKUPIPES-SKIN")]
    public void Validate_Accepts_Both_Known_Format_Ids(string format)
    {
        using var package = TestPackage.Create();
        package.Manifest.Format = format;

        var result = SkinCatalog.Validate(package.SkinDirectory, package.Manifest);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void Validate_Rejects_An_Unknown_Format_Id()
    {
        using var package = TestPackage.Create();
        package.Manifest.Format = "something-else";

        var result = SkinCatalog.Validate(package.SkinDirectory, package.Manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("format", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("..\\evil")]
    [InlineData("sub/dir")]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("trailing ")]
    [InlineData("trailing.")]
    [InlineData(".")]
    [InlineData("..")]
    public void Validate_Rejects_Skin_Id_That_Is_Not_A_Folder_Name(string skinId)
    {
        using var package = TestPackage.Create();
        package.Manifest.Id = skinId;

        var result = SkinCatalog.Validate(package.SkinDirectory, package.Manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("folder name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_Rejects_Actual_Asset_Dimension_Mismatch()
    {
        using var package = TestPackage.Create();
        File.WriteAllBytes(Path.Combine(package.SkinDirectory, package.Manifest.Slots["avatar"].Asset), TestPackage.Png(1, 1));

        var result = SkinCatalog.Validate(package.SkinDirectory, package.Manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("124x124", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryLoad_Returns_Invalid_Package_For_Missing_Asset()
    {
        using var package = TestPackage.Create();
        File.Delete(Path.Combine(package.SkinDirectory, package.Manifest.Slots["avatar"].Asset));
        File.WriteAllText(Path.Combine(package.SkinDirectory, "skin.json"), JsonSerializer.Serialize(package.Manifest, SkinManifestJson.Options));

        var loaded = new SkinCatalog(package.Root).TryLoad(package.SkinDirectory);

        Assert.NotNull(loaded);
        Assert.False(loaded!.Validation.IsValid);
    }

    private sealed class TestPackage : IDisposable
    {
        private TestPackage(string root, string skinDirectory, SkinManifest manifest)
        {
            Root = root;
            SkinDirectory = skinDirectory;
            Manifest = manifest;
        }

        public string Root { get; }
        public string SkinDirectory { get; }
        public SkinManifest Manifest { get; }

        public static TestPackage Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "DeckUPipesSkinTests", Guid.NewGuid().ToString("N"));
            var skinDirectory = Path.Combine(root, "neon-noir");
            Directory.CreateDirectory(skinDirectory);
            var assets = new Dictionary<string, (int Width, int Height, string? Repeat, int? RepeatCount, bool Once)>
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
            var manifest = new SkinManifest { Format = "earclarinet-skin", Version = 1, Id = "neon-noir" };
            foreach (var (id, shape) in assets)
            {
                var filename = id + ".png";
                manifest.Slots[id] = new SkinSlotDefinition { Asset = filename, Width = shape.Width, Height = shape.Height, Repeat = shape.Repeat, RepeatCount = shape.RepeatCount, Once = shape.Once };
                File.WriteAllBytes(Path.Combine(skinDirectory, filename), Png(shape.Width, shape.Height));
            }

            return new TestPackage(root, skinDirectory, manifest);
        }

        public static byte[] Png(int width, int height)
        {
            var png = new byte[24];
            png[0] = 137; png[1] = 80; png[2] = 78; png[3] = 71;
            png[4] = 13; png[5] = 10; png[6] = 26; png[7] = 10;
            png[12] = (byte)'I'; png[13] = (byte)'H'; png[14] = (byte)'D'; png[15] = (byte)'R';
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16, 4), (uint)width);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20, 4), (uint)height);
            return png;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}


