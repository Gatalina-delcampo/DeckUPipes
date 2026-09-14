using DeckUPipes.Core;

namespace DeckUPipes.Tests;

/// <summary>
/// Validates the skin that ships inside the repository. A skin installed under
/// %APPDATA% only exists on the machine that installed it, so that check silently
/// did nothing in CI; the repository sample is always present.
/// </summary>
public class SampleSkinTests
{
    [Fact]
    public void Repository_Sample_Skin_Is_Valid()
    {
        var sample = Path.Combine(RepositoryRoot(), "samples", "skins", "pastel-grid");

        var package = new SkinCatalog(Path.GetDirectoryName(sample)!).TryLoad(sample);

        Assert.NotNull(package);
        Assert.True(package!.Validation.IsValid, string.Join(Environment.NewLine, package.Validation.Errors));
        Assert.Equal(11, package.Manifest.Slots.Count);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "samples", "skins")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate the repository root from {AppContext.BaseDirectory}.");
    }
}
