using System.IO.Compression;
using System.Text.Json;
using DeckUPipes.Core;

namespace DeckUPipes.Tests;

public class SkinInstallerTests
{
    [Fact]
    public void Inspect_Accepts_A_Valid_Folder()
    {
        using var area = new TempArea();
        var folder = Path.Combine(area.Root, "my-skin");
        SkinFixture.WriteSkin(folder, "my-skin");

        using var candidate = SkinInstaller.Inspect(folder, area.StagingRoot);

        Assert.True(candidate.IsValid);
        Assert.Equal(SkinSourceKind.Folder, candidate.Kind);
        Assert.Equal("my-skin", candidate.Manifest!.Id);
        Assert.Equal(folder, candidate.SkinRoot);
    }

    [Fact]
    public void Inspect_Rejects_A_Folder_Without_A_Manifest()
    {
        using var area = new TempArea();
        var folder = Path.Combine(area.Root, "not-a-skin");
        Directory.CreateDirectory(folder);

        using var candidate = SkinInstaller.Inspect(folder, area.StagingRoot);

        Assert.False(candidate.IsValid);
        Assert.Contains(candidate.Validation.Errors, error => error.Contains("skin.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Inspect_Accepts_A_Package()
    {
        using var area = new TempArea();
        var archive = area.BuildPackage("my-skin", wrappingFolder: null);

        using var candidate = SkinInstaller.Inspect(archive, area.StagingRoot);

        Assert.True(candidate.IsValid);
        Assert.Equal(SkinSourceKind.Archive, candidate.Kind);
        Assert.Equal("my-skin", candidate.Manifest!.Id);
        Assert.True(Directory.Exists(candidate.SkinRoot));
        Assert.StartsWith(area.StagingRoot, candidate.SkinRoot, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_Finds_The_Skin_Inside_A_Wrapping_Folder()
    {
        using var area = new TempArea();
        var archive = area.BuildPackage("my-skin", wrappingFolder: "my-skin");

        using var candidate = SkinInstaller.Inspect(archive, area.StagingRoot);

        Assert.True(candidate.IsValid);
        Assert.Equal("my-skin", candidate.Manifest!.Id);
    }

    [Fact]
    public void Inspect_Rejects_A_Package_Without_A_Manifest()
    {
        using var area = new TempArea();
        var archive = Path.Combine(area.Root, "empty.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using var stream = zip.CreateEntry("readme.png").Open();
            stream.Write(SkinFixture.Png(8, 8));
        }

        using var candidate = SkinInstaller.Inspect(archive, area.StagingRoot);

        Assert.False(candidate.IsValid);
        Assert.Contains(candidate.Validation.Errors, error => error.Contains("skin.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Inspect_Rejects_A_Package_That_Escapes_The_Staging_Folder()
    {
        using var area = new TempArea();
        var archive = area.BuildPackage("my-skin", wrappingFolder: null, extraEntryName: "../escaped.png");

        using var candidate = SkinInstaller.Inspect(archive, area.StagingRoot);

        Assert.False(candidate.IsValid);
        Assert.Contains(candidate.Validation.Errors, error => error.Contains("outside the skin folder", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(Path.Combine(area.StagingRoot, "escaped.png")));
    }

    [Fact]
    public void Inspect_Rejects_An_Unsafe_Skin_Id()
    {
        using var area = new TempArea();
        var folder = Path.Combine(area.Root, "sneaky");
        SkinFixture.WriteSkin(folder, "..\\evil");

        using var candidate = SkinInstaller.Inspect(folder, area.StagingRoot);

        Assert.False(candidate.IsValid);
        Assert.Contains(candidate.Validation.Errors, error => error.Contains("folder name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Commit_Installs_Into_An_Empty_Root()
    {
        using var area = new TempArea();
        var folder = Path.Combine(area.Root, "source");
        SkinFixture.WriteSkin(folder, "my-skin");
        var skinsRoot = Path.Combine(area.Root, "skins");

        using var candidate = SkinInstaller.Inspect(folder, area.StagingRoot);
        SkinInstaller.Commit(candidate, skinsRoot);

        Assert.True(SkinInstaller.IsInstalled(skinsRoot, "my-skin"));
        Assert.True(File.Exists(Path.Combine(skinsRoot, "my-skin", "skin.json")));
        Assert.Equal(11, Directory.GetFiles(Path.Combine(skinsRoot, "my-skin"), "*.png").Length);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void Commit_Replaces_An_Existing_Skin()
    {
        using var area = new TempArea();
        var folder = Path.Combine(area.Root, "source");
        SkinFixture.WriteSkin(folder, "my-skin");
        var skinsRoot = Path.Combine(area.Root, "skins");
        var installed = Path.Combine(skinsRoot, "my-skin");
        Directory.CreateDirectory(installed);
        File.WriteAllText(Path.Combine(installed, "old-marker.txt"), "old");

        using var candidate = SkinInstaller.Inspect(folder, area.StagingRoot);
        SkinInstaller.Commit(candidate, skinsRoot);

        Assert.False(File.Exists(Path.Combine(installed, "old-marker.txt")));
        Assert.True(File.Exists(Path.Combine(installed, "skin.json")));
        Assert.Empty(Directory.GetDirectories(skinsRoot, ".backup-*"));
    }

    [Fact]
    public void Commit_Refuses_An_Invalid_Candidate()
    {
        using var area = new TempArea();
        var folder = Path.Combine(area.Root, "source");
        SkinFixture.WriteSkin(folder, "my-skin");
        File.Delete(Path.Combine(folder, "avatar.png"));

        using var candidate = SkinInstaller.Inspect(folder, area.StagingRoot);

        Assert.False(candidate.IsValid);
        Assert.Throws<InvalidOperationException>(() => SkinInstaller.Commit(candidate, Path.Combine(area.Root, "skins")));
    }

    [Fact]
    public void Remove_Deletes_An_Installed_Skin()
    {
        using var area = new TempArea();
        var folder = Path.Combine(area.Root, "source");
        SkinFixture.WriteSkin(folder, "my-skin");
        var skinsRoot = Path.Combine(area.Root, "skins");

        using var candidate = SkinInstaller.Inspect(folder, area.StagingRoot);
        SkinInstaller.Commit(candidate, skinsRoot);
        SkinInstaller.Remove(Path.Combine(skinsRoot, "my-skin"), new[] { skinsRoot });

        Assert.False(Directory.Exists(Path.Combine(skinsRoot, "my-skin")));
    }

    [Fact]
    public void Remove_Refuses_A_Folder_Outside_The_Skins_Root()
    {
        using var area = new TempArea();
        var skinsRoot = Path.Combine(area.Root, "skins");
        var elsewhere = Path.Combine(area.Root, "important");
        Directory.CreateDirectory(skinsRoot);
        Directory.CreateDirectory(elsewhere);

        Assert.Throws<InvalidOperationException>(() => SkinInstaller.Remove(elsewhere, new[] { skinsRoot }));
        Assert.True(Directory.Exists(elsewhere));
    }

    [Fact]
    public void Disposing_A_Candidate_Cleans_Up_Its_Staging()
    {
        using var area = new TempArea();
        var archive = area.BuildPackage("my-skin", wrappingFolder: null);

        string staging;
        using (var candidate = SkinInstaller.Inspect(archive, area.StagingRoot))
        {
            Assert.True(candidate.IsValid);
            staging = candidate.SkinRoot;
            Assert.True(Directory.Exists(staging));
        }

        Assert.False(Directory.Exists(staging));
    }

    private sealed class TempArea : IDisposable
    {
        public TempArea()
        {
            Root = Path.Combine(Path.GetTempPath(), "DeckUPipesInstallerTests", Guid.NewGuid().ToString("N"));
            StagingRoot = Path.Combine(Root, "staging");
            Directory.CreateDirectory(StagingRoot);
        }

        public string Root { get; }

        public string StagingRoot { get; }

        public string BuildPackage(string skinId, string? wrappingFolder, string? extraEntryName = null)
        {
            var source = Path.Combine(Root, "package-source", wrappingFolder ?? "flat");
            SkinFixture.WriteSkin(source, skinId);

            var archive = Path.Combine(Root, (wrappingFolder ?? "flat") + ".zip");
            using var zip = ZipFile.Open(archive, ZipArchiveMode.Create);
            foreach (var file in Directory.GetFiles(source))
            {
                var entryName = wrappingFolder is null
                    ? Path.GetFileName(file)
                    : Path.Combine(wrappingFolder, Path.GetFileName(file));
                zip.CreateEntryFromFile(file, entryName.Replace('\\', '/'));
            }

            if (extraEntryName is not null)
            {
                using var stream = zip.CreateEntry(extraEntryName).Open();
                stream.Write(SkinFixture.Png(4, 4));
            }

            return archive;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private static class SkinFixture
    {
        private static readonly Dictionary<string, (int Width, int Height, string? Repeat, int? RepeatCount, bool Once)> Slots = new()
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

        public static void WriteSkin(string directory, string skinId)
        {
            Directory.CreateDirectory(directory);

            var manifest = new SkinManifest { Format = "earclarinet-skin", Version = 1, Id = skinId };
            foreach (var (slot, shape) in Slots)
            {
                manifest.Slots[slot] = new SkinSlotDefinition
                {
                    Asset = slot + ".png",
                    Width = shape.Width,
                    Height = shape.Height,
                    Repeat = shape.Repeat,
                    RepeatCount = shape.RepeatCount,
                    Once = shape.Once,
                };
                File.WriteAllBytes(Path.Combine(directory, slot + ".png"), Png(shape.Width, shape.Height));
            }

            File.WriteAllText(Path.Combine(directory, "skin.json"), JsonSerializer.Serialize(manifest, SkinManifestJson.Options));
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
    }
}
