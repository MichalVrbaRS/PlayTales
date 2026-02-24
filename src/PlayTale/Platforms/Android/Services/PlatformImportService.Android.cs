#if ANDROID
using System.Security.Cryptography;
using System.Text;
using PlayTale.Features.Audiobooks.Models;

namespace PlayTale.Features.Audiobooks.Services;

public sealed partial class PlatformImportService
{
    private async partial Task<IReadOnlyList<ImportSource>> PickPlatformSourcesAsync(CancellationToken cancellationToken)
    {
        var pickedFiles = await FilePicker.Default.PickMultipleAsync(new PickOptions
        {
            PickerTitle = "Select audiobook files",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "audio/mpeg", "audio/*" } }
            })
        });

        var sourceList = (pickedFiles ?? Array.Empty<FileResult?>())
            .OfType<FileResult>()
            .ToList();
        if (sourceList.Count == 0)
        {
            return Array.Empty<ImportSource>();
        }

        var displayName = InferDisplayName(sourceList);
        var persistedFiles = await PersistPickedFilesAsync(sourceList, displayName, cancellationToken);
        if (persistedFiles.Count == 0)
        {
            return Array.Empty<ImportSource>();
        }

        var importDirectory = Path.GetDirectoryName(persistedFiles[0].FullPath);
        if (string.IsNullOrWhiteSpace(importDirectory))
        {
            return BuildSourcesFromPaths(persistedFiles);
        }

        return new[]
        {
            new ImportSource(
                ImportSourceType.Folder,
                displayName,
                importDirectory,
                SourceUri: null,
                SecurityScopedBookmarkBase64: null)
        };
    }

    private static async Task<IReadOnlyList<(string FileName, string FullPath)>> PersistPickedFilesAsync(
        IReadOnlyList<FileResult> pickedFiles,
        string displayName,
        CancellationToken cancellationToken)
    {
        var importRoot = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, "imports");
        Directory.CreateDirectory(importRoot);

        var signature = string.Join(
            "|",
            pickedFiles
                .Select(x => x.FileName ?? string.Empty)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        var folderHash = ComputeShortHash(signature);
        var folderName = $"{SanitizeFolderName(displayName)}-{folderHash}";
        var folderPath = Path.Combine(importRoot, folderName);
        Directory.CreateDirectory(folderPath);

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(string FileName, string FullPath)>();

        foreach (var file in pickedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceName = string.IsNullOrWhiteSpace(file.FileName) ? "chapter.mp3" : file.FileName;
            var baseName = SanitizeFileName(sourceName);
            if (!baseName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var targetName = EnsureUniqueName(baseName, usedNames);
            var targetPath = Path.Combine(folderPath, targetName);

            await using var sourceStream = await file.OpenReadAsync();
            await using var targetStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(targetStream, cancellationToken);

            result.Add((targetName, targetPath));
        }

        return result;
    }

    private static string InferDisplayName(IReadOnlyList<FileResult> files)
    {
        var fullPaths = files
            .Select(x => x.FullPath)
            .Where(x => !string.IsNullOrWhiteSpace(x) && Path.IsPathRooted(x))
            .ToList();

        if (fullPaths.Count > 0)
        {
            var firstDirectory = Path.GetDirectoryName(fullPaths[0]);
            var sameDirectory = !string.IsNullOrWhiteSpace(firstDirectory) &&
                                fullPaths.All(x => string.Equals(Path.GetDirectoryName(x), firstDirectory, StringComparison.OrdinalIgnoreCase));

            if (sameDirectory)
            {
                var name = Path.GetFileName(firstDirectory);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name.Trim();
                }
            }
        }

        var firstName = files.Select(x => x.FileName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        var withoutExtension = Path.GetFileNameWithoutExtension(firstName ?? string.Empty)?.Trim();
        return string.IsNullOrWhiteSpace(withoutExtension) ? "Imported Audiobook" : withoutExtension;
    }

    private static string ComputeShortHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..10].ToLowerInvariant();
    }

    private static string EnsureUniqueName(string fileName, ISet<string> usedNames)
    {
        if (usedNames.Add(fileName))
        {
            return fileName;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var suffix = 1;
        string candidate;
        do
        {
            candidate = $"{stem}_{suffix}{extension}";
            suffix++;
        }
        while (!usedNames.Add(candidate));

        return candidate;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "import" : sanitized;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "chapter.mp3" : sanitized;
    }
}
#endif
