using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace DeckUPipes.Core;

public enum SkinSourceKind
{
    Folder,
    Archive,
}

/// <summary>
/// A skin that has been read from the user's folder or package and validated, but
/// not installed yet. Disposing removes the staging directory and never touches the
/// folder the user picked.
/// </summary>
public sealed class SkinInstallCandidate : IDisposable
{
    private readonly string? _stagingDirectory;

    internal SkinInstallCandidate(
        SkinSourceKind kind,
        string sourcePath,
        string skinRoot,
        SkinManifest? manifest,
        SkinValidationResult validation,
        string? stagingDirectory)
    {
        Kind = kind;
        SourcePath = sourcePath;
        SkinRoot = skinRoot;
        Manifest = manifest;
        Validation = validation;
        _stagingDirectory = stagingDirectory;
    }

    public SkinSourceKind Kind { get; }

    /// <summary>The path the user picked. Shown in the confirmation card.</summary>
    public string SourcePath { get; }

    /// <summary>The directory that holds skin.json.</summary>
    public string SkinRoot { get; }

    public SkinManifest? Manifest { get; }

    public SkinValidationResult Validation { get; }

    public bool IsValid => Validation.IsValid;

    public void Dispose() => TryDeleteDirectory(_stagingDirectory);

    internal static void TryDeleteDirectory(string? directory)
    {
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>
/// Reads a skin from a folder or a .zip package, validates it before anything is
/// written, and installs or removes it under the skins root.
/// </summary>
public static class SkinInstaller
{
    /// <summary>Guards against a package that unpacks into something unreasonable.</summary>
    public const long MaxArchiveBytes = 64L * 1024 * 1024;

    private const string ManifestFileName = "skin.json";

    public static SkinInstallCandidate Inspect(string sourcePath, string stagingRoot)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return Invalid(SkinSourceKind.Folder, string.Empty, "No skin was selected.");
        }

        if (Directory.Exists(sourcePath))
        {
            return InspectFolder(sourcePath);
        }

        if (File.Exists(sourcePath))
        {
            return InspectArchive(sourcePath, stagingRoot);
        }

        return Invalid(SkinSourceKind.Folder, sourcePath, "The selected path could not be found.");
    }

    public static bool IsInstalled(string skinsRoot, string skinId) =>
        Directory.Exists(Path.Combine(skinsRoot, skinId));

    /// <summary>
    /// Copies a validated candidate into the skins root. An existing skin with the
    /// same id is moved aside first and restored if the copy fails, so the previous
    /// skin is never lost.
    /// </summary>
    public static void Commit(SkinInstallCandidate candidate, string skinsRoot)
    {
        if (candidate.Manifest is null)
        {
            throw new InvalidOperationException("There is nothing to install.");
        }

        if (!candidate.IsValid)
        {
            throw new InvalidOperationException("The skin did not pass validation.");
        }

        Directory.CreateDirectory(skinsRoot);
        var destination = Path.Combine(skinsRoot, candidate.Manifest.Id);

        string? backup = null;
        if (Directory.Exists(destination))
        {
            backup = Path.Combine(skinsRoot, ".backup-" + Guid.NewGuid().ToString("N"));
            Directory.Move(destination, backup);
        }

        try
        {
            CopyDirectory(candidate.SkinRoot, destination);
        }
        catch
        {
            SkinInstallCandidate.TryDeleteDirectory(destination);
            Restore(backup, destination);
            throw;
        }

        SkinInstallCandidate.TryDeleteDirectory(backup);
    }

    /// <summary>
    /// Deletes an installed skin. The directory must live inside one of the allowed
    /// roots, so this can never remove a folder the user picked elsewhere.
    /// </summary>
    public static void Remove(string skinDirectory, IEnumerable<string> allowedRoots)
    {
        string target;
        try
        {
            target = Path.GetFullPath(skinDirectory);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidOperationException("That folder cannot be used.", exception);
        }

        var allowed = allowedRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Path.GetFullPath(root) + Path.DirectorySeparatorChar)
            .Any(root => target.StartsWith(root, StringComparison.OrdinalIgnoreCase));

        if (!allowed)
        {
            throw new InvalidOperationException("That folder is not inside a skin directory.");
        }

        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }
    }

    private static SkinInstallCandidate InspectFolder(string path)
    {
        var root = ResolveSkinRoot(path);
        return root is null
            ? Invalid(SkinSourceKind.Folder, path, $"No {ManifestFileName} was found in that folder.")
            : Build(SkinSourceKind.Folder, path, root, stagingDirectory: null);
    }

    private static SkinInstallCandidate InspectArchive(string path, string stagingRoot)
    {
        var staging = Path.Combine(stagingRoot, "import-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            Extract(path, staging);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            SkinInstallCandidate.TryDeleteDirectory(staging);
            return Invalid(SkinSourceKind.Archive, path, exception.Message);
        }

        var root = ResolveSkinRoot(staging);
        if (root is null)
        {
            SkinInstallCandidate.TryDeleteDirectory(staging);
            return Invalid(SkinSourceKind.Archive, path, $"The package does not contain a {ManifestFileName}.");
        }

        return Build(SkinSourceKind.Archive, path, root, staging);
    }

    private static void Extract(string archivePath, string destination)
    {
        using var archive = ZipFile.OpenRead(archivePath);

        var totalBytes = archive.Entries.Sum(entry => entry.Length);
        if (totalBytes > MaxArchiveBytes)
        {
            throw new InvalidDataException($"The package unpacks to more than {MaxArchiveBytes / (1024 * 1024)} MB.");
        }

        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            var target = Path.GetFullPath(Path.Combine(destination, relative));
            if (Path.IsPathRooted(relative) || !target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The package contains a path that points outside the skin folder.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    /// <summary>Finds the directory holding skin.json, following one wrapping folder.</summary>
    private static string? ResolveSkinRoot(string directory)
    {
        if (File.Exists(Path.Combine(directory, ManifestFileName)))
        {
            return directory;
        }

        var children = Directory.GetDirectories(directory);
        return children.Length == 1 && File.Exists(Path.Combine(children[0], ManifestFileName))
            ? children[0]
            : null;
    }

    private static SkinInstallCandidate Build(SkinSourceKind kind, string sourcePath, string skinRoot, string? stagingDirectory)
    {
        SkinManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<SkinManifest>(
                File.ReadAllText(Path.Combine(skinRoot, ManifestFileName)),
                SkinManifestJson.Options);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            manifest = null;
        }

        if (manifest is null)
        {
            SkinInstallCandidate.TryDeleteDirectory(stagingDirectory);
            return Invalid(kind, sourcePath, $"{ManifestFileName} could not be read.");
        }

        return new SkinInstallCandidate(
            kind,
            sourcePath,
            skinRoot,
            manifest,
            SkinCatalog.Validate(skinRoot, manifest),
            stagingDirectory);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static void Restore(string? backup, string destination)
    {
        if (backup is null || !Directory.Exists(backup))
        {
            return;
        }

        try
        {
            SkinInstallCandidate.TryDeleteDirectory(destination);
            Directory.Move(backup, destination);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static SkinInstallCandidate Invalid(SkinSourceKind kind, string sourcePath, string error) =>
        new(kind, sourcePath, string.Empty, null, new SkinValidationResult(false, new[] { error }, Array.Empty<string>()), null);
}
