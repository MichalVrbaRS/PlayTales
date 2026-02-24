using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public sealed partial class PlatformImportService
{
#if WINDOWS
    private async partial Task<IReadOnlyList<ImportSource>> PickPlatformSourcesAsync(CancellationToken cancellationToken)
    {
        var pickedFiles = await FilePicker.Default.PickMultipleAsync(new PickOptions
        {
            PickerTitle = "Select audiobook files",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".mp3" } }
            })
        });

        var files = await PersistPickedFilesAsync(pickedFiles ?? Array.Empty<FileResult>(), cancellationToken);

        return BuildSourcesFromPaths(files);
    }

    private static async Task<IReadOnlyList<(string FileName, string FullPath)>> PersistPickedFilesAsync(
        IEnumerable<FileResult> pickedFiles,
        CancellationToken cancellationToken)
    {
        var sourceList = pickedFiles
            .Where(x => !string.IsNullOrWhiteSpace(x.FullPath) && File.Exists(x.FullPath))
            .ToList();

        if (sourceList.Count == 0)
        {
            return Array.Empty<(string FileName, string FullPath)>();
        }

        var importRoot = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "imports");
        Directory.CreateDirectory(importRoot);

        var sourceDirectory = Path.GetDirectoryName(sourceList[0].FullPath!);
        var sameDirectory = !string.IsNullOrWhiteSpace(sourceDirectory) &&
                            sourceList.All(x => string.Equals(Path.GetDirectoryName(x.FullPath!), sourceDirectory, StringComparison.OrdinalIgnoreCase));

        var importFolderName = sameDirectory
            ? SanitizeFolderName(Path.GetFileName(sourceDirectory) ?? "import")
            : $"manual-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var importFolderPath = Path.Combine(importRoot, importFolderName);
        Directory.CreateDirectory(importFolderPath);

        var result = new List<(string FileName, string FullPath)>();
        foreach (var file in sourceList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourcePath = file.FullPath!;
            var fileName = Path.GetFileName(sourcePath);
            var targetPath = EnsureUniquePath(Path.Combine(importFolderPath, fileName));

            await using var sourceStream = File.OpenRead(sourcePath);
            await using var targetStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(targetStream, cancellationToken);

            result.Add((fileName, targetPath));
        }

        return result;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "import" : sanitized;
    }

    private static string EnsureUniquePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var index = 1;

        while (true)
        {
            var candidate = Path.Combine(directory, $"{baseName}_{index}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            index++;
        }
    }
#endif
}
